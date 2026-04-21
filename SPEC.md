# GTA V Mod Manager — Project Spec

## Overview
An open source desktop application for managing GTA V mods on Windows. Tracks installed mods, detects file conflicts, manages keybinds across config formats, and enforces dependency rules.

**Platform:** Windows desktop  
**Stack:** C# / WPF  
**License:** Open source (TBD — MIT recommended)

---

## Core Features

### 1. Mod Inventory
Every installed mod gets a record with:

- **Name** — display name
- **Version** — semantic version if available
- **Author**
- **Source URL** — where it came from (Nexus, GitHub, etc.)
- **Install date**
- **Status** — Enabled / Disabled
- **Type** — ASI plugin, ScriptHookVDotNet script, OpenIV package, ELS config, standalone tool, etc.
- **File manifest** — every file the mod owns (relative paths from GTA V root)
- **Dependencies** — other mods this one requires
- **Notes** — freeform user notes

Mods are stored in a local JSON/SQLite database alongside the GTA V install.

---

### 2. Enable / Disable (Non-Destructive)
Disabling a mod does NOT delete files. Instead:

- Mod files are moved to a quarantine folder (e.g. `ModManager/Disabled/<ModName>/`)
- File manifest is preserved so re-enabling restores exact original paths
- Dependency check fires before disabling — warns if other enabled mods depend on this one

---

### 3. File Manifest & Conflict Detection
When a mod is installed or imported:

- The app records every file it owns
- On each install/enable, the app checks for **file path collisions** with other enabled mods
- Conflicts are surfaced in the UI with the list of affected mods
- User can choose a resolution (load order, keep one, or manual override)

---

### 4. Keybind Manager
The flagship feature. Automatically parses mod config files to extract registered keybinds.

#### Supported Config Formats (v1)
| Format | Example Files |
|--------|--------------|
| INI (key = value) | ScriptHookVDotNet configs, most plugin configs |
| XML | LSPDFR plugin configs, ELS |
| JSON | Newer mods |

#### Parsing Strategy
- On install/import, scan the mod's files for known config formats
- Apply format-specific parsers to extract keybind entries
- Store: `{ mod, configFile, action, key, modifiers }`
- Flag anything ambiguous for manual review in the UI

#### Conflict Detection
- After parsing, run a conflict scan across all **enabled** mods
- A conflict = two mods binding the same key+modifier combo to different actions
- Conflicts are displayed in a dedicated **Keybind Conflicts** view
- User can reassign a keybind in-app and the change is written back to the config file

#### Keybind Browser
- View all keybinds across all mods in one table
- Filter by mod, key, or action
- Edit keybinds in-app (writes to config file)
- Visual keyboard map (stretch goal)

---

### 5. Dependency Management
- Each mod record can declare dependencies (name + minimum version)
- On disable/uninstall: warn if other enabled mods depend on it
- On install: check if declared dependencies are present and enabled
- Dependency graph view (stretch goal)

---

## Data Model (simplified)

```
Mod
├── id (guid)
├── name
├── version
├── author
├── sourceUrl
├── installDate
├── status (enabled/disabled)
├── type (enum)
├── notes
├── files[] → FilePath (relative to GTA V root)
├── dependencies[] → ModDependency { modId, minVersion }
└── keybinds[] → Keybind { configFile, action, key, modifiers, ambiguous }
```

---

## UI Views (v1)

1. **Mod List** — sortable/filterable table of all mods, enable/disable toggle
2. **Mod Detail** — full metadata, file list, keybinds, dependencies
3. **Keybind Manager** — all keybinds in one place, conflict highlighting, inline edit
4. **Conflict Report** — file conflicts + keybind conflicts, grouped by severity
5. **Settings** — GTA V install path, quarantine folder location

---

### 6. Load Order Management
Some mods care about the order they initialize. The app will:

- Display a drag-and-drop load order list for ASI plugins and SHVDN scripts
- Persist the order and apply it on enable (by renaming files with numeric prefixes or managing a load order config)
- Warn when a dependency's load order conflicts with the mod that needs it

---

### 7. Mod Profiles
Switch between entirely different mod setups with one click. Examples:
- "LSPDFR Roleplay" — police mods, realistic graphics, no cheats
- "Freeplay" — trainer, graphics overhaul, no LSPDFR

Each profile stores:
- Which mods are enabled/disabled
- Load order for that profile
- Active keybind overrides

Switching profiles triggers a quarantine swap — disabled mods are moved out, enabled mods are moved in. Use folder junctions/symlinks (not file copies) for performance — same approach as GTAModsManager.

---

### 8. Game Version Snapshots

When Rockstar pushes a GTA V update through Steam, ScriptHookV and RagePluginHook break until their authors release compatibility updates (typically a few days). This feature lets you save copies of the core game files beforehand so you can roll back immediately.

**Files captured in each snapshot:**
- `GTA5.exe` (required)
- `update\update.rpf` (required)
- `GTAVLauncher.exe` (if present)
- `PlayGTAV.exe` (if present)
- `GTAVLanguageSelect.exe` (if present)
- `ScriptHookV.dll` (if present)
- `dinput8.dll` — ScriptHookV ASI loader (if present)

**Workflow:**
1. Before allowing Steam to update, click **+ Save Snapshot** and give it a label (e.g., "v1.68 pre-update")
2. After the update breaks mods, click **Restore** next to that snapshot
3. Core files are copied back to the GTA V root — game runs on the old executable again

**Storage:** Snapshot files live at `{GTA V Root}\ModManager\snapshots\{id}\`. Metadata (label, game version, file list, size) is stored in `%AppData%\GtavModManager\snapshots.json`.

**Game version** is extracted automatically from `GTA5.exe` file properties and displayed in the snapshot list.

**CLI:**
```
GtavModManager.exe snapshot list
GtavModManager.exe snapshot create "v1.68 pre-update"
GtavModManager.exe snapshot restore "v1.68"
GtavModManager.exe snapshot delete "v1.68"
```

---

## Out of Scope (v1)
- Automatic mod downloading
- Visual keyboard map
- Cloud sync
- macOS / Linux support

---

## Open Questions
- Distribution: GitHub Releases with a simple installer (NSIS or ClickOnce)?

---

## Next Steps
1. Set up GitHub repo (public, MIT license)
2. Scaffold C# / WPF project
3. Implement mod inventory + file manifest (foundation for everything else)
4. Add enable/disable with quarantine
5. Build keybind parser (INI first, then XML)
6. Build conflict detection UI
