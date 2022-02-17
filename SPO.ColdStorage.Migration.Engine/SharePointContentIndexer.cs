using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Migration.Engine.Migration;
using SPO.ColdStorage.Models;

namespace SPO.ColdStorage.Migration.Engine
{
    /// <summary>
    /// Finds files to migrate in a SharePoint site-collection
    /// </summary>
    public class SharePointContentIndexer : BaseComponent
    {
        #region Constructors & Privates

        private BlobServiceClient _blobServiceClient;
        private BlobContainerClient? _containerClient;
        private SharePointFileMigrator _sharePointFileMigrator;

        public SharePointContentIndexer(Config config, DebugTracer debugTracer) : base(config, debugTracer)
        {
            var sbConnectionProps = ServiceBusConnectionStringProperties.Parse(_config.ConnectionStrings.ServiceBus);
            _tracer.TrackTrace($"Sending new SharePoint files to migrate to service-bus '{sbConnectionProps.Endpoint}'.");


            // Create a BlobServiceClient object which will be used to create a container client
            _blobServiceClient = new BlobServiceClient(_config.ConnectionStrings.Storage);
            _sharePointFileMigrator = new SharePointFileMigrator(config, _tracer);
        }

        #endregion

        public async Task StartMigrateAllSites()
        {
            // Create the container and return a container client object
            this._containerClient = _blobServiceClient.GetBlobContainerClient(_config.BlobContainerName);

            // Create container with no access to public
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            using (var db = new SPOColdStorageDbContext(this._config))
            {
                var sitesToMigrate = await db.TargetSharePointSites.ToListAsync();
                foreach (var s in sitesToMigrate)
                {
                    SiteListFilterConfig? siteFilterConfig = null;
                    if (!string.IsNullOrEmpty(s.FilterConfigJson))
                    {
                        try
                        {
                            siteFilterConfig = SiteListFilterConfig.FromJson(s.FilterConfigJson);
                        }
                        catch (Exception ex)
                        {
                            _tracer.TrackTrace($"Couldn't deserialise filter JSon for site '{s.RootURL}': {ex.Message}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning);
                        }
                    }
                    
                    // Instantiate "allow all" config if none can be found in the DB
                    if (siteFilterConfig == null)
                        siteFilterConfig = new SiteListFilterConfig();

                    await StartSiteMigration(s.RootURL, siteFilterConfig);
                }
            }
        }

        async Task StartSiteMigration(string siteUrl, SiteListFilterConfig siteFolderConfig)
        {
            var ctx = await AuthUtils.GetClientContext(_config, siteUrl);

            _tracer.TrackTrace($"Scanning site-collection '{siteUrl}'...");

            var crawler = new SiteListsAndLibrariesCrawler(ctx, _tracer, Crawler_SharePointFileFound);
            await crawler.CrawlContextRootWebAndSubwebs(siteFolderConfig);
        }

        /// <summary>
        /// Crawler found a relevant file
        /// </summary>
        private async Task Crawler_SharePointFileFound(SharePointFileInfo foundFileInfo)
        {
            await _sharePointFileMigrator.QueueSharePointFileMigrationIfNeeded(foundFileInfo, _containerClient!);
        }
    }
}
