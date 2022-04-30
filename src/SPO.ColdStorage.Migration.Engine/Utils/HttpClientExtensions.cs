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

            var response = await ExecuteHttpCallWithThrottleRetries(async () => await httpClient.GetAsync(url, completionOption), url, debugTracer);


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

            var response = await ExecuteHttpCallWithThrottleRetries(async () => await httpClient.PostAsync(url, httpContent), url, debugTracer);

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

            var response = await ExecuteHttpCallWithThrottleRetries(async () => await httpClient.PostAsync(url, body), url, debugTracer);

            return response;
        }

        public static async Task<HttpResponseMessage> ExecuteHttpCallWithThrottleRetries(Func<Task<HttpResponseMessage>> httpAction, string url, DebugTracer debugTracer)
        {
            HttpResponseMessage? response = null;
            int retries = 0, secondsToWait = 0;
            bool retryDownload = true;
            while (retryDownload)
            {
                // Get response but don't buffer full content (which will buffer overlflow for large files)
                response = await httpAction();

                if (!response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    retries++;

                    // Do we have a "retry-after" header?
                    var waitValue = response.GetRetryAfterHeaderSeconds();
                    if (waitValue.HasValue)
                    {
                        secondsToWait = waitValue.Value;
                        debugTracer.TrackTrace($"{Constants.THROTTLE_ERROR} for {url}. Waiting {secondsToWait} seconds to for retry #{retries} (from 'retry-after' header)...",
                            Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning);
                    }
                    else
                    {
                        // No retry value given so we have to guess. Loop with ever-increasing wait.
                        if (retries == Constants.MAX_SPO_API_RETRIES)
                        {
                            // Don't try forever
                            debugTracer.TrackTrace($"{Constants.THROTTLE_ERROR}. Maximum retry attempts {Constants.MAX_SPO_API_RETRIES} has been attempted for {url}.",
                                Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error);

                            // Allow normal HTTP exception & abort download
                            response.EnsureSuccessStatusCode();
                        }

                        // We've not reached throttling max retries...keep retrying
                        debugTracer.TrackTrace($"{Constants.THROTTLE_ERROR} downloading from REST. Waiting {retries} seconds to try again...",
                            Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning);

                        secondsToWait = retries;
                    }

                    // Wait before trying again
                    await Task.Delay(1000 * secondsToWait);
                }
                else
                {
                    // Not HTTP 429. Don't bother retrying & let caller handle any error
                    retryDownload = false;
                }

            }

            return response!;
        }


        public static int? GetRetryAfterHeaderSeconds(this HttpResponseMessage response)
        {
            int responseWaitVal = 0;
            response.Headers.TryGetValues("Retry-After", out var r);

            if (r != null)
            foreach (var retryAfterHeaderVal in r)
            {
                if (int.TryParse(retryAfterHeaderVal, out responseWaitVal))
                {
                    return responseWaitVal;
                }
            }

            return null;
        }
    }
}
