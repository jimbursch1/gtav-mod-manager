using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using GtavModManager.Core;
using GtavModManager.Services;

namespace GtavModManager.ViewModels
{
    public class KeybindRowViewModel : ViewModelBase
    {
        private bool _hasConflict;

        public string ModId { get; set; }
        public string ModName { get; set; }
        public string Action { get; set; }
        public string Key { get; set; }
        public string Modifiers { get; set; }
        public string ConfigFile { get; set; }
        public ConfigFormat Format { get; set; }
        public bool IsAmbiguous { get; set; }

        public bool HasConflict
        {
            get => _hasConflict;
            set => SetProperty(ref _hasConflict, value);
        }

        public string DisplayKey => string.IsNullOrEmpty(Modifiers) ? Key : $"{Modifiers}+{Key}";
    }

    public class KeybindManagerViewModel : ViewModelBase
    {
        private readonly ModInventoryService _inventory;
        private readonly ConflictDetectionService _detector;
        private string _filterText;

        public ObservableCollection<KeybindRowViewModel> AllKeybinds { get; } = new ObservableCollection<KeybindRowViewModel>();
        public ICollectionView FilteredKeybinds { get; }

        public string FilterText
        {
            get => _filterText;
            set
            {
                SetProperty(ref _filterText, value);
                FilteredKeybinds.Refresh();
            }
        }

        public RelayCommand RunConflictScanCommand { get; }

        public KeybindManagerViewModel(ModInventoryService inventory, ConflictDetectionService detector)
        {
            _inventory = inventory;
            _detector = detector;

            FilteredKeybinds = CollectionViewSource.GetDefaultView(AllKeybinds);
            FilteredKeybinds.Filter = FilterRow;
            RunConflictScanCommand = new RelayCommand(Reload);
        }

        public void Reload()
        {
            AllKeybinds.Clear();

            var mods = _inventory.GetEnabledMods();
            var report = _detector.GenerateReport(_inventory.GetAllMods());
            var conflictedKeys = new System.Collections.Generic.HashSet<string>(
                report.KeybindConflicts.SelectMany(c => c.Entries.Select(e => $"{e.ModId}|{e.Action}")));

            foreach (var mod in mods)
            {
                if (mod.Keybinds == null) continue;
                foreach (var kb in mod.Keybinds)
                {
                    string lookup = $"{mod.Id}|{kb.Action}";
                    AllKeybinds.Add(new KeybindRowViewModel
                    {
                        ModId = mod.Id,
                        ModName = mod.Name,
                        Action = kb.Action,
                        Key = kb.Key,
                        Modifiers = kb.Modifiers != null ? string.Join("+", kb.Modifiers) : "",
                        ConfigFile = kb.ConfigFile,
                        Format = kb.Format,
                        IsAmbiguous = kb.IsAmbiguous,
                        HasConflict = conflictedKeys.Contains(lookup)
                    });
                }
            }
        }

        private bool FilterRow(object obj)
        {
            if (string.IsNullOrWhiteSpace(_filterText)) return true;
            if (obj is KeybindRowViewModel row)
                return row.ModName.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0
                    || (row.Action?.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (row.Key?.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0);
            return true;
        }
    }
}
