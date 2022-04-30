using Microsoft.Graph;
using SPO.ColdStorage.Migration.Engine.Model;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SPO.ColdStorage.Migration.Engine.Utils
{
    public static class GraphFileInfoListExtensions
    {

        const int MAX_BATCH = 10;
        public static async Task<Dictionary<GraphFileInfo, ItemAnalyticsRepsonse>> GetDriveItemsAnalytics(this List<GraphFileInfo> graphFiles, string baseSiteAddress, string token, DebugTracer tracer)
        {
            var allReqs = new Dictionary<IBaseRequest, GraphFileInfo>();
            var httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);


            // Get back results over X batches
            var fileResults = await ProcessAllRequestsInParallel(graphFiles, httpClient, baseSiteAddress, tracer);

            return fileResults;
        }


        private static async Task<Dictionary<GraphFileInfo, ItemAnalyticsRepsonse>> ProcessAllRequestsInParallel(List<GraphFileInfo> reqsForFiles, HttpClient httpClient, string baseSiteAddress, DebugTracer tracer)
        {
            var fileSuccessResults = new ConcurrentDictionary<GraphFileInfo, ItemAnalyticsRepsonse>();
            var pendingResults = new ConcurrentBag<GraphFileInfo>(reqsForFiles);

            var batchList = new ParallelListProcessor<GraphFileInfo>(MAX_BATCH, 10);      // Limit to just 10 threads of MAX_BATCH for now to avoid heavy throttling

            await batchList.ProcessListInParallel(reqsForFiles, async (threadListChunk, threadIndex) =>
            {
                foreach (var req in threadListChunk)
                {
                    var url = $"{baseSiteAddress}/_api/v2.0/drives/{req.DriveId}/items/{req.ItemId}" +
                        $"/analytics/allTime";

                    try
                    {
                        using (var r = await httpClient.GetAsyncWithThrottleRetries(url, tracer))
                        {
                            var body = await r.Content.ReadAsStringAsync();

                            r.EnsureSuccessStatusCode();

                            var activitiesResponse = JsonSerializer.Deserialize<ItemAnalyticsRepsonse>(body) ?? new ItemAnalyticsRepsonse();
                            fileSuccessResults.AddOrUpdate(req, activitiesResponse, (index, oldVal) => activitiesResponse);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        tracer.TrackException(ex);
                        tracer.TrackTrace($"Got exception {ex.Message} getting analytics data for drive item {req.ItemId}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error);
                    }
                }
            });

            return new Dictionary<GraphFileInfo, ItemAnalyticsRepsonse>(fileSuccessResults);
        }

    }

}
