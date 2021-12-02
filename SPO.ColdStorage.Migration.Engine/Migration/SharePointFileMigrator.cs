﻿using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.SharePoint.Client;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Entities.DBEntities;
using SPO.ColdStorage.Migration.Engine.Model;

namespace SPO.ColdStorage.Migration.Engine.Migration
{
    /// <summary>
    /// The top-level file migration logic
    /// </summary>
    public class SharePointFileMigrator : BaseComponent, IDisposable
    {
        private ServiceBusClient _sbClient;
        private ServiceBusSender _sbSender;
        private SPOColdStorageDbContext _db;
        public SharePointFileMigrator(Config config, DebugTracer debugTracer) : base(config, debugTracer)
        {
            _sbClient = new ServiceBusClient(_config.ServiceBusConnectionString);
            _sbSender = _sbClient.CreateSender(_config.ServiceBusQueueName);
            _db = new SPOColdStorageDbContext(_config.SQLConnectionString);
        }

        public async Task QueueSharePointFileMigrationIfNeeded(SharePointFileUpdateInfo sharePointFileInfo, BlobContainerClient containerClient)
        {
            bool needsMigrating = await SharePointFileNeedsMigrating(sharePointFileInfo, containerClient);
            if (needsMigrating)
            {
                // Send msg to migrate file
                var sbMsg = new ServiceBusMessage(System.Text.Json.JsonSerializer.Serialize(sharePointFileInfo));
                await _sbSender.SendMessageAsync(sbMsg);
                _tracer.TrackTrace($"+'{sharePointFileInfo.FullUrl}'...");
            }
        }

        public async Task<bool> SharePointFileNeedsMigrating(SharePointFileUpdateInfo sharePointFileInfo, BlobContainerClient containerClient)
        {
            // Check if blob exists in account
            var fileRef = containerClient.GetBlobClient(sharePointFileInfo.FileRelativePath);
            var fileExistsInAzureBlob = await fileRef.ExistsAsync();

            // Verify version migrated in SQL
            bool logExistsAndIsForSameVersion = false;
            var migratedFile = await _db.Files.Where(f => f.FileName.ToLower() == sharePointFileInfo.FullUrl.ToLower()).FirstOrDefaultAsync();
            if (migratedFile != null)
            {
                var log = await _db.SuccesfulMigrations.Where(l => l.File == migratedFile).SingleOrDefaultAsync();
                if (log != null)
                {
                    logExistsAndIsForSameVersion = log.LastModified == sharePointFileInfo.LastModified;
                }
            }
            bool haveRightFile = logExistsAndIsForSameVersion && fileExistsInAzureBlob;

            return !haveRightFile;
        }

        /// <summary>
        /// Download from SP and upload to blob-storage
        /// </summary>
        public async Task<long> MigrateFromSharePointToBlobStorage(SharePointFileUpdateInfo fileToMigrate, ClientContext ctx)
        {
            // Download from SP to local
            var downloader = new SharePointFileDownloader(ctx, _config, _tracer);
            var tempFileNameAndSize = await downloader.DownloadFileToTempDir(fileToMigrate);

            // Index file properties - EDIT: ignore. Search indexing to be done directly on the blobs
            //var searchIndexer = new SharePointFileSearchProcessor(_config, _tracer);
            //await searchIndexer.ProcessFileContent(msg);

            // Upload local file to az blob
            var blobUploader = new BlobStorageUploader(_config, _tracer);
            await blobUploader.UploadFileToAzureBlob(tempFileNameAndSize.Item1, fileToMigrate);

            // Log a success in SQL (update/create)
            var migratedFile = await _db.Files.Where(f=> f.FileName.ToLower() == fileToMigrate.FullUrl.ToLower()).FirstOrDefaultAsync();
            if (migratedFile == null)
            {
                migratedFile = new SharePointFile 
                {
                    FileName = fileToMigrate.FullUrl.ToLower()
                };
                _db.Files.Append(migratedFile);
            }
            var log = await _db.SuccesfulMigrations.Where(l=> l.File == migratedFile).SingleOrDefaultAsync();
            if (log == null)
            {
                log = new SuccesfulMigrationLog { File = migratedFile };
                _db.SuccesfulMigrations.Add(log);
            }
            log.Migrated = DateTime.Now;
            log.LastModified = fileToMigrate.LastModified;
            await _db.SaveChangesAsync();

            // Clean-up temp file
            try
            {
                System.IO.File.Delete(tempFileNameAndSize.Item1);
            }
            catch (IOException ex)
            {
                _tracer.TrackTrace($"Got errror {ex.Message} cleaning temp file '{tempFileNameAndSize.Item1}'. Ignoring.", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning);
            }

            // Return file-size
            return tempFileNameAndSize.Item2;
        }

        public void Dispose()
        {
            _db.Dispose(); 
        }
    }
}
