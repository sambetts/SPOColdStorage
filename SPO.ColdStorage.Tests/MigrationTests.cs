using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Migration.Engine;
using SPO.ColdStorage.Migration.Engine.Migration;
using SPO.ColdStorage.Migration.Engine.Model;
using SPO.ColdStorage.Migration.Engine.Utils;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Tests
{
    [TestClass]
    public class MigrationTests
    {
        #region Plumbing
        const string FILE_CONTENTS = "En un lugar de la Mancha, de cuyo nombre no quiero acordarme, no ha mucho tiempo que viv�a un hidalgo de los de lanza en astillero, adarga antigua, roc�n flaco y galgo corredor";

        private Config? _config;
        private DebugTracer _tracer = DebugTracer.ConsoleOnlyTracer();

        [TestInitialize]
        public void Init()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly())
                .AddEnvironmentVariables()
                .AddJsonFile("appsettings.json", true);


            var config = builder.Build();
            _config = new Config(config);
        }
        #endregion

        /// <summary>
        /// Runs nearly all tests without using Service Bus. Creates a new file in SP, then migrates it to Azure Blob, and verifies the contents.
        /// </summary>
        [TestMethod]
        public async Task SharePointFileMigrationTests()
        {
            var migrator = new SharePointFileMigrator(_config!);

            var ctx = await AuthUtils.GetClientContext(_config!, _config!.DevConfig.DefaultSharePointSite);

            // Upload a test file to SP
            var targetList = ctx.Web.Lists.GetByTitle("Documents");

            var fileTitle = $"unit-test file {DateTime.Now.Ticks}.txt";
            await targetList.SaveNewFile(ctx, fileTitle, System.Text.Encoding.UTF8.GetBytes(FILE_CONTENTS));


            // Discover file in SP with crawler
            var crawler = new SiteListsAndLibrariesCrawler(ctx, _tracer);
            var allResults = await crawler.CrawlList(targetList);

            // Check it's the right file
            var discoveredFile = allResults.Where(r => r.FileRelativePath.Contains(fileTitle)).FirstOrDefault();
            Assert.IsNotNull(discoveredFile);

            // Migrate the file to az blob
            await migrator.MigrateFromSharePointToBlobStorage(discoveredFile, ctx);

            // Download file again from az blob
            var tempLocalFile = SharePointFileDownloader.GetTempFileNameAndCreateDir(discoveredFile);
            var blobServiceClient = new BlobServiceClient(_config.StorageConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_config.BlobContainerName);
            var blobClient = containerClient.GetBlobClient(discoveredFile.FileRelativePath);

            await blobClient.DownloadToAsync(tempLocalFile);
            

            // Check az blob file contents matches original data
            var azDownloadedFile = File.ReadAllText(tempLocalFile);
            Assert.AreEqual(azDownloadedFile, FILE_CONTENTS);
            File.Delete(tempLocalFile);
        }

        [TestMethod]
        public async Task SharePointFileDownloaderTests()
        {
            var testMsg = new SharePointFileInfo 
            { 
                SiteUrl = _config!.DevConfig.DefaultSharePointSite, 
                FileRelativePath = "/sites/MigrationHost/Shared%20Documents/Blank%20Office%20PPT.pptx"
            };
            var ctx = await AuthUtils.GetClientContext(_config!, testMsg.SiteUrl);

            var m = new SharePointFileDownloader(ctx, _config!);
            await m.DownloadFileToTempDir(testMsg);
        }

        [TestMethod]
        public async Task SharePointFileSearchProcessorTests()
        {
            var testMsg = new SharePointFileInfo
            {
                SiteUrl = _config!.DevConfig.DefaultSharePointSite,
                FileRelativePath = "/sites/MigrationHost/Shared%20Documents/Blank%20Office%20PPT.pptx"
            };

            var m = new SharePointFileSearchProcessor(_config!);
            await m.ProcessFileContent(testMsg);
        }

        [TestMethod]
        public async Task BlobStorageFileUploadTests()
        {
            var testMsg = new SharePointFileInfo
            {
                SiteUrl = _config!.DevConfig.DefaultSharePointSite,
                FileRelativePath = $"/sites/MigrationHost/Unit tests/textfile{DateTime.Now.Ticks}.txt"
            };

            // Write a fake file 
            string tempFileName = SharePointFileDownloader.GetTempFileNameAndCreateDir(testMsg);
            System.IO.File.WriteAllText(tempFileName, FILE_CONTENTS);

            // Upload - shouldn't exist in destination
            var m = new BlobStorageUploader(_config!);
            await m.UploadFileToAzureBlob(tempFileName, testMsg);

            // Write same file again. Should also work.
            await m.UploadFileToAzureBlob(tempFileName, testMsg);
        }

    }
}