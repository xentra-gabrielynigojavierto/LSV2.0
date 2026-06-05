using System.Collections.Concurrent;

namespace CareConnect.Application.Services;

/// <summary>
/// CC2-ENROLL: In-memory OTP store for the provider self-enrollment flow.
///
/// Keyed by lowercased email address. Each entry holds a 6-digit code
/// and a UTC expiry timestamp. Entries are automatically evicted when
/// verified or when they expire.
///
/// This is intentionally simple: the enrollment flow is a low-frequency
/// operation on a single-instance service. For multi-instance deployments
/// a distributed cache (Redis) should replace this singleton.
/// </summary>
public sealed class EnrollmentOtpStore
{
    private readonly ConcurrentDictionary<string, OtpEntry> _store = new();

    private sealed record OtpEntry(string Code, DateTime Expiry);

    public string Generate(string email)
    {
        var code  = new Random().Next(100_000, 999_999).ToString();
        var key   = email.Trim().ToLowerInvariant();
        _store[key] = new OtpEntry(code, DateTime.UtcNow.AddMinutes(10));
        return code;
    }

    public bool Verify(string email, string code)
    {
        var key = email.Trim().ToLowerInvariant();
        if (!_store.TryGetValue(key, out var entry)) return false;
        if (entry.Expiry < DateTime.UtcNow) { _store.TryRemove(key, out _); return false; }
        if (!string.Equals(entry.Code, code.Trim(), StringComparison.Ordinal)) return false;
        _store.TryRemove(key, out _);
        return true;
    }
}
