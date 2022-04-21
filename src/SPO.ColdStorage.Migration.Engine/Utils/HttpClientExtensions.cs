using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Migration.Engine.Utils
{
    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> GetAsyncWithThrottleRetries(this HttpClient httpClient, string url, DebugTracer debugTracer)
        {
            // Default to return when full content is read
            return await GetAsyncWithThrottleRetries(httpClient, url, HttpCompletionOption.ResponseContentRead, debugTracer);
        }
        public static async Task<HttpResponseMessage> GetAsyncWithThrottleRetries(this HttpClient httpClient, string url, HttpCompletionOption completionOption, DebugTracer debugTracer)
        {
            if (httpClient is null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException($"'{nameof(url)}' cannot be null or empty.", nameof(url));
            }

            if (debugTracer is null)
            {
                throw new ArgumentNullException(nameof(debugTracer));
            }

            HttpResponseMessage? response = null;
            int retries = 0;
            bool retryDownload = true;
            while (retryDownload)
            {
                // Get response but don't buffer full content (which will buffer overlflow for large files)
                response = await httpClient.GetAsync(url, completionOption);

                if (!response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    // Worth trying any more?
                    if (retries == Constants.MAX_SPO_API_RETRIES)
                    {
                        debugTracer.TrackTrace($"{Constants.THROTTLE_ERROR} downloading response from SPO REST. Maximum retry attempts {Constants.MAX_SPO_API_RETRIES} has been attempted.",
                            Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error);

                        // Allow normal HTTP exception & abort download
                        response.EnsureSuccessStatusCode();
                    }

                    // We've not reached throttling max retries...keep retrying
                    retries++;
                    debugTracer.TrackTrace($"{Constants.THROTTLE_ERROR} downloading from SPO REST. Waiting {retries} seconds to try again...",
                        Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning);
                    await Task.Delay(1000 * retries);
                }

                // Sucess
                retryDownload = false;
            }

            return response!;
        }
    }
}
