﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SPO.ColdStorage.Entities.Configuration;

namespace SPO.ColdStorage.Entities
{
    /// <summary>
    /// SQL model.
    /// </summary>
    public class SPOColdStorageDbContext : DbContext
    {
        private readonly Config? _config;


        public SPOColdStorageDbContext(Config config)
        {
            this._config = config;
        }

        public SPOColdStorageDbContext(DbContextOptions<SPOColdStorageDbContext> options, Config? config) : base(options)
        {
            this._config = config;
        }


        // Migrations:
        // Add-Migration -Name "FilterConfig" -Project "SPO.ColdStorage.Entities" -StartupProject "SPO.ColdStorage.Tests" -Context SPOColdStorageDbContext
        // Script-Migration -Project "SPO.ColdStorage.Entities" -StartupProject "SPO.ColdStorage.Tests" -From "PreviousMigration" -Context SPOColdStorageDbContext

        public DbSet<DBEntities.TargetMigrationSite> TargetSharePointSites { get; set; } = null!;
        public DbSet<DBEntities.Site> Sites { get; set; } = null!;
        public DbSet<DBEntities.Web> Webs { get; set; } = null!;
        public DbSet<DBEntities.File> Files { get; set; } = null!;
        public DbSet<DBEntities.FileMigrationErrorLog> FileMigrationErrors { get; set; } = null!;
        public DbSet<DBEntities.FileMigrationCompletedLog> FileMigrationsCompleted { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlServer(_config!.ConnectionStrings.SQLConnectionString, op => op.EnableRetryOnFailure());
    }


    /// <summary>
    /// For EF migrations
    /// </summary>
    public class BuildSyncDbContextFactory : IDesignTimeDbContextFactory<SPOColdStorageDbContext>
    {
        public SPOColdStorageDbContext CreateDbContext(string[] args)
        {
            var c = new List<KeyValuePair<string, string>>();
            c.Add(new KeyValuePair<string, string>("KeyVaultUrl", "Unit testing"));
            c.Add(new KeyValuePair<string, string>("BaseServerAddress", "Unit testing"));
            c.Add(new KeyValuePair<string, string>("ConnectionStrings:SQLConnectionString", "Server=(localdb)\\mssqllocaldb;Database=BuildSyncDev;Trusted_Connection=True;MultipleActiveResultSets=true"));

            var configCollection = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddUserSecrets(System.Reflection.Assembly.GetEntryAssembly(), true)
                .AddJsonFile("appsettings.json", true).Build();

            var optionsBuilder = new DbContextOptionsBuilder<SPOColdStorageDbContext>();
            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=BuildSyncDev;Trusted_Connection=True;MultipleActiveResultSets=true");

            return new SPOColdStorageDbContext(new Config(configCollection));
        }
    }
}
