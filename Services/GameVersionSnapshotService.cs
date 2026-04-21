using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GtavModManager.Core;
using GtavModManager.Data;

namespace GtavModManager.Services
{
    public class GameVersionSnapshotService
    {
        // Files captured in every snapshot. Paths are relative to GTA V root.
        // Optional files are skipped if not present rather than failing the operation.
        private static readonly (string RelativePath, bool Required)[] TargetFiles =
        {
            (@"GTA5.exe",                  required: true),
            (@"GTAVLauncher.exe",          required: false),
            (@"PlayGTAV.exe",              required: false),
            (@"GTAVLanguageSelect.exe",    required: false),
            (@"update\update.rpf",         required: true),
            (@"ScriptHookV.dll",           required: false),
            (@"dinput8.dll",               required: false),
        };

        private readonly SnapshotRepository _repo;
        private List<GameVersionSnapshot> _snapshots = new List<GameVersionSnapshot>();

        private string _gtavRoot = "";
        private string _snapshotStorageRoot = "";

        public GameVersionSnapshotService(SnapshotRepository repo)
        {
            _repo = repo;
        }

        public void Configure(string gtavRoot, string snapshotStorageRoot)
        {
            _gtavRoot = gtavRoot ?? "";
            _snapshotStorageRoot = snapshotStorageRoot ?? "";
        }

        public void Load()
        {
            _snapshots = _repo.Load();
        }

        public void Save()
        {
            _repo.Save(_snapshots);
        }

        public IReadOnlyList<GameVersionSnapshot> GetAllSnapshots() => _snapshots.AsReadOnly();

        public OperationResult CreateSnapshot(string label)
        {
            if (string.IsNullOrEmpty(_gtavRoot) || !Directory.Exists(_gtavRoot))
                return OperationResult.Fail("GTA V root is not configured or does not exist.");

            string id = Guid.NewGuid().ToString("N");
            string destFolder = Path.Combine(_snapshotStorageRoot, id);

            try
            {
                Directory.CreateDirectory(destFolder);
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Cannot create snapshot folder: {ex.Message}");
            }

            var snapshot = new GameVersionSnapshot
            {
                Id = id,
                Label = string.IsNullOrWhiteSpace(label) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm") : label,
                CreatedAt = DateTime.UtcNow,
                GameVersion = ExtractGameVersion(Path.Combine(_gtavRoot, "GTA5.exe")),
            };

            foreach (var (relativePath, required) in TargetFiles)
            {
                string src = Path.Combine(_gtavRoot, relativePath);
                if (!File.Exists(src))
                {
                    if (required)
                    {
                        // Clean up partial snapshot
                        TryDeleteFolder(destFolder);
                        return OperationResult.Fail($"Required file not found: {relativePath}");
                    }
                    continue;
                }

                string dest = Path.Combine(destFolder, relativePath);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(src, dest, overwrite: true);
                    snapshot.Files.Add(new SnapshotFile
                    {
                        RelativePath = relativePath,
                        SizeBytes = new FileInfo(src).Length
                    });
                }
                catch (Exception ex)
                {
                    TryDeleteFolder(destFolder);
                    return OperationResult.Fail($"Failed to copy {relativePath}: {ex.Message}");
                }
            }

            _snapshots.Add(snapshot);
            return OperationResult.Ok();
        }

        public OperationResult RestoreSnapshot(string snapshotId)
        {
            var snapshot = _snapshots.FirstOrDefault(s => s.Id == snapshotId);
            if (snapshot == null)
                return OperationResult.Fail($"Snapshot '{snapshotId}' not found.");

            if (string.IsNullOrEmpty(_gtavRoot) || !Directory.Exists(_gtavRoot))
                return OperationResult.Fail("GTA V root is not configured or does not exist.");

            string srcFolder = Path.Combine(_snapshotStorageRoot, snapshotId);
            if (!Directory.Exists(srcFolder))
                return OperationResult.Fail("Snapshot files are missing from storage.");

            foreach (var file in snapshot.Files)
            {
                string src = Path.Combine(srcFolder, file.RelativePath);
                string dest = Path.Combine(_gtavRoot, file.RelativePath);

                if (!File.Exists(src))
                    return OperationResult.Fail($"Snapshot file missing: {file.RelativePath}");

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(src, dest, overwrite: true);
                }
                catch (Exception ex)
                {
                    return OperationResult.Fail($"Failed to restore {file.RelativePath}: {ex.Message}");
                }
            }

            return OperationResult.Ok();
        }

        public OperationResult DeleteSnapshot(string snapshotId)
        {
            var snapshot = _snapshots.FirstOrDefault(s => s.Id == snapshotId);
            if (snapshot == null)
                return OperationResult.Fail($"Snapshot '{snapshotId}' not found.");

            string folder = Path.Combine(_snapshotStorageRoot, snapshotId);
            TryDeleteFolder(folder);
            _snapshots.Remove(snapshot);
            return OperationResult.Ok();
        }

        private static string ExtractGameVersion(string exePath)
        {
            try
            {
                if (!File.Exists(exePath)) return "";
                var info = FileVersionInfo.GetVersionInfo(exePath);
                return info.FileVersion ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static void TryDeleteFolder(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SnapshotService] Failed to delete folder {path}: {ex.Message}");
            }
        }
    }
}
