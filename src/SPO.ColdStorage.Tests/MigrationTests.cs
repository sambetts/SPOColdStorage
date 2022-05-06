using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.SharePoint.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Migration.Engine;
using SPO.ColdStorage.Migration.Engine.Migration;
using SPO.ColdStorage.Migration.Engine.Utils;
using SPO.ColdStorage.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Tests
{
    [TestClass]
    public class MigrationTests : AbstractTest
    {

        [TestMethod]
        public async Task GetDriveItemAnalyticsTests()
        {
            var app = await AuthUtils.GetNewClientApp(_config!);
            var ctx = await AuthUtils.GetClientContext(app, _config!.BaseServerAddress, _config!.DevConfig.DefaultSharePointSite, _tracer);

            // Upload a test file to SP
            var targetList = ctx.Web.Lists.GetByTitle("Documents");

            var fileTitle = $"unit-test file {DateTime.Now.Ticks}.txt";
            var newItemId = await targetList.SaveFile(ctx, fileTitle, System.Text.Encoding.UTF8.GetBytes(FILE_CONTENTS));

            // Update contents
            await targetList.SaveFile(ctx, fileTitle, System.Text.Encoding.UTF8.GetBytes(FILE_CONTENTS + "v2"));

            var uploaded = targetList.GetItemByUniqueId(newItemId);

            await uploaded.FullLoadListItemDoc(ctx);

            var creds = new ClientSecretCredential(_config.AzureAdConfig.TenantId, _config.AzureAdConfig.ClientID, _config.AzureAdConfig.Secret);
            var gc = new GraphServiceClient(creds);

            var httpClient = new SecureSPThrottledHttpClient(_config!, false, DebugTracer.ConsoleOnlyTracer());

            // Test batch method with files in doc-lib
            var driveItems = await gc.Drives[uploaded.File.VroomDriveID].Root.Children.Request().GetAsync();

            var graphFileInfoList = new System.Collections.Generic.List<DocumentSiteWithMetadata>();
            var graphFiles = driveItems.Select(d => new DocumentSiteWithMetadata { DriveId = uploaded.File.VroomDriveID, GraphItemId = d.Id });
            graphFileInfoList.AddRange(graphFiles);

            var batchAnalytics = await graphFileInfoList.GetDriveItemsAnalytics(_config!.DevConfig.DefaultSharePointSite, httpClient, _tracer);
            Assert.IsTrue(batchAnalytics.Count == graphFiles.Count());

            var filesWithAnalytics = batchAnalytics.Select(d => d.Value).Where(v=> v.AccessStats != null && v.AccessStats.ActionCount > 0).ToList();
        }

        /// <summary>
        /// Runs nearly all tests without using Service Bus. Creates a new file in SP, then migrates it to Azure Blob, and verifies the contents.
        /// </summary>
        [TestMethod]
        public async Task SharePointFileMigrationTests()
        {
            var migrator = new SharePointFileMigrator(_config!, _tracer);

            var app = await AuthUtils.GetNewClientApp(_config!);
            var ctx = await AuthUtils.GetClientContext(app, _config!.BaseServerAddress, _config!.DevConfig.DefaultSharePointSite, _tracer);

            // Upload a test file to SP
            var targetList = ctx.Web.Lists.GetByTitle("Documents");

            var fileTitle = $"unit-test file {DateTime.Now.Ticks}.txt";
            await targetList.SaveFile(ctx, fileTitle, System.Text.Encoding.UTF8.GetBytes(FILE_CONTENTS));


            // Discover file in SP with crawler
            var crawler = new SiteListsAndLibrariesCrawler(ctx, _tracer);
            var allResults = await crawler.CrawlList(targetList);

            // Check it's the right file
            var discoveredFile = allResults.Where(r => r.ServerRelativeFilePath.Contains(fileTitle)).FirstOrDefault();
            Assert.IsNotNull(discoveredFile);

            // Migrate the file to az blob
            await migrator.MigrateFromSharePointToBlobStorage(discoveredFile, app);

            // Download file again from az blob
            var tempLocalFile = SharePointFileDownloader.GetTempFileNameAndCreateDir(discoveredFile);
            var blobServiceClient = new BlobServiceClient(_config.ConnectionStrings.Storage);
            var containerClient = blobServiceClient.GetBlobContainerClient(_config.BlobContainerName);
            var blobClient = containerClient.GetBlobClient(discoveredFile.ServerRelativeFilePath);

            await blobClient.DownloadToAsync(tempLocalFile);


            // Check az blob file contents matches original data
            var azDownloadedFile = System.IO.File.ReadAllText(tempLocalFile);
            Assert.AreEqual(azDownloadedFile, FILE_CONTENTS);
            System.IO.File.Delete(tempLocalFile);
        }

        /// <summary>
        /// Checks we don't migrate files that are already in az blob
        /// </summary>
        [TestMethod]
        public async Task SharePointFileNeedsMigratingTests()
        {
            var migrator = new SharePointFileMigrator(_config!, _tracer);


            var app = await AuthUtils.GetNewClientApp(_config!);
            var ctx = await AuthUtils.GetClientContext(app, _config!.BaseServerAddress, _config!.DevConfig.DefaultSharePointSite, _tracer);

            // Upload a test file to SP
            var targetList = ctx.Web.Lists.GetByTitle("Documents");
            var fileTitle = $"unit-test file {DateTime.Now.Ticks}.txt";
            await targetList.SaveFile(ctx, fileTitle, System.Text.Encoding.UTF8.GetBytes(FILE_CONTENTS));

            // Prepare for file migration
            var discoveredFile = await GetFromIndex(ctx, fileTitle, targetList);
            var blobServiceClient = new BlobServiceClient(_config.ConnectionStrings.Storage);
            var containerClient = blobServiceClient.GetBlobContainerClient(_config.BlobContainerName);

            // Before migration: SharePointFileNeedsMigrating should be true
            var needsMigratingBeforeMigration = await migrator.DoesSharePointFileNeedMigrating(discoveredFile!, containerClient);
            Assert.IsTrue(needsMigratingBeforeMigration);

            // Migrate the file to az blob & save result to SQL 
            await migrator.MigrateFromSharePointToBlobStorage(discoveredFile!, app);
            await migrator.SaveSucessfulFileMigrationToSql(discoveredFile!);

            // Now SharePointFileNeedsMigrating should be false
            var needsMigratingPostMigration = await migrator.DoesSharePointFileNeedMigrating(discoveredFile!, containerClient);
            Assert.IsFalse(needsMigratingPostMigration);

            // Update file with new content and recrawl
            await targetList.SaveFile(ctx, fileTitle, System.Text.Encoding.UTF8.GetBytes(FILE_CONTENTS + " + extra data"));
            discoveredFile = await GetFromIndex(ctx, fileTitle, targetList);

            // Now the file's been updated, it should need a new migration
            var needsMigratingPostEdit = await migrator.DoesSharePointFileNeedMigrating(discoveredFile!, containerClient);

            Assert.IsTrue(needsMigratingPostEdit);

            // Migrate again edited file, save to SQL & check status one last time
            await migrator.MigrateFromSharePointToBlobStorage(discoveredFile!, app);
            await migrator.SaveSucessfulFileMigrationToSql(discoveredFile!);

            needsMigratingPostMigration = await migrator.DoesSharePointFileNeedMigrating(discoveredFile!, containerClient);
            Assert.IsFalse(needsMigratingPostMigration);
        }

        async Task<BaseSharePointFileInfo?> GetFromIndex(ClientContext ctx, string fileTitle, Microsoft.SharePoint.Client.List targetList)
        {
            var crawler = new SiteListsAndLibrariesCrawler(ctx, _tracer);
            var allResults = await crawler.CrawlList(targetList);
            var discoveredFile = allResults.Where(r => r.ServerRelativeFilePath.Contains(fileTitle)).FirstOrDefault();
            return discoveredFile;
        }

        [TestMethod]
        public async Task SharePointFileDownloaderTests()
        {
            var testMsg = new BaseSharePointFileInfo
            {
                SiteUrl = _config!.DevConfig.DefaultSharePointSite,
                WebUrl = _config!.DevConfig.DefaultSharePointSite,
                ServerRelativeFilePath = "/sites/MigrationHost/Shared%20Documents/Blank%20Office%20PPT.pptx"
            };
            var app = await AuthUtils.GetNewClientApp(_config);

            var m = new SharePointFileDownloader(app, _config!, _tracer);
            await m.DownloadFileToTempDir(testMsg);
        }



        [TestMethod]
        public async Task BlobStorageFileUploadTests()
        {
            var testMsg = new BaseSharePointFileInfo
            {
                SiteUrl = _config!.DevConfig.DefaultSharePointSite,
                ServerRelativeFilePath = $"/sites/MigrationHost/Unit tests/textfile{DateTime.Now.Ticks}.txt"
            };

            // Write a fake file 
            string tempFileName = SharePointFileDownloader.GetTempFileNameAndCreateDir(testMsg);
            System.IO.File.WriteAllText(tempFileName, FILE_CONTENTS);

            // Upload - shouldn't exist in destination
            var m = new BlobStorageUploader(_config!, _tracer);
            await m.UploadFileToAzureBlob(tempFileName, testMsg);

            // Write same file again. Should also work.
            await m.UploadFileToAzureBlob(tempFileName, testMsg);
        }

    }
}
