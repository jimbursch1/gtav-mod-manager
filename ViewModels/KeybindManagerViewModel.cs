using System;
using System.Collections.Generic;
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
        private readonly KeybindParserService _parser;
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
        public RelayCommand<KeybindRowViewModel> EditKeybindCommand { get; }

        /// <summary>
        /// Issue #6: Raised when the user requests to edit a keybind.
        /// The view's code-behind shows an input dialog and calls ConfirmKeybindEdit.
        /// </summary>
        public Action<KeybindRowViewModel> EditKeybindRequested;

        public KeybindManagerViewModel(ModInventoryService inventory, ConflictDetectionService detector, KeybindParserService parser)
        {
            _inventory = inventory;
            _detector = detector;
            _parser = parser;

            FilteredKeybinds = CollectionViewSource.GetDefaultView(AllKeybinds);
            FilteredKeybinds.Filter = FilterRow;
            RunConflictScanCommand = new RelayCommand(Reload);
            EditKeybindCommand = new RelayCommand<KeybindRowViewModel>(row => EditKeybindRequested?.Invoke(row));
        }

        public void Reload()
        {
            AllKeybinds.Clear();

            var mods = _inventory.GetEnabledMods();
            var report = _detector.GenerateReport(_inventory.GetAllMods());
            var conflictedKeys = new HashSet<string>(
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

        /// <summary>
        /// Issue #6: Called by the view code-behind after the user confirms an edit.
        /// Writes the new value back to the config file and refreshes the grid.
        /// </summary>
        public void ConfirmKeybindEdit(KeybindRowViewModel row, string newValue)
        {
            if (row == null || string.IsNullOrWhiteSpace(newValue)) return;

            var mod = _inventory.GetModById(row.ModId);
            if (mod == null) return;

            var keybind = mod.Keybinds?.FirstOrDefault(
                k => k.Action == row.Action && k.ConfigFile == row.ConfigFile);
            if (keybind == null) return;

            bool ok = _parser.WriteKeybindValue(keybind, newValue.Trim(), keybind.ConfigFile);
            if (!ok) return;

            // Update in-memory model
            string[] parts = newValue.Trim().Split('+');
            keybind.Key = parts[parts.Length - 1].Trim();
            keybind.Modifiers = parts.Length > 1
                ? parts.Take(parts.Length - 1).Select(m => m.Trim()).ToList()
                : new List<string>();
            keybind.RawValue = newValue.Trim();

            _inventory.Save();
            Reload();
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
