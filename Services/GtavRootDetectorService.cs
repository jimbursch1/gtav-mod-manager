using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace GtavModManager.Services
{
    /// <summary>
    /// Searches common locations and registry keys to find GTA V installations.
    /// A valid candidate must contain GTA5.exe.
    /// </summary>
    public class GtavRootDetectorService
    {
        public List<string> FindCandidates()
        {
            var candidates = new List<string>();

            TryAdd(candidates, FromRockstarRegistry());
            TryAdd(candidates, FromSteamRegistry());
            foreach (var path in FromSteamLibraryFolders())
                TryAdd(candidates, path);
            foreach (var path in CommonPaths())
                TryAdd(candidates, path);

            return candidates;
        }

        /// <summary>
        /// Returns true if the path looks like a valid GTA V root (contains GTA5.exe).
        /// </summary>
        public bool IsValidRoot(string path)
        {
            return !string.IsNullOrEmpty(path)
                && Directory.Exists(path)
                && File.Exists(Path.Combine(path, "GTA5.exe"));
        }

        // ── Registry sources ──────────────────────────────────────────────────

        private string FromRockstarRegistry()
        {
            try
            {
                // Rockstar Games Launcher / Social Club installs
                using (var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V"))
                {
                    return key?.GetValue("InstallFolder") as string;
                }
            }
            catch { return null; }
        }

        private string FromSteamRegistry()
        {
            try
            {
                string steamPath = GetSteamPath();
                if (string.IsNullOrEmpty(steamPath)) return null;

                string candidate = Path.Combine(steamPath, "steamapps", "common", "Grand Theft Auto V");
                return IsValidRoot(candidate) ? candidate : null;
            }
            catch { return null; }
        }

        private IEnumerable<string> FromSteamLibraryFolders()
        {
            // Steam can have multiple library folders (e.g. on different drives).
            // They're listed in libraryfolders.vdf.
            var results = new List<string>();
            try
            {
                string steamPath = GetSteamPath();
                if (string.IsNullOrEmpty(steamPath)) return results;

                string vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) return results;

                foreach (var line in File.ReadAllLines(vdf))
                {
                    // VDF line format:  "path"  "C:\\SteamLibrary"
                    string trimmed = line.Trim();
                    if (!trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase)) continue;

                    int first = trimmed.IndexOf('"', 6);
                    int last = trimmed.LastIndexOf('"');
                    if (first < 0 || last <= first) continue;

                    string libPath = trimmed.Substring(first + 1, last - first - 1).Replace("\\\\", "\\");
                    string candidate = Path.Combine(libPath, "steamapps", "common", "Grand Theft Auto V");
                    if (IsValidRoot(candidate))
                        results.Add(candidate);
                }
            }
            catch { }
            return results;
        }

        private string GetSteamPath()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                    ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    return key?.GetValue("InstallPath") as string;
                }
            }
            catch { return null; }
        }

        // ── Common install paths ──────────────────────────────────────────────

        private static IEnumerable<string> CommonPaths()
        {
            var drives = new[] { "C:", "D:", "E:", "F:" };
            var suffixes = new[]
            {
                @"Program Files\Rockstar Games\Grand Theft Auto V",
                @"Program Files (x86)\Rockstar Games\Grand Theft Auto V",
                @"Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V",
                @"Program Files\Epic Games\GTAV",
                @"SteamLibrary\steamapps\common\Grand Theft Auto V",
                @"Games\Grand Theft Auto V",
                @"Games\GTA V",
                @"Games\GTAV",
            };

            foreach (var drive in drives)
                foreach (var suffix in suffixes)
                    yield return Path.Combine(drive + Path.DirectorySeparatorChar, suffix);
        }

        private static void TryAdd(List<string> list, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            // Normalise separators and deduplicate
            string normalised = path.TrimEnd('\\', '/');
            if (!list.Contains(normalised, StringComparer.OrdinalIgnoreCase))
                list.Add(normalised);
        }
    }

    // Extension for StringComparer list contains
    internal static class ListExtensions
    {
        public static bool Contains(this List<string> list, string value, StringComparer comparer)
        {
            foreach (var item in list)
                if (comparer.Equals(item, value)) return true;
            return false;
        }
    }
}
