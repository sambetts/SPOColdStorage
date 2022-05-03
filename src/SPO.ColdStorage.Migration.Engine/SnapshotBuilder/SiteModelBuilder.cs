using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Entities.DBEntities;
using SPO.ColdStorage.Migration.Engine.Utils;
using SPO.ColdStorage.Models;
using System.Net.Http.Headers;

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

        private List<SharePointFileInfo> _fileFoundBuffer = new();
        private SemaphoreSlim _fileResultsUpdateTaskLock = new(1, 1);
        private List<Task<Dictionary<DriveItemSharePointFileInfo, ItemAnalyticsRepsonse>>> _backgroundMetaTasks = new();
        public SiteModelBuilder(Config config, DebugTracer debugTracer, TargetMigrationSite site) : base(config, debugTracer)
        {
            this._site = site;
            _db = new SPOColdStorageDbContext(this._config);
            _model = new SiteSnapshotModel();

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
            return await Build(100, null);
        }
        public async Task<SiteSnapshotModel> Build(int batchSize, Action<List<SharePointFileInfo>>? newFilesCallback)
        {
            if (batchSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(batchSize));
            }

            if (!_model.Finished.HasValue)
            {
                var ctx = await AuthUtils.GetClientContext(_config, _site.RootURL, _tracer);

                var crawler = new SiteListsAndLibrariesCrawler(ctx, _tracer, 
                    (SharePointFileInfo foundFile) => Crawler_SharePointFileFound(foundFile, batchSize, newFilesCallback));

                // Begin and block until all files crawled
                _model.Started = DateTime.Now;
                await crawler.StartCrawl(_siteFilterConfig);

                // Get auth for REST
                var app = await AuthUtils.GetNewClientApp(_config);
                var auth = await app.AuthForSharePointOnline(_config.BaseServerAddress);
                var httpClient = new ThrottledHttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

                var filesToGetAnalysisFor = true;
                while (filesToGetAnalysisFor)
                {
                    // Check every second
                    await Task.Delay(1000);

                    // Begin background loading of extra metadata
                    var outstandingFiles = _model.DocsPendingAnalysis;
                    filesToGetAnalysisFor = outstandingFiles.Any();
                    _tracer.TrackTrace($"START: Analysing {outstandingFiles.Count.ToString("N0")} files for last-usage...");

                    var pendingFilesToAnalyse = new List<DriveItemSharePointFileInfo>();

                    foreach (var file in outstandingFiles)
                    {
                        // Avoid analysing more than once
                        file.State = SiteFileAnalysisState.AnalysisInProgress;

                        pendingFilesToAnalyse.Add(file);

                        // Start new background every $CHUNK_SIZE
                        if (pendingFilesToAnalyse.Count >= batchSize)
                        {
                            var newFileChunkCopy = new List<DriveItemSharePointFileInfo>(pendingFilesToAnalyse);
                            pendingFilesToAnalyse.Clear();

                            // Background process chunk
                            _backgroundMetaTasks.Add(ProcessMetaChunk(newFileChunkCopy, httpClient));
                        }
                    }

                    // Background process the rest
                    _backgroundMetaTasks.Add(ProcessMetaChunk(pendingFilesToAnalyse, httpClient));

                    Console.WriteLine("Waiting for background tasks...");
                    await Task.WhenAll(_backgroundMetaTasks);

                    // Update with results
                    foreach (var backgroundTask in _backgroundMetaTasks)
                    {
                        foreach (var stat in backgroundTask.Result)
                        {
                            if (stat.Value.AccessStats != null)
                            {
                                _model.UpdateDocItem(stat.Key, stat.Value.AccessStats);
                            }
                        }
                    }

                    // Check again if anything to do
                    filesToGetAnalysisFor = outstandingFiles.Any();
                }

                _model.Finished = DateTime.Now;
                var ts = _model.Finished.Value.Subtract(_model.Started);
                Console.WriteLine($"Finished site - done in {ts.TotalMinutes.ToString("N2")} mins");
            }


            return _model;
        }


        private async Task Crawler_SharePointFileFound(SharePointFileInfo foundFile, int batchSize, Action<List<SharePointFileInfo>>? newFilesCallback)
        {
            SharePointFileInfo? newFile = null;

            if (foundFile is DriveItemSharePointFileInfo)
            {
                var driveArg = (DriveItemSharePointFileInfo)foundFile;

                // Set newly found file as "pending" analysis data
                newFile = new DocumentSiteFile(driveArg) { State = SiteFileAnalysisState.AnalysisPending };
            }
            else
            {
                // Nothing to analyse for list item attachments
                newFile = foundFile;
            }

            // Ensure only single thread execution 
            await _fileResultsUpdateTaskLock.WaitAsync();

            try
            {
                // Add new found files to model & event buffer
                _fileFoundBuffer.Add(newFile);
                _model.AddFile(newFile, foundFile.List);

                if (_fileFoundBuffer.Count == batchSize)
                {
                    if (newFilesCallback != null)
                    {
                        newFilesCallback(new List<SharePointFileInfo>(_fileFoundBuffer));
                    }
                    _fileFoundBuffer.Clear();
                }
            }
            finally
            {
                _fileResultsUpdateTaskLock.Release();
            }
        }

        private async Task<Dictionary<DriveItemSharePointFileInfo, ItemAnalyticsRepsonse>> ProcessMetaChunk(List<DriveItemSharePointFileInfo> files, ThrottledHttpClient httpClient)
        {
            var stats = await files.GetDriveItemsAnalytics(_site.RootURL, httpClient, _tracer);

            _tracer.TrackTrace($"Got stats for {files.Count} files.");
            return stats;

        }
    }
}
