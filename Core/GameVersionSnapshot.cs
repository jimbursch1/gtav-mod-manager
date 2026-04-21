using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GtavModManager.Core
{
    public class GameVersionSnapshot
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("gameVersion")]
        public string GameVersion { get; set; }

        [JsonProperty("files")]
        public List<SnapshotFile> Files { get; set; } = new List<SnapshotFile>();

        [JsonIgnore]
        public long TotalSizeBytes
        {
            get
            {
                long total = 0;
                foreach (var f in Files) total += f.SizeBytes;
                return total;
            }
        }

        [JsonIgnore]
        public string TotalSizeDisplay
        {
            get
            {
                long bytes = TotalSizeBytes;
                if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
                if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
                if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
                return $"{bytes} B";
            }
        }
    }

    public class SnapshotFile
    {
        [JsonProperty("relativePath")]
        public string RelativePath { get; set; }

        [JsonProperty("sizeBytes")]
        public long SizeBytes { get; set; }
    }
}
