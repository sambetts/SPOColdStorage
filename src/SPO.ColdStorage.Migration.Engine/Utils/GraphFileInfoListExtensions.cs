using Microsoft.Graph;
using SPO.ColdStorage.Migration.Engine.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Migration.Engine.Utils
{
    public static class GraphFileInfoListExtensions
    {
        const int MAX_BATCH = 2;
        public static async Task<Dictionary<GraphFileInfo, ItemAnalyticsRepsonse>> GetDriveItemsAnalytics(this List<GraphFileInfo> graphFiles, GraphServiceClient serviceClient, DebugTracer tracer)
        {
            var allReqs = new Dictionary<IBaseRequest, GraphFileInfo>();
            foreach (var file in graphFiles)
            {
                var req = new AllTimeAnalyticsRequest(file);

                allReqs.Add(req, file);
            }

            // Get back results over X batches
            var fileResults = await ProcessAllRequestsInParallel(allReqs, serviceClient, tracer);

            return fileResults;
        }

        private static async Task<Dictionary<GraphFileInfo, ItemAnalyticsRepsonse>> ProcessAllRequestsInParallel(Dictionary<IBaseRequest, GraphFileInfo> reqsForFiles, GraphServiceClient serviceClient, DebugTracer tracer)
        {
            var fileSuccessResults = new ConcurrentDictionary<GraphFileInfo, ItemAnalyticsRepsonse>();
            var pendingResults = new ConcurrentDictionary<IBaseRequest, GraphFileInfo>(reqsForFiles);

            var batchList = new ParallelListProcessor<IBaseRequest>(MAX_BATCH, 1);      // Limit to just 1 thread for now to avoid heavy throttling

            while (pendingResults.Count > 0)
            {
                int batchWaitValSeconds = 0;

                await batchList.ProcessListInParallel(reqsForFiles.Keys, async (threadListChunk, threadIndex) =>
                {

                    // Build a batch request for this chunk. Get back request ID for each request
                    var batchRequestContent = new BatchRequestContent();
                    var fileResponsesBatchIdDic = new Dictionary<string, GraphFileInfo>();
                    foreach (var req in threadListChunk)
                    {
                        fileResponsesBatchIdDic.Add(batchRequestContent.AddBatchRequestStep(req), reqsForFiles[req]);
                    }

                    // Read back responses
                    var response = await serviceClient.Batch.Request().PostAsync(batchRequestContent);
                    var batchResponses = await response.GetResponsesAsync();

                    foreach (var responseId in fileResponsesBatchIdDic.Keys)
                    {
                        var itemResponse = batchResponses[responseId];
                        var responseContent = await itemResponse.Content.ReadAsStringAsync();

                        var fileInfo = fileResponsesBatchIdDic[responseId];
                        var originalReq = reqsForFiles.Where(r => r.Value == fileInfo).FirstOrDefault().Key;

                        // Success?
                        if (itemResponse.IsSuccessStatusCode)
                        {
                            var analyticsData = JsonSerializer.Deserialize<ItemAnalyticsRepsonse>(responseContent) ?? new ItemAnalyticsRepsonse();
                            fileSuccessResults.AddOrUpdate(fileResponsesBatchIdDic[responseId], analyticsData, (index, oldVal) => analyticsData);

                            pendingResults.Remove(originalReq, out fileInfo);
                        }
                        else
                        {
                            if (itemResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                var responseWaitVal = itemResponse.GetRetryAfterHeaderSeconds();

                                if (responseWaitVal.HasValue && responseWaitVal > batchWaitValSeconds) batchWaitValSeconds = responseWaitVal.Value;
                            }
                            else
                            {
                                // Blow up
                                itemResponse.EnsureSuccessStatusCode();
                            }
                        }
                    }
                });

                if (pendingResults.Count > 0)
                {
                    // Trace standard throttling message
                    tracer.TrackTrace($"{Constants.THROTTLE_ERROR} executing Graph request. Sleeping for {batchWaitValSeconds} seconds (read from 'Retry-After' response from Graph).", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning);

                    // Delay for the requested seconds
                    await Task.Delay(batchWaitValSeconds * 1000);
                    tracer.TrackTrace($"Got another {pendingResults.Count} to retry...");

                    // Reset in case next throttle is less
                    batchWaitValSeconds = 0;
                }
            }

            return new Dictionary<GraphFileInfo, ItemAnalyticsRepsonse>(fileSuccessResults);
        }

    }



    public class AllTimeAnalyticsRequest : IBaseRequest
    {
        private readonly GraphFileInfo _graphFileInfo;

        public AllTimeAnalyticsRequest(GraphFileInfo graphFileInfo)
        {
            this._graphFileInfo = graphFileInfo;
        }

        public string ContentType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IList<HeaderOption> Headers => throw new NotImplementedException();

        public IBaseClient Client => throw new NotImplementedException();

        public HttpMethods Method { get => HttpMethods.GET; set => throw new NotImplementedException(); }

        public string RequestUrl => $"https://graph.microsoft.com/v1.0/drives/{_graphFileInfo.DriveId}/items/{_graphFileInfo.ItemId}/analytics/allTime";

        public IList<QueryOption> QueryOptions => new List<QueryOption>();

        public IDictionary<string, IMiddlewareOption> MiddlewareOptions => throw new NotImplementedException();

        public IResponseHandler ResponseHandler => throw new NotImplementedException();

        public HttpRequestMessage GetHttpRequestMessage()
        {
            return new HttpRequestMessage(HttpMethod.Get, RequestUrl);
        }
    }
}
