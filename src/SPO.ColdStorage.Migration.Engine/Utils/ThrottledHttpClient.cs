using Microsoft.Identity.Client;
using SPO.ColdStorage.Entities.Configuration;
using System.Net.Http.Headers;

namespace SPO.ColdStorage.Migration.Engine.Utils
{
    /// <summary>
    /// HttpClient that can handle HTTP 429s automatically
    /// </summary>
    public class SecureSPThrottledHttpClient : HttpClient
    {
        #region Constructor, Props, and Privates

        private readonly bool ignoreRetryHeader;
        private readonly DebugTracer debugTracer;
        private DateTime? _nextCallEarliestTime = null;
        private int _concurrentCalls = 0, _throttledCalls = 0, _completedCalls = 0;
        private object _concurrentCallsObj = new object(), _throttledCallsObject = new object(), _completedCallsObject = new object();


        public SecureSPThrottledHttpClient(Config config, bool ignoreRetryHeader, DebugTracer debugTracer) : base(new SecureSPHandler(config, debugTracer))
        { 
            this.Timeout = TimeSpan.FromHours(1);
            this.ignoreRetryHeader = ignoreRetryHeader;
            this.debugTracer = debugTracer;
        }

        public int ConcurrentCalls
        {
            get
            {
                lock (_concurrentCallsObj)
                {
                    return _concurrentCalls;
                }
            }
        }
        public int ThrottledCalls
        {
            get
            {
                lock (_throttledCallsObject)
                {
                    return _throttledCalls;
                }
            }
        }

        public int CompletedCalls
        {
            get
            {
                lock (_completedCallsObject)
                {
                    return _completedCalls;
                }
            }
        }

        #endregion

        /// <summary>
        /// Execute a method that returns a HttpResponseMessage, with throttling retry logic
        /// </summary>
        public async Task<HttpResponseMessage> ExecuteHttpCallWithThrottleRetries(Func<Task<HttpResponseMessage>> httpAction, string url)
        {
            HttpResponseMessage? response = null;
            int retries = 0, secondsToWait = 0;
            bool retryDownload = true;
            while (retryDownload)
            {
                lock (_concurrentCallsObj)
                {
                    _concurrentCalls++;
                }

                // Figure out if we need to wait. Sleep thread outside lock
                TimeSpan? sleepTimeNeeded = null;
                lock (this)
                {
                    if (_nextCallEarliestTime != null && _nextCallEarliestTime > DateTime.Now)
                    {
                        sleepTimeNeeded = _nextCallEarliestTime.Value.Subtract(DateTime.Now);
                    }
                }
                if (sleepTimeNeeded.HasValue)
                {
                    lock (this)
                    {
                        _throttledCalls++;
                    }
                    Thread.Sleep(sleepTimeNeeded.Value);
                    lock (this)
                    {
                        _nextCallEarliestTime = null;
                    }
                }

                // Get response but don't buffer full content (which will buffer overlflow for large files)
                response = await httpAction();

                lock (_concurrentCallsObj)
                {
                    _concurrentCalls--;
                }

                if (!response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    retries++;
                    lock (this)
                    {
                        _throttledCalls++;
                    }

                    // Do we have a "retry-after" header & should we use it?
                    var waitValue = response.GetRetryAfterHeaderSeconds();
                    if (!ignoreRetryHeader && waitValue.HasValue)
                    {
                        secondsToWait = waitValue.Value;
                        debugTracer.TrackTrace($"{Constants.THROTTLE_ERROR} for {url}. Waiting to retry for attempt #{retries} (from 'retry-after' header)...",
                            Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information);
                    }
                    else
                    {
                        // We have to guess how much to back-off. Loop with ever-increasing wait.
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
                            Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);

                        secondsToWait = retries * 2;
                    }

                    // Wait before trying again
                    lock (this)
                    {
                        _nextCallEarliestTime = DateTime.Now.AddSeconds(secondsToWait);
                    }

                }
                else
                {
                    // Not HTTP 429. Don't bother retrying & let caller handle any error
                    retryDownload = false;

                    lock (_completedCallsObject)
                    {
                        _completedCalls++;
                    }
                }
            }

            return response!;
        }

    }

    public class SecureSPHandler : DelegatingHandler
    {
        protected Config _config;
        private AuthenticationResult? auth = null;
        public SecureSPHandler(Config config, DebugTracer debugTracer)
        {
            this._config = config;
            InnerHandler = new HttpClientHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {

            // Get auth for REST
            var app = await AuthUtils.GetNewClientApp(_config);

            if (auth == null || auth.ExpiresOn < DateTimeOffset.Now.AddMinutes(5))
            {
                auth = await app.AuthForSharePointOnline(_config.BaseServerAddress);
            }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

            return await base.SendAsync(request, cancellationToken);
        }

    }
}
