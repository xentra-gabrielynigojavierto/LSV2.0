namespace LegalSynq.AuditClient;

/// <summary>
/// Deterministic idempotency key builder. Keys are colon-joined URL-safe segments,
/// truncated to 280 chars (well within the 300-char server limit).
/// </summary>
public static class IdempotencyKey
{
    private const int MaxLength = 280;

    public static string For(params string[] segments)
    {
        var key = string.Join(":", segments
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => Uri.EscapeDataString(s.Trim().ToLowerInvariant())));

        return key.Length > MaxLength ? key[..MaxLength] : key;
    }

    public static string ForWithTimestamp(DateTimeOffset occurredAt, params string[] segments)
    {
        var ts = occurredAt.UtcDateTime.ToString("yyyyMMddTHHmmssZ");
        return For([.. segments, ts]);
    }
}
