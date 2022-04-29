using Microsoft.Graph;
using Microsoft.Identity.Client;
using SPO.ColdStorage.Migration.Engine.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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

                    using (var r = await httpClient.GetAsyncWithThrottleRetries(url, tracer))
                    {
                        var body = await r.Content.ReadAsStringAsync();

                        r.EnsureSuccessStatusCode();

                        var activitiesResponse = JsonSerializer.Deserialize<ItemAnalyticsRepsonse>(body) ?? new ItemAnalyticsRepsonse();
                        fileSuccessResults.AddOrUpdate(req, activitiesResponse, (index, oldVal) => activitiesResponse);
                    }

                }
            });



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
