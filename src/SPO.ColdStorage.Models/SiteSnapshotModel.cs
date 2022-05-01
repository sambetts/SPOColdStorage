using System.Text.Json.Serialization;

namespace SPO.ColdStorage.Models
{
    public class Snapshot
    {
        public List<SiteSnapshotModel> SiteSnapshots { get; set; } = new List<SiteSnapshotModel>();
    }
    public class SiteSnapshotModel
    {
        public DateTime Started { get; set; } = DateTime.Now;
        public DateTime? Finished { get; set; }

        public List<SiteList> Lists { get; set; } = new List<SiteList> { };
        public List<DocLib> DocLibs => Lists.Where(f => f.GetType() == typeof(DocLib)).Cast<DocLib>().ToList();
        public List<SharePointFileInfo> AllFiles => Lists.SelectMany(l => l.Files).ToList();

        public List<DocumentSiteFile> DocsPendingAnalysis => AllFiles
            .Where(f=> f is DocumentSiteFile && ((DocumentSiteFile)f).State == SiteFileAnalysisState.AnalysisPending).Cast<DocumentSiteFile>().ToList();


        public void UpdateDocItem(DriveItemSharePointFileInfo updatedDocInfo, ItemAnalyticsRepsonse.AnalyticsItemActionStat accessStats)
        {
            var docLib = DocLibs.Where(l => l.DriveId == updatedDocInfo.DriveId).SingleOrDefault();
            if (docLib == null) throw new ArgumentOutOfRangeException(nameof(updatedDocInfo), $"No library in model for drive Id {updatedDocInfo.DriveId}");

            var file = docLib.Documents.Where(d=> d.GraphItemId == updatedDocInfo.GraphItemId).SingleOrDefault();
            if (file != null)
            {
                file.AccessCount = accessStats.ActionCount;
                file.State = SiteFileAnalysisState.Complete;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(updatedDocInfo), $"No doc in model doc-lib with item Id {updatedDocInfo.GraphItemId}");
            }
        }

        public void AddFile(SharePointFileInfo newFile, SiteList list)
        {
            lock (this)
            {
                var targetList = Lists.Where(l => l.Equals(list)).SingleOrDefault();
                if (targetList == null)
                {
                    targetList = list;
                    Lists.Add(targetList);
                }

                targetList.Files.Add(newFile);
            }
        }
    }

    public class SiteList : IEquatable<SiteList>
    {
        public SiteList() { }
        public SiteList(SiteList l)
        {
            this.Title = l.Title;
            this.ServerRelativeUrl = l.ServerRelativeUrl;
        }

        public string Title { get; set; } = string.Empty;
        public string ServerRelativeUrl { get; set; } = string.Empty;
        public List<SharePointFileInfo> Files { get; set; } = new List<SharePointFileInfo>();

        public bool Equals(SiteList? other)
        {
            if (other == null) return false;
            return ServerRelativeUrl == other.ServerRelativeUrl && Title == other.Title;
        }
    }

    public class DocLib : SiteList
    {
        public DocLib() { }
        public DocLib(SiteList l) :base(l)
        {
            if (l is DocLib)
            {
                var lib = (DocLib)l;
                this.DriveId = lib.DriveId;
                this.Delta = lib.Delta;
                this.Files = lib.Files;
            }
        }
        public string DriveId { get; set; } = string.Empty;

        public List<DocumentSiteFile> Documents => Files.Where(f => f.GetType() == typeof(DocumentSiteFile)).Cast<DocumentSiteFile>().ToList();
        public string Delta { get; set; } = string.Empty;
    }

    public enum SiteFileAnalysisState
    {
        Unknown,
        AnalysisPending,
        AnalysisInProgress,
        Complete
    }

    public class DocumentSiteFile : DriveItemSharePointFileInfo
    {
        public DocumentSiteFile(DriveItemSharePointFileInfo driveArg) :base(driveArg)
        {
            this.AccessCount = null;
        }

        public SiteFileAnalysisState State { get; set; } = SiteFileAnalysisState.Unknown;

        public int? AccessCount { get; set; } = null;
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
            s += $"--batch_{BatchId}--\n\n";

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
