using Microsoft.EntityFrameworkCore;
using SPO.ColdStorage.Entities;
using System.Reflection;

namespace SPO.ColdStorage.Migration.Engine.SnapshotBuilder
{
    /// <summary>
    /// Migrates data from StagingFiles to proper tables + lookups with raw SQL for speed.
    /// </summary>
    public class StagingFilesMigrator
    {
        private static string _sqlTemplate = string.Empty;

        /// <summary>
        /// Migrate from staging to real tables a specific block. Staging cleaned after migrate.
        /// </summary>
        public int MigrateBlockAndCleanFromStaging(SPOColdStorageDbContext context, Guid blockGuid)
        {
            lock (this)
            {
                if (string.IsNullOrEmpty(_sqlTemplate))
                {
                    _sqlTemplate = ReadResource("SPO.ColdStorage.Migration.Engine.SQL.MergeStagingFiles.sql");
                }
                var blockSql = _sqlTemplate.Replace("--[blockset]--", $"SET @blockGuid='{blockGuid}';");
                var rowsAffected = context.Database.ExecuteSqlRaw(blockSql);

                return rowsAffected;
            }

        }

        public async Task CleanStagingAll(SPOColdStorageDbContext context)
        {
            var blockSql = "delete from [StagingFiles]";
            await context.Database.ExecuteSqlRawAsync(blockSql);
        }

        protected string ReadResource(string resourcePath)
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Format: "{Namespace}.{Folder}.{filename}.{Extension}"
            var manifests = assembly.GetManifestResourceNames();

            using (var stream = assembly.GetManifestResourceStream(resourcePath))
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(resourcePath), $"No resource found by name '{resourcePath}'");
                }
        }

    }
}
