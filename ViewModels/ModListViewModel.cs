using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using GtavModManager.Core;
using GtavModManager.Services;

namespace GtavModManager.ViewModels
{
    public class ModListViewModel : ViewModelBase
    {
        private readonly ModInventoryService _inventory;
        private readonly QuarantineService _quarantine;
        private readonly ConflictDetectionService _conflicts;
        private readonly ModScannerService _scanner;

        private ModRowViewModel _selectedMod;
        private string _filterText;
        private bool _isOperationRunning;
        private string _operationError;

        public ObservableCollection<ModRowViewModel> Mods { get; } = new ObservableCollection<ModRowViewModel>();
        public ICollectionView FilteredMods { get; }

        public ModRowViewModel SelectedMod
        {
            get => _selectedMod;
            set
            {
                SetProperty(ref _selectedMod, value);
                ToggleModCommand.RaiseCanExecuteChanged();
                DeleteModCommand.RaiseCanExecuteChanged();
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                SetProperty(ref _filterText, value);
                FilteredMods.Refresh();
            }
        }

        public bool IsOperationRunning
        {
            get => _isOperationRunning;
            private set
            {
                SetProperty(ref _isOperationRunning, value);
                ToggleModCommand.RaiseCanExecuteChanged();
            }
        }

        public string OperationError
        {
            get => _operationError;
            private set => SetProperty(ref _operationError, value);
        }

        public RelayCommand<ModRowViewModel> ToggleModCommand { get; }
        public RelayCommand AddModCommand { get; }
        public RelayCommand ScanModsCommand { get; }
        public RelayCommand<ModRowViewModel> DeleteModCommand { get; }
        public RelayCommand RefreshCommand { get; }

        // Raised when the list changes so MainViewModel can update conflict badge
        public event Action ModsChanged;
        public event Action ScanRequested;
        public event Action AddModRequested;

        public string GtavRoot { get; set; }

        public ModListViewModel(
            ModInventoryService inventory,
            QuarantineService quarantine,
            ConflictDetectionService conflicts,
            ModScannerService scanner)
        {
            _inventory = inventory;
            _quarantine = quarantine;
            _conflicts = conflicts;
            _scanner = scanner;

            FilteredMods = CollectionViewSource.GetDefaultView(Mods);
            FilteredMods.Filter = FilterMod;

            ToggleModCommand = new RelayCommand<ModRowViewModel>(ToggleMod,
                row => row != null && !_isOperationRunning);
            AddModCommand = new RelayCommand(() => AddModRequested?.Invoke());
            ScanModsCommand = new RelayCommand(() => ScanRequested?.Invoke());
            DeleteModCommand = new RelayCommand<ModRowViewModel>(DeleteMod,
                row => row != null && !_isOperationRunning);
            RefreshCommand = new RelayCommand(Reload);
        }

        public void Reload()
        {
            Mods.Clear();
            foreach (var mod in _inventory.GetAllMods())
                Mods.Add(new ModRowViewModel(mod));
            RefreshConflictFlags();
        }

        public void RefreshConflictFlags()
        {
            var report = _conflicts.GenerateReport(_inventory.GetAllMods());
            var conflictedIds = new System.Collections.Generic.HashSet<string>(
                report.FileConflicts.SelectMany(c => c.OwnerModIds)
                    .Concat(report.KeybindConflicts.SelectMany(c => c.Entries.Select(e => e.ModId)))
                    .Concat(report.DependencyIssues.Select(d => d.ModId)));

            foreach (var row in Mods)
                row.HasConflicts = conflictedIds.Contains(row.Id);
        }

        private void ToggleMod(ModRowViewModel row)
        {
            if (row == null) return;

            OperationError = null;
            IsOperationRunning = true;

            try
            {
                if (row.Mod.Status == ModStatus.Enabled)
                {
                    var blockers = _quarantine.ValidateCanDisable(row.Mod, _inventory.GetEnabledMods());
                    if (blockers.Count > 0)
                    {
                        OperationError = $"Cannot disable '{row.Mod.Name}' — required by: {string.Join(", ", blockers)}";
                        return;
                    }

                    var result = _quarantine.DisableMod(row.Mod);
                    if (!result.Success)
                        OperationError = result.ErrorMessage;
                }
                else
                {
                    var result = _quarantine.EnableMod(row.Mod);
                    if (!result.Success)
                        OperationError = result.ErrorMessage;
                }

                _inventory.Save();
                row.Refresh();
                RefreshConflictFlags();
                ModsChanged?.Invoke();
            }
            finally
            {
                IsOperationRunning = false;
            }
        }

        public void AddNewMod(string name, ModType type, System.Collections.Generic.List<string> files, string gtavRoot)
        {
            var mod = _inventory.ImportMod(name, type, files, gtavRoot);

            // Move files from GTA V root into permanent storage, then create links
            if (files != null && files.Count > 0 && !string.IsNullOrEmpty(gtavRoot))
            {
                var importResult = _quarantine.ImportToStorage(mod);
                if (!importResult.Success)
                {
                    OperationError = $"Import warning: {importResult.ErrorMessage}";
                    // Mod record still saved — user can fix storage manually
                }
            }

            _inventory.Save();
            Mods.Add(new ModRowViewModel(mod));
            RefreshConflictFlags();
            ModsChanged?.Invoke();
        }

        /// <summary>
        /// Runs a scan and returns the results for the dialog to display.
        /// Returns null if GTA V root is not configured.
        /// </summary>
        public System.Collections.Generic.List<Core.ScanResult> RunScan()
        {
            if (string.IsNullOrEmpty(GtavRoot)) return null;
            return _scanner.Scan(GtavRoot, _inventory.GetAllMods());
        }

        /// <summary>
        /// Imports a batch of confirmed scan results.
        /// </summary>
        public void BulkImport(System.Collections.Generic.List<(string name, ModType type, System.Collections.Generic.List<string> files)> imports)
        {
            foreach (var (name, type, files) in imports)
                AddNewMod(name, type, files, GtavRoot);
        }

        private void DeleteMod(ModRowViewModel row)
        {
            if (row == null) return;
            _inventory.RemoveMod(row.Mod.Id);
            _inventory.Save();
            Mods.Remove(row);
            if (SelectedMod == row) SelectedMod = null;
            ModsChanged?.Invoke();
        }

        private bool FilterMod(object obj)
        {
            if (string.IsNullOrWhiteSpace(_filterText)) return true;
            if (obj is ModRowViewModel row)
                return row.Name.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0
                    || (row.Author != null && row.Author.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0);
            return true;
        }
    }
}
