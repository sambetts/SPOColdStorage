using Microsoft.Graph;
using SPO.ColdStorage.Migration.Engine.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Migration.Engine.Utils
{
    public static class GraphFileInfoListExtensions
    {
        const int MAX_BATCH = 20;
        public static async Task<Dictionary<GraphFileInfo, ItemAnalytics>> GetDriveItemsAnalytics(this List<GraphFileInfo> graphFiles, GraphServiceClient serviceClient)
        {
            // Build all needed
            var allReqs = new Dictionary<IBaseRequest, GraphFileInfo>();
            foreach (var file in graphFiles)
            {
                var req = serviceClient.Drives[file.DriveId].Items[file.ItemId].Analytics.Request().Select( s=> s.AllTime);

                var res = await req.GetAsync();
                allReqs.Add(req, file);
            }

            // Get back results over X batches
            var fileResults = await ProcessAllRequestsInParallel(allReqs, serviceClient);


            return fileResults;
        }

        private static async Task<Dictionary<GraphFileInfo, ItemAnalytics>> ProcessAllRequestsInParallel(Dictionary<IBaseRequest, GraphFileInfo> reqs, GraphServiceClient serviceClient)
        {
            var fileResults = new ConcurrentDictionary<GraphFileInfo, ItemAnalytics>();
            var batchList = new ParallelListProcessor<IBaseRequest>(MAX_BATCH);

            await batchList.ProcessListInParallel(reqs.Keys, async (threadListChunk, threadIndex) =>
            {
                // Build a batch request for this chunk
                var batchRequestContent = new BatchRequestContent();
                var fileResponses = new Dictionary<string, GraphFileInfo>();
                foreach (var req in threadListChunk)
                {
                    fileResponses.Add(batchRequestContent.AddBatchRequestStep(req), reqs[req]);
                }

                // Read back responses
                var response = await serviceClient.Batch.Request().PostAsync(batchRequestContent);
                foreach (var responseId in fileResponses.Keys)
                {
                    var r = await response.GetResponseByIdAsync<ItemAnalytics>(responseId);
                    fileResults.AddOrUpdate(fileResponses[responseId], r, (index, oldVal) => r);
                }
            });

            return new Dictionary<GraphFileInfo, ItemAnalytics>(fileResults);
        }
    }
}
