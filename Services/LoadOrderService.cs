using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GtavModManager.Core;

namespace GtavModManager.Services
{
    public class LoadOrderService
    {
        private string _gtavRoot;

        public void Configure(string gtavRoot)
        {
            _gtavRoot = gtavRoot;
        }

        public IOrderedEnumerable<Mod> GetOrderedMods(IReadOnlyList<Mod> mods)
        {
            return mods.OrderBy(m => m.LoadOrder == 0 ? int.MaxValue : m.LoadOrder);
        }

        public void SetLoadOrder(Mod mod, int order)
        {
            mod.LoadOrder = order;
        }

        public List<string> ValidateLoadOrder(IReadOnlyList<Mod> mods)
        {
            var warnings = new List<string>();
            var orders = mods.Where(m => m.LoadOrder > 0).GroupBy(m => m.LoadOrder);
            foreach (var group in orders.Where(g => g.Count() > 1))
                warnings.Add($"Load order {group.Key} assigned to multiple mods: {string.Join(", ", group.Select(m => m.Name))}");
            return warnings;
        }

        /// <summary>
        /// Applies numeric prefixes to ASI/RagePlugin files to enforce load order.
        /// Stores original filename in file list for reversibility.
        /// Only applies to enabled mods with LoadOrder > 0.
        /// </summary>
        public OperationResult ApplyLoadOrder(IReadOnlyList<Mod> mods)
        {
            if (string.IsNullOrEmpty(_gtavRoot))
                return OperationResult.Fail("GTA V root not configured.");

            var ordered = mods
                .Where(m => m.Status == ModStatus.Enabled && m.LoadOrder > 0)
                .Where(m => m.Type == ModType.AsiPlugin || m.Type == ModType.RagePlugin)
                .OrderBy(m => m.LoadOrder)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                var mod = ordered[i];
                foreach (var relativePath in mod.Files.Where(f =>
                    f.EndsWith(".asi", StringComparison.OrdinalIgnoreCase) ||
                    (f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && f.StartsWith("plugins", StringComparison.OrdinalIgnoreCase))))
                {
                    string dir = Path.GetDirectoryName(relativePath);
                    string filename = Path.GetFileName(relativePath);
                    string prefix = $"{(i + 1) * 10:D3}_";

                    // Only rename if not already prefixed
                    if (!filename.StartsWith(prefix))
                    {
                        string newFilename = prefix + filename;
                        string newRelative = string.IsNullOrEmpty(dir)
                            ? newFilename
                            : Path.Combine(dir, newFilename);

                        string src = Path.Combine(_gtavRoot, relativePath);
                        string dst = Path.Combine(_gtavRoot, newRelative);

                        try
                        {
                            if (File.Exists(src))
                                File.Move(src, dst);
                        }
                        catch (Exception ex)
                        {
                            return OperationResult.Fail($"Failed to rename {filename}: {ex.Message}");
                        }
                    }
                }
            }

            return OperationResult.Ok();
        }
    }
}
