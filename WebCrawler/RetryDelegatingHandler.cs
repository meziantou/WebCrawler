using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebCrawler
{
    internal class RetryDelegatingHandler : DelegatingHandler
    {
        public RetryDelegatingHandler()
        {
        }

        public RetryDelegatingHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        public int MaxAttemptCount { get; set; } = 3;

        private bool MustContinue(int attempt)
        {
            return attempt < MaxAttemptCount;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var attempt = 0;
            while (true)
            {
                attempt++;

                try
                {
                    // base.SendAsync calls the inner handler
                    var response = await base.SendAsync(request, cancellationToken);
                    if (!MustContinue(attempt))
                        return response;

                    if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        await Task.Delay(5000, cancellationToken);
                        continue;
                    }

                    if (response.StatusCode == (HttpStatusCode)429)
                    {
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    return response;
                }
                catch (Exception ex) when (IsNetworkError(ex))
                {
                    if (!MustContinue(attempt))
                        throw;

                    await Task.Delay(2000, cancellationToken);
                    continue;
                }
            }
        }

        private static bool IsNetworkError(Exception ex)
        {
            // Check if it's a network error
            if (ex is SocketException)
                return true;
            if (ex.InnerException != null)
                return IsNetworkError(ex.InnerException);
            return false;
        }
    }
}
