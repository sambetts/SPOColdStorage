using Microsoft.Identity.Client;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Entities.DBEntities;
using SPO.ColdStorage.Migration.Engine.Model;
using SPO.ColdStorage.Migration.Engine.Utils;
using SPO.ColdStorage.Models;

namespace SPO.ColdStorage.Migration.Engine.SnapshotBuilder
{
    /// <summary>
    /// Builds a snapshot for a single site
    /// </summary>
    public class SiteModelBuilder : BaseComponent, IDisposable
    {
        #region Privates & Constructors

        private readonly TargetMigrationSite site;
        private readonly SPOColdStorageDbContext _db;
        private readonly SiteListFilterConfig _siteFilterConfig;
        private readonly SiteSnapshotModel _model;
        private readonly CSOMv2Helper _CSOMv2Helper;
        public SiteModelBuilder(IConfidentialClientApplication app, Config config, DebugTracer debugTracer, TargetMigrationSite site) : base(config, debugTracer)
        {
            this.site = site;
            _db = new SPOColdStorageDbContext(this._config);
            _model = new SiteSnapshotModel();
            _CSOMv2Helper = new CSOMv2Helper(app, config.BaseServerAddress, site.RootURL, _tracer);

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
                var ctx = await AuthUtils.GetClientContext(_config, site.RootURL, _tracer);
                var crawler = new SiteListsAndLibrariesCrawler(ctx, _tracer, Crawler_SharePointFileFound);
                await crawler.CrawlContextRootWebAndSubwebs(_siteFilterConfig);
            }

            return _model;
        }


        private async Task Crawler_SharePointFileFound(SharePointFileInfo arg)
        {
            var newFile = new SiteFile() { FileName = arg.ServerRelativeFilePath };

            if (arg is DriveItemSharePointFileInfo)
            {
                var driveArg = (DriveItemSharePointFileInfo)arg;
                var stats = await _CSOMv2Helper.GetDriveItemAnalytics(driveArg.DriveId, driveArg.GraphItemId);

                newFile.FileType = FileType.DocumentLibraryFile;
                if (stats.AccessStats != null)
                {
                    newFile.AccessCount = stats.AccessStats.ActionCount;
                }
                else
                {
                    newFile.AccessCount = null;
                }
            }
            else
            {
                newFile.FileType = FileType.ListItemAttachement;
            }

            _model.Files.Add(newFile);
            Console.WriteLine($"lol {arg.FullSharePointUrl}");
        }
    }


}
