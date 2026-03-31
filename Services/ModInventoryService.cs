using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GtavModManager.Core;
using GtavModManager.Data;

namespace GtavModManager.Services
{
    public class ModInventoryService
    {
        private readonly InventoryRepository _repo;
        private readonly KeybindParserService _keybindParser;
        private List<Mod> _mods = new List<Mod>();

        public ModInventoryService(InventoryRepository repo, KeybindParserService keybindParser)
        {
            _repo = repo;
            _keybindParser = keybindParser;
        }

        public void Load()
        {
            _mods = _repo.Load();
        }

        public void Save()
        {
            _repo.Save(_mods);
        }

        public IReadOnlyList<Mod> GetAllMods() => _mods.AsReadOnly();

        public IReadOnlyList<Mod> GetEnabledMods() =>
            _mods.Where(m => m.Status == ModStatus.Enabled).ToList().AsReadOnly();

        public Mod GetModById(string id) =>
            _mods.FirstOrDefault(m => m.Id == id);

        public void AddMod(Mod mod)
        {
            if (mod == null) throw new ArgumentNullException(nameof(mod));
            _mods.Add(mod);
        }

        public void UpdateMod(Mod updated)
        {
            int idx = _mods.FindIndex(m => m.Id == updated.Id);
            if (idx >= 0)
                _mods[idx] = updated;
        }

        public void RemoveMod(string modId)
        {
            _mods.RemoveAll(m => m.Id == modId);
        }

        /// <summary>
        /// Creates a new Mod record from name, type, and file paths.
        /// Scans config files for keybinds automatically.
        /// Does NOT move files — call QuarantineService.ImportToStorage separately to
        /// move files into permanent storage and create initial links.
        /// </summary>
        public Mod ImportMod(string name, ModType type, List<string> relativeFilePaths, string gtavRoot = null)
        {
            var mod = new Mod
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Type = type,
                Status = ModStatus.Enabled,
                InstallDate = DateTime.UtcNow,
                Files = relativeFilePaths ?? new List<string>(),
                Dependencies = new List<ModDependency>(),
                Keybinds = new List<Keybind>()
            };

            // Scan config files for keybinds before files move to storage
            if (!string.IsNullOrEmpty(gtavRoot) && mod.Files.Count > 0)
            {
                var absolutePaths = mod.Files
                    .Select(f => Path.Combine(gtavRoot, f))
                    .Where(File.Exists)
                    .ToList();

                mod.Keybinds = _keybindParser.ScanModFiles(absolutePaths);

                // Issue #10: auto-detect version from DLL file properties
                foreach (var f in absolutePaths.Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        var info = FileVersionInfo.GetVersionInfo(f);
                        string v = info.ProductVersion ?? info.FileVersion;
                        if (!string.IsNullOrWhiteSpace(v) && v != "0.0.0.0" && v != "1.0.0.0")
                        {
                            mod.Version = v.Trim();
                            break;
                        }
                    }
                    catch { }
                }

                // Issue #10: fallback — look for <Version> or <ModVersion> in XML config files
                if (string.IsNullOrEmpty(mod.Version))
                {
                    foreach (var f in absolutePaths.Where(p => p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            var doc = XDocument.Load(f);
                            var versionEl = doc.Descendants()
                                .FirstOrDefault(e =>
                                    (e.Name.LocalName.Equals("Version", StringComparison.OrdinalIgnoreCase)
                                     || e.Name.LocalName.Equals("ModVersion", StringComparison.OrdinalIgnoreCase))
                                    && !e.HasElements
                                    && !string.IsNullOrWhiteSpace(e.Value));
                            if (versionEl != null)
                            {
                                mod.Version = versionEl.Value.Trim();
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }

            _mods.Add(mod);
            return mod;
        }
    }
}
