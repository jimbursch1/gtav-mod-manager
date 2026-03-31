using System.IO;
using System.Windows.Forms;
using GtavModManager.Core;
using GtavModManager.Data;

namespace GtavModManager.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsRepository _repo;
        private AppSettings _settings;

        private string _gtavRootPath;
        private string _quarantineFolder;
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

        public bool IsGtavRootValid
        {
            get => _isGtavRootValid;
            private set => SetProperty(ref _isGtavRootValid, value);
        }

        public RelayCommand BrowseGtavRootCommand { get; }
        public RelayCommand BrowseQuarantineCommand { get; }
        public RelayCommand SaveCommand { get; }

        public SettingsViewModel(SettingsRepository repo, AppSettings settings)
        {
            _repo = repo;
            _settings = settings;

            _gtavRootPath = settings.GtavRootPath ?? "";
            _quarantineFolder = settings.QuarantineFolder ?? "";
            _isGtavRootValid = ValidatePath(_gtavRootPath);

            BrowseGtavRootCommand = new RelayCommand(BrowseGtavRoot);
            BrowseQuarantineCommand = new RelayCommand(BrowseQuarantine);
            SaveCommand = new RelayCommand(Save, () => IsGtavRootValid);
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

        private void Save()
        {
            _settings.GtavRootPath = _gtavRootPath;
            _settings.QuarantineFolder = string.IsNullOrEmpty(_quarantineFolder)
                ? Path.Combine(_gtavRootPath, "ModManager", "Disabled")
                : _quarantineFolder;
            _repo.Save(_settings);
        }

        private bool ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return Directory.Exists(path);
        }
    }
}
