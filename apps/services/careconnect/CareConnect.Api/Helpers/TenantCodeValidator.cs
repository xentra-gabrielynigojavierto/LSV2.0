// CC2-INT-B09-01: Mirrors the slug validation rules enforced by the Identity service.
// Identity uses SlugGenerator.Validate — this client-side guard prevents unnecessary round-trips.
using System.Text.RegularExpressions;

namespace CareConnect.Api.Helpers;

/// <summary>
/// Validates tenant code/subdomain format before sending to Identity.
/// Rules: 2–30 characters, lowercase letters/digits only, hyphens allowed
/// in interior positions but NOT at start or end.
/// Must match Identity's SlugGenerator.Validate rules.
/// </summary>
public static class TenantCodeValidator
{
    // ^[a-z0-9]([a-z0-9-]{0,28}[a-z0-9])?$  covers lengths 1–30.
    // For length == 1: just [a-z0-9]. For lengths 2–30: starts/ends with alnum, interior allows hyphens.
    private static readonly Regex _pattern =
        new(@"^[a-z0-9]([a-z0-9\-]{0,28}[a-z0-9])?$", RegexOptions.Compiled);

    public const string FormatHint =
        "Subdomain must be 2–30 characters, use only lowercase letters, numbers, and hyphens, " +
        "and must not start or end with a hyphen.";

    public static bool IsValid(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        if (code.Length < 2 || code.Length > 30)
            return false;

        return _pattern.IsMatch(code);
    }
}
