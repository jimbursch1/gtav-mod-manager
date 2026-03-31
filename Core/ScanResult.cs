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
    }
}
