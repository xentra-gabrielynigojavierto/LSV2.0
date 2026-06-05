using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Providers.Adapters;

public class TwilioAdapter : ISmsProviderAdapter, ISmsProviderStatusLookup
{
    public string ProviderType => "twilio";

    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _defaultFromNumber;
    private readonly HttpClient _http;
    private readonly ILogger<TwilioAdapter> _logger;

    public TwilioAdapter(string accountSid, string authToken, string defaultFromNumber, HttpClient http, ILogger<TwilioAdapter> logger)
    {
        _accountSid = accountSid;
        _authToken = authToken;
        _defaultFromNumber = defaultFromNumber;
        _http = http;
        _logger = logger;
    }

    public Task<bool> ValidateConfigAsync()
        => Task.FromResult(!string.IsNullOrEmpty(_accountSid) && !string.IsNullOrEmpty(_authToken) && !string.IsNullOrEmpty(_defaultFromNumber));

    public async Task<SmsSendResult> SendAsync(SmsSendPayload payload)
    {
        if (!await ValidateConfigAsync())
            return new SmsSendResult { Success = false, Failure = new ProviderFailure { Category = "auth_config_failure", Message = "Twilio is not configured", Retryable = false } };

        var url = $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/Messages.json";
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("To", payload.To),
            new KeyValuePair<string, string>("From", payload.From ?? _defaultFromNumber),
            new KeyValuePair<string, string>("Body", payload.Body)
        });

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = formContent };
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_accountSid}:{_authToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _http.SendAsync(request, cts.Token);
            var statusCode = (int)response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            if (statusCode == 201)
            {
                string? sid = null;
                try { var parsed = JsonSerializer.Deserialize<JsonElement>(responseBody); sid = parsed.TryGetProperty("sid", out var s) ? s.GetString() : null; } catch { }
                _logger.LogInformation("Twilio: SMS sent successfully to {To}, SID={Sid}", payload.To, sid);
                return new SmsSendResult { Success = true, ProviderMessageId = sid };
            }

            var category = ClassifyError(statusCode, responseBody);
            _logger.LogWarning("Twilio: send failed {StatusCode} {Category}", statusCode, category);
            return new SmsSendResult { Success = false, Failure = new ProviderFailure { Category = category, ProviderCode = statusCode.ToString(), Message = responseBody[..Math.Min(responseBody.Length, 500)], Retryable = category is "retryable_provider_failure" or "provider_unavailable" } };
        }
        catch (Exception ex)
        {
            var isTimeout = ex is TaskCanceledException or OperationCanceledException;
            _logger.LogError(ex, "Twilio: network error during send");
            return new SmsSendResult { Success = false, Failure = new ProviderFailure { Category = isTimeout ? "provider_unavailable" : "retryable_provider_failure", Message = ex.Message, Retryable = true } };
        }
    }

    public async Task<ProviderHealthResult> HealthCheckAsync()
    {
        if (string.IsNullOrEmpty(_accountSid) || string.IsNullOrEmpty(_authToken)) return new ProviderHealthResult { Status = "down" };
        var start = DateTime.UtcNow;
        try
        {
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}.json";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_accountSid}:{_authToken}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var response = await _http.SendAsync(request, cts.Token);
            var latencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds;
            var status = (int)response.StatusCode switch { 200 => "healthy", 401 or 403 => "down", >= 500 => "degraded", _ => "healthy" };
            return new ProviderHealthResult { Status = status, LatencyMs = latencyMs };
        }
        catch { return new ProviderHealthResult { Status = "down", LatencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds }; }
    }

    // ── ISmsProviderStatusLookup ──────────────────────────────────────────────

    /// <summary>
    /// Query Twilio for the current delivery status of an outbound message by MessageSid.
    /// URL: GET https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages/{messageSid}.json
    /// Never throws — returns failure result on any error.
    /// Credentials are not logged.
    /// </summary>
    public async Task<SmsMessageStatusResult> GetMessageStatusAsync(
        string providerMessageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerMessageId))
            return SmsMessageStatusResult.Failure("twilio", providerMessageId ?? "", "missing_provider_message_id", "No MessageSid provided", false);

        if (string.IsNullOrEmpty(_accountSid) || string.IsNullOrEmpty(_authToken))
            return SmsMessageStatusResult.Failure("twilio", providerMessageId, "auth_config_failure", "Twilio credentials not configured", false);

        var url = $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/Messages/{providerMessageId}.json";

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_accountSid}:{_authToken}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

            var response  = await _http.SendAsync(request, cts.Token);
            var statusCode = (int)response.StatusCode;
            var body       = await response.Content.ReadAsStringAsync(cts.Token);

            if (statusCode == 200)
            {
                return ParseTwilioMessageResponse(providerMessageId, body);
            }

            if (statusCode is 401 or 403)
                return SmsMessageStatusResult.Failure("twilio", providerMessageId, "auth_config_failure", "Twilio auth rejected", false);

            if (statusCode == 404)
                return SmsMessageStatusResult.Failure("twilio", providerMessageId, "message_not_found", "MessageSid not found", false);

            if (statusCode == 429)
                return SmsMessageStatusResult.Failure("twilio", providerMessageId, "provider_rate_limited", "Twilio rate limit exceeded", true);

            if (statusCode >= 500)
                return SmsMessageStatusResult.Failure("twilio", providerMessageId, "provider_unavailable", $"Twilio server error {statusCode}", true);

            return SmsMessageStatusResult.Failure("twilio", providerMessageId, "unexpected_provider_error", $"HTTP {statusCode}", true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Twilio: GetMessageStatus timed out for SID={Sid}", providerMessageId);
            return SmsMessageStatusResult.Failure("twilio", providerMessageId, "provider_unavailable", "Request timed out", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio: GetMessageStatus network error for SID={Sid}", providerMessageId);
            return SmsMessageStatusResult.Failure("twilio", providerMessageId, "provider_unavailable", ex.Message, true);
        }
    }

    private static SmsMessageStatusResult ParseTwilioMessageResponse(string sid, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var rawStatus    = root.TryGetProperty("status",        out var s)  ? s.GetString()  : null;
            var errorCode    = root.TryGetProperty("error_code",    out var ec) ? ec.GetString() : null;
            var errorMessage = root.TryGetProperty("error_message", out var em) ? em.GetString() : null;

            DateTimeOffset? sentAt    = null;
            DateTimeOffset? updatedAt = null;

            if (root.TryGetProperty("date_sent", out var ds) && ds.ValueKind != JsonValueKind.Null)
            {
                if (DateTimeOffset.TryParse(ds.GetString(), out var tmp1))
                    sentAt = tmp1;
            }
            if (root.TryGetProperty("date_updated", out var du) && du.ValueKind != JsonValueKind.Null)
            {
                if (DateTimeOffset.TryParse(du.GetString(), out var tmp2))
                    updatedAt = tmp2;
            }

            var normalized = NormalizeTwilioStatus(rawStatus);

            return new SmsMessageStatusResult
            {
                Success           = true,
                Provider          = "twilio",
                ProviderMessageId = sid,
                ProviderStatus    = rawStatus,
                NormalizedStatus  = normalized,
                ErrorCode         = errorCode,
                ErrorMessage      = errorMessage,
                SentAt            = sentAt,
                UpdatedAt         = updatedAt,
                Retryable         = false,
            };
        }
        catch
        {
            return SmsMessageStatusResult.Failure("twilio", sid, "parse_error", "Failed to parse Twilio response", true);
        }
    }

    /// <summary>
    /// Maps Twilio message `status` field to internal normalized status.
    /// Twilio statuses: queued | accepted | scheduled | sending | sent | delivered | undelivered | failed | canceled
    /// </summary>
    private static string? NormalizeTwilioStatus(string? twilioStatus) => twilioStatus?.ToLowerInvariant() switch
    {
        "queued" or "accepted" or "scheduled" => "queued",
        "sending"                              => "processing",
        "sent"                                 => "sent",
        "delivered"                            => "delivered",
        "undelivered" or "failed" or "canceled" => "failed",
        _                                      => null,
    };

    private static string ClassifyError(int statusCode, string body)
    {
        if (statusCode is 401 or 403) return "auth_config_failure";
        if (statusCode == 400)
        {
            var lower = body.ToLowerInvariant();
            if (lower.Contains("21211") || lower.Contains("21614") || lower.Contains("invalid")) return "invalid_recipient";
            return "non_retryable_failure";
        }
        if (statusCode is 429 or >= 500) return "retryable_provider_failure";
        return "non_retryable_failure";
    }
}
