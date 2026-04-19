using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using GtavModManager.Core;
using GtavModManager.Services;

namespace GtavModManager.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        private readonly ModInventoryService _inventory;
        private readonly ConflictDetectionService _conflicts;
        private readonly GameLauncherService _launcher;
        private readonly AppSettings _settings;

        private int _enabledCount;
        private int _totalCount;
        private int _conflictCount;
        private string _activeProfileName;
        private string _launchError;
        private bool _rphAvailable;
        private bool _gtaAvailable;
        private bool _streamDeckAvailable;
        private bool _streamDeckRunning;

        public int EnabledCount
        {
            get => _enabledCount;
            private set => SetProperty(ref _enabledCount, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            private set => SetProperty(ref _totalCount, value);
        }

        public int ConflictCount
        {
            get => _conflictCount;
            private set => SetProperty(ref _conflictCount, value);
        }

        public string ActiveProfileName
        {
            get => _activeProfileName;
            private set => SetProperty(ref _activeProfileName, value);
        }

        public string LaunchError
        {
            get => _launchError;
            private set => SetProperty(ref _launchError, value);
        }

        public bool RphAvailable
        {
            get => _rphAvailable;
            private set => SetProperty(ref _rphAvailable, value);
        }

        public bool GtaAvailable
        {
            get => _gtaAvailable;
            private set => SetProperty(ref _gtaAvailable, value);
        }

        public bool StreamDeckAvailable
        {
            get => _streamDeckAvailable;
            private set => SetProperty(ref _streamDeckAvailable, value);
        }

        public bool StreamDeckRunning
        {
            get => _streamDeckRunning;
            private set
            {
                SetProperty(ref _streamDeckRunning, value);
                OnPropertyChanged(nameof(StreamDeckButtonLabel));
            }
        }

        public string StreamDeckButtonLabel => _streamDeckRunning ? "Relaunch" : "Launch";

        public bool GtavRootConfigured => !string.IsNullOrEmpty(_settings.GtavRootPath);

        public RelayCommand LaunchRphCommand { get; }
        public RelayCommand LaunchDirectCommand { get; }
        public RelayCommand LaunchSteamCommand { get; }
        public RelayCommand LaunchStreamDeckCommand { get; }

        public HomeViewModel(
            ModInventoryService inventory,
            ConflictDetectionService conflicts,
            GameLauncherService launcher,
            AppSettings settings)
        {
            _inventory = inventory;
            _conflicts = conflicts;
            _launcher = launcher;
            _settings = settings;

            LaunchRphCommand = new RelayCommand(() => Launch(LaunchMethod.RagePluginHook),
                () => RphAvailable);
            LaunchDirectCommand = new RelayCommand(() => Launch(LaunchMethod.Direct),
                () => GtaAvailable);
            LaunchSteamCommand = new RelayCommand(() => Launch(LaunchMethod.Steam));
            LaunchStreamDeckCommand = new RelayCommand(LaunchStreamDeck, () => StreamDeckAvailable);

            Refresh();
        }

        public void Refresh()
        {
            var all = _inventory.GetAllMods();
            TotalCount = all.Count;
            EnabledCount = _inventory.GetEnabledMods().Count;

            var report = _conflicts.GenerateReport(all);
            ConflictCount = report.TotalErrors;

            RphAvailable = _launcher.CanLaunch(LaunchMethod.RagePluginHook);
            GtaAvailable = _launcher.CanLaunch(LaunchMethod.Direct);
            StreamDeckAvailable = !string.IsNullOrEmpty(_settings.StreamDeckPath)
                && File.Exists(Path.Combine(_settings.StreamDeckPath, "server.js"));
            StreamDeckRunning = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners().Any(ep => ep.Port == 3000);

            LaunchRphCommand.RaiseCanExecuteChanged();
            LaunchDirectCommand.RaiseCanExecuteChanged();
            LaunchStreamDeckCommand.RaiseCanExecuteChanged();

            OnPropertyChanged(nameof(GtavRootConfigured));
        }

        public void SetActiveProfileName(string name)
        {
            ActiveProfileName = name;
        }

        private void Launch(LaunchMethod method)
        {
            LaunchError = null;
            var result = _launcher.Launch(method);
            if (!result.Success)
                LaunchError = result.ErrorMessage;
        }

        private void LaunchStreamDeck()
        {
            LaunchError = null;
            try
            {
                // Kill any existing server.js process on port 3000 before starting
                var kill = new ProcessStartInfo("cmd.exe")
                {
                    Arguments = "/c for /f \"tokens=5\" %a in ('netstat -aon ^| findstr :3000 ^| findstr LISTENING') do taskkill /F /PID %a",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (var p = Process.Start(kill)) p?.WaitForExit(3000);

                var psi = new ProcessStartInfo("cmd.exe")
                {
                    Arguments = $"/k node server.js",
                    WorkingDirectory = _settings.StreamDeckPath,
                    UseShellExecute = true,
                };
                Process.Start(psi);
                StreamDeckRunning = true;
            }
            catch (System.Exception ex)
            {
                LaunchError = $"Failed to launch Stream Deck: {ex.Message}";
            }
        }
    }
}
