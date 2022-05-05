using Microsoft.Identity.Client;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Migration.Engine.Utils;
using SPO.ColdStorage.Models;
using System.Net.Http.Headers;

namespace SPO.ColdStorage.Migration.Engine.Migration
{
    /// <summary>
    /// Downloads files from SharePoint to local file-system
    /// </summary>
    public class SharePointFileDownloader : BaseComponent
    {
        private readonly IConfidentialClientApplication _app;
        private readonly SecureSPThrottledHttpClient _client;
        public SharePointFileDownloader(IConfidentialClientApplication app, Config config, DebugTracer debugTracer) : base(config, debugTracer)
        {
            _app = app;
            _client = new SecureSPThrottledHttpClient(config, true, debugTracer);

            var productValue = new ProductInfoHeaderValue("SPOColdStorageMigration", "1.0");
            var commentValue = new ProductInfoHeaderValue("(+https://github.com/sambetts/SPOColdStorage)");

            _client.DefaultRequestHeaders.UserAgent.Add(productValue);
            _client.DefaultRequestHeaders.UserAgent.Add(commentValue);
        }

        /// <summary>
        /// Download file & return temp file-name + size
        /// </summary>
        /// <returns>Temp file-path and size</returns>
        /// <remarks>
        /// Uses manual HTTP calls as CSOM doesn't work with files > 2gb. 
        /// This routine writes 2mb chunks at a time to a temp file from HTTP response.
        /// </remarks>
        public async Task<(string, long)> DownloadFileToTempDir(BaseSharePointFileInfo sharePointFile)
        {
            // Write to temp file
            var tempFileName = GetTempFileNameAndCreateDir(sharePointFile);

            _tracer.TrackTrace($"Downloading '{sharePointFile.FullSharePointUrl}'...", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
            var url = $"{sharePointFile.WebUrl}/_api/web/GetFileByServerRelativeUrl('{sharePointFile.ServerRelativeFilePath}')/OpenBinaryStream";

            long fileSize = 0;

            // Get response but don't buffer full content (which will buffer overlflow for large files)
            using (var response = await _client.GetAsyncWithThrottleRetries(url, HttpCompletionOption.ResponseHeadersRead, _tracer))
            {
                using (var streamToReadFrom = await response.Content.ReadAsStreamAsync())
                using (var streamToWriteTo = File.Open(tempFileName, FileMode.Create))
                {
                    await streamToReadFrom.CopyToAsync(streamToWriteTo);
                    fileSize = streamToWriteTo.Length;
                }
            }

            _tracer.TrackTrace($"Wrote {fileSize.ToString("N0")} bytes to '{tempFileName}'.", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);


            // Return file name & size
            return (tempFileName, fileSize);
        }

        public static string GetTempFileNameAndCreateDir(BaseSharePointFileInfo sharePointFile)
        {
            var tempFileName = Path.GetTempPath() + @"\SpoColdStorageMigration\" + DateTime.Now.Ticks + @"\" + sharePointFile.ServerRelativeFilePath.Replace("/", @"\");
            var tempFileInfo = new FileInfo(tempFileName);
            Directory.CreateDirectory(tempFileInfo.DirectoryName!);

            return tempFileName;
        }
    }
}
