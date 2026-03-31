# GTA V Mod Manager

A Windows desktop app for managing GTA V mods. Tracks everything in one place — installed mods, file conflicts, keybind collisions, load order, and mod profiles.

## Features

- **Mod Inventory** — Track all installed mods with metadata, file lists, version, author, source URL, and notes
- **Non-Destructive Enable/Disable** — Mod files live permanently in a storage folder; enabling/disabling creates or removes hard links at the GTA V paths so the game sees exactly what it needs and nothing is ever deleted
- **File Conflict Detection** — Flags mods that overwrite the same files
- **Keybind Manager** — Auto-parses INI, XML, and JSON config files; detects key collisions across all mods; edit keybinds in-app
- **Load Order** — Drag-and-drop initialization order for ASI and SHVDN plugins
- **Mod Profiles** — Named configurations (e.g. "LSPDFR", "Freeplay") with one-click switching via NTFS junctions

## Getting Started

### Requirements
- Windows 10 or 11 (64-bit)
- [Visual Studio Build Tools 2022](https://aka.ms/vs/17/release/vs_BuildTools.exe) (for MSBuild)
- .NET Framework 4.8

### Setup & Build

```cmd
git clone https://github.com/jimbursch1/gtav-mod-manager.git
cd gtav-mod-manager
powershell -ExecutionPolicy Bypass -File setup.ps1
MSBuild.exe GtavModManager.csproj /p:Configuration=Release
```

`setup.ps1` downloads `Newtonsoft.Json.dll` into `libs\` automatically. Output: `bin\Release\GtavModManager.exe`

### First Run

1. Launch `GtavModManager.exe`
2. Go to the **Settings** tab
3. Set your GTA V root folder (the folder containing `GTA5.exe`) — click **Auto-detect** to find it automatically
4. Click **Save Settings**
5. Go to the **Mods** tab and click **Scan** to detect mods already in your GTA V folder, or **+ Add Mod** to add one manually

### How the Storage Model Works

When you add a mod (via Scan or + Add Mod), the app *imports* it:

1. The mod's files are moved from your GTA V directory into a permanent storage folder (`<GTA V Root>\ModManager\storage\<mod-id>\`)
2. Hard links are created back at the original GTA V paths — the game sees the files exactly where it expects them

From that point on:
- **Enabling a mod** creates the hard links at the GTA V paths
- **Disabling a mod** removes those links — the files remain safe in storage
- **The game never notices the difference** — it reads the same paths whether the mod is enabled or disabled from its perspective; a disabled mod simply has no links pointing to it

No mod files are ever deleted. If you remove a mod from the inventory, its files stay in storage until you manually clean them up.

## Data Storage

All app data lives in `%AppData%\GtavModManager\`:
- `inventory.json` — mod records
- `profiles.json` — saved profiles
- `settings.json` — app settings (GTA V root path, storage folder)

Mod files are stored in `<GTA V Root>\ModManager\storage\` by default (configurable in Settings).

## Architecture

```
Core/          Domain models (Mod, Profile, ConflictReport, AppSettings)
Data/          JSON persistence (InventoryRepository, ProfileRepository, SettingsRepository)
Services/      Business logic (ModInventoryService, QuarantineService, ConflictDetectionService, KeybindParserService, SymlinkService, LoadOrderService, ProfileService)
ViewModels/    MVVM ViewModels (MainViewModel owns all child VMs and services)
Views/         WPF XAML views (TabControl shell, ModListView master-detail, AddModDialog)
Converters/    WPF value converters
Resources/     Styles (dark theme) and colors
```

## Contributing

PRs welcome. Please branch off `main` and open a pull request — don't push directly.

## License

MIT
