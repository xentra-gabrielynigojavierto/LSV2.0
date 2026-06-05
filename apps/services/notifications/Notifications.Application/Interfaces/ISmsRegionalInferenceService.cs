namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-015: Lightweight regional/country inference from E.164 phone prefixes.
/// Stateless — no DB reads, no external calls, no phone number persistence.
///
/// Security: Raw phone numbers are NEVER stored. Only derived country/region codes are returned.
/// The caller is responsible for discarding the phone number after inference.
/// </summary>
public interface ISmsRegionalInferenceService
{
    /// <summary>
    /// Infer ISO 3166-1 alpha-2 country code from an E.164 phone number.
    /// Returns null when the prefix is unknown or the phone is null/empty.
    /// IMPORTANT: This mapping is approximate — +1 covers NANP (US and CA are not distinguished).
    /// </summary>
    string? InferCountryCode(string? recipientPhone);

    /// <summary>
    /// Map a country code to a broad region label (e.g. "NANP", "EU", "APAC").
    /// Returns null when the country code is unknown or null.
    /// </summary>
    string? InferRegion(string? countryCode);
}
