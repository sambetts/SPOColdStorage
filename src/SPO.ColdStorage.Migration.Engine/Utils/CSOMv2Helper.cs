using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SPO.ColdStorage.Migration.Engine.Utils
{
    /// <summary>
    /// For accessing V2 APIs in SharePoint that aren't supported in CSOM
    /// </summary>
    public class CSOMv2Helper
    {
        private IConfidentialClientApplication app;
        private readonly string baseServerAddress;
        private readonly string baseSiteAddress;
        private readonly DebugTracer tracer;
        private AuthenticationResult? authentication = null;
        private readonly HttpClient httpClient;

        public CSOMv2Helper(IConfidentialClientApplication app, string baseServerAddress, string baseSiteAddress, DebugTracer tracer)
        {
            this.app = app;
            this.baseServerAddress = baseServerAddress;
            this.baseSiteAddress = baseSiteAddress;
            this.tracer = tracer;
            this.httpClient = new HttpClient();
        }

        public async Task<ItemAnalyticsRepsonse> GetDriveItemAnalytics(string driveId, string graphItemId)
        {
            if (authentication == null || authentication.ExpiresOn.AddMinutes(5) < DateTime.Now)
            {
                tracer.TrackTrace("Generating new OAuth token");
                authentication = await app.AuthForSharePointOnline(baseServerAddress);

                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authentication.AccessToken);
            }

            var url = $"{baseSiteAddress}/_api/v2.0/drives/{driveId}/items/{graphItemId}" +
                $"/analytics/allTime";

            using (var r = await httpClient.GetAsyncWithThrottleRetries(url, this.tracer))
            {
                var body = await r.Content.ReadAsStringAsync();

                r.EnsureSuccessStatusCode();

                var activitiesResponse = JsonSerializer.Deserialize<ItemAnalyticsRepsonse>(body);
                return activitiesResponse ?? new ItemAnalyticsRepsonse();
            }

        }
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

}
