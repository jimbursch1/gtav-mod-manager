using System.IO;
using GtavModManager.Core;
using GtavModManager.Data;
using GtavModManager.Services;

namespace GtavModManager.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private int _selectedTabIndex;
        private string _statusMessage = "Ready";
        private int _conflictBadgeCount;

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int ConflictBadgeCount
        {
            get => _conflictBadgeCount;
            set => SetProperty(ref _conflictBadgeCount, value);
        }

        // Services
        public AppSettings Settings { get; }
        public ModInventoryService Inventory { get; }
        public QuarantineService Quarantine { get; }
        public ConflictDetectionService ConflictDetection { get; }
        public ProfileService ProfileSvc { get; }
        public LoadOrderService LoadOrder { get; }
        public KeybindParserService KeybindParser { get; }
        public ModScannerService Scanner { get; }
        public GameLauncherService Launcher { get; }

        // Child ViewModels
        public HomeViewModel Home { get; }
        public ModListViewModel ModList { get; }
        public ModDetailViewModel ModDetail { get; }
        public ConflictReportViewModel ConflictReport { get; }
        public KeybindManagerViewModel KeybindManager { get; }
        public ProfileManagerViewModel ProfileManager { get; }
        public SettingsViewModel SettingsVm { get; }

        public MainViewModel()
        {
            // Repositories
            var settingsPath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "GtavModManager", "settings.json");
            var settingsRepo = new SettingsRepository(settingsPath);
            Settings = settingsRepo.Load();

            // Derive inventory folder from settings or default
            string inventoryFolder = !string.IsNullOrEmpty(Settings.InventoryFolder)
                ? Settings.InventoryFolder
                : Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                    "GtavModManager");

            Settings.InventoryFolder = inventoryFolder;

            var inventoryRepo = new InventoryRepository(inventoryFolder);
            var profileRepo = new ProfileRepository(inventoryFolder);

            // Services
            KeybindParser = new KeybindParserService();
            Inventory = new ModInventoryService(inventoryRepo, KeybindParser);
            Inventory.Load();

            var fileOps = new FileOperationService();
            var symlink = new SymlinkService();
            Quarantine = new QuarantineService(fileOps, symlink);
            ConfigureQuarantine();

            ConflictDetection = new ConflictDetectionService();
            ProfileSvc = new ProfileService(profileRepo, Quarantine);
            ProfileSvc.Load();
            LoadOrder = new LoadOrderService();
            LoadOrder.Configure(Settings.GtavRootPath ?? "");
            Scanner = new ModScannerService();
            Launcher = new GameLauncherService();
            Launcher.Configure(Settings.GtavRootPath ?? "");
            var rootDetector = new GtavRootDetectorService();

            // Child ViewModels
            Home = new HomeViewModel(Inventory, ConflictDetection, Launcher, Settings);
            ModList = new ModListViewModel(Inventory, Quarantine, ConflictDetection, Scanner)
            {
                GtavRoot = Settings.GtavRootPath ?? ""
            };
            ModDetail = new ModDetailViewModel(Inventory);
            ConflictReport = new ConflictReportViewModel(ConflictDetection, Inventory);
            KeybindManager = new KeybindManagerViewModel(Inventory, ConflictDetection);
            ProfileManager = new ProfileManagerViewModel(ProfileSvc, Inventory, Settings);
            SettingsVm = new SettingsViewModel(settingsRepo, Settings, rootDetector);

            // Wire up events
            ModList.ModsChanged += OnModsChanged;
            SettingsVm.EmergencyRestoreRequested += DoEmergencyRestore;
            ConflictReport.ConflictCountChanged += count => ConflictBadgeCount = count;

            // Wire selection: when a row is selected, update detail panel
            ModList.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ModListViewModel.SelectedMod))
                    ModDetail.SelectedMod = ModList.SelectedMod?.Mod;
            };

            // Initial load
            ModList.Reload();
            ConflictReport.Rescan();
            ProfileManager.Reload();
            KeybindManager.Reload();

            var activeProfile = ProfileSvc.GetActiveProfile(Settings.ActiveProfileId);
            Home.SetActiveProfileName(activeProfile?.Name ?? "");

            UpdateStatusMessage();
        }

        private void ConfigureQuarantine()
        {
            // Storage root is where mod files permanently live (not the old "Disabled" quarantine)
            string storageRoot = !string.IsNullOrEmpty(Settings.QuarantineFolder)
                ? Settings.QuarantineFolder
                : Path.Combine(Settings.GtavRootPath ?? "", "ModManager", "storage");
            Quarantine.Configure(Settings.GtavRootPath ?? "", storageRoot);
        }

        private void OnModsChanged()
        {
            ConflictReport.Rescan();
            KeybindManager.Reload();
            Home.Refresh();
            UpdateStatusMessage();
        }

        private void DoEmergencyRestore()
        {
            var confirm = System.Windows.MessageBox.Show(
                "This will copy all mod files from storage back to their GTA V locations and mark all mods as enabled.\n\n" +
                "Use this if the game won't launch or mods are missing after a crash.\n\n" +
                "Continue?",
                "Emergency Restore",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            var result = Quarantine.RestoreAll(Inventory.GetAllMods());
            Inventory.Save();
            ModList.Reload();
            Home.Refresh();
            UpdateStatusMessage();

            System.Windows.MessageBox.Show(
                result.Success
                    ? "All mod files restored to GTA V directory."
                    : $"Restore completed with errors:\n\n{result.ErrorMessage}",
                "Emergency Restore",
                System.Windows.MessageBoxButton.OK,
                result.Success ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Warning);
        }

        private void UpdateStatusMessage()
        {
            int total = Inventory.GetAllMods().Count;
            int enabled = Inventory.GetEnabledMods().Count;
            StatusMessage = $"{enabled}/{total} mods enabled";
        }
    }
}
