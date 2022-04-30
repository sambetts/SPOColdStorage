using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Migration.Engine.Utils
{

    public class ThrottledHttpClient : HttpClient
    {
        private DateTime? _nextCall = null;

        public async Task<HttpResponseMessage> ExecuteHttpCallWithThrottleRetries(Func<Task<HttpResponseMessage>> httpAction, string url, DebugTracer debugTracer)
        {
            HttpResponseMessage? response = null;
            int retries = 0, secondsToWait = 0;
            bool retryDownload = true;
            while (retryDownload)
            {
                lock (this)
                {
                    if (_nextCall != null && _nextCall > DateTime.Now)
                    {
                        var tsToWait = _nextCall.Value.Subtract(DateTime.Now);
                        Thread.Sleep(tsToWait.Milliseconds);
                        _nextCall = null;
                    }
                }

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
                        debugTracer.TrackTrace($"{Constants.THROTTLE_ERROR} for {url}. Waiting to retry for attempt #{retries} (from 'retry-after' header)...",
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
                    lock (this)
                    {
                        _nextCall = DateTime.Now.AddSeconds(secondsToWait);
                    }

                }
                else
                {
                    // Not HTTP 429. Don't bother retrying & let caller handle any error
                    retryDownload = false;
                }

            }

            return response!;
        }


    }
}
