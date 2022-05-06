using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Models
{

    public class DriveItemVersionInfo
    {
        [JsonPropertyName("value")]
        public List<DriveItemVersion> Versions { get; set; } = new List<DriveItemVersion>();
    }


    public class DriveItemVersion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = String.Empty;

        [JsonPropertyName("lastModifiedDateTime")]
        public DateTime LastModifiedDateTime { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }
    }
}
