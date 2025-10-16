using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeoTrack.Models;

namespace GeoTrack;

public static class HttpClientFactory
{
    public static HttpClient Create(ExternalAppHttpConfigDto httpConfig)
    {
        var timeoutSeconds = httpConfig.TimeoutSeconds <= 0 ? 10 : httpConfig.TimeoutSeconds;
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
    }

    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        this HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        ExternalAppHttpConfigDto httpConfig,
        CancellationToken cancellationToken)
    {
        var retries = Math.Max(0, httpConfig.RetryCount);
        var delay = TimeSpan.FromSeconds(httpConfig.RetryDelaySeconds <= 0 ? 2 : httpConfig.RetryDelaySeconds);

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            using var request = requestFactory();
            HttpResponseMessage? response = null;
            try
            {
                response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (attempt == retries || !ShouldRetry(response.StatusCode))
                {
                    return response;
                }
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                response?.Dispose();
                if (attempt == retries)
                {
                    throw new HttpRequestException("Request timed out", ex);
                }
            }
            catch (HttpRequestException) when (attempt < retries)
            {
                response?.Dispose();
            }

            response?.Dispose();

            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        throw new InvalidOperationException("Retry loop exited unexpectedly.");
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        var numeric = (int)statusCode;
        return numeric >= 500;
    }
}
