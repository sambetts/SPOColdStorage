using Azure.Storage.Blobs;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Models;

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
            _blobServiceClient = new BlobServiceClient(_config.ConnectionStrings.Storage);
        }


        public async Task UploadFileToAzureBlob(string localTempFileName, SharePointFileInfo msg)
        {
            // Create the container and return a container client object
            if (_containerClient == null)
            {
                this._containerClient = _blobServiceClient.GetBlobContainerClient(_config.BlobContainerName);
            }

            _tracer.TrackTrace($"Uploading '{msg.ServerRelativeFilePath}' to blob storage...", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
            using (var fs = File.OpenRead(localTempFileName))
            {
                var fileRef = _containerClient.GetBlobClient(msg.ServerRelativeFilePath);
                var fileExists = await fileRef.ExistsAsync();
                if (fileExists)
                {
                    // MD5 has the local file
                    byte[] tempFileHash;
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        using (var tempFileStream = File.OpenRead(localTempFileName))
                        {
                            tempFileHash = md5.ComputeHash(tempFileStream);
                        }
                    }

                    // Get az blob MD5 & compare
                    var existingProps = await fileRef.GetPropertiesAsync();

                    // For some reason, sometimes the SP hash is null
                    var match = existingProps.Value.ContentHash != null && existingProps.Value.ContentHash.SequenceEqual(tempFileHash);
                    if (!match)
                        await fileRef.UploadAsync(fs, true);
                    else
                        _tracer.TrackTrace($"Skipping '{msg.ServerRelativeFilePath}' as destination hash is identical to local file.", 
                            Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
                }
                else
                    await _containerClient.UploadBlobAsync(msg.ServerRelativeFilePath, fs);
            }
        }
    }
}
