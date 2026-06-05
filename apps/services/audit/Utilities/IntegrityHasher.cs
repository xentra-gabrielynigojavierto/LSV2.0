using System.Security.Cryptography;
using System.Text;
using PlatformAuditEventService.Models;

namespace PlatformAuditEventService.Utilities;

/// <summary>
/// Produces a deterministic HMAC-SHA256 integrity hash over an AuditEvent's
/// canonical fields. Used to detect post-write tampering.
/// </summary>
public static class IntegrityHasher
{
    /// <summary>
    /// Computes the integrity hash for an event using the provided HMAC secret.
    /// The canonical string is a pipe-delimited concatenation of immutable fields.
    /// </summary>
    public static string Compute(AuditEvent evt, byte[] hmacSecret)
    {
        var canonical = string.Join("|",
            evt.Id.ToString("D"),
            evt.Source,
            evt.EventType,
            evt.Category,
            evt.Severity,
            evt.TenantId  ?? string.Empty,
            evt.ActorId   ?? string.Empty,
            evt.TargetType ?? string.Empty,
            evt.TargetId  ?? string.Empty,
            evt.Description,
            evt.Outcome,
            evt.OccurredAtUtc.ToString("O"),
            evt.IngestedAtUtc.ToString("O"));

        using var hmac = new HMACSHA256(hmacSecret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Verifies that a stored integrity hash matches a recomputed hash.
    /// Constant-time comparison to resist timing attacks.
    /// </summary>
    public static bool Verify(AuditEvent evt, byte[] hmacSecret)
    {
        if (evt.IntegrityHash is null) return false;
        var expected = Compute(evt, hmacSecret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(evt.IntegrityHash));
    }
}
