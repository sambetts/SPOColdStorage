using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Entities.DBEntities;
using SPO.ColdStorage.Models;

namespace SPO.ColdStorage.Migration.Engine.SnapshotBuilder
{
    public class TenantModelBuilder : BaseComponent
    {
        public TenantModelBuilder(Config config, DebugTracer debugTracer) : base(config, debugTracer)
        {
        }

        public async Task Build()
        {
            var app = new ClientSecretCredential(_config.AzureAdConfig.TenantId, _config.AzureAdConfig.ClientID, _config.AzureAdConfig.Secret);

            using (var db = new SPOColdStorageDbContext(this._config))
            {
                var tenantModel = new Snapshot();
                var siteTasks = new List<Task<SiteSnapshotModel>>();
                var sitesToAnalyse = await db.TargetSharePointSites.ToListAsync();

                if (sitesToAnalyse.Count == 0)
                {
                    _tracer.TrackTrace($"No sites configured to snapshot.");
                }
                else
                    _tracer.TrackTrace($"Taking snapshot of {sitesToAnalyse.Count} sites:");
                foreach (var s in sitesToAnalyse)
                {
                    _tracer.TrackTrace($"--{s.RootURL}");
                    siteTasks.Add(StartSiteAnalysis(s, app));
                }

                await Task.WhenAll(siteTasks);
                tenantModel.SiteSnapshots.AddRange(siteTasks.Select(s => s.Result));
            }
        }

        private async Task<SiteSnapshotModel> StartSiteAnalysis(TargetMigrationSite site, ClientSecretCredential app)
        {
            var s = new SiteModelBuilder(app, base._config, base._tracer, site);

            return await s.Build();
        }
    }
}
