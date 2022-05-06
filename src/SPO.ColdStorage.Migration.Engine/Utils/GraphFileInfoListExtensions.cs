using SPO.ColdStorage.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SPO.ColdStorage.Migration.Engine.Utils
{
    public static class GraphFileInfoListExtensions
    {
        const int MAX_BATCH = 10;
        static int reqsBackOk = 0, reqsErrored = 0;
        static object lockObj = new object();
        public static async Task<Dictionary<DriveItemSharePointFileInfo, ItemAnalyticsRepsonse>> GetDriveItemsAnalytics(this List<DocumentSiteFile> graphFiles, string baseSiteAddress, SecureSPThrottledHttpClient httpClient, DebugTracer tracer)
        {
            var fileSuccessResults = new ConcurrentDictionary<DriveItemSharePointFileInfo, ItemAnalyticsRepsonse>();
            var pendingResults = new ConcurrentBag<DriveItemSharePointFileInfo>(graphFiles);

            var batchList = new ParallelListProcessor<DocumentSiteFile>(MAX_BATCH, 10);
            
            await batchList.ProcessListInParallel(graphFiles, async (threadListChunk, threadIndex) =>
            {
                foreach (var fileToUpdate in threadListChunk)
                {
                    // Read doc analytics
                    var url = $"{baseSiteAddress}/_api/v2.0/drives/{fileToUpdate.DriveId}/items/{fileToUpdate.GraphItemId}" +
                        $"/analytics/allTime";

                    try
                    {
                        // Do our own parsing as Graph SDK doesn't do this very well
                        using (var analyticsResponse = await httpClient.GetAsyncWithThrottleRetries(url, tracer))
                        {
                            var analyticsResponseBody = await analyticsResponse.Content.ReadAsStringAsync();

                            analyticsResponse.EnsureSuccessStatusCode();

                            var activitiesResponse = JsonSerializer.Deserialize<ItemAnalyticsRepsonse>(analyticsResponseBody) ?? new ItemAnalyticsRepsonse();
                            fileSuccessResults.AddOrUpdate(fileToUpdate, activitiesResponse, (index, oldVal) => activitiesResponse);
                            lock (lockObj)
                            {
                                reqsBackOk++;
                            }
                            await Task.Delay(100);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        lock (lockObj)
                        {
                            reqsErrored++;
                        }
                        fileToUpdate.State = SiteFileAnalysisState.Error;
                        tracer.TrackException(ex);
                        tracer.TrackTrace($"Got exception {ex.Message} getting analytics data for drive item {fileToUpdate.GraphItemId}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error);
                    }
                }
            });

            Console.WriteLine($"\nHttpClient: {reqsBackOk} back ok, {reqsErrored} errors.");

            return new Dictionary<DriveItemSharePointFileInfo, ItemAnalyticsRepsonse>(fileSuccessResults);
        }

    }

}
