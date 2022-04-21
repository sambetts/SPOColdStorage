using Microsoft.EntityFrameworkCore;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Entities.DBEntities;
using SPO.ColdStorage.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Migration.Engine.SnapshotBuilder
{
    public class TenantModelBuilder : BaseComponent
    {
        public TenantModelBuilder(Config config, DebugTracer debugTracer) : base(config, debugTracer)
        {
        }

        public async Task Build()
        {
            var app = await AuthUtils.GetNewClientApp(_config);
            using (var db = new SPOColdStorageDbContext(this._config))
            {
                var sitesToMigrate = await db.TargetSharePointSites.ToListAsync();
                foreach (var s in sitesToMigrate)
                {
                    await StartSiteAnalysis(s, app);
                }
            }
        }

        private async Task StartSiteAnalysis(TargetMigrationSite site, Microsoft.Identity.Client.IConfidentialClientApplication app)
        {

            var s = new SiteModelBuilder(app, base._config, base._tracer, site);

            await s.Build();
        }
    }
}
