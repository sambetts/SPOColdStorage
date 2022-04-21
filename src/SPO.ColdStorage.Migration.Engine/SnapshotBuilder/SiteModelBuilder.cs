using Microsoft.Identity.Client;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Entities.DBEntities;
using SPO.ColdStorage.Migration.Engine.Utils;
using SPO.ColdStorage.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Migration.Engine.SnapshotBuilder
{
    public class SiteModelBuilder : BaseComponent, IDisposable
    {
        private readonly TargetMigrationSite site;
        private readonly SPOColdStorageDbContext _db;
        private readonly SiteListFilterConfig _siteFilterConfig;
        private readonly SiteModel _model;
        private readonly CSOMv2Helper _CSOMv2Helper;
        public SiteModelBuilder(IConfidentialClientApplication app, Config config, DebugTracer debugTracer, TargetMigrationSite site) : base(config, debugTracer)
        {
            this.site = site;
            _db = new SPOColdStorageDbContext(this._config);
            _model = new SiteModel();
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

        public async Task<SiteModel> Build()
        {
            if (!_model.Finished.HasValue)
            {
                var ctx = await AuthUtils.GetClientContext(_config, site.RootURL, _tracer);
                var crawler = new SiteListsAndLibrariesCrawler(ctx, _tracer, Crawler_SharePointFileFound);
                await crawler.CrawlContextRootWebAndSubwebs(_siteFilterConfig);
            }

            return _model;
        }

        public void Dispose()
        {
            _db.Dispose();
        }

        private async Task Crawler_SharePointFileFound(SharePointFileInfo arg)
        {
            var newFile = new SiteFile() { FileName = arg.ServerRelativeFilePath };

            if (arg is DriveItemSharePointFileInfo)
            {
                var driveArg = (DriveItemSharePointFileInfo)arg;
                var stats = await _CSOMv2Helper.GetDriveItemAnalytics(driveArg.DriveId, driveArg.GraphItemId);
            }

            _model.Files.Add(newFile);
            Console.WriteLine($"lol {arg.FullSharePointUrl}");
        }
    }

    public class SiteModel
    {
        public DateTime Started { get; set; } = DateTime.Now;
        public DateTime? Finished { get; set; }

        public TargetMigrationSite Site { get; set; } = new TargetMigrationSite();

        public List<SiteFile> Files { get; set; } = new List<SiteFile>();
    }
    public class SiteFile
    {
        public string FileName { get; set; } = string.Empty;
    }

}
