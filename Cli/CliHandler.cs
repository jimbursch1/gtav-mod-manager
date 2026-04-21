using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using GtavModManager.Core;
using GtavModManager.Data;
using GtavModManager.Services;
using Newtonsoft.Json;

namespace GtavModManager.Cli
{
    /// <summary>
    /// Handles command-line invocations of GtavModManager.exe.
    /// Loaded services are headless — no WPF required.
    /// </summary>
    public static class CliHandler
    {
        [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
        [DllImport("kernel32.dll")] static extern bool AllocConsole();

        /// <summary>
        /// Attaches to the parent console (or opens a new one), runs the command,
        /// and returns the process exit code (0 = success, 1 = error).
        /// </summary>
        public static int Run(string[] args)
        {
            // Attach to the calling console (cmd/PowerShell).
            // If there is no parent console, open a new window so output is visible.
            if (!AttachConsole(-1))
                AllocConsole();

            // Redirect Console.Out/Err so Console.WriteLine works after late attachment
            var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
            Console.SetOut(stdout);
            Console.SetError(stderr);

            try
            {
                return Dispatch(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return 1;
            }
        }

        // ── Command dispatch ──────────────────────────────────────────────────

        private static int Dispatch(string[] args)
        {
            if (args.Length == 0 || args[0] == "help" || args[0] == "--help" || args[0] == "-h")
            {
                PrintHelp();
                return 0;
            }

            string cmd = args[0].ToLowerInvariant();
            bool json = args.Contains("--json");

            switch (cmd)
            {
                case "list":     return CmdList(json);
                case "status":   return CmdStatus(json);
                case "enable":   return CmdEnable(Tail(args), json);
                case "disable":  return CmdDisable(Tail(args), json);
                case "scan":     return CmdScan(json);
                case "profile":  return CmdProfile(Tail(args), json);
                case "restore":  return CmdRestore();
                case "snapshot": return CmdSnapshot(Tail(args), json);
                default:
                    Console.Error.WriteLine($"error: unknown command '{args[0]}'. Run with 'help' for usage.");
                    return 1;
            }
        }

        // ── Commands ──────────────────────────────────────────────────────────

        private static int CmdList(bool json)
        {
            var (inventory, _, settings) = LoadServices();
            var mods = inventory.GetAllMods();

            if (json)
            {
                Console.WriteLine(JsonConvert.SerializeObject(mods.Select(ModSummary), Formatting.Indented));
                return 0;
            }

            if (!mods.Any())
            {
                Console.WriteLine("No mods in inventory.");
                return 0;
            }

            int nameW = Math.Max(4, mods.Max(m => m.Name.Length));
            int typeW = Math.Max(4, mods.Max(m => m.Type.ToString().Length));

            Console.WriteLine($"{"NAME".PadRight(nameW)}  {"TYPE".PadRight(typeW)}  STATUS");
            Console.WriteLine(new string('-', nameW + typeW + 12));
            foreach (var m in mods.OrderBy(m => m.Name))
            {
                string status = m.Status == ModStatus.Enabled ? "enabled" : "disabled";
                Console.WriteLine($"{m.Name.PadRight(nameW)}  {m.Type.ToString().PadRight(typeW)}  {status}");
            }
            return 0;
        }

        private static int CmdStatus(bool json)
        {
            var (inventory, _, settings) = LoadServices();
            var mods = inventory.GetAllMods();
            int enabled = mods.Count(m => m.Status == ModStatus.Enabled);
            int total = mods.Count;
            bool rootConfigured = !string.IsNullOrEmpty(settings.GtavRootPath)
                && Directory.Exists(settings.GtavRootPath);

            if (json)
            {
                Console.WriteLine(JsonConvert.SerializeObject(new
                {
                    enabled_mods = enabled,
                    total_mods = total,
                    gtav_root = settings.GtavRootPath,
                    gtav_root_valid = rootConfigured
                }, Formatting.Indented));
                return 0;
            }

            Console.WriteLine($"Mods: {enabled} enabled / {total} total");
            Console.WriteLine($"GTA V root: {(rootConfigured ? settings.GtavRootPath : "(not configured)")}");
            return 0;
        }

        private static int CmdEnable(string[] args, bool json)
        {
            if (args.Length == 0) { Console.Error.WriteLine("usage: enable <name>"); return 1; }
            string query = string.Join(" ", args.Where(a => a != "--json"));

            var (inventory, quarantine, _) = LoadServices();
            var mod = FindMod(inventory, query);
            if (mod == null) { Console.Error.WriteLine($"error: no mod matching '{query}'"); return 1; }

            if (mod.Status == ModStatus.Enabled)
            {
                Console.WriteLine($"'{mod.Name}' is already enabled.");
                return 0;
            }

            var result = quarantine.EnableMod(mod);
            if (!result.Success) { Console.Error.WriteLine($"error: {result.ErrorMessage}"); return 1; }

            inventory.Save();
            Console.WriteLine($"Enabled: {mod.Name}");
            return 0;
        }

        private static int CmdDisable(string[] args, bool json)
        {
            if (args.Length == 0) { Console.Error.WriteLine("usage: disable <name>"); return 1; }
            string query = string.Join(" ", args.Where(a => a != "--json"));

            var (inventory, quarantine, _) = LoadServices();
            var mod = FindMod(inventory, query);
            if (mod == null) { Console.Error.WriteLine($"error: no mod matching '{query}'"); return 1; }

            if (mod.Status == ModStatus.Disabled)
            {
                Console.WriteLine($"'{mod.Name}' is already disabled.");
                return 0;
            }

            var result = quarantine.DisableMod(mod);
            if (!result.Success) { Console.Error.WriteLine($"error: {result.ErrorMessage}"); return 1; }

            inventory.Save();
            Console.WriteLine($"Disabled: {mod.Name}");
            return 0;
        }

        private static int CmdScan(bool json)
        {
            var (inventory, _, settings) = LoadServices();
            if (string.IsNullOrEmpty(settings.GtavRootPath))
            {
                Console.Error.WriteLine("error: GTA V root not configured. Run the GUI and set it in Settings.");
                return 1;
            }

            var scanner = new ModScannerService();
            var report = scanner.Scan(settings.GtavRootPath, inventory.GetAllMods());

            if (json)
            {
                Console.WriteLine(JsonConvert.SerializeObject(new
                {
                    mods = report.Mods.Select(r => new
                    {
                        name = r.SuggestedName,
                        type = r.Type.ToString(),
                        files = r.RelativeFiles.Count,
                        already_imported = r.AlreadyImported
                    }),
                    frameworks = report.DetectedFrameworks.Select(f => f.Framework.DisplayName)
                }, Formatting.Indented));
                return 0;
            }

            if (report.DetectedFrameworks.Any())
                Console.WriteLine("Frameworks: " + string.Join(", ", report.DetectedFrameworks.Select(f => f.Framework.DisplayName)));

            if (!report.Mods.Any())
            {
                Console.WriteLine("No mod candidates found.");
                return 0;
            }

            int nameW = Math.Max(4, report.Mods.Max(m => m.SuggestedName.Length));
            int typeW = Math.Max(4, report.Mods.Max(m => m.Type.ToString().Length));

            Console.WriteLine();
            Console.WriteLine($"{"NAME".PadRight(nameW)}  {"TYPE".PadRight(typeW)}  FILES  STATUS");
            Console.WriteLine(new string('-', nameW + typeW + 18));
            foreach (var r in report.Mods)
            {
                string status = r.AlreadyImported ? "already imported" : "new";
                Console.WriteLine($"{r.SuggestedName.PadRight(nameW)}  {r.Type.ToString().PadRight(typeW)}  {r.RelativeFiles.Count,5}  {status}");
            }
            return 0;
        }

        private static int CmdProfile(string[] args, bool json)
        {
            if (args.Length == 0) { Console.Error.WriteLine("usage: profile <list|switch> [name]"); return 1; }
            string sub = args[0].ToLowerInvariant();

            var (inventory, quarantine, settings) = LoadServices();
            string invFolder = settings.InventoryFolder
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GtavModManager");
            var profileRepo = new ProfileRepository(invFolder);
            var profileSvc = new ProfileService(profileRepo, quarantine);
            profileSvc.Load();

            if (sub == "list")
            {
                var profiles = profileSvc.GetAllProfiles();

                if (json)
                {
                    Console.WriteLine(JsonConvert.SerializeObject(profiles.Select(p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        mod_count = p.EnabledModIds.Count,
                        last_activated = p.LastActivated
                    }), Formatting.Indented));
                    return 0;
                }

                if (!profiles.Any()) { Console.WriteLine("No profiles saved."); return 0; }
                foreach (var p in profiles)
                    Console.WriteLine($"{p.Name}  ({p.EnabledModIds.Count} mods)");
                return 0;
            }

            if (sub == "switch")
            {
                if (args.Length < 2) { Console.Error.WriteLine("usage: profile switch <name>"); return 1; }
                string query = string.Join(" ", args.Skip(1).Where(a => a != "--json"));
                var profile = profileSvc.GetAllProfiles()
                    .FirstOrDefault(p => p.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
                    ?? profileSvc.GetAllProfiles()
                        .FirstOrDefault(p => p.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

                if (profile == null) { Console.Error.WriteLine($"error: no profile matching '{query}'"); return 1; }

                var result = profileSvc.SwitchProfile(profile.Id, inventory.GetAllMods());
                if (!result.Success) { Console.Error.WriteLine($"error: {result.ErrorMessage}"); return 1; }

                inventory.Save();
                Console.WriteLine($"Switched to profile: {profile.Name}");
                return 0;
            }

            Console.Error.WriteLine($"error: unknown profile subcommand '{sub}'");
            return 1;
        }

        private static int CmdRestore()
        {
            var (inventory, quarantine, _) = LoadServices();
            var result = quarantine.RestoreAll(inventory.GetAllMods());
            inventory.Save();

            if (!result.Success)
            {
                Console.Error.WriteLine($"error: {result.ErrorMessage}");
                return 1;
            }

            Console.WriteLine("All mod files restored to GTA V directory.");
            return 0;
        }

        private static int CmdSnapshot(string[] args, bool json)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("usage: snapshot <list|create|restore|delete> [args]");
                return 1;
            }

            string sub = args[0].ToLowerInvariant();
            var (_, _, settings) = LoadServices();
            var snapshotSvc = LoadSnapshotService(settings);

            switch (sub)
            {
                case "list":
                {
                    var snapshots = snapshotSvc.GetAllSnapshots();
                    if (json)
                    {
                        Console.WriteLine(JsonConvert.SerializeObject(snapshots.Select(s => new
                        {
                            id = s.Id,
                            label = s.Label,
                            game_version = s.GameVersion,
                            created_at = s.CreatedAt,
                            size = s.TotalSizeDisplay,
                            files = s.Files.Count
                        }), Formatting.Indented));
                        return 0;
                    }
                    if (!snapshots.Any()) { Console.WriteLine("No snapshots saved."); return 0; }
                    int labelW = Math.Max(5, snapshots.Max(s => s.Label.Length));
                    int verW   = Math.Max(12, snapshots.Max(s => s.GameVersion.Length));
                    Console.WriteLine($"{"LABEL".PadRight(labelW)}  {"GAME VERSION".PadRight(verW)}  DATE                 SIZE");
                    Console.WriteLine(new string('-', labelW + verW + 30));
                    foreach (var s in snapshots)
                        Console.WriteLine($"{s.Label.PadRight(labelW)}  {s.GameVersion.PadRight(verW)}  {s.CreatedAt:yyyy-MM-dd HH:mm}  {s.TotalSizeDisplay}");
                    return 0;
                }

                case "create":
                {
                    string label = args.Length > 1
                        ? string.Join(" ", args.Skip(1).Where(a => a != "--json"))
                        : "";
                    var result = snapshotSvc.CreateSnapshot(label);
                    if (!result.Success) { Console.Error.WriteLine($"error: {result.ErrorMessage}"); return 1; }
                    snapshotSvc.Save();
                    Console.WriteLine("Snapshot saved.");
                    return 0;
                }

                case "restore":
                {
                    if (args.Length < 2) { Console.Error.WriteLine("usage: snapshot restore <label|id>"); return 1; }
                    string query = string.Join(" ", args.Skip(1).Where(a => a != "--json"));
                    var snapshot = FindSnapshot(snapshotSvc, query);
                    if (snapshot == null) { Console.Error.WriteLine($"error: no snapshot matching '{query}'"); return 1; }
                    var result = snapshotSvc.RestoreSnapshot(snapshot.Id);
                    if (!result.Success) { Console.Error.WriteLine($"error: {result.ErrorMessage}"); return 1; }
                    Console.WriteLine($"Restored snapshot: {snapshot.Label}");
                    return 0;
                }

                case "delete":
                {
                    if (args.Length < 2) { Console.Error.WriteLine("usage: snapshot delete <label|id>"); return 1; }
                    string query = string.Join(" ", args.Skip(1).Where(a => a != "--json"));
                    var snapshot = FindSnapshot(snapshotSvc, query);
                    if (snapshot == null) { Console.Error.WriteLine($"error: no snapshot matching '{query}'"); return 1; }
                    var result = snapshotSvc.DeleteSnapshot(snapshot.Id);
                    if (!result.Success) { Console.Error.WriteLine($"error: {result.ErrorMessage}"); return 1; }
                    snapshotSvc.Save();
                    Console.WriteLine($"Deleted snapshot: {snapshot.Label}");
                    return 0;
                }

                default:
                    Console.Error.WriteLine($"error: unknown snapshot subcommand '{sub}'");
                    return 1;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static GtavModManager.Services.GameVersionSnapshotService LoadSnapshotService(AppSettings settings)
        {
            string invFolder = !string.IsNullOrEmpty(settings.InventoryFolder)
                ? settings.InventoryFolder
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GtavModManager");
            var repo = new GtavModManager.Data.SnapshotRepository(invFolder);
            string storageRoot = !string.IsNullOrEmpty(settings.GtavRootPath)
                ? Path.Combine(settings.GtavRootPath, "ModManager", "snapshots")
                : Path.Combine(invFolder, "snapshots");
            var svc = new GtavModManager.Services.GameVersionSnapshotService(repo);
            svc.Configure(settings.GtavRootPath ?? "", storageRoot);
            svc.Load();
            return svc;
        }

        private static GtavModManager.Core.GameVersionSnapshot FindSnapshot(
            GtavModManager.Services.GameVersionSnapshotService svc, string query)
        {
            var all = svc.GetAllSnapshots();
            var exact = all.FirstOrDefault(s =>
                s.Id.Equals(query, StringComparison.OrdinalIgnoreCase) ||
                s.Label.Equals(query, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            var partial = all.Where(s =>
                s.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (partial.Count == 1) return partial[0];
            if (partial.Count > 1)
            {
                Console.Error.WriteLine($"error: '{query}' matches multiple snapshots:");
                foreach (var s in partial)
                    Console.Error.WriteLine($"  {s.Label} ({s.CreatedAt:yyyy-MM-dd})");
                Console.Error.WriteLine("Use a more specific label or the snapshot ID.");
            }
            return null;
        }

        private static (ModInventoryService inventory, QuarantineService quarantine, AppSettings settings) LoadServices()
        {
            string settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GtavModManager", "settings.json");
            var settingsRepo = new SettingsRepository(settingsPath);
            var settings = settingsRepo.Load();

            string invFolder = !string.IsNullOrEmpty(settings.InventoryFolder)
                ? settings.InventoryFolder
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GtavModManager");

            settings.InventoryFolder = invFolder;

            var inventoryRepo = new InventoryRepository(invFolder);
            var keybindParser = new KeybindParserService();
            var inventory = new ModInventoryService(inventoryRepo, keybindParser);
            inventory.Load();

            var quarantine = new QuarantineService(new FileOperationService(), new SymlinkService());
            string storageRoot = !string.IsNullOrEmpty(settings.QuarantineFolder)
                ? settings.QuarantineFolder
                : (!string.IsNullOrEmpty(settings.GtavRootPath)
                    ? Path.Combine(settings.GtavRootPath, "ModManager", "storage")
                    : Path.Combine(invFolder, "storage"));
            quarantine.Configure(settings.GtavRootPath ?? "", storageRoot);

            return (inventory, quarantine, settings);
        }

        /// <summary>
        /// Finds a mod by exact name match first, then case-insensitive partial match.
        /// Returns null if no match or ambiguous.
        /// </summary>
        private static Mod FindMod(ModInventoryService inventory, string query)
        {
            var all = inventory.GetAllMods();
            var exact = all.FirstOrDefault(m => m.Name.Equals(query, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            var partial = all.Where(m => m.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (partial.Count == 1) return partial[0];

            if (partial.Count > 1)
            {
                Console.Error.WriteLine($"error: '{query}' matches multiple mods:");
                foreach (var m in partial)
                    Console.Error.WriteLine($"  {m.Name}");
                Console.Error.WriteLine("Use a more specific name.");
            }

            return null;
        }

        private static object ModSummary(Mod m) => new
        {
            id = m.Id,
            name = m.Name,
            type = m.Type.ToString(),
            status = m.Status.ToString().ToLowerInvariant(),
            version = m.Version,
            author = m.Author
        };

        private static string[] Tail(string[] args) =>
            args.Length > 1 ? args.Skip(1).ToArray() : Array.Empty<string>();

        private static void PrintHelp()
        {
            Console.WriteLine("GTA V Mod Manager — CLI");
            Console.WriteLine();
            Console.WriteLine("Usage: GtavModManager.exe <command> [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  list                       List all mods and their status");
            Console.WriteLine("  enable <name>              Enable a mod (partial name match)");
            Console.WriteLine("  disable <name>             Disable a mod (partial name match)");
            Console.WriteLine("  status                     Show summary (mod counts, GTA V root)");
            Console.WriteLine("  scan                       Scan GTA V directory for unimported mods");
            Console.WriteLine("  profile list               List saved profiles");
            Console.WriteLine("  profile switch <name>      Switch to a profile");
            Console.WriteLine("  restore                    Copy all mod files from storage back to GTA V directory");
            Console.WriteLine("  snapshot list              List saved game version snapshots");
            Console.WriteLine("  snapshot create [label]    Save a snapshot of GTA V core files");
            Console.WriteLine("  snapshot restore <label>   Restore GTA V core files from a snapshot");
            Console.WriteLine("  snapshot delete <label>    Delete a saved snapshot");
            Console.WriteLine("  help                       Show this message");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --json                Output as JSON (all commands)");
            Console.WriteLine();
            Console.WriteLine("Exit codes: 0 = success, 1 = error");
        }
    }
}
