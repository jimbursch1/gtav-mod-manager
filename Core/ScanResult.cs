using System.Collections.Generic;

namespace GtavModManager.Core
{
    /// <summary>
    /// A mod candidate detected by ModScannerService.
    /// Not yet imported — exists only in the scan results dialog.
    /// </summary>
    public class ScanResult
    {
        public string SuggestedName { get; set; }
        public ModType Type { get; set; }
        public List<string> RelativeFiles { get; set; } = new List<string>();

        /// <summary>True if this mod's files are already tracked in the inventory.</summary>
        public bool AlreadyImported { get; set; }

        /// <summary>User-selected in the review dialog. Defaults to true unless already imported.</summary>
        public bool Selected { get; set; } = true;

        /// <summary>
        /// Dependencies auto-detected from the frameworks present in the GTA V directory.
        /// These are pre-populated on the Mod record at import time.
        /// </summary>
        public List<ModDependency> DetectedDependencies { get; set; } = new List<ModDependency>();
    }

    /// <summary>
    /// A known framework file detected in the GTA V root during a scan.
    /// </summary>
    public class DetectedFramework
    {
        public KnownFramework Framework { get; set; }
        public bool IsPresent { get; set; }
    }

    /// <summary>
    /// Full output of a mod scan: detected mods + detected frameworks.
    /// </summary>
    public class ScanReport
    {
        public List<ScanResult> Mods { get; set; } = new List<ScanResult>();
        public List<DetectedFramework> DetectedFrameworks { get; set; } = new List<DetectedFramework>();
    }
}
