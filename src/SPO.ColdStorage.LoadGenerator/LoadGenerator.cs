using Microsoft.SharePoint.Client;
using SPO.ColdStorage.Migration.Engine;
using SPO.ColdStorage.Migration.Engine.Utils;
using System.Text;

namespace SPO.ColdStorage.LoadGenerator
{
    internal class LoadGenerator
    {
        private readonly Options _options;

        public LoadGenerator(Options options)
        {
            this._options = options;
        }

        public async Task Go(int fileCount)
        {
            int filesAdded = 0;

            const int MAX_FILES_PER_THREAD = 500;

            var threadsNeeded = fileCount / MAX_FILES_PER_THREAD;
            if (threadsNeeded == 0)
            {
                threadsNeeded = 1;
            }
            var tasks = new List<Task>();

            for (int threadIndex = 0; threadIndex < threadsNeeded; threadIndex++)
            {
                var filesToInsert = MAX_FILES_PER_THREAD;
                if (threadIndex == threadsNeeded - 1)
                {
                    filesToInsert = fileCount - filesAdded;
                }

                // Multi-thread the file create
                tasks.Add(AddFiles(filesAdded, filesToInsert, threadIndex));
                filesAdded += MAX_FILES_PER_THREAD;

#if DEBUG
                Console.Write($"+#{threadIndex}/{threadsNeeded}...");
#endif
            }
            await Task.WhenAll(tasks.ToArray());

        }

        private async Task AddFiles(int fileStartIndex, int filesToInsert, int threadIndex)
        {
            var ctx = await AuthUtils.GetClientContext(_options.TargetWeb!, _options.TenantId!, _options.ClientID!, _options.ClientSecret!, _options.KeyVaultUrl!, _options.BaseServerAddress!, DebugTracer.ConsoleOnlyTracer());

            var targetLists = await GetAllListsAllWebs(ctx);

            for (int i = 0; i < filesToInsert; i++)
            {
                foreach (var list in targetLists)
                {
                    try
                    {

                        if (list.BaseType == BaseType.GenericList)
                        {
                            await AddFileToCustomList(list, ctx);
                        }
                        else if (list.BaseType == BaseType.DocumentLibrary)
                        {
                            await AddFileToDocLib(list, ctx);
                        }
                    }
                    catch (System.Net.WebException ex)
                    {
                        Console.WriteLine($"Got error on thread {threadIndex} creating file: {ex.Message}.");
                        
                    }
                    Console.WriteLine(fileStartIndex + i);
                }
            }
        }

        public async Task<IEnumerable<List>> GetAllListsAllWebs(ClientContext ctx)
        {
            var results = new List<List>();
            var rootWeb = ctx.Web;
            ctx.Load(rootWeb);
            ctx.Load(rootWeb.Webs);
            await ctx.ExecuteQueryAsyncWithThrottleRetries(DebugTracer.ConsoleOnlyTracer());

            results.AddRange(await GetAllLists(rootWeb, ctx));

            foreach (var subSweb in rootWeb.Webs)
            {
                results.AddRange(await GetAllLists(subSweb, ctx));
            }

            return results;
        }

        private async Task<IEnumerable<List>> GetAllLists(Web web, ClientContext ctx)
        {
            var results = new List<List>();
            ctx.Load(web.Lists);
            ctx.Load(web.Webs);
            await ctx.ExecuteQueryAsync();

            foreach (var list in web.Lists)
            {
                ctx.Load(list, l => l.BaseType, l => l.IsSystemList);
                ctx.Load(list.RootFolder);
                ctx.Load(list, l => l.RootFolder.Name);
                await ctx.ExecuteQueryAsync();

                // Only upload to safe lists
                if (!list.IsSystemList && !list.Hidden)
                {
                    results.Add(list);
                }
            }

            return results;
        }

        private async Task AddFileToDocLib(List list, ClientContext ctx)
        {
            await list.SaveFile(ctx, $"test{DateTime.Now.Ticks}.txt", Encoding.UTF8.GetBytes("bum"));
        }

        private async Task AddFileToCustomList(List list, ClientContext ctx)
        {
            var newName = DateTime.Now.Ticks.ToString();
            var newItemCreateInfo = new ListItemCreationInformation();
            var oListItem = list.AddItem(newItemCreateInfo);
            oListItem["Title"] = newName;

            oListItem.Update();

            var tracer = DebugTracer.ConsoleOnlyTracer();
            await ctx.ExecuteQueryAsyncWithThrottleRetries(tracer);

            var attInfo = new AttachmentCreationInformation();
            attInfo.FileName = newName + ".txt";
            attInfo.ContentStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("bum"));

            Attachment att = oListItem.AttachmentFiles.Add(attInfo); //Add to File

            ctx.Load(att);

            try
            {
                await ctx.ExecuteQueryAsyncWithThrottleRetries(tracer);
            }
            catch (ServerException ex)
            {
                Console.WriteLine($"Got unexpected error saving against list '{list.Title}': {ex.Message}");
            }
        }
    }
}
