using System;
using System.Collections.Generic;
using System.Linq;

namespace GtavModManager.Core
{
    public class ConflictReport
    {
        public DateTime GeneratedAt { get; set; }
        public List<FileConflict> FileConflicts { get; set; } = new List<FileConflict>();
        public List<KeybindConflict> KeybindConflicts { get; set; } = new List<KeybindConflict>();
        public List<DependencyIssue> DependencyIssues { get; set; } = new List<DependencyIssue>();

        public int TotalErrors =>
            FileConflicts.Count(c => c.Severity == ConflictSeverity.Error) +
            KeybindConflicts.Count;

        public int TotalIssues =>
            FileConflicts.Count + KeybindConflicts.Count + DependencyIssues.Count;
    }

    public class FileConflict
    {
        public string FilePath { get; set; }
        public List<string> OwnerModIds { get; set; } = new List<string>();
        public List<string> OwnerModNames { get; set; } = new List<string>();
        public ConflictSeverity Severity { get; set; }
        public string Resolution { get; set; }
    }

    public class KeybindConflict
    {
        public string Key { get; set; }
        public List<string> Modifiers { get; set; } = new List<string>();
        public List<KeybindConflictEntry> Entries { get; set; } = new List<KeybindConflictEntry>();

        public string DisplayKey => Modifiers != null && Modifiers.Count > 0
            ? string.Join("+", Modifiers) + "+" + Key
            : Key;
    }

    public class KeybindConflictEntry
    {
        public string ModId { get; set; }
        public string ModName { get; set; }
        public string Action { get; set; }
        public string ConfigFile { get; set; }
    }

    public class DependencyIssue
    {
        public string ModId { get; set; }
        public string ModName { get; set; }
        public ModDependency MissingDependency { get; set; }
        public ConflictSeverity Severity { get; set; }
    }
}
