using Newtonsoft.Json;

namespace GtavModManager.Core
{
    public class ModDependency
    {
        [JsonProperty("modId")]
        public string ModId { get; set; }

        [JsonProperty("modName")]
        public string ModName { get; set; }

        [JsonProperty("minVersion")]
        public string MinVersion { get; set; }
    }
}
