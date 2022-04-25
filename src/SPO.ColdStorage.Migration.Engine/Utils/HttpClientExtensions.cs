using System.Net.Http.Headers;
using System.Text.Json;

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

            var response = await ExecuteHttpCallWithThrottleRetries(async () => await httpClient.GetAsync(url, completionOption), debugTracer);


            return response!;
        }

        public static async Task<HttpResponseMessage> PostAsyncWithThrottleRetries(this HttpClient httpClient, string url, object body, DebugTracer debugTracer)
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

            var payload = JsonSerializer.Serialize(body);
            var httpContent = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            var response = await ExecuteHttpCallWithThrottleRetries(async () => await httpClient.PostAsync(url, httpContent), debugTracer);

            return response;
        }


        public static async Task<HttpResponseMessage> PostAsyncWithThrottleRetries(this HttpClient httpClient, string url, string bodyContent, string mimeType, string boundary, DebugTracer debugTracer)
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

            var body = new StringContent(bodyContent);
            var header = new MediaTypeHeaderValue(mimeType);
            header.Parameters.Add(new NameValueHeaderValue("boundary", boundary));
            body.Headers.ContentType = header;

            var response = await ExecuteHttpCallWithThrottleRetries(async () => await httpClient.PostAsync(url, body), debugTracer);

            return response;
        }

        public static async Task<HttpResponseMessage> ExecuteHttpCallWithThrottleRetries(Func<Task<HttpResponseMessage>> httpAction, DebugTracer debugTracer)
        {
            HttpResponseMessage? response = null;
            int retries = 0;
            bool retryDownload = true;
            while (retryDownload)
            {
                // Get response but don't buffer full content (which will buffer overlflow for large files)
                response = await httpAction();

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
