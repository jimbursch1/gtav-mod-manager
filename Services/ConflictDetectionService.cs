using System;
using System.Collections.Generic;
using System.Linq;
using GtavModManager.Core;

namespace GtavModManager.Services
{
    public class ConflictDetectionService
    {
        public ConflictReport GenerateReport(IReadOnlyList<Mod> allMods)
        {
            var enabled = allMods.Where(m => m.Status == ModStatus.Enabled).ToList();

            return new ConflictReport
            {
                GeneratedAt = DateTime.UtcNow,
                FileConflicts = DetectFileConflicts(enabled),
                KeybindConflicts = DetectKeybindConflicts(enabled),
                DependencyIssues = DetectDependencyIssues(allMods)
            };
        }

        public List<FileConflict> DetectFileConflicts(IReadOnlyList<Mod> enabledMods)
        {
            var fileOwners = new Dictionary<string, List<(string id, string name)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in enabledMods)
            {
                if (mod.Files == null) continue;
                foreach (var file in mod.Files)
                {
                    if (!fileOwners.ContainsKey(file))
                        fileOwners[file] = new List<(string, string)>();
                    fileOwners[file].Add((mod.Id, mod.Name));
                }
            }

            return fileOwners
                .Where(kvp => kvp.Value.Count > 1)
                .Select(kvp => new FileConflict
                {
                    FilePath = kvp.Key,
                    OwnerModIds = kvp.Value.Select(v => v.id).ToList(),
                    OwnerModNames = kvp.Value.Select(v => v.name).ToList(),
                    Severity = ConflictSeverity.Error
                })
                .ToList();
        }

        public List<KeybindConflict> DetectKeybindConflicts(IReadOnlyList<Mod> enabledMods)
        {
            var keybindOwners = new Dictionary<string, List<KeybindConflictEntry>>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in enabledMods)
            {
                if (mod.Keybinds == null) continue;
                foreach (var kb in mod.Keybinds)
                {
                    if (string.IsNullOrEmpty(kb.Key)) continue;

                    string compositeKey = kb.Modifiers != null && kb.Modifiers.Count > 0
                        ? string.Join("+", kb.Modifiers.OrderBy(m => m)) + "+" + kb.Key.ToUpperInvariant()
                        : kb.Key.ToUpperInvariant();

                    if (!keybindOwners.ContainsKey(compositeKey))
                        keybindOwners[compositeKey] = new List<KeybindConflictEntry>();

                    keybindOwners[compositeKey].Add(new KeybindConflictEntry
                    {
                        ModId = mod.Id,
                        ModName = mod.Name,
                        Action = kb.Action,
                        ConfigFile = kb.ConfigFile
                    });
                }
            }

            return keybindOwners
                .Where(kvp => kvp.Value.Count > 1)
                .Select(kvp =>
                {
                    var parts = kvp.Key.Split('+');
                    return new KeybindConflict
                    {
                        Key = parts[parts.Length - 1],
                        Modifiers = parts.Length > 1 ? parts.Take(parts.Length - 1).ToList() : new List<string>(),
                        Entries = kvp.Value
                    };
                })
                .ToList();
        }

        public List<DependencyIssue> DetectDependencyIssues(IReadOnlyList<Mod> allMods)
        {
            var issues = new List<DependencyIssue>();
            var enabledIds = new HashSet<string>(allMods
                .Where(m => m.Status == ModStatus.Enabled)
                .Select(m => m.Id));

            foreach (var mod in allMods.Where(m => m.Status == ModStatus.Enabled))
            {
                if (mod.Dependencies == null) continue;
                foreach (var dep in mod.Dependencies)
                {
                    if (!enabledIds.Contains(dep.ModId))
                    {
                        issues.Add(new DependencyIssue
                        {
                            ModId = mod.Id,
                            ModName = mod.Name,
                            MissingDependency = dep,
                            Severity = ConflictSeverity.Error
                        });
                    }
                }
            }

            return issues;
        }
    }
}
