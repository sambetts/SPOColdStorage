using SPO.ColdStorage.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SPO.ColdStorage.Migration.Engine.Utils
{
    public static class GraphFileInfoListExtensions
    {

        const int MAX_BATCH = 10;
        public static async Task<Dictionary<DriveItemSharePointFileInfo, ItemAnalyticsRepsonse>> GetDriveItemsAnalytics(this List<DriveItemSharePointFileInfo> graphFiles, string baseSiteAddress, ThrottledHttpClient httpClient, DebugTracer tracer)
        {
            var fileSuccessResults = new ConcurrentDictionary<DriveItemSharePointFileInfo, ItemAnalyticsRepsonse>();
            var pendingResults = new ConcurrentBag<DriveItemSharePointFileInfo>(graphFiles);

            var batchList = new ParallelListProcessor<DriveItemSharePointFileInfo>(MAX_BATCH, 10);      // Limit to just 10 threads of MAX_BATCH for now to avoid heavy throttling

            await batchList.ProcessListInParallel(graphFiles, async (threadListChunk, threadIndex) =>
            {
                foreach (var req in threadListChunk)
                {
                    var url = $"{baseSiteAddress}/_api/v2.0/drives/{req.DriveId}/items/{req.GraphItemId}" +
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
                        tracer.TrackTrace($"Got exception {ex.Message} getting analytics data for drive item {req.GraphItemId}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error);
                    }
                }
            });

            return new Dictionary<DriveItemSharePointFileInfo, ItemAnalyticsRepsonse>(fileSuccessResults);
        }

    }

}
