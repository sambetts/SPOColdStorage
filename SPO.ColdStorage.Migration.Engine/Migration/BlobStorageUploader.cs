﻿using Azure.Storage.Blobs;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Migration.Engine.Model;

namespace SPO.ColdStorage.Migration.Engine.Migration
{
    /// <summary>
    /// Uploads files from local file-system to Azure blob
    /// </summary>
    public class BlobStorageUploader : BaseComponent
    {
        private BlobServiceClient _blobServiceClient;
        private BlobContainerClient? _containerClient;
        public BlobStorageUploader(Config config, DebugTracer debugTracer) : base(config, debugTracer)
        {
            // Create a BlobServiceClient object which will be used to create a container client
            _blobServiceClient = new BlobServiceClient(_config.StorageConnectionString);
        }


        public async Task UploadFileToAzureBlob(string localTempFileName, SharePointFileLocationInfo msg)
        {
            // Create the container and return a container client object
            if (_containerClient == null)
            {
                this._containerClient = _blobServiceClient.GetBlobContainerClient(_config.BlobContainerName);
            }

            _tracer.TrackTrace($"Uploading '{msg.FileRelativePath}' to blob storage...", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
            using (var fs = File.OpenRead(localTempFileName))
            {
                var fileRef = _containerClient.GetBlobClient(msg.FileRelativePath);
                var fileExists = await fileRef.ExistsAsync();
                if (fileExists)
                {
                    // MD5 has the local file
                    byte[] hash;
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        using (var stream = File.OpenRead(localTempFileName))
                        {
                            hash = md5.ComputeHash(stream);
                        }
                    }

                    // Get az blob MD5 & compare
                    var existingProps = await fileRef.GetPropertiesAsync();
                    var match = existingProps.Value.ContentHash.SequenceEqual(hash);
                    if (!match)
                        await fileRef.UploadAsync(fs, true);
                    else
                        _tracer.TrackTrace($"Skipping '{msg.FileRelativePath}' as destination hash is identical to local file.", 
                            Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
                }
                else
                    await _containerClient.UploadBlobAsync(msg.FileRelativePath, fs);
            }
        }
    }
}
