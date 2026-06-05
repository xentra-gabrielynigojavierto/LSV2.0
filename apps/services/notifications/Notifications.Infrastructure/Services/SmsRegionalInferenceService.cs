using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-015: Lightweight E.164 phone prefix → country/region inference.
/// Stateless, no DB reads, no external calls, no persistence.
///
/// Limitation: +1 (NANP) covers both US and Canada — they are not distinguished at this level.
/// All other mappings are single-country. The mapping is approximate; a robust parser
/// (e.g., libphonenumber) would be needed for production-grade accuracy.
///
/// Security: Phone numbers are NEVER logged, persisted, or retained. The method
/// returns only derived country/region codes. Callers must discard the raw phone
/// immediately after this call.
/// </summary>
public class SmsRegionalInferenceService : ISmsRegionalInferenceService
{
    // Ordered longest-prefix first so the most specific match wins.
    private static readonly (string Prefix, string CountryCode)[] PrefixMap =
    {
        // Extended NANP exceptions (caller with country distinction, longest match first)
        ("+1868", "TT"), ("+1876", "JM"), ("+1809", "DO"), ("+1787", "PR"), ("+1784", "VC"),
        ("+1767", "DM"), ("+1758", "LC"), ("+1721", "SX"), ("+1664", "MS"), ("+1649", "TC"),
        ("+1473", "GD"), ("+1441", "BM"), ("+1345", "KY"), ("+1284", "VG"), ("+1268", "AG"),
        ("+1246", "BB"), ("+1242", "BS"),
        // Remaining +1 = NANP (US/CA not distinguished)
        ("+1",  "US"),
        // Common assignments
        ("+7",  "RU"),
        ("+20", "EG"),
        ("+27", "ZA"),
        ("+30", "GR"),
        ("+31", "NL"),
        ("+32", "BE"),
        ("+33", "FR"),
        ("+34", "ES"),
        ("+36", "HU"),
        ("+39", "IT"),
        ("+40", "RO"),
        ("+41", "CH"),
        ("+43", "AT"),
        ("+44", "GB"),
        ("+45", "DK"),
        ("+46", "SE"),
        ("+47", "NO"),
        ("+48", "PL"),
        ("+49", "DE"),
        ("+51", "PE"),
        ("+52", "MX"),
        ("+53", "CU"),
        ("+54", "AR"),
        ("+55", "BR"),
        ("+56", "CL"),
        ("+57", "CO"),
        ("+58", "VE"),
        ("+60", "MY"),
        ("+61", "AU"),
        ("+62", "ID"),
        ("+63", "PH"),
        ("+64", "NZ"),
        ("+65", "SG"),
        ("+66", "TH"),
        ("+81", "JP"),
        ("+82", "KR"),
        ("+84", "VN"),
        ("+86", "CN"),
        ("+90", "TR"),
        ("+91", "IN"),
        ("+92", "PK"),
        ("+93", "AF"),
        ("+94", "LK"),
        ("+95", "MM"),
        ("+98", "IR"),
        ("+212", "MA"),
        ("+213", "DZ"),
        ("+216", "TN"),
        ("+218", "LY"),
        ("+220", "GM"),
        ("+221", "SN"),
        ("+222", "MR"),
        ("+223", "ML"),
        ("+224", "GN"),
        ("+225", "CI"),
        ("+226", "BF"),
        ("+227", "NE"),
        ("+228", "TG"),
        ("+229", "BJ"),
        ("+230", "MU"),
        ("+231", "LR"),
        ("+232", "SL"),
        ("+233", "GH"),
        ("+234", "NG"),
        ("+235", "TD"),
        ("+236", "CF"),
        ("+237", "CM"),
        ("+238", "CV"),
        ("+239", "ST"),
        ("+240", "GQ"),
        ("+241", "GA"),
        ("+242", "CG"),
        ("+243", "CD"),
        ("+244", "AO"),
        ("+245", "GW"),
        ("+248", "SC"),
        ("+249", "SD"),
        ("+250", "RW"),
        ("+251", "ET"),
        ("+252", "SO"),
        ("+253", "DJ"),
        ("+254", "KE"),
        ("+255", "TZ"),
        ("+256", "UG"),
        ("+257", "BI"),
        ("+258", "MZ"),
        ("+260", "ZM"),
        ("+261", "MG"),
        ("+262", "RE"),
        ("+263", "ZW"),
        ("+264", "NA"),
        ("+265", "MW"),
        ("+266", "LS"),
        ("+267", "BW"),
        ("+268", "SZ"),
        ("+269", "KM"),
        ("+290", "SH"),
        ("+291", "ER"),
        ("+297", "AW"),
        ("+298", "FO"),
        ("+299", "GL"),
        ("+350", "GI"),
        ("+351", "PT"),
        ("+352", "LU"),
        ("+353", "IE"),
        ("+354", "IS"),
        ("+355", "AL"),
        ("+356", "MT"),
        ("+357", "CY"),
        ("+358", "FI"),
        ("+359", "BG"),
        ("+370", "LT"),
        ("+371", "LV"),
        ("+372", "EE"),
        ("+373", "MD"),
        ("+374", "AM"),
        ("+375", "BY"),
        ("+376", "AD"),
        ("+377", "MC"),
        ("+378", "SM"),
        ("+380", "UA"),
        ("+381", "RS"),
        ("+382", "ME"),
        ("+385", "HR"),
        ("+386", "SI"),
        ("+387", "BA"),
        ("+389", "MK"),
        ("+420", "CZ"),
        ("+421", "SK"),
        ("+423", "LI"),
        ("+502", "GT"),
        ("+503", "SV"),
        ("+504", "HN"),
        ("+505", "NI"),
        ("+506", "CR"),
        ("+507", "PA"),
        ("+509", "HT"),
        ("+591", "BO"),
        ("+592", "GY"),
        ("+593", "EC"),
        ("+595", "PY"),
        ("+597", "SR"),
        ("+598", "UY"),
        ("+670", "TL"),
        ("+672", "NF"),
        ("+673", "BN"),
        ("+674", "NR"),
        ("+675", "PG"),
        ("+676", "TO"),
        ("+677", "SB"),
        ("+678", "VU"),
        ("+679", "FJ"),
        ("+680", "PW"),
        ("+681", "WF"),
        ("+682", "CK"),
        ("+683", "NU"),
        ("+685", "WS"),
        ("+686", "KI"),
        ("+687", "NC"),
        ("+688", "TV"),
        ("+689", "PF"),
        ("+690", "TK"),
        ("+691", "FM"),
        ("+692", "MH"),
        ("+850", "KP"),
        ("+852", "HK"),
        ("+853", "MO"),
        ("+855", "KH"),
        ("+856", "LA"),
        ("+880", "BD"),
        ("+886", "TW"),
        ("+960", "MV"),
        ("+961", "LB"),
        ("+962", "JO"),
        ("+963", "SY"),
        ("+964", "IQ"),
        ("+965", "KW"),
        ("+966", "SA"),
        ("+967", "YE"),
        ("+968", "OM"),
        ("+970", "PS"),
        ("+971", "AE"),
        ("+972", "IL"),
        ("+973", "BH"),
        ("+974", "QA"),
        ("+975", "BT"),
        ("+976", "MN"),
        ("+977", "NP"),
        ("+992", "TJ"),
        ("+993", "TM"),
        ("+994", "AZ"),
        ("+995", "GE"),
        ("+996", "KG"),
        ("+998", "UZ"),
    };

