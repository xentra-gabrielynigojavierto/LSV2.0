using System.Text.RegularExpressions;

namespace Identity.Domain;

/// <summary>
/// Lightweight validation/normalisation for phone numbers stored on the User
/// record. Phones are kept in E.164 form (e.g. "+15551234567") so the
/// notifications service can hand them straight to the SMS provider without
/// per-tenant reformatting.
///
/// We deliberately keep this validator minimal — the goal is to reject clearly
/// invalid input (letters, missing country code, too short/too long) rather
/// than to verify that a number is actually reachable. A real-time carrier
/// lookup belongs in the SMS provider, not the identity service.
/// </summary>
public static class PhoneNumber
{
    // E.164: a leading '+' followed by 1..15 digits, the first of which must
    // be 1..9 (no leading zero on the country code).
    private static readonly Regex E164 = new(
        @"^\+[1-9]\d{1,14}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    /// <summary>
    /// Attempts to normalise raw user input into E.164. Strips any internal
    /// whitespace, hyphens, dots, and parentheses (formatting artefacts) so
    /// that "+1 (555) 123-4567" is accepted and stored as "+15551234567".
    /// Returns null when the input is null/whitespace (intentional clear) or
    /// fails E.164 validation.
    /// </summary>
    public static (bool Ok, string? Normalised, string? Error) TryNormalise(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (true, null, null);

        // Strip common formatting characters but preserve the leading '+'.
        var trimmed = raw.Trim();
        var stripped = new System.Text.StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (ch == ' ' || ch == '-' || ch == '.' || ch == '(' || ch == ')')
                continue;
            stripped.Append(ch);
        }
        var candidate = stripped.ToString();

        if (!E164.IsMatch(candidate))
            return (false, null, "Phone number must be in international format (e.g. +15551234567).");

        return (true, candidate, null);
    }
}
