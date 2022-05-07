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
        private StagingFilesMigrator stagingFilesMigrator = new();
        public TenantModelBuilder(Config config, DebugTracer debugTracer) : base(config, debugTracer)
        {
        }

        public async Task Build()
        {
            using (var db = new SPOColdStorageDbContext(this._config))
            {
                // Clean staging 1st
                await stagingFilesMigrator.CleanStagingAll(db);

                // Start analysis
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
                    await StartSiteAnalysisAsync(s);
                }
            }
        }

        private async Task<SiteSnapshotModel> StartSiteAnalysisAsync(TargetMigrationSite site)
        {
            var s = new SiteModelBuilder(base._config, base._tracer, site);

            var siteModel = await s.Build(100,
                async filesDiscovered => await InsertFilesAsync(filesDiscovered),
                async updatedFiles => await UpdateFiles(updatedFiles)
            );

            await Task.WhenAll(_updateTasks);
            _tracer.TrackTrace($"--{site.RootURL} finished.");
            return siteModel;
        }
        async Task InsertFilesAsync(List<SharePointFileInfoWithList> insertedFiles)
        {
            using (var db = new SPOColdStorageDbContext(this._config))
            {
                var executionStrategy = db.Database.CreateExecutionStrategy();
                await executionStrategy.Execute(async () =>
           {
               using (var trans = await db.Database.BeginTransactionAsync())
               {
                   var blockGuid = Guid.NewGuid();
                   var inserted = DateTime.Now;

                   // Insert staging data
                   var files = new List<StagingTempFile>();
                   foreach (var insertedFile in insertedFiles)
                   {
                       var f = new StagingTempFile(insertedFile, blockGuid, inserted);
                       files.Add(f);
                   }
                   await db.StagingFiles.AddRangeAsync(files);
                   await db.SaveChangesAsync();

                   // Merge from staging to tables
                   var inserts = stagingFilesMigrator.MigrateBlockAndCleanFromStaging(db, blockGuid);

                   await trans.CommitAsync();
               }
           });


            }
        }
        Task UpdateFiles(List<DocumentSiteWithMetadata> updatedFiles)
        {
            _updateTasks.Add(Task.Run(async () =>
            {
                int updated = 0, inserted = 0;
                using (var db = new SPOColdStorageDbContext(this._config))
                {
                    _tracer.TrackTrace($"Updating {updatedFiles.Count} files to DB from downloaded metadata");
                    foreach (var updatedFile in updatedFiles)
                    {
                        var r = await UpdateStats(updatedFile, db);
                        if (r == StatsSaveResult.New) inserted++;
                        else if (r == StatsSaveResult.Updated) updated++;
                    }
                    await db.SaveChangesAsync();
                }
            }));
            return Task.CompletedTask;
        }


        async Task<StatsSaveResult> UpdateStats(DocumentSiteWithMetadata updatedFile, SPOColdStorageDbContext db)
        {
            var results = StatsSaveResult.New;
            var existingFile = await db.Files.Where(f => f.Url == updatedFile.ServerRelativeFilePath).SingleOrDefaultAsync();
            if (existingFile == null)
            {
                _tracer.TrackTrace($"Got update for a file that we haven't inserted yet...", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning);
                existingFile = await updatedFile.GetDbFileForFileInfo(db);
            }
            if (existingFile.StatsUpdated.HasValue)
            {
                results = StatsSaveResult.Updated;
            }

            // Set stats
            existingFile.StatsUpdated = DateTime.Now;
            existingFile.AccessCount = updatedFile.AccessCount;
            existingFile.VersionCount = updatedFile.VersionCount;
            existingFile.VersionHistorySize = updatedFile.VersionHistorySize;
            existingFile.LastModified = updatedFile.LastModified;
            existingFile.FileSize = updatedFile.FileSize;

            return results;
        }

        enum StatsSaveResult
        {
            New,
            Updated
        }

    }
}
