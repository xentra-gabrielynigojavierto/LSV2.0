using System.Text.Json;

namespace Notifications.Infrastructure.Webhooks.Normalizers;

public class SendGridEventItem
{
    public string Event { get; set; } = string.Empty;
    public string? SgMessageId { get; set; }
    public long? Timestamp { get; set; }
    public string? Email { get; set; }
    public string? Reason { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? Url { get; set; }
}

public class NormalizedSendGridEvent
{
    public string RawEventType { get; set; } = string.Empty;
    public string NormalizedEventType { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public DateTime EventTimestamp { get; set; }
    public string? RecipientEmail { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public static class SendGridNormalizer
{
    private static readonly Dictionary<string, string> EventMap = new()
    {
        ["processed"] = "accepted",
        ["deferred"] = "deferred",
        ["delivered"] = "delivered",
        ["bounce"] = "bounced",
        ["blocked"] = "rejected",
        ["dropped"] = "rejected",
        ["open"] = "opened",
        ["click"] = "clicked",
        ["spamreport"] = "complained",
        ["unsubscribe"] = "unsubscribed",
        ["group_unsubscribe"] = "unsubscribed",
        ["group_resubscribe"] = "accepted",
        ["machine_opened"] = "opened"
    };

    public static NormalizedSendGridEvent Normalize(SendGridEventItem raw)
    {
        var rawEventType = raw.Event ?? "unknown";
        var normalizedEventType = EventMap.GetValueOrDefault(rawEventType, "failed");
        var ts = raw.Timestamp.HasValue
            ? DateTimeOffset.FromUnixTimeSeconds(raw.Timestamp.Value).UtcDateTime
            : DateTime.UtcNow;

        string? messageId = null;
        if (!string.IsNullOrEmpty(raw.SgMessageId))
        {
            var dotIndex = raw.SgMessageId.IndexOf('.');
            messageId = dotIndex > 0 ? raw.SgMessageId[..dotIndex] : raw.SgMessageId;
        }

        return new NormalizedSendGridEvent
        {
            RawEventType = rawEventType,
            NormalizedEventType = normalizedEventType,
            ProviderMessageId = messageId,
            EventTimestamp = ts,
            RecipientEmail = raw.Email,
            Metadata = new Dictionary<string, object>()
        };
    }

    public static List<SendGridEventItem> ParseEvents(string rawBody)
    {
        try
        {
            var doc = JsonDocument.Parse(rawBody);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return doc.RootElement.EnumerateArray().Select(ParseSingleEvent).ToList();
            }
            return new List<SendGridEventItem> { ParseSingleEvent(doc.RootElement) };
        }
        catch
        {
            return new List<SendGridEventItem>();
        }
    }

    private static SendGridEventItem ParseSingleEvent(JsonElement element)
    {
        return new SendGridEventItem
        {
            Event = element.TryGetProperty("event", out var e) ? e.GetString() ?? "" : "",
            SgMessageId = element.TryGetProperty("sg_message_id", out var m) ? m.GetString() : null,
            Timestamp = element.TryGetProperty("timestamp", out var t) && t.TryGetInt64(out var ts) ? ts : null,
            Email = element.TryGetProperty("email", out var em) ? em.GetString() : null,
            Reason = element.TryGetProperty("reason", out var r) ? r.GetString() : null,
        };
    }
}
