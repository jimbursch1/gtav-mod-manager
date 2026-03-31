using System;
using System.Collections.ObjectModel;
using GtavModManager.Core;
using GtavModManager.Services;

namespace GtavModManager.ViewModels
{
    public class ConflictReportViewModel : ViewModelBase
    {
        private readonly ConflictDetectionService _detector;
        private readonly ModInventoryService _inventory;
        private DateTime? _lastScanTime;

        public ObservableCollection<FileConflict> FileConflicts { get; } = new ObservableCollection<FileConflict>();
        public ObservableCollection<KeybindConflict> KeybindConflicts { get; } = new ObservableCollection<KeybindConflict>();
        public ObservableCollection<DependencyIssue> DependencyIssues { get; } = new ObservableCollection<DependencyIssue>();

        public DateTime? LastScanTime
        {
            get => _lastScanTime;
            private set => SetProperty(ref _lastScanTime, value);
        }

        public int TotalIssues => FileConflicts.Count + KeybindConflicts.Count + DependencyIssues.Count;

        public RelayCommand RescanCommand { get; }

        public event Action<int> ConflictCountChanged;

        public ConflictReportViewModel(ConflictDetectionService detector, ModInventoryService inventory)
        {
            _detector = detector;
            _inventory = inventory;
            RescanCommand = new RelayCommand(Rescan);
        }

        public void Rescan()
        {
            var report = _detector.GenerateReport(_inventory.GetAllMods());

            FileConflicts.Clear();
            foreach (var c in report.FileConflicts)
                FileConflicts.Add(c);

            KeybindConflicts.Clear();
            foreach (var c in report.KeybindConflicts)
                KeybindConflicts.Add(c);

            DependencyIssues.Clear();
            foreach (var c in report.DependencyIssues)
                DependencyIssues.Add(c);

            LastScanTime = report.GeneratedAt;
            OnPropertyChanged(nameof(TotalIssues));
            ConflictCountChanged?.Invoke(report.TotalErrors);
        }
    }
}
