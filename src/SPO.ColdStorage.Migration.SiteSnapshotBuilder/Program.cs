
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Migration.Engine;
using SPO.ColdStorage.Migration.Engine.SnapshotBuilder;
using SPO.ColdStorage.Migration.Engine.Utils;

Console.WriteLine("SPO Cold Storage - Site Snapshot Builder");
Console.WriteLine("This app will build new space snapshots for configured site-collections.");

var config = ConsoleUtils.GetConfigurationWithDefaultBuilder();
ConsoleUtils.PrintCommonStartupDetails();

// Send to application insights or just the stdout?
DebugTracer tracer;
if (config.HaveAppInsightsConfigured)
{
    tracer = new DebugTracer(config.AppInsightsInstrumentationKey, "SnapshotBuilder");
}
else
    tracer = DebugTracer.ConsoleOnlyTracer();


// Init DB
using (var db = new SPOColdStorageDbContext(config))
{
    await DbInitializer.Init(db, config.DevConfig);
}


var analyser = new TenantModelBuilder(config, tracer);
await analyser.Build();


Console.WriteLine("\nAll sites scanned. Finished building snapshot.");