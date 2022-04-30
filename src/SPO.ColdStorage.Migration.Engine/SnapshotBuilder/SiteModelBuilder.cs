using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Entities.DBEntities;
using SPO.ColdStorage.Migration.Engine.Model;
using SPO.ColdStorage.Migration.Engine.Utils;
using SPO.ColdStorage.Models;
using System.Collections.Concurrent;

namespace SPO.ColdStorage.Migration.Engine.SnapshotBuilder
{
    /// <summary>
    /// Builds a snapshot for a single site
    /// </summary>
    public class SiteModelBuilder : BaseComponent, IDisposable
    {
        #region Privates & Constructors

        private readonly TargetMigrationSite _site;
        private readonly SPOColdStorageDbContext _db;
        private readonly SiteListFilterConfig _siteFilterConfig;
        private readonly SiteSnapshotModel _model;
        private readonly GraphServiceClient _graphServiceClient;
        const int MAX_BATCH_PETITIONS = 20;

        private ConcurrentBag<GraphFileInfo> _pendingMetaFiles = new();
        private SemaphoreSlim _backgroundTaskLock = new(1, 1);
        private SemaphoreSlim _fileResultsUpdateTaskLock = new(1, 1);
        private List<Task<Dictionary<GraphFileInfo, ItemAnalyticsRepsonse>>> _backgroundMetaTasks = new();
        public SiteModelBuilder(ClientSecretCredential app, Config config, DebugTracer debugTracer, TargetMigrationSite site) : base(config, debugTracer)
        {
            this._site = site;
            _db = new SPOColdStorageDbContext(this._config);
            _model = new SiteSnapshotModel();

            _graphServiceClient = new GraphServiceClient(app);

            // Figure out what to analyse
            SiteListFilterConfig? siteFilterConfig = null;
            if (!string.IsNullOrEmpty(site.FilterConfigJson))
            {
                try
                {
                    siteFilterConfig = SiteListFilterConfig.FromJson(site.FilterConfigJson);
                }
                catch (Exception ex)
                {
                    _tracer.TrackTrace($"Couldn't deserialise filter JSon for site '{site.RootURL}': {ex.Message}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning);
                }
            }

            // Instantiate "allow all" config if none can be found in the DB
            if (siteFilterConfig == null)
                _siteFilterConfig = new SiteListFilterConfig();
            else
            {
                _siteFilterConfig = siteFilterConfig;
            }
        }
        public void Dispose()
        {
            _db.Dispose();
        }

        #endregion

        public async Task<SiteSnapshotModel> Build()
        {
            if (!_model.Finished.HasValue)
            {
                var ctx = await AuthUtils.GetClientContext(_config, _site.RootURL, _tracer);
                var crawler = new SiteListsAndLibrariesCrawler(ctx, _tracer, Crawler_SharePointFileFound);
                await crawler.CrawlContextRootWebAndSubwebs(_siteFilterConfig);
            }

            Console.WriteLine("Waiting for background tasks...");
            await Task.WhenAll(_backgroundMetaTasks);

            foreach (var backgroundTask in _backgroundMetaTasks)
            {
                foreach (var stat in backgroundTask.Result)
                {
                    if (stat.Value.AccessStats != null)
                    {
                        var file = _model.Documents.Where(f => f.GraphFileInfo.ItemId == stat.Key.ItemId).FirstOrDefault();
                        if (file != null)
                        {
                            file.AccessCount = stat.Value.AccessStats.ActionCount;
                        }
                    }
                }
            }

            Console.WriteLine("Finished site");

            return _model;
        }


        private async Task Crawler_SharePointFileFound(SharePointFileInfo arg)
        {
            SiteFile? newFile = null;

            if (arg is DriveItemSharePointFileInfo)
            {
                var driveArg = (DriveItemSharePointFileInfo)arg;

                var graphInfo = new GraphFileInfo { DriveId = driveArg.DriveId, ItemId = driveArg.GraphItemId };
                newFile = new DocumentSiteFile() { FileName = arg.ServerRelativeFilePath, GraphFileInfo = graphInfo };


                _pendingMetaFiles.Add(graphInfo);
                if (_pendingMetaFiles.Count >= MAX_BATCH_PETITIONS)
                {
                    var files = new List<GraphFileInfo>(_pendingMetaFiles);
                    _pendingMetaFiles.Clear();

                    // Fire & forget
                    _backgroundMetaTasks.Add(ProcessMetaChunk(files));
                }
            }
            else
            {
                newFile = new SiteFile() { FileName = arg.ServerRelativeFilePath };
            }

            // Add file to site files list
            await _fileResultsUpdateTaskLock.WaitAsync();

            if (_model.Files.Count % 100 == 0)
            {
                Console.WriteLine($"Processed {_model.Files.Count} files");
            }
            try
            {
                _model.Files.Add(newFile);
            }
            finally
            {
                _fileResultsUpdateTaskLock.Release();
            }
        }

        private async Task<Dictionary<GraphFileInfo, ItemAnalyticsRepsonse>> ProcessMetaChunk(List<GraphFileInfo> files)
        {
            var app = await AuthUtils.GetNewClientApp(_config);
            var auth = await app.AuthForSharePointOnline(_config.BaseServerAddress);

            var stats = await files.GetDriveItemsAnalytics(_site.RootURL, auth.AccessToken, _tracer);

            _tracer.TrackTrace($"Updated stats for {files.Count} files.");
            return stats;

        }
    }
}
