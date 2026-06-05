using System.Net;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.FlowClient;

/// <summary>
/// LS-FLOW-MERGE-P4 — bounded retry handler for the typed Flow HttpClient.
/// Retries on transient transport failures and on upstream 408/429/5xx
/// responses (excluding 501 NotImplemented and 505 HttpVersionNotSupported)
/// with exponential backoff + jitter. POST/PUT/PATCH are only retried when
/// the failure is purely transport-level (no response received) so that
/// non-idempotent writes are never replayed against the server.
/// </summary>
internal sealed class FlowRetryHandler : DelegatingHandler
{
    private static readonly Random Jitter = new();
    private readonly ILogger<FlowRetryHandler> _logger;
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;

    public FlowRetryHandler(ILogger<FlowRetryHandler> logger, int maxAttempts = 3, TimeSpan? baseDelay = null)
    {
        _logger = logger;
        _maxAttempts = Math.Max(1, maxAttempts);
        _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(200);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var isIdempotent = request.Method == HttpMethod.Get
            || request.Method == HttpMethod.Head
            || request.Method == HttpMethod.Options
            || request.Method == HttpMethod.Delete;

        for (var attempt = 1; ; attempt++)
        {
            HttpResponseMessage? response = null;
            Exception? transportEx = null;
            try
            {
                response = await base.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null)
            {
                transportEx = ex;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                transportEx = ex;
            }

            var transientResponse = response is not null && IsTransientStatus(response.StatusCode);
            var canRetry = attempt < _maxAttempts &&
                           (transportEx is not null || (transientResponse && isIdempotent));

            if (!canRetry)
            {
                if (transportEx is not null) throw transportEx;
                return response!;
            }

            var delay = ComputeDelay(attempt);
            _logger.LogWarning(
                "FlowClient retry {Attempt}/{Max} {Method} {Uri} after {DelayMs}ms (status={Status}, transport={Transport})",
                attempt, _maxAttempts, request.Method, request.RequestUri,
                (int)delay.TotalMilliseconds,
                response is null ? "-" : ((int)response.StatusCode).ToString(),
                transportEx?.GetType().Name ?? "-");

            response?.Dispose();
            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                throw;
            }
        }
    }

    private TimeSpan ComputeDelay(int attempt)
    {
        var exp = Math.Pow(2, attempt - 1);
        var jitterMs = Jitter.Next(0, 100);
        return TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * exp + jitterMs);
    }

    private static bool IsTransientStatus(HttpStatusCode status) =>
        status == HttpStatusCode.RequestTimeout              // 408
        || status == (HttpStatusCode)429                      // Too Many Requests
        || status == HttpStatusCode.BadGateway                // 502
        || status == HttpStatusCode.ServiceUnavailable        // 503
        || status == HttpStatusCode.GatewayTimeout            // 504
        || status == HttpStatusCode.InternalServerError;      // 500
}
