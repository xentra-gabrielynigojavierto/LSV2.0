using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Providers.Adapters;

/// <summary>
/// LS-NOTIF-SMS-014: Vonage SMS provider adapter using the Vonage classic SMS REST API.
///
/// API: POST https://rest.nexmo.com/sms/json (application/x-www-form-urlencoded)
/// Auth: api_key + api_secret as form parameters
/// Response: { "messages": [{ "status": "0", "message-id": "..." }] }
/// Status "0" = success; any other status = failure.
///
/// Capabilities:
///   - SupportsSend = true
///   - SupportsStatusLookup = false (Vonage status is webhook-only in this adapter version)
///   - SupportsHealthCheck = false (no safe zero-cost probe endpoint)
///
/// Credentials: never logged. ProviderType = "vonage".
/// Does NOT implement ISmsProviderStatusLookup — reconciliation auto-skips this provider.
/// </summary>
public class VonageAdapter : ISmsProviderAdapter
{
    public string ProviderType => "vonage";

    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly string _defaultFromNumber;
    private readonly HttpClient _http;
    private readonly ILogger<VonageAdapter> _logger;

    private const string VonageSmsUrl = "https://rest.nexmo.com/sms/json";

    public VonageAdapter(
        string apiKey,
        string apiSecret,
        string defaultFromNumber,
        HttpClient http,
        ILogger<VonageAdapter> logger)
    {
        _apiKey            = apiKey;
        _apiSecret         = apiSecret;
        _defaultFromNumber = defaultFromNumber;
        _http              = http;
        _logger            = logger;
    }

    public Task<bool> ValidateConfigAsync()
        => Task.FromResult(
            !string.IsNullOrEmpty(_apiKey) &&
            !string.IsNullOrEmpty(_apiSecret) &&
            !string.IsNullOrEmpty(_defaultFromNumber));

    public async Task<SmsSendResult> SendAsync(SmsSendPayload payload)
    {
        if (!await ValidateConfigAsync())
            return new SmsSendResult
            {
                Success = false,
                Failure = new ProviderFailure
                {
                    Category   = "auth_config_failure",
                    Message    = "Vonage is not configured (missing api_key, api_secret, or from number)",
                    Retryable  = false,
                },
            };

        var formParams = new List<KeyValuePair<string, string>>
        {
            new("api_key",    _apiKey),
            new("api_secret", _apiSecret),
            new("to",         payload.To),
            new("from",       payload.From ?? _defaultFromNumber),
            new("text",       payload.Body),
        };

        var request = new HttpRequestMessage(HttpMethod.Post, VonageSmsUrl)
        {
            Content = new FormUrlEncodedContent(formParams),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var response = await _http.SendAsync(request, cts.Token);
            var statusCode   = (int)response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            if (statusCode is < 200 or >= 300)
            {
                var httpCategory = ClassifyHttpError(statusCode);
                _logger.LogWarning("Vonage: HTTP {StatusCode} — {Category}", statusCode, httpCategory);
                return new SmsSendResult
                {
                    Success = false,
                    Failure = new ProviderFailure
                    {
                        Category    = httpCategory,
                        ProviderCode = statusCode.ToString(),
                        Message     = responseBody[..Math.Min(responseBody.Length, 300)],
                        Retryable   = httpCategory is "retryable_provider_failure" or "provider_unavailable",
                    },
                };
            }

            // Parse Vonage response body
            return ParseVonageResponse(responseBody);
        }
        catch (Exception ex)
        {
            var isTimeout = ex is TaskCanceledException or OperationCanceledException;
            _logger.LogError(ex, "Vonage: network error during send");
            return new SmsSendResult
            {
                Success = false,
                Failure = new ProviderFailure
                {
                    Category  = isTimeout ? "provider_unavailable" : "retryable_provider_failure",
                    Message   = ex.Message,
                    Retryable = true,
                },
            };
        }
    }

