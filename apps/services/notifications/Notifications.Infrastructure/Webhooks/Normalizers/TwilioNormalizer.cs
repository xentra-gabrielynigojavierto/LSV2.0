namespace Notifications.Infrastructure.Webhooks.Normalizers;

public class NormalizedTwilioEvent
{
    public string RawEventType { get; set; } = string.Empty;
    public string NormalizedEventType { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public DateTime EventTimestamp { get; set; }
    public string? RecipientPhone { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public static class TwilioNormalizer
{
    private static readonly Dictionary<string, string> StatusMap = new()
    {
        ["queued"] = "queued",
        ["accepted"] = "accepted",
        ["sending"] = "queued",
        ["sent"] = "sent",
        ["receiving"] = "queued",
        ["received"] = "delivered",
        ["delivered"] = "delivered",
        ["undelivered"] = "undeliverable",
        ["failed"] = "failed",
        ["canceled"] = "failed",
        ["scheduled"] = "queued",
        ["read"] = "opened"
    };

    public static NormalizedTwilioEvent Normalize(Dictionary<string, string> formParams)
    {
        var rawStatus = (formParams.GetValueOrDefault("MessageStatus") ?? formParams.GetValueOrDefault("SmsStatus") ?? "unknown").ToLowerInvariant();
        var normalizedEventType = StatusMap.GetValueOrDefault(rawStatus, "failed");
        var messageSid = formParams.GetValueOrDefault("MessageSid") ?? formParams.GetValueOrDefault("SmsSid");

        return new NormalizedTwilioEvent
        {
            RawEventType = rawStatus,
            NormalizedEventType = normalizedEventType,
            ProviderMessageId = messageSid,
            EventTimestamp = DateTime.UtcNow,
            RecipientPhone = formParams.GetValueOrDefault("To"),
            ErrorCode = formParams.GetValueOrDefault("ErrorCode"),
            ErrorMessage = formParams.GetValueOrDefault("ErrorMessage"),
            Metadata = new Dictionary<string, object>()
        };
    }
}
