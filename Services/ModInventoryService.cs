using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// Does NOT move any files on disk.
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

            // Scan for keybinds if we have a GTA V root to resolve absolute paths
            if (!string.IsNullOrEmpty(gtavRoot) && mod.Files.Count > 0)
            {
                var absolutePaths = mod.Files
                    .Select(f => Path.Combine(gtavRoot, f))
                    .Where(File.Exists)
                    .ToList();

                mod.Keybinds = _keybindParser.ScanModFiles(absolutePaths);
            }

            _mods.Add(mod);
            return mod;
        }
    }
}
