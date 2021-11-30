﻿// dotnet user-secrets set "AzureAd:ClientID" "" --project "SPO.ColdStorage.Migration.Indexer"
// dotnet user-secrets set "AzureAd:Secret" "" --project "SPO.ColdStorage.Migration.Indexer"
// dotnet user-secrets set "AzureAd:TenantId" "" --project "SPO.ColdStorage.Migration.Indexer"
// dotnet user-secrets set "ConnectionStrings:ColdStorageDbContext" "Server=(localdb)\\mssqllocaldb;Database=SPOColdStorageDbContextDev;Trusted_Connection=True;MultipleActiveResultSets=true" --project "SPO.ColdStorage.Migration.Indexer"
// dotnet user-secrets set "ConnectionStrings:ServiceBus" "" --project "SPO.ColdStorage.Migration.Indexer"
// dotnet user-secrets set "ConnectionStrings:Storage" "" --project "SPO.ColdStorage.Migration.Indexer"
// dotnet user-secrets set "Dev:DefaultStorageConnection" "" --project "SPO.ColdStorage.Migration.Indexer"
// dotnet user-secrets set "Dev:DefaultSharePointSite" "" --project "SPO.ColdStorage.Migration.Indexer"
// dotnet user-secrets set "KeyVaultUrl" "https://spocoldstoragedev.vault.azure.net" --project "SPO.ColdStorage.Migration.Indexer"
// dotnet user-secrets set "BaseServerAddress" "https://m365x352268.sharepoint.com" --project "SPO.ColdStorage.Migration.Indexer"

using Microsoft.Extensions.Configuration;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Migration.Engine;
using System.Reflection;

Console.WriteLine("Indexer");

var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .AddEnvironmentVariables()
    .AddJsonFile("appsettings.json", true);

var config = builder.Build(); 
var allConfig = new Config(config);

// Init DB
using (var db = new ColdStorageDbContext(allConfig.SQLConnectionString))
{
    await DbInitializer.Init(db, allConfig.DevConfig);
}

// Start discovery
var discovery = new SharePointContentIndexer(allConfig);
await discovery.StartMigrateNeededFiles();


Console.WriteLine("Done");
