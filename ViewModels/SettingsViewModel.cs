using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using GtavModManager.Core;
using GtavModManager.Data;
using GtavModManager.Services;

namespace GtavModManager.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsRepository _repo;
        private readonly GtavRootDetectorService _detector;
        private AppSettings _settings;

        private string _gtavRootPath;
        private string _quarantineFolder;
        private string _streamDeckPath;
        private bool _isGtavRootValid;

        public string GtavRootPath
        {
            get => _gtavRootPath;
            set
            {
                SetProperty(ref _gtavRootPath, value);
                IsGtavRootValid = ValidatePath(value);
                OnPropertyChanged(nameof(IsGtavRootValid));
            }
        }

        public string QuarantineFolder
        {
            get => _quarantineFolder;
            set => SetProperty(ref _quarantineFolder, value);
        }

        public string StreamDeckPath
        {
            get => _streamDeckPath;
            set => SetProperty(ref _streamDeckPath, value);
        }

        public bool IsGtavRootValid
        {
            get => _isGtavRootValid;
            private set => SetProperty(ref _isGtavRootValid, value);
        }

        public RelayCommand BrowseGtavRootCommand { get; }
        public RelayCommand BrowseQuarantineCommand { get; }
        public RelayCommand BrowseStreamDeckCommand { get; }
        public RelayCommand AutoDetectCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand EmergencyRestoreCommand { get; }

        public event Action EmergencyRestoreRequested;

        public SettingsViewModel(SettingsRepository repo, AppSettings settings, GtavRootDetectorService detector)
        {
            _repo = repo;
            _detector = detector;
            _settings = settings;

            _gtavRootPath = settings.GtavRootPath ?? "";
            _quarantineFolder = settings.QuarantineFolder ?? "";
            _streamDeckPath = settings.StreamDeckPath ?? "";
            _isGtavRootValid = ValidatePath(_gtavRootPath);

            BrowseGtavRootCommand = new RelayCommand(BrowseGtavRoot);
            BrowseQuarantineCommand = new RelayCommand(BrowseQuarantine);
            BrowseStreamDeckCommand = new RelayCommand(BrowseStreamDeck);
            AutoDetectCommand = new RelayCommand(AutoDetect);
            SaveCommand = new RelayCommand(Save, () => IsGtavRootValid);
            EmergencyRestoreCommand = new RelayCommand(() => EmergencyRestoreRequested?.Invoke());
        }

        private void AutoDetect()
        {
            var candidates = _detector.FindCandidates();
            if (candidates.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "GTA V installation not found.\n\nTry browsing manually to the folder containing GTA5.exe.",
                    "Auto-detect", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (candidates.Count == 1)
            {
                GtavRootPath = candidates[0];
                return;
            }

            // Multiple candidates — let the user pick
            var list = string.Join("\n", candidates.Select((p, i) => $"{i + 1}. {p}"));
            var msg = $"Found {candidates.Count} GTA V installations:\n\n{list}\n\nUsing the first one. You can change it by browsing manually.";
            System.Windows.MessageBox.Show(msg, "Auto-detect", MessageBoxButton.OK, MessageBoxImage.Information);
            GtavRootPath = candidates[0];
        }

        private void BrowseGtavRoot()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select GTA V Root Folder";
                dialog.SelectedPath = _gtavRootPath;
                if (dialog.ShowDialog() == DialogResult.OK)
                    GtavRootPath = dialog.SelectedPath;
            }
        }

        private void BrowseQuarantine()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Quarantine Folder";
                dialog.SelectedPath = _quarantineFolder;
                if (dialog.ShowDialog() == DialogResult.OK)
                    QuarantineFolder = dialog.SelectedPath;
            }
        }

        private void BrowseStreamDeck()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select DIY Stream Deck Folder (containing server.js)";
                dialog.SelectedPath = _streamDeckPath;
                if (dialog.ShowDialog() == DialogResult.OK)
                    StreamDeckPath = dialog.SelectedPath;
            }
        }

        private void Save()
        {
            _settings.GtavRootPath = _gtavRootPath;
            _settings.QuarantineFolder = string.IsNullOrEmpty(_quarantineFolder)
                ? Path.Combine(_gtavRootPath, "ModManager", "storage")
                : _quarantineFolder;
            _settings.StreamDeckPath = _streamDeckPath;
            _repo.Save(_settings);
        }

        private bool ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return Directory.Exists(path);
        }
    }
}
