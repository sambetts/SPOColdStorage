
using SPO.ColdStorage.Migration.Engine;
using SPO.ColdStorage.Migration.Engine.Utils;

Console.WriteLine("SPO Cold Storage - Site Snapshot Builder");
Console.WriteLine("This app will listen for messages from service-bus and handle them when they arrive, untill you close this application.");

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

var listener = new ServiceBusMigrationListener(config, tracer);
await listener.ListenForFilesToMigrate();
