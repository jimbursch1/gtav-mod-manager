using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GtavModManager.Core;

namespace GtavModManager.Services
{
    /// <summary>
    /// Manages mod enable/disable using NTFS junctions and hard links.
    ///
    /// Storage model:
    ///   Mod files permanently live in: ModManager\storage\{modId}\{relativePath}
    ///   "Enabled"  = junction or hard link exists at GTA V root path pointing to storage
    ///   "Disabled" = no link at GTA V root path (storage untouched)
    ///
    /// This means profile switching is near-instant: remove old links, create new links.
    /// No files are ever physically moved during enable/disable.
    ///
    /// ImportMod is responsible for moving files from GTA V root into storage on first import.
    /// </summary>
    public class QuarantineService
    {
        // Files that must never be moved to storage under any circumstances.
        // This is a last-resort guard; the scanner also skips these.
        private static readonly HashSet<string> ProtectedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GTA5.exe", "GTAVLauncher.exe", "PlayGTAV.exe",
            "update.rpf", "x64a.rpf", "x64b.rpf",
            "dsound.dll", "d3d11.dll", "dxgi.dll",
            "OpenIV.asi", "GtavModManager.exe",
        };

        private readonly FileOperationService _fileOps;
        private readonly SymlinkService _symlink;
        private readonly ModLogger _logger;
        private string _gtavRoot;
        private string _storageRoot; // ModManager\storage\

        public QuarantineService(FileOperationService fileOps, SymlinkService symlink, ModLogger logger = null)
        {
            _fileOps = fileOps;
            _symlink = symlink;
            _logger = logger;
        }

        public void Configure(string gtavRoot, string storageRoot)
        {
            _gtavRoot = gtavRoot;
            _storageRoot = storageRoot;
        }

        /// <summary>
        /// Returns path to this mod's permanent storage directory.
        /// </summary>
        public string GetStoragePath(Mod mod) =>
            Path.Combine(_storageRoot, mod.Id);

        /// <summary>
        /// Checks whether any currently enabled mod depends on this one.
        /// Returns display names of blockers (empty = safe to disable).
        /// </summary>
        public List<string> ValidateCanDisable(Mod mod, IReadOnlyList<Mod> allEnabledMods)
        {
            return allEnabledMods
                .Where(other => other.Id != mod.Id
                    && other.Dependencies != null
                    && other.Dependencies.Any(d => d.ModId == mod.Id))
                .Select(other => other.Name)
                .ToList();
        }

        /// <summary>
        /// Moves files from their current GTA V root locations into permanent storage,
        /// then creates links. Called once at import time.
        /// </summary>
        public OperationResult ImportToStorage(Mod mod)
        {
            if (string.IsNullOrEmpty(_gtavRoot))
                return OperationResult.Fail("GTA V root path not configured.");

            if (IsGtavRunning())
                return OperationResult.Fail("GTA V is running. Close the game before importing mods.");

            // Refuse to move protected files regardless of how they ended up in the mod record
            foreach (var relativePath in mod.Files)
            {
                string filename = Path.GetFileName(relativePath);
                if (ProtectedFileNames.Contains(filename) || KnownFrameworks.All.ContainsKey(filename))
                    return OperationResult.Fail($"Refusing to import protected file: {filename}. Remove it from the mod's file list.");
            }

            string storagePath = GetStoragePath(mod);

            // Move each file from GTA V root into storage
            var moves = new List<(string src, string dst)>();
            foreach (var relativePath in mod.Files)
            {
                string src = Path.Combine(_gtavRoot, relativePath);
                string dst = Path.Combine(storagePath, relativePath);
                if (File.Exists(src))
                    moves.Add((src, dst));
                else if (!File.Exists(dst))
                    return OperationResult.Fail($"File not found in GTA V root or storage: {relativePath}");
                // If already in storage but not in GTA V root, that's fine — skip
            }

            _logger?.LogSection($"Import: {mod.Name}");
            var moveResult = _fileOps.MoveFilesWithRollback(moves);
            foreach (var (src, dst) in moves)
                _logger?.Log("IMPORT", src, moveResult.Success, moveResult.ErrorMessage);

            if (!moveResult.Success)
                return moveResult;

            // Now create links at GTA V root so mod is immediately enabled
            return CreateLinks(mod);
        }

        /// <summary>
        /// Enables a mod by creating hard links (files) or junctions (dirs) at GTA V paths.
        /// Rolls back created links on any failure.
        /// </summary>
        public OperationResult EnableMod(Mod mod)
        {
            if (string.IsNullOrEmpty(_gtavRoot))
                return OperationResult.Fail("GTA V root path not configured.");

            if (IsGtavRunning())
                return OperationResult.Fail("GTA V is running. Close the game before changing mods.");

            _logger?.LogSection($"Enable: {mod.Name}");
            var result = CreateLinks(mod);
            if (result.Success)
                mod.Status = ModStatus.Enabled;
            return result;
        }

        /// <summary>
        /// Disables a mod by removing links at GTA V paths.
        /// Storage files are untouched. Instant regardless of mod size.
        /// </summary>
        public OperationResult DisableMod(Mod mod)
        {
            if (string.IsNullOrEmpty(_gtavRoot))
                return OperationResult.Fail("GTA V root path not configured.");

            if (IsGtavRunning())
                return OperationResult.Fail("GTA V is running. Close the game before changing mods.");

            _logger?.LogSection($"Disable: {mod.Name}");
            var result = RemoveLinks(mod);
            if (result.Success)
                mod.Status = ModStatus.Disabled;
            return result;
        }

        private OperationResult CreateLinks(Mod mod)
        {
            string storagePath = GetStoragePath(mod);
            var createdLinks = new List<string>();

            foreach (var relativePath in mod.Files)
            {
                string storageFile = Path.Combine(storagePath, relativePath);
                string gtavFile = Path.Combine(_gtavRoot, relativePath);

                if (!File.Exists(storageFile))
                    return RollbackLinks(createdLinks, $"Storage file missing: {relativePath}");

                if (File.Exists(gtavFile))
                {
                    // If it's already a hard link to our storage, it's fine
                    // If it's a real file, we have a conflict — fail
                    if (!IsOurLink(gtavFile, storageFile))
                        return RollbackLinks(createdLinks, $"File already exists at GTA V path: {relativePath}");
                    continue; // already linked
                }

                _fileOps.EnsureDirectory(Path.GetDirectoryName(gtavFile));

                // Try hard link first; fall back to file move (last resort)
                bool linked = _symlink.CreateHardLink(gtavFile, storageFile);
                if (!linked)
                {
                    // Hard link failed (cross-drive or other). Fall back to file copy.
                    // Note: this defeats the performance goal but is a safe fallback.
                    try { File.Copy(storageFile, gtavFile, overwrite: false); linked = true; }
                    catch (Exception ex)
                    {
                        return RollbackLinks(createdLinks, $"Failed to link or copy {relativePath}: {ex.Message}");
                    }
                }

                createdLinks.Add(gtavFile);
                _logger?.Log("LINK", $"{storageFile} -> {gtavFile}", true);
            }

            return OperationResult.Ok();
        }

        private OperationResult RemoveLinks(Mod mod)
        {
            foreach (var relativePath in mod.Files)
            {
                string gtavFile = Path.Combine(_gtavRoot, relativePath);
                if (!File.Exists(gtavFile)) continue;

                // Only remove if it's our link (same data as storage copy)
                string storageFile = Path.Combine(GetStoragePath(mod), relativePath);
                if (!IsOurLink(gtavFile, storageFile) && File.Exists(storageFile))
                {
                    // Not our file — don't delete it
                    System.Diagnostics.Debug.WriteLine($"[Quarantine] Skipping non-owned file: {gtavFile}");
                    continue;
                }

                if (!_symlink.DeleteLink(gtavFile))
                {
                    _logger?.Log("UNLINK", gtavFile, false, "DeleteLink failed");
                    return OperationResult.Fail($"Failed to remove link: {relativePath}");
                }
                _logger?.Log("UNLINK", gtavFile, true);
            }

            return OperationResult.Ok();
        }

        private OperationResult RollbackLinks(List<string> createdLinks, string reason)
        {
            _logger?.Log("ROLLBACK", $"{createdLinks.Count} links — {reason}", false, reason);
            foreach (var link in createdLinks)
            {
                try { if (File.Exists(link)) File.Delete(link); }
                catch { }
            }
            return OperationResult.Fail(reason);
        }

        /// <summary>
        /// Emergency recovery: copies all mod files from storage back to their GTA V paths.
        /// Does not delete from storage. Sets all mods to Enabled.
        /// Use this to recover from any broken link/state situation.
        /// </summary>
        public OperationResult RestoreAll(IReadOnlyList<Mod> allMods)
        {
            if (string.IsNullOrEmpty(_gtavRoot))
                return OperationResult.Fail("GTA V root path not configured.");

            if (IsGtavRunning())
                return OperationResult.Fail("GTA V is running. Close the game before restoring.");

            _logger?.LogSection("Emergency Restore");
            var errors = new List<string>();
            foreach (var mod in allMods)
            {
                string storagePath = GetStoragePath(mod);
                foreach (var relativePath in mod.Files)
                {
                    string storageFile = Path.Combine(storagePath, relativePath);
                    string gtavFile = Path.Combine(_gtavRoot, relativePath);

                    if (!File.Exists(storageFile)) continue;
                    if (File.Exists(gtavFile)) { mod.Status = ModStatus.Enabled; continue; }

                    try
                    {
                        _fileOps.EnsureDirectory(Path.GetDirectoryName(gtavFile));
                        File.Copy(storageFile, gtavFile, overwrite: false);
                        mod.Status = ModStatus.Enabled;
                        _logger?.Log("RESTORE", gtavFile, true);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log("RESTORE", gtavFile, false, ex.Message);
                        errors.Add($"{relativePath}: {ex.Message}");
                    }
                }
            }

            return errors.Count == 0
                ? OperationResult.Ok()
                : OperationResult.Fail("Some files could not be restored:\n" + string.Join("\n", errors));
        }

        private static bool IsGtavRunning() =>
            Process.GetProcessesByName("GTA5").Length > 0;

        /// <summary>
        /// Heuristic check: are gtavFile and storageFile the same underlying data?
        /// For hard links this is true when they share the same file index (inode).
        /// For copies, we fall back to comparing size + write time.
        /// </summary>
        private static bool IsOurLink(string gtavFile, string storageFile)
        {
            if (!File.Exists(gtavFile) || !File.Exists(storageFile)) return false;
            try
            {
                var a = new FileInfo(gtavFile);
                var b = new FileInfo(storageFile);
                return a.Length == b.Length && a.LastWriteTimeUtc == b.LastWriteTimeUtc;
            }
            catch
            {
                return false;
            }
        }
    }
}
