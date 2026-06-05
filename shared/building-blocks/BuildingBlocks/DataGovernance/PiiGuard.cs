namespace BuildingBlocks.DataGovernance;

/// <summary>
/// BLK-COMP-02: Provides safe masking helpers for PII fields before they appear
/// in structured log messages, audit event descriptions, or external metadata.
///
/// PRINCIPLE: raw PII must never appear in structured logs or audit Descriptions.
/// Use entity/user IDs as the primary identifier; use masked forms only when the
/// full ID is not available (e.g. failed login before user resolution).
///
/// USAGE:
///   _logger.LogWarning("Login failed: email={Email}", PiiGuard.MaskEmail(email));
///   Description = $"Login failed for {PiiGuard.MaskEmail(email)} in tenant {code}."
/// </summary>
public static class PiiGuard
{
    /// <summary>
    /// Returns a partially masked email safe for structured log output.
    /// "john.doe@example.com" → "jo**@ex*****.com"
    ///
    /// Preserves enough signal to correlate log lines without storing a
    /// searchable raw email in the log aggregator.
    /// Returns "[unknown]" for null/empty input.
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "[unknown]";

        var atIdx = email.IndexOf('@');
        if (atIdx <= 0) return "[masked]";

        var local  = email[..atIdx];
        var domain = email[(atIdx + 1)..];

        var maskedLocal  = MaskSegment(local,  visibleChars: 2);
        var maskedDomain = MaskDomain(domain);

        return $"{maskedLocal}@{maskedDomain}";
    }

    /// <summary>
    /// Returns a partially masked phone number safe for structured log output.
    /// "+12125551234" → "+1212*****34"
    /// Returns "[unknown]" for null/empty input.
    /// </summary>
    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "[unknown]";
        if (phone.Length <= 4) return new string('*', phone.Length);

        var tail = phone[^4..];
        var head = phone.Length > 7 ? phone[..3] : phone[..1];
        var mid  = new string('*', phone.Length - head.Length - 4);
        return $"{head}{mid}{tail}";
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private static string MaskSegment(string segment, int visibleChars)
    {
        if (segment.Length <= visibleChars)
            return new string('*', segment.Length);

        return segment[..visibleChars] + new string('*', segment.Length - visibleChars);
    }

    private static string MaskDomain(string domain)
    {
        var dotIdx = domain.LastIndexOf('.');
        if (dotIdx <= 0) return MaskSegment(domain, visibleChars: 2);

        var domainName = domain[..dotIdx];
        var tld        = domain[dotIdx..];              // ".com", ".org", etc.
        var masked     = MaskSegment(domainName, visibleChars: 2);
        return $"{masked}{tld}";
    }
}
