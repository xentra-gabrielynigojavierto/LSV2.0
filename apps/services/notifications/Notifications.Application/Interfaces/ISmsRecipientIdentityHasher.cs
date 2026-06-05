namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-016: Deterministic recipient identity hasher.
/// Converts a raw phone number into an opaque, irreversible hash for safe persistence.
///
/// Security: Raw phone never stored, logged, or returned. Hash uses HMAC-SHA256 with a
/// configured salt (SmsRecipientIntelligence:RecipientHashSalt). Only the 64-char hex
/// token is safe to persist.
/// </summary>
public interface ISmsRecipientIdentityHasher
{
    /// <summary>
    /// Returns a deterministic 64-char hex HMAC-SHA256 hash of the normalized phone number.
    /// Normalizes by stripping all characters except digits and leading '+' before hashing.
    /// Returns null when phone is null or empty.
    /// Never logs or returns the raw phone.
    /// </summary>
    string? HashRecipient(string? rawPhone);
}
