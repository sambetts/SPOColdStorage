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
            using (var db = new SPOColdStorageDbContext(this._config))
            {
                var tenantModel = new SiteSnapshot();
                var siteTasks = new List<Task<SiteSnapshotModel>>();
                var sitesToAnalyse = await db.TargetSharePointSites.ToListAsync();

                if (sitesToAnalyse.Count == 0)
                {
                    _tracer.TrackTrace($"No sites configured to snapshot.");
                }
                else
                    _tracer.TrackTrace($"Taking snapshot of {sitesToAnalyse.Count} site(s):");
                foreach (var s in sitesToAnalyse)
                {
                    _tracer.TrackTrace($"--{s.RootURL}");
                    siteTasks.Add(StartSiteAnalysisAsync(s));
                }

                await Task.WhenAll(siteTasks);
                tenantModel.SiteSnapshots.AddRange(siteTasks.Select(s => s.Result));
            }
        }

        private async Task<SiteSnapshotModel> StartSiteAnalysisAsync(TargetMigrationSite site)
        {
            var s = new SiteModelBuilder(base._config, base._tracer, site);

            return await s.Build(100, files => 
            {
                using (var db = new SPOColdStorageDbContext(this._config))
                {
                }
            }, updatedFiles => 
            {
                using (var db = new SPOColdStorageDbContext(this._config))
                {
                    _tracer.TrackTrace($"Updating {updatedFiles.Count} to DB");
                }
            });
        }
    }
}
