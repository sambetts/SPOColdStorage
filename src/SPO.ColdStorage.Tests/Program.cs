using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Migration.Engine;
using SPO.ColdStorage.Migration.Engine.Utils;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using SPO.ColdStorage.Models;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace SPO.ColdStorage.Tests
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World! This is a console app for testing whatever isn't working. Shouldn't be run normally.");

            var config = ConsoleUtils.GetConfigurationWithDefaultBuilder();

            var optionsBuilder = new DbContextOptionsBuilder<SPOColdStorageDbContext>();
            optionsBuilder
                .UseSqlServer(config.ConnectionStrings.SQLConnectionString);

            var tracer = DebugTracer.ConsoleOnlyTracer();

            using (var server = new WebHostBuilder()
                    .UseEnvironment("Test")
                    .UseStartup<Startup>()
                    .UseKestrel(options =>
                    {
                        options.Listen(System.Net.IPAddress.Loopback, 5000);
                    })
                .Build())
            {
                server.Start();

                using (var db = new SPOColdStorageDbContext(config))
                {
                    await DbInitializer.Init(db, config.DevConfig);

                    var sw = new Stopwatch();
                    sw.Start();
                    Console.WriteLine("Start tests");

                    // Run tests
                    var httpClient = new SecureSPThrottledHttpClient(config, true, tracer);
                    await DoThings(db, config, DebugTracer.ConsoleOnlyTracer(), httpClient);

                    Console.WriteLine(sw.Elapsed);
                }
            }
            Console.WriteLine("All done");
        }

        private static async Task DoThings(SPOColdStorageDbContext db, Entities.Configuration.Config config, DebugTracer tracer, SecureSPThrottledHttpClient client)
        {
            var resp = await client.GetAsync("http://localhost:5000/api/values");
            const int loopCount = 1000;
            var fakeDocs = new List<DocumentSiteWithMetadata>();
            for (int i = 0; i < loopCount; i++)
            {
                fakeDocs.Add(new DocumentSiteWithMetadata { DriveId = i.ToString(), GraphItemId = i.ToString() });
            }

            var results = await fakeDocs.GetDriveItemsAnalytics("http://localhost:5000", client, tracer, 0);
            Assert.IsTrue(results.Count == loopCount);

            for (int i = 0; i < loopCount; i++)
            {
                var item = results.Where(r=> r.Key.GraphItemId == i.ToString()).FirstOrDefault();
                Assert.IsNotNull(item);
            }
        }
    }


    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}