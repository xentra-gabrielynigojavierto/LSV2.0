using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Providers.Adapters;

public class SendGridAdapter : IEmailProviderAdapter
{
    public string ProviderType => "sendgrid";

    private readonly string _apiKey;
    private readonly string _defaultFromEmail;
    private readonly string _defaultFromName;
    private readonly HttpClient _http;
    private readonly ILogger<SendGridAdapter> _logger;

    public SendGridAdapter(string apiKey, string defaultFromEmail, string defaultFromName, HttpClient http, ILogger<SendGridAdapter> logger)
    {
        _apiKey = apiKey;
        _defaultFromEmail = defaultFromEmail;
        _defaultFromName = defaultFromName;
        _http = http;
        _logger = logger;
    }

    public Task<bool> ValidateConfigAsync()
        => Task.FromResult(!string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_defaultFromEmail));

    public async Task<EmailSendResult> SendAsync(EmailSendPayload payload)
    {
        if (!await ValidateConfigAsync())
            return new EmailSendResult { Success = false, Failure = new ProviderFailure { Category = "auth_config_failure", Message = "SendGrid is not configured", Retryable = false } };

        var body = new
        {
            personalizations = new[] { new { to = new[] { new { email = payload.To } } } },
            from = new { email = payload.From ?? _defaultFromEmail, name = _defaultFromName },
            subject = payload.Subject,
            content = new List<object> { new { type = "text/plain", value = payload.Body } }
                .Concat(payload.Html != null ? new[] { new { type = "text/html", value = payload.Html } } : Array.Empty<object>()).ToArray()
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _http.SendAsync(request, cts.Token);
            var statusCode = (int)response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            if (statusCode == 202)
            {
                // Extract the provider message ID from the X-Message-Id response header.
                // This ID is required to correlate incoming webhook events (delivered,
                // bounced, opened, etc.) back to the correct NotificationAttempt record.
                response.Headers.TryGetValues("X-Message-Id", out var msgIdValues);
                var providerMessageId = msgIdValues?.FirstOrDefault();
                _logger.LogInformation("SendGrid: email sent successfully to {To}, messageId={MessageId}", payload.To, providerMessageId ?? "none");
                return new EmailSendResult { Success = true, ProviderMessageId = providerMessageId };
            }

            var category = ClassifyError(statusCode, responseBody);
            _logger.LogWarning("SendGrid: send failed {StatusCode} {Category}", statusCode, category);
            return new EmailSendResult { Success = false, Failure = new ProviderFailure { Category = category, ProviderCode = statusCode.ToString(), Message = responseBody[..Math.Min(responseBody.Length, 500)], Retryable = category == "retryable_provider_failure" } };
        }
        catch (Exception ex)
        {
            var isTimeout = ex is TaskCanceledException or OperationCanceledException;
            _logger.LogError(ex, "SendGrid: network error during send");
            return new EmailSendResult { Success = false, Failure = new ProviderFailure { Category = isTimeout ? "provider_unavailable" : "retryable_provider_failure", Message = ex.Message, Retryable = true } };
        }
    }

    public async Task<ProviderHealthResult> HealthCheckAsync()
    {
        if (string.IsNullOrEmpty(_apiKey)) return new ProviderHealthResult { Status = "down" };
        var start = DateTime.UtcNow;
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.sendgrid.com/v3/scopes");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var response = await _http.SendAsync(request, cts.Token);
            var latencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds;
            var status = (int)response.StatusCode switch
            {
                200 => "healthy",
                401 or 403 => "down",
                >= 500 => "degraded",
                _ => "healthy"
            };
            return new ProviderHealthResult { Status = status, LatencyMs = latencyMs };
        }
        catch
        {
            return new ProviderHealthResult { Status = "down", LatencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds };
        }
    }

    private static string ClassifyError(int statusCode, string body)
    {
        if (statusCode is 401 or 403) return "auth_config_failure";
        if (statusCode == 400)
        {
            var lower = body.ToLowerInvariant();
            if (lower.Contains("invalid") && (lower.Contains("email") || lower.Contains("recipient"))) return "invalid_recipient";
            return "non_retryable_failure";
        }
        if (statusCode is 413 or 422) return "non_retryable_failure";
        if (statusCode is 429 or >= 500) return "retryable_provider_failure";
        return "non_retryable_failure";
    }
}
