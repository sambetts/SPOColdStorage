using Microsoft.Extensions.Configuration;
using SPO.ColdStorage.Entities.Configuration;

namespace SPO.ColdStorage.Migration.Engine.Utils
{
    public class ConsoleUtils
    {
        public static Config GetConfigurationWithDefaultBuilder()
        {
            var builder = GetConfigurationBuilder();

            var configCollection = builder.Build();
            return new Config(configCollection);
        }

        public static IConfigurationBuilder GetConfigurationBuilder()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddUserSecrets(System.Reflection.Assembly.GetEntryAssembly(), true)
                .AddEnvironmentVariables()
                .AddJsonFile("appsettings.json", true);
        }

        public static void PrintCommonStartupDetails()
        {
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            Console.WriteLine($"Start-up: '{assembly?.FullName}'.");
        }
    }
}
