using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GtavModManager.Core
{
    public class Profile
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("enabledModIds")]
        public List<string> EnabledModIds { get; set; } = new List<string>();

        [JsonProperty("loadOrder")]
        public Dictionary<string, int> LoadOrder { get; set; } = new Dictionary<string, int>();

        [JsonProperty("lastActivated")]
        public DateTime? LastActivated { get; set; }

        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "1.0";
    }
}