    // Region groupings by country code
    private static readonly Dictionary<string, string> CountryToRegion =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // North America / NANP
            { "US", "NANP" }, { "CA", "NANP" }, { "MX", "NANP" },
            { "PR", "NANP" }, { "DO", "NANP" }, { "JM", "NANP" },
            { "TT", "NANP" }, { "BB", "NANP" }, { "BS", "NANP" },
            { "BM", "NANP" }, { "TC", "NANP" }, { "VC", "NANP" },
            { "LC", "NANP" }, { "GD", "NANP" }, { "DM", "NANP" },
            { "AG", "NANP" }, { "KY", "NANP" }, { "VG", "NANP" },
            { "MS", "NANP" }, { "SX", "NANP" },
            // Latin America
            { "AR", "LATAM" }, { "BR", "LATAM" }, { "CL", "LATAM" },
            { "CO", "LATAM" }, { "PE", "LATAM" }, { "VE", "LATAM" },
            { "EC", "LATAM" }, { "BO", "LATAM" }, { "PY", "LATAM" },
            { "UY", "LATAM" }, { "GY", "LATAM" }, { "SR", "LATAM" },
            { "GT", "LATAM" }, { "SV", "LATAM" }, { "HN", "LATAM" },
            { "NI", "LATAM" }, { "CR", "LATAM" }, { "PA", "LATAM" },
            { "CU", "LATAM" }, { "HT", "LATAM" },
            // Western Europe
            { "GB", "EU" }, { "FR", "EU" }, { "DE", "EU" }, { "IT", "EU" },
            { "ES", "EU" }, { "PT", "EU" }, { "NL", "EU" }, { "BE", "EU" },
            { "CH", "EU" }, { "AT", "EU" }, { "SE", "EU" }, { "NO", "EU" },
            { "DK", "EU" }, { "FI", "EU" }, { "IE", "EU" }, { "LU", "EU" },
            { "IS", "EU" }, { "GI", "EU" }, { "MC", "EU" }, { "AD", "EU" },
            { "SM", "EU" }, { "LI", "EU" }, { "MT", "EU" }, { "CY", "EU" },
            // Eastern Europe
            { "PL", "EU" }, { "CZ", "EU" }, { "SK", "EU" }, { "HU", "EU" },
            { "RO", "EU" }, { "BG", "EU" }, { "GR", "EU" }, { "HR", "EU" },
            { "SI", "EU" }, { "BA", "EU" }, { "RS", "EU" }, { "ME", "EU" },
            { "MK", "EU" }, { "AL", "EU" }, { "MD", "EU" }, { "BY", "EU" },
            { "UA", "EU" }, { "LT", "EU" }, { "LV", "EU" }, { "EE", "EU" },
            { "AM", "EU" }, { "GE", "EU" }, { "AZ", "EU" },
            // Middle East
            { "TR", "ME" }, { "SA", "ME" }, { "AE", "ME" }, { "IL", "ME" },
            { "JO", "ME" }, { "LB", "ME" }, { "SY", "ME" }, { "IQ", "ME" },
            { "KW", "ME" }, { "QA", "ME" }, { "BH", "ME" }, { "OM", "ME" },
            { "YE", "ME" }, { "IR", "ME" }, { "PS", "ME" },
            // South Asia
            { "IN", "APAC" }, { "PK", "APAC" }, { "BD", "APAC" },
            { "LK", "APAC" }, { "NP", "APAC" }, { "BT", "APAC" },
            { "MV", "APAC" }, { "AF", "APAC" },
            // East / Southeast Asia
            { "CN", "APAC" }, { "JP", "APAC" }, { "KR", "APAC" },
            { "TW", "APAC" }, { "HK", "APAC" }, { "MO", "APAC" },
            { "SG", "APAC" }, { "MY", "APAC" }, { "ID", "APAC" },
            { "PH", "APAC" }, { "TH", "APAC" }, { "VN", "APAC" },
            { "MM", "APAC" }, { "KH", "APAC" }, { "LA", "APAC" },
            { "BN", "APAC" }, { "TL", "APAC" }, { "MN", "APAC" },
            { "KP", "APAC" },
            // Oceania
            { "AU", "APAC" }, { "NZ", "APAC" }, { "PG", "APAC" },
            { "FJ", "APAC" }, { "SB", "APAC" }, { "VU", "APAC" },
            { "TO", "APAC" }, { "WS", "APAC" }, { "KI", "APAC" },
            { "FM", "APAC" }, { "MH", "APAC" }, { "PW", "APAC" },
            { "NR", "APAC" }, { "TV", "APAC" }, { "CK", "APAC" },
            // Africa
            { "NG", "AF" }, { "ZA", "AF" }, { "EG", "AF" }, { "KE", "AF" },
            { "GH", "AF" }, { "ET", "AF" }, { "TZ", "AF" }, { "UG", "AF" },
            { "MA", "AF" }, { "DZ", "AF" }, { "TN", "AF" }, { "SD", "AF" },
            { "AO", "AF" }, { "CM", "AF" }, { "CI", "AF" }, { "SN", "AF" },
            { "ZM", "AF" }, { "ZW", "AF" }, { "MZ", "AF" }, { "MG", "AF" },
            { "ML", "AF" }, { "BF", "AF" }, { "RW", "AF" }, { "SO", "AF" },
            { "ER", "AF" }, { "CD", "AF" }, { "CG", "AF" }, { "GA", "AF" },
            { "MU", "AF" }, { "SC", "AF" }, { "MW", "AF" }, { "NA", "AF" },
            { "BW", "AF" }, { "LS", "AF" }, { "SZ", "AF" }, { "LY", "AF" },
            { "LR", "AF" }, { "SL", "AF" }, { "GN", "AF" }, { "GM", "AF" },
            { "NE", "AF" }, { "TD", "AF" }, { "CF", "AF" }, { "GQ", "AF" },
            { "ST", "AF" }, { "CV", "AF" }, { "GW", "AF" }, { "DJ", "AF" },
            { "KM", "AF" }, { "BI", "AF" }, { "TG", "AF" }, { "BJ", "AF" },
            { "MR", "AF" },
            // Russia / CIS
            { "RU", "CIS" }, { "KZ", "CIS" }, { "UZ", "CIS" },
            { "TM", "CIS" }, { "TJ", "CIS" }, { "KG", "CIS" },
        };

    public string? InferCountryCode(string? recipientPhone)
    {
        if (string.IsNullOrWhiteSpace(recipientPhone)) return null;

        var phone = recipientPhone.Trim();
        if (!phone.StartsWith('+')) return null;

        // Longest-prefix match (entries are already ordered longest first in the array)
        foreach (var (prefix, code) in PrefixMap)
        {
            if (phone.StartsWith(prefix, StringComparison.Ordinal))
                return code;
        }
        return null;
    }

    public string? InferRegion(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return null;
        return CountryToRegion.TryGetValue(countryCode.Trim(), out var region) ? region : null;
    }
}
