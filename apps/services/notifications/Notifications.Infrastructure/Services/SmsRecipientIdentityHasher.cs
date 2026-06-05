using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-016: HMAC-SHA256 recipient identity hasher.
///
/// Security:
/// - Normalizes phone by stripping all characters except digits and leading '+'.
/// - Hashes using HMAC-SHA256 with configured salt (SmsRecipientIntelligence:RecipientHashSalt).
/// - When salt is not configured (dev/test only), falls back to SHA256 with a fixed prefix.
/// - Raw phone is never stored, logged, or returned.
/// - Output is a 64-char lowercase hex string.
/// </summary>
public class SmsRecipientIdentityHasher : ISmsRecipientIdentityHasher
{
    private static readonly Regex NonE164Chars = new(@"[^\d+]", RegexOptions.Compiled);
    private const string DevFallbackPrefix = "legalsynq-sms-016-dev:";

    private readonly byte[]? _saltBytes;

    public SmsRecipientIdentityHasher(IOptions<SmsRecipientIntelligenceOptions> opts)
    {
        var salt = opts.Value.RecipientHashSalt;
        _saltBytes = string.IsNullOrEmpty(salt) ? null : Encoding.UTF8.GetBytes(salt);
    }

    public string? HashRecipient(string? rawPhone)
    {
        if (string.IsNullOrWhiteSpace(rawPhone)) return null;

        var normalized = Normalize(rawPhone);
        if (string.IsNullOrEmpty(normalized)) return null;

        if (_saltBytes != null)
        {
            var hash = HMACSHA256.HashData(_saltBytes, Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        // Dev fallback — never use in production
        var devInput = DevFallbackPrefix + normalized;
        var devHash  = SHA256.HashData(Encoding.UTF8.GetBytes(devInput));
        return Convert.ToHexString(devHash).ToLowerInvariant();
    }

    private static string Normalize(string rawPhone)
    {
        var trimmed = rawPhone.Trim();
        return NonE164Chars.Replace(trimmed, string.Empty);
    }
}
