using System.Collections.Generic;
using Newtonsoft.Json;

namespace Commodore_Repair_Toolbox
{
    // Changed from internal to public for accessibility
    public class DataUpdate
    {
        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("checksum")]
        public string Checksum { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        // Reads and deserializes the JSON file from a local path or URL
        public static List<DataUpdate> LoadFromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<DataUpdate>>(json);
        }
    }
}