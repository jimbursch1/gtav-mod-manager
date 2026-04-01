using Newtonsoft.Json;

namespace GtavModManager.Core
{
    public class AppSettings
    {
        [JsonProperty("gtavRootPath")]
        public string GtavRootPath { get; set; }

        [JsonProperty("quarantineFolder")]
        public string QuarantineFolder { get; set; }

        [JsonProperty("inventoryFolder")]
        public string InventoryFolder { get; set; }

        [JsonProperty("activeProfileId")]
        public string ActiveProfileId { get; set; }

        [JsonProperty("streamDeckPath")]
        public string StreamDeckPath { get; set; }

        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "1.0";
    }
}
