namespace Notifications.Application.Interfaces;

public class EmailSendPayload
{
    public string To { get; set; } = string.Empty;
    public string? From { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Html { get; set; }
    public string? ReplyTo { get; set; }
}

public class EmailSendResult
{
    public bool Success { get; set; }
    public string? ProviderMessageId { get; set; }
    public ProviderFailure? Failure { get; set; }
}

public class SmsSendPayload
{
    public string To { get; set; } = string.Empty;
    public string? From { get; set; }
    public string Body { get; set; } = string.Empty;
}

public class SmsSendResult
{
    public bool Success { get; set; }
    public string? ProviderMessageId { get; set; }
    public ProviderFailure? Failure { get; set; }
}

public class ProviderFailure
{
    public string Category { get; set; } = string.Empty;
    public string? ProviderCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Retryable { get; set; }
}

public class ProviderHealthResult
{
    public string Status { get; set; } = "down";
    public int? LatencyMs { get; set; }
}

public interface IEmailProviderAdapter
{
    string ProviderType { get; }
    Task<bool> ValidateConfigAsync();
    Task<EmailSendResult> SendAsync(EmailSendPayload payload);
    Task<ProviderHealthResult> HealthCheckAsync();
}

public interface ISmsProviderAdapter
{
    string ProviderType { get; }
    Task<bool> ValidateConfigAsync();
    Task<SmsSendResult> SendAsync(SmsSendPayload payload);
    Task<ProviderHealthResult> HealthCheckAsync();
}

// NOTE: ISmsProviderStatusLookup is defined in ISmsReconciliationService.cs
// alongside SmsMessageStatusResult and SmsReconciliationResult.
// TwilioAdapter implements both ISmsProviderAdapter and ISmsProviderStatusLookup.
