using Microsoft.Identity.Client;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Models;
using System.Net.Http.Headers;

namespace SPO.ColdStorage.Migration.Engine.Migration
{
    /// <summary>
    /// Downloads files from SharePoint to local file-system
    /// </summary>
    public class SharePointFileDownloader : BaseComponent
    {
        const int MAX_RETRIES = 5;
        private readonly IConfidentialClientApplication _app;
        private readonly HttpClient _client;
        public SharePointFileDownloader(IConfidentialClientApplication app, Config config, DebugTracer debugTracer) : base(config, debugTracer)
        {
            _app = app;
            _client = new HttpClient();


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
        public async Task<(string, long)> DownloadFileToTempDir(SharePointFileInfo sharePointFile)
        {
            // Write to temp file
            var tempFileName = GetTempFileNameAndCreateDir(sharePointFile);

            _tracer.TrackTrace($"Downloading '{sharePointFile.FullSharePointUrl}'...", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);

            var auth = await _app.AuthForSharePointOnline(_config.BaseServerAddress);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            var url = $"{sharePointFile.WebUrl}/_api/web/GetFileByServerRelativeUrl('{sharePointFile.ServerRelativeFilePath}')/OpenBinaryStream";

            long fileSize = 0;
            int retries = 0;    
            bool retryDownload = true;
            while (retryDownload)
            {
                // Get response but don't buffer full content (which will buffer overlflow for large files)
                using (var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        if (retries == MAX_RETRIES)
                        {
                            // Allow normal HTTP exception & abort download
                            response.EnsureSuccessStatusCode();
                        }

                        retries++;
                        Console.WriteLine($"Got throttled downloading '{sharePointFile.FullSharePointUrl}'. Waiting {retries} seconds to try again...");
                        await Task.Delay(1000 * retries);
                    }

                    using (var streamToReadFrom = await response.Content.ReadAsStreamAsync())
                    using (var streamToWriteTo = File.Open(tempFileName, FileMode.Create))
                    {
                        await streamToReadFrom.CopyToAsync(streamToWriteTo);
                        fileSize = streamToWriteTo.Length;
                    }

                    // Sucess
                    retryDownload = false;
                }

                _tracer.TrackTrace($"Wrote {fileSize.ToString("N0")} bytes to '{tempFileName}'.", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);
            }
            
            // Return file name & size
            return (tempFileName, fileSize);
        }

        public static string GetTempFileNameAndCreateDir(SharePointFileInfo sharePointFile)
        {
            var tempFileName = Path.GetTempPath() + @"\SpoColdStorageMigration\" + DateTime.Now.Ticks + @"\" + sharePointFile.ServerRelativeFilePath.Replace("/", @"\");
            var tempFileInfo = new FileInfo(tempFileName);
            Directory.CreateDirectory(tempFileInfo.DirectoryName!);

            return tempFileName;
        }
    }
}
