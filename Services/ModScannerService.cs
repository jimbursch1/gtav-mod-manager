using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GtavModManager.Core;

namespace GtavModManager.Services
{
    /// <summary>
    /// Scans a GTA V root directory and groups files into mod candidates.
    ///
    /// Scanning strategy:
    ///   - Root *.asi                         → AsiPlugin   (one mod per file)
    ///   - scripts\*.dll / *.cs               → ShvdnScript (one mod per file)
    ///   - plugins\*.dll (non-LSPDFR)         → RagePlugin  (one mod per file)
    ///   - plugins\LSPDFR\*.dll (loose)       → LspdfPlugin (one mod per file)
    ///   - plugins\LSPDFR\<subfolder>\        → LspdfPlugin (one mod per folder)
    ///
    /// Issue #8: If a loose DLL and a same-named subfolder both exist (e.g.
    ///   686Callouts.dll + 686Callouts\), they are merged into a single mod entry.
    ///
    /// Config files (.ini, .xml, .json) sharing a base name or subfolder are
    /// automatically included with their associated mod.
    ///
    /// Known framework files (ScriptHookV, RPH, etc.) are NOT imported as mods
    /// but are detected and reported separately as DetectedFrameworks. Their
    /// presence is used to auto-populate Dependencies on each mod candidate.
    /// </summary>
    public class ModScannerService
    {
        // Files that are GTA V itself or engine infrastructure — not frameworks, not mods
        private static readonly HashSet<string> HardSkipFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GTA5.exe",
            "GTAVLauncher.exe",
            "PlayGTAV.exe",
            "dsound.dll",
            "d3d11.dll",
            "dxgi.dll",
            "OpenIV.asi",
            "GtavModManager.exe",
        };

