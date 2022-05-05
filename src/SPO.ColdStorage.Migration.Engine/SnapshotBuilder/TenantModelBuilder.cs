using Microsoft.EntityFrameworkCore;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Entities.DBEntities;
using SPO.ColdStorage.Models;

namespace SPO.ColdStorage.Migration.Engine.SnapshotBuilder
{
    public class TenantModelBuilder : BaseComponent
    {
        private List<Task> _updateTasks = new();
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

            return await s.Build(100,
                async filesDiscovered => await InsertFilesAsync(filesDiscovered),
                async updatedFiles => await UpdateFiles(updatedFiles)
            );
        }
        async Task InsertFilesAsync(List<SharePointFileInfoWithList> insertedFiles)
        {
            int inserted = 0;
            Console.WriteLine("START INSERT FILES");
            using (var db = new SPOColdStorageDbContext(this._config))
            {
                var files = new List<StagingTempFile>();
                foreach (var insertedFile in insertedFiles)
                {
                    var f = new StagingTempFile(insertedFile);
                    files.Add(f);
                }
                await db.StagingFiles.AddRangeAsync(files);
                await db.SaveChangesAsync();
                _tracer.TrackTrace($"END INSERT: Inserted {inserted} new files.");
            }
        }
        Task UpdateFiles(List<SharePointFileInfoWithList> updatedFiles)
        {
            _updateTasks.Add(Task.Run(async () =>
            {
                int updated = 0, inserted = 0;
                using (var db = new SPOColdStorageDbContext(this._config))
                {
                    _tracer.TrackTrace($"Updating {updatedFiles.Count} to DB");
                    foreach (var updatedFile in updatedFiles)
                    {
                        var r = await UpdateStats(updatedFile, db);
                        if (r == StatsSaveResult.New) inserted++;
                        else if (r == StatsSaveResult.Updated) updated++;
                    }

                    _tracer.TrackTrace($"Inserted {inserted} stats and updated {updated}");
                    await db.SaveChangesAsync();
                }
            }));
            return Task.CompletedTask;
        }
        

        async Task<StatsSaveResult> UpdateStats(BaseSharePointFileInfo updatedFile, SPOColdStorageDbContext db)
        {
            var existingFile = await db.Files.Where(f => f.Url == updatedFile.FullSharePointUrl).SingleOrDefaultAsync();
            if (existingFile == null)
            {
                _tracer.TrackTrace($"Got update for a file that we haven't inserted yet...", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning);
                existingFile = await updatedFile.GetDbFileForFileInfo(db);
            }
            var stats = await db.FileStats.Where(s => s.File == existingFile).SingleOrDefaultAsync();
            if (stats == null)
            {
                stats = new FileStats();
                db.FileStats.Add(stats);
                return StatsSaveResult.New;
            }
            else
            {
                return StatsSaveResult.Updated;
            }
        }

        enum StatsSaveResult
        {
            New,
            Updated
        }

    }
}
