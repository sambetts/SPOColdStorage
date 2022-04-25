using Microsoft.Graph;
using SPO.ColdStorage.Migration.Engine.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Migration.Engine.Utils
{
    public static class GraphFileInfoListExtensions
    {
        public static async Task<Dictionary<GraphFileInfo, ItemAnalytics>> Batch(this List<GraphFileInfo> graphFiles, GraphServiceClient serviceClient)
        {
            var batchRequestContent = new BatchRequestContent();

            var fileResponses = new Dictionary<string, GraphFileInfo>();
            var fileResults = new Dictionary<GraphFileInfo, ItemAnalytics>();

            foreach (var file in graphFiles)
            {
                var req = serviceClient.Drives[file.DriveId].Items[file.ItemId].Analytics.Request(new Option[] { new Option() });

                await req.GetAsync();
                fileResponses.Add(batchRequestContent.AddBatchRequestStep(req), file);
            }

            var response = await serviceClient.Batch.Request().PostAsync(batchRequestContent);
            foreach (var responseId in fileResponses.Keys)
            {
                var r = await response.GetResponseByIdAsync<ItemAnalytics>(responseId);
                fileResults.Add(fileResponses[responseId], r);
            }

            return fileResults;
        }
    }
}
