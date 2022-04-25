using Microsoft.Identity.Client;
using SPO.ColdStorage.Migration.Engine.Model;
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


        public async Task<ItemAnalyticsRepsonse> GetDriveItemAnalytics(List<GraphFileInfo> graphFiles)
        {
            if (authentication == null || authentication.ExpiresOn.AddMinutes(-5) < DateTime.Now)
            {
                tracer.TrackTrace("Generating new OAuth token");
                authentication = await app.AuthForSharePointOnline(baseServerAddress);

                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authentication.AccessToken);
            }

            var reqs = new BatchRequestList();
            int i = 0;
            foreach (var file in graphFiles)
            {
                var url = $"{baseSiteAddress}/_api/v2.0/drives/{file.DriveId}/items/{file.ItemId}" +
                    $"/analytics/allTime";

                reqs.Requests.Add(new BatchRequest { Id = i.ToString(), Url = url });
            }

            var batchBody = reqs.ToSharePointBatchBody();
            using (var r = await httpClient.PostAsyncWithThrottleRetries($"{baseSiteAddress}/_api/$batch", batchBody, $"multipart/mixed", $"batch_{reqs.BatchId}", this.tracer))
            {
                var body = await r.Content.ReadAsStringAsync();

                r.EnsureSuccessStatusCode();

                var activitiesResponse = JsonSerializer.Deserialize<ItemAnalyticsRepsonse>(body);
                return activitiesResponse ?? new ItemAnalyticsRepsonse();
            }

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


}
