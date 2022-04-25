using SPO.ColdStorage.Entities.DBEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Migration.Engine.Model
{
    public class Snapshot
    {

        public List<SiteSnapshotModel> SiteSnapshots { get; set; } = new List<SiteSnapshotModel>();
    }
    public class SiteSnapshotModel
    {
        public DateTime Started { get; set; } = DateTime.Now;
        public DateTime? Finished { get; set; }

        public TargetMigrationSite Site { get; set; } = new TargetMigrationSite();

        public List<SiteFile> Files { get; set; } = new List<SiteFile>();
    }
    public class SiteFile
    {
        public string FileName { get; set; } = string.Empty;
        public GraphFileInfo GraphFileInfo { get; set; } = new GraphFileInfo();

        public int? AccessCount { get; set; }

        public FileType FileType { get; set; }
    }

    public enum FileType
    {
        Unknown,
        ListItemAttachement,
        DocumentLibraryFile
    }

    public class GraphFileInfo
    {
        public string DriveId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
    }


    // https://docs.microsoft.com/en-us/graph/api/resources/itemactivitystat?view=graph-rest-1.0
    public class ItemAnalyticsRepsonse
    {

        [JsonPropertyName("incompleteData")]
        public AnalyticsIncompleteData? IncompleteData { get; set; }

        [JsonPropertyName("access")]
        public AnalyticsItemActionStat? AccessStats { get; set; }

        [JsonPropertyName("startDateTime")]
        public DateTime StartDateTime { get; set; }

        [JsonPropertyName("endDateTime")]
        public DateTime EndDateTime { get; set; }


        public class AnalyticsIncompleteData
        {
            [JsonPropertyName("wasThrottled")]
            public bool WasThrottled { get; set; }

            [JsonPropertyName("resultsPending")]
            public bool ResultsPending { get; set; }

            [JsonPropertyName("notSupported")]
            public bool NotSupported { get; set; }
        }
        public class AnalyticsItemActionStat
        {
            /// <summary>
            /// The number of times the action took place.
            /// </summary>
            [JsonPropertyName("actionCount")]
            public int ActionCount { get; set; } = 0;

            /// <summary>
            /// The number of distinct actors that performed the action.
            /// </summary>
            [JsonPropertyName("actorCount")]
            public int ActorCount { get; set; } = 0;
        }
    }

    public class BatchRequestList
    {
        [JsonPropertyName("requests")]
        public List<BatchRequest> Requests { get; set; } = new List<BatchRequest>();

        public Guid BatchId { get; set; } = Guid.NewGuid();

        internal string ToSharePointBatchBody()
        {
            var s = string.Empty;
            foreach (var req in Requests)
            {
                s += $"--batch_{BatchId} {req.ToSharePointBatchBody()}\n\n";
            }
            s+= $"--batch_{BatchId}--\n\n";

            return s;
        }
    }
    public class BatchRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;


        [JsonPropertyName("method")]
        public string Method => "GET";

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        internal string ToSharePointBatchBody()
        {
            return $"Content-Type: application/http\nContent-Transfer-Encoding: binary\n\n{Method} {Url}";
        }
    }
}
