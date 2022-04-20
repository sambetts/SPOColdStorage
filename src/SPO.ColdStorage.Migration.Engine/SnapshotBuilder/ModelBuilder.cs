using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Migration.Engine.SnapshotBuilder
{
    public class ModelBuilder : BaseComponent
    {
        public ModelBuilder(Config config, DebugTracer debugTracer) : base(config, debugTracer)
        {
        }

        public async Task Build(string siteUrl, SiteListFilterConfig siteFolderConfig)
        {
            var ctx = await AuthUtils.GetClientContext(_config, siteUrl, _tracer);
            var crawler = new SiteListsAndLibrariesCrawler(ctx, _tracer, Crawler_SharePointFileFound);
            await crawler.CrawlContextRootWebAndSubwebs(siteFolderConfig);
        }

        private Task Crawler_SharePointFileFound(SharePointFileInfo arg)
        {
            throw new NotImplementedException();
        }
    }
}