        // Issue #9: file extensions that are never mod content worth tracking
        private static readonly HashSet<string> SkipExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".log", ".license", ".version", ".txt", ".md", ".pdb",
            ".png", ".jpg", ".jpeg", ".gif", ".ico", ".bmp",
            ".ogg", ".wav", ".mp3", ".bik", ".nfo"
        };

        private static readonly HashSet<string> ConfigExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".ini", ".xml", ".json"
        };

        public ScanReport Scan(string gtavRoot, IReadOnlyList<Mod> existingMods)
        {
            if (string.IsNullOrEmpty(gtavRoot) || !Directory.Exists(gtavRoot))
                return new ScanReport();

            // Detect which known frameworks are present
            var presentFrameworks = DetectFrameworks(gtavRoot);

            // Build a set of files already tracked so we can flag duplicates
            var trackedFiles = new HashSet<string>(
                existingMods.SelectMany(m => m.Files),
                StringComparer.OrdinalIgnoreCase);

            var mods = new List<ScanResult>();
            mods.AddRange(ScanAsiPlugins(gtavRoot, trackedFiles));
            mods.AddRange(ScanShvdnScripts(gtavRoot, trackedFiles));
            mods.AddRange(ScanRagePlugins(gtavRoot, trackedFiles));
            mods.AddRange(ScanLspdfPlugins(gtavRoot, trackedFiles));

            // Auto-populate dependencies based on mod type and present frameworks
            foreach (var mod in mods)
            {
                mod.DetectedDependencies = new List<ModDependency>();
                foreach (var fw in presentFrameworks)
                {
                    if (fw.Framework.RequiredBy.Contains(mod.Type))
                    {
                        mod.DetectedDependencies.Add(new ModDependency
                        {
                            ModName = fw.Framework.DisplayName,
                            ModId = fw.Framework.FileName // use filename as a stable identifier
                        });
                    }
                }
            }

            return new ScanReport
            {
                Mods = mods.OrderBy(r => r.AlreadyImported).ThenBy(r => r.Type).ThenBy(r => r.SuggestedName).ToList(),
                DetectedFrameworks = presentFrameworks
            };
        }

        private List<DetectedFramework> DetectFrameworks(string root)
        {
            var found = new List<DetectedFramework>();
            foreach (var kvp in KnownFrameworks.All)
            {
                string fullPath = Path.Combine(root, kvp.Key);
                // Also check scripts\ and plugins\ for utility DLLs
                bool present = File.Exists(fullPath)
                    || File.Exists(Path.Combine(root, "scripts", kvp.Key))
                    || File.Exists(Path.Combine(root, "plugins", kvp.Key));

                if (present)
                    found.Add(new DetectedFramework { Framework = kvp.Value, IsPresent = true });
            }
            return found;
        }

        private List<ScanResult> ScanAsiPlugins(string root, HashSet<string> tracked)
        {
            var results = new List<ScanResult>();
            if (!Directory.Exists(root)) return results;

            foreach (var file in Directory.GetFiles(root, "*.asi", SearchOption.TopDirectoryOnly))
            {
                string filename = Path.GetFileName(file);
                if (ShouldSkipFile(filename)) continue;

                string rel = filename;
                var files = new List<string> { rel };

                // Include matching config files in same directory
                files.AddRange(FindSiblingConfigs(root, root, Path.GetFileNameWithoutExtension(file)));

                results.Add(new ScanResult
                {
                    SuggestedName = Path.GetFileNameWithoutExtension(file),
                    Type = ModType.AsiPlugin,
                    RelativeFiles = files,
                    AlreadyImported = tracked.Contains(rel)
                });
            }
            return results;
        }

        private List<ScanResult> ScanShvdnScripts(string root, HashSet<string> tracked)
        {
            var results = new List<ScanResult>();
            string scriptsDir = Path.Combine(root, "scripts");
            if (!Directory.Exists(scriptsDir)) return results;

            // Issue #8: collect loose DLLs/CS files, keyed by name for subfolder merge
            var dllResults = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);
            var scriptFiles = Directory.GetFiles(scriptsDir, "*.dll", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(scriptsDir, "*.cs", SearchOption.TopDirectoryOnly));

            foreach (var file in scriptFiles)
            {
                string filename = Path.GetFileName(file);
                if (ShouldSkipFile(filename)) continue;

                string modName = Path.GetFileNameWithoutExtension(file);
                string rel = Path.Combine("scripts", filename);
                var files = new List<string> { rel };
                files.AddRange(FindSiblingConfigs(root, scriptsDir, modName));

                var result = new ScanResult
                {
                    SuggestedName = modName,
                    Type = ModType.ShvdnScript,
                    RelativeFiles = files,
                    AlreadyImported = tracked.Contains(rel)
                };
                dllResults[modName] = result;
            }

            // Subfolders in scripts/ — merge into DLL result if names match
            foreach (var dir in Directory.GetDirectories(scriptsDir))
            {
                string folderName = Path.GetFileName(dir);
                var folderFiles = GetAllFilesRelative(root, dir);
                if (folderFiles.Count == 0) continue;

                if (dllResults.TryGetValue(folderName, out var existing))
                {
                    existing.RelativeFiles.AddRange(folderFiles);
                    if (folderFiles.Any(f => tracked.Contains(f)))
                        existing.AlreadyImported = true;
                }
                else
                {
                    results.Add(new ScanResult
                    {
                        SuggestedName = folderName,
                        Type = ModType.ShvdnScript,
                        RelativeFiles = folderFiles,
                        AlreadyImported = folderFiles.Any(f => tracked.Contains(f))
                    });
                }
            }

            results.AddRange(dllResults.Values);
            return results;
        }

        private List<ScanResult> ScanRagePlugins(string root, HashSet<string> tracked)
        {
            var results = new List<ScanResult>();
            string pluginsDir = Path.Combine(root, "plugins");
            if (!Directory.Exists(pluginsDir)) return results;

            // Issue #8: collect loose DLLs keyed by name for subfolder merge
            var dllResults = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(pluginsDir, "*.dll", SearchOption.TopDirectoryOnly))
            {
                string filename = Path.GetFileName(file);
                if (ShouldSkipFile(filename)) continue;

                string modName = Path.GetFileNameWithoutExtension(file);
                string rel = Path.Combine("plugins", filename);
                var files = new List<string> { rel };
                files.AddRange(FindSiblingConfigs(root, pluginsDir, modName));

                var result = new ScanResult
                {
                    SuggestedName = modName,
                    Type = ModType.RagePlugin,
                    RelativeFiles = files,
                    AlreadyImported = tracked.Contains(rel)
                };
                dllResults[modName] = result;
            }

            // Non-LSPDFR subfolders in plugins\ — merge into DLL result if names match
            foreach (var dir in Directory.GetDirectories(pluginsDir))
            {
                string folderName = Path.GetFileName(dir);
                if (folderName.Equals("LSPDFR", StringComparison.OrdinalIgnoreCase)) continue;

                var folderFiles = GetAllFilesRelative(root, dir);
                if (folderFiles.Count == 0) continue;

                if (dllResults.TryGetValue(folderName, out var existing))
                {
                    existing.RelativeFiles.AddRange(folderFiles);
                    if (folderFiles.Any(f => tracked.Contains(f)))
                        existing.AlreadyImported = true;
                }
                else
                {
                    results.Add(new ScanResult
                    {
                        SuggestedName = folderName,
                        Type = ModType.RagePlugin,
                        RelativeFiles = folderFiles,
                        AlreadyImported = folderFiles.Any(f => tracked.Contains(f))
                    });
                }
            }

            results.AddRange(dllResults.Values);
            return results;
        }

        private List<ScanResult> ScanLspdfPlugins(string root, HashSet<string> tracked)
        {
            var results = new List<ScanResult>();
            string lspdfDir = Path.Combine(root, "plugins", "LSPDFR");
            if (!Directory.Exists(lspdfDir)) return results;

            // Issue #8: first pass — collect loose DLLs keyed by name
            var dllResults = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(lspdfDir, "*.dll", SearchOption.TopDirectoryOnly))
            {
                string filename = Path.GetFileName(file);
                if (ShouldSkipFile(filename)) continue;

                string modName = Path.GetFileNameWithoutExtension(file);
                string rel = Path.Combine("plugins", "LSPDFR", filename);
                var files = new List<string> { rel };
                files.AddRange(FindSiblingConfigs(root, lspdfDir, modName));

                var result = new ScanResult
                {
                    SuggestedName = modName,
                    Type = ModType.LspdfPlugin,
                    RelativeFiles = files,
                    AlreadyImported = tracked.Contains(rel)
                };
                dllResults[modName] = result;
            }

            // Issue #8: second pass — subfolders; merge into DLL result if names match
            foreach (var dir in Directory.GetDirectories(lspdfDir))
            {
                string folderName = Path.GetFileName(dir);
                var folderFiles = GetAllFilesRelative(root, dir);
                if (folderFiles.Count == 0) continue;

                if (dllResults.TryGetValue(folderName, out var existing))
                {
                    // Merge: DLL + subfolder are the same mod
                    existing.RelativeFiles.AddRange(folderFiles);
                    if (folderFiles.Any(f => tracked.Contains(f)))
                        existing.AlreadyImported = true;
                }
                else
                {
                    results.Add(new ScanResult
                    {
                        SuggestedName = folderName,
                        Type = ModType.LspdfPlugin,
                        RelativeFiles = folderFiles,
                        AlreadyImported = folderFiles.Any(f => tracked.Contains(f))
                    });
                }
            }

            results.AddRange(dllResults.Values);
            return results;
        }

        /// <summary>
        /// Returns relative paths (from gtavRoot) of all files in a directory tree,
        /// skipping system and asset files.
        /// </summary>
        private static List<string> GetAllFilesRelative(string gtavRoot, string directory)
        {
            return Directory
                .GetFiles(directory, "*", SearchOption.AllDirectories)
                .Where(f => !ShouldSkipFile(Path.GetFileName(f)))
                .Select(f => MakeRelative(gtavRoot, f))
                .ToList();
        }

        /// <summary>
        /// Finds config files (.ini, .xml, .json) in a directory whose base name
        /// matches <paramref name="baseName"/>. Returns relative paths.
        /// </summary>
        private static List<string> FindSiblingConfigs(string gtavRoot, string directory, string baseName)
        {
            var configs = new List<string>();
            foreach (var ext in ConfigExtensions)
            {
                string candidate = Path.Combine(directory, baseName + ext);
                if (File.Exists(candidate))
                    configs.Add(MakeRelative(gtavRoot, candidate));
            }
            return configs;
        }

        // Issue #9: skip known system files and asset-only extensions
        private static bool ShouldSkipFile(string filename) =>
            HardSkipFiles.Contains(filename)
            || KnownFrameworks.All.ContainsKey(filename)
            || SkipExtensions.Contains(Path.GetExtension(filename));

        // Keep old name as alias so nothing else breaks
        private static bool IsSystemFile(string filename) => ShouldSkipFile(filename);

        private static string MakeRelative(string root, string fullPath)
        {
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString()))
                root += Path.DirectorySeparatorChar;
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(root.Length)
                : fullPath;
        }
    }
}
