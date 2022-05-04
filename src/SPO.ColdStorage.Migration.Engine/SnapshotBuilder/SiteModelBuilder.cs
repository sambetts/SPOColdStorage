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
        private SecureSPThrottledHttpClient _httpClient;

        private bool _processBackgroundDocQueue = false;
        private bool _showStats = false;
        private List<SharePointFileInfo> _outstandingFilesBuffer = new();
        private List<SharePointFileInfo> _fileFoundBuffer = new();
        private List<Task<Dictionary<DriveItemSharePointFileInfo, ItemAnalyticsRepsonse>>> _backgroundMetaTasks = new();
        public SiteModelBuilder(Config config, DebugTracer debugTracer, TargetMigrationSite site) : base(config, debugTracer)
        {
            this._site = site;
            _db = new SPOColdStorageDbContext(this._config);
            _model = new SiteSnapshotModel();
            _httpClient = new SecureSPThrottledHttpClient(_config, true, _tracer);

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
            return await Build(100, null, null);
        }
        public async Task<SiteSnapshotModel> Build(int batchSize, Action<List<SharePointFileInfo>>? newFilesCallback, Action<List<SharePointFileInfo>>? filesUpdatedCallback)
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

                // Run background tasks
                _ = Task.Run(() => StartStatsUpdates()).ConfigureAwait(false);
                _ = Task.Run(() => StartBackgroundDocQueue(batchSize, filesUpdatedCallback)).ConfigureAwait(false);

                await crawler.StartCrawl(_siteFilterConfig);

                // Now we have all the files we'll find, update the rest of the stats on this thread
                StopBackgroundUpdates();

                _tracer.TrackTrace($"STAGE 1/2: Finished crawling site files. Waiting for background update tasks to finish...");
                await Task.WhenAll(_backgroundMetaTasks);

                var filesToGetAnalysisFor = true;
                while (filesToGetAnalysisFor)
                {
                    // Check every second
                    await Task.Delay(1000);

                    await UpdatePendingAsync(batchSize, _model.DocsPendingAnalysis.Cast<SharePointFileInfo>().ToList(), filesUpdatedCallback);

                    // Check again if anything to do
                    filesToGetAnalysisFor = _model.DocsPendingAnalysis.Any();
                }
                StopStatsUpdates();

                _model.Finished = DateTime.Now;
                var ts = _model.Finished.Value.Subtract(_model.Started);
                _tracer.TrackTrace($"STAGE 2/2: Finished getting metadata for site files. All done in {ts.TotalMinutes.ToString("N2")} minutes.");
            }

            return _model;
        }

        #region Background Files Processing

        private void StopBackgroundUpdates()
        {
            lock (_outstandingFilesBuffer)
            {
                _processBackgroundDocQueue = false;
            }
        }

        void AddToBackgroundDocQueue(List<SharePointFileInfo> documentSiteFiles)
        {
            lock (_outstandingFilesBuffer)
            {
                _outstandingFilesBuffer.AddRange(documentSiteFiles);
            }
        }

        async Task StartBackgroundDocQueue(int batchSize, Action<List<SharePointFileInfo>>? filesUpdatedCallback)
        {
            Console.WriteLine("Starting background doc queue");
            _processBackgroundDocQueue = true;
            while (_processBackgroundDocQueue)
            {
                var count = 0;
                var newProcessingChunk = new List<SharePointFileInfo>();
                lock (_outstandingFilesBuffer)
                {
                    count = _outstandingFilesBuffer.Count > batchSize ? batchSize : _outstandingFilesBuffer.Count;
                    newProcessingChunk = new List<SharePointFileInfo>(_outstandingFilesBuffer.Take(count));
                }

                if (count > 0)
                {
                    await UpdatePendingAsync(batchSize, newProcessingChunk, filesUpdatedCallback);
                    lock (_outstandingFilesBuffer)
                    {
                        _outstandingFilesBuffer.RemoveRange(0, count);
                    }
                }
                await Task.Delay(1000);
            }
            Console.WriteLine("Done processing background doc queue");
        }

        #endregion


        #region Stats Update

        private void StopStatsUpdates()
        {
            lock (this)
            {
                _showStats = false;
            }
        }

        async Task StartStatsUpdates()
        {
            _showStats = true;
            while (_showStats)
            {
                lock (this)
                {
                    if (_model.AllFiles.Count > 0)
                        Console.WriteLine($"{_model.DocsPendingAnalysis.Count}/{_model.AllFiles.Count} files pending metadata: " +
                            $"{_httpClient.CompletedCalls} calls completed; {_httpClient.ThrottledCalls} throttled (total); {_httpClient.ConcurrentCalls} currently active");

                }
                await Task.Delay(5000);
            }
        }

        #endregion

        async Task UpdatePendingAsync(int batchSize, List<SharePointFileInfo> filesToUpdate, Action<List<SharePointFileInfo>>? filesUpdatedCallback)
        {
            var backgroundMetaTasksThisChunk = new List<Task<Dictionary<DriveItemSharePointFileInfo, ItemAnalyticsRepsonse>>>();

            // Begin background loading of extra metadata
            var pendingFilesToAnalyse = new List<DocumentSiteFile>();

            foreach (var fileToUpdate in filesToUpdate)
            {
                // We only get stats for docs, not attachments
                if (fileToUpdate is DocumentSiteFile)
                {
                    var docToUpdate = (DocumentSiteFile)fileToUpdate;

                    // Avoid analysing more than once
                    docToUpdate.State = SiteFileAnalysisState.AnalysisInProgress;
                    pendingFilesToAnalyse.Add(docToUpdate);
                }

                // Start new background every $CHUNK_SIZE
                if (pendingFilesToAnalyse.Count >= batchSize)
                {
                    var newFileChunkCopy = new List<DocumentSiteFile>(pendingFilesToAnalyse);
                    pendingFilesToAnalyse.Clear();

                    // Background process chunk
                    backgroundMetaTasksThisChunk.Add(newFileChunkCopy.GetDriveItemsAnalytics(_site.RootURL, _httpClient, _tracer));
                }
            }

            // Background process the rest
            if (pendingFilesToAnalyse.Count > 0)
            {
                backgroundMetaTasksThisChunk.Add(pendingFilesToAnalyse.GetDriveItemsAnalytics(_site.RootURL, _httpClient, _tracer));
            }

            // Update global tasks
            lock (this)
            {
                _backgroundMetaTasks.AddRange(backgroundMetaTasksThisChunk);
            }

            await Task.WhenAll(backgroundMetaTasksThisChunk);

            // Compile results
            var updates = new Dictionary<DriveItemSharePointFileInfo, ItemAnalyticsRepsonse.AnalyticsItemActionStat>();
            foreach (var backgroundTask in backgroundMetaTasksThisChunk)
            {
                foreach (var stat in backgroundTask.Result)
                {
                    if (stat.Value.AccessStats != null)
                    {
                        updates.Add(stat.Key, stat.Value.AccessStats);
                    }
                }
            }

            // Update model & fire event
            var updatedFiles = new List<SharePointFileInfo>(); 
            foreach (var fileUpdated in updates)
            {
                lock (this)
                {
                    // Update model
                    updatedFiles.Add(_model.UpdateDocItem(fileUpdated.Key, fileUpdated.Value));
                }
            }

            filesUpdatedCallback?.Invoke(updatedFiles);
        }

        private Task Crawler_SharePointFileFound(SharePointFileInfo foundFile, int batchSize, Action<List<SharePointFileInfo>>? newFilesCallback)
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

            // Add new found files to model & event buffer
            lock (this)
            {
                _fileFoundBuffer.Add(newFile);
                _model.AddFile(newFile, foundFile.List);
            }

            // Do things every $batchSize
            if (_fileFoundBuffer.Count == batchSize)
            {
                var bufferCopy = new List<SharePointFileInfo>(_fileFoundBuffer);
                if (newFilesCallback != null)
                {
                    newFilesCallback(bufferCopy);
                }
                _fileFoundBuffer.Clear();

                // Start background refresh of new files
                AddToBackgroundDocQueue(bufferCopy.Cast<SharePointFileInfo>().ToList());
            }

            return Task.CompletedTask;
        }
    }
}