    /// <summary>
    /// Vonage classic REST API has no safe health check endpoint that doesn't incur a cost
    /// or require a special permission scope. Returns "unknown" — health checks are skipped
    /// for this provider in ProviderHealthWorker.
    /// </summary>
    public Task<ProviderHealthResult> HealthCheckAsync()
        => Task.FromResult(new ProviderHealthResult { Status = "unknown" });

    // ── Private helpers ───────────────────────────────────────────────────────

    private SmsSendResult ParseVonageResponse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("messages", out var messagesEl) ||
                messagesEl.ValueKind != JsonValueKind.Array ||
                messagesEl.GetArrayLength() == 0)
            {
                _logger.LogWarning("Vonage: unexpected response shape (no 'messages' array)");
                return new SmsSendResult
                {
                    Success = false,
                    Failure = new ProviderFailure
                    {
                        Category  = "non_retryable_failure",
                        Message   = "Vonage response missing 'messages' array",
                        Retryable = false,
                    },
                };
            }

            var firstMessage = messagesEl[0];
            var status = firstMessage.TryGetProperty("status", out var s) ? s.GetString() : null;

            // Vonage status "0" = delivered to network
            if (status == "0")
            {
                var messageId = firstMessage.TryGetProperty("message-id", out var mid) ? mid.GetString() : null;
                _logger.LogInformation("Vonage: SMS sent successfully, message-id={MsgId}", messageId);
                return new SmsSendResult { Success = true, ProviderMessageId = messageId };
            }

            var errorText = firstMessage.TryGetProperty("error-text", out var et) ? et.GetString() : null;
            var category  = ClassifyVonageStatus(status);
            _logger.LogWarning("Vonage: send failed status={Status} error={Error}", status, errorText);
            return new SmsSendResult
            {
                Success = false,
                Failure = new ProviderFailure
                {
                    Category    = category,
                    ProviderCode = status,
                    Message     = errorText ?? $"Vonage status {status}",
                    Retryable   = category is "retryable_provider_failure" or "provider_unavailable",
                },
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vonage: failed to parse response body");
            return new SmsSendResult
            {
                Success = false,
                Failure = new ProviderFailure
                {
                    Category  = "non_retryable_failure",
                    Message   = "Vonage response parse error: " + ex.Message,
                    Retryable = false,
                },
            };
        }
    }

    /// <summary>
    /// Maps Vonage status codes to internal failure categories.
    /// https://developer.vonage.com/en/api/sms#errors
    /// Status 0 = success (handled above).
    /// Status 1 = throttled (retryable).
    /// Status 2/3 = missing/invalid param (non-retryable).
    /// Status 4/5 = invalid credentials/IP (auth_config_failure).
    /// Status 6/7/8/9/... = various delivery failures.
    /// Status 15 = invalid destination (invalid_recipient).
    /// </summary>
    private static string ClassifyVonageStatus(string? status) => status switch
    {
        "1"  => "retryable_provider_failure",   // throttled
        "2"  => "non_retryable_failure",         // missing params
        "3"  => "invalid_recipient",             // invalid params (often bad number)
        "4"  => "auth_config_failure",           // invalid credentials
        "5"  => "auth_config_failure",           // internal error (treated as auth since config-related)
        "6"  => "non_retryable_failure",         // invalid message
        "7"  => "non_retryable_failure",         // number barred
        "8"  => "non_retryable_failure",         // partner account barred
        "9"  => "retryable_provider_failure",    // partner quota exceeded
        "11" => "provider_unavailable",          // account not enabled for REST
        "12" => "non_retryable_failure",         // message too long
        "13" => "non_retryable_failure",         // communication failed
        "14" => "non_retryable_failure",         // invalid signature
        "15" => "invalid_recipient",             // invalid sender address
        "22" => "invalid_recipient",             // invalid number format
        _    => "non_retryable_failure",
    };

    private static string ClassifyHttpError(int statusCode)
    {
        if (statusCode is 401 or 403) return "auth_config_failure";
        if (statusCode == 429) return "retryable_provider_failure";
        if (statusCode >= 500) return "provider_unavailable";
        return "non_retryable_failure";
    }
}
