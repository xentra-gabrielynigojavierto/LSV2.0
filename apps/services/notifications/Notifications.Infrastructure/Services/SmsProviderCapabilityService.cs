using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-014: Static registry of SMS provider capability metadata.
/// Config-driven. Does not call any external provider APIs.
///
/// Capabilities:
/// - Twilio: full capability (send, status lookup, health check, cost estimate, tenant-owned, platform)
/// - Vonage: send only (status lookup = webhook-only, health check = not safe)
///
/// Additional providers should be added here when their adapters are implemented.
/// </summary>
public class SmsProviderCapabilityService : ISmsProviderCapabilityService
{
    private static readonly IReadOnlyList<SmsProviderCapabilityDto> Registry = new List<SmsProviderCapabilityDto>
    {
        new()
        {
            ProviderType              = "twilio",
            DisplayName               = "Twilio",
            SupportsSend              = true,
            SupportsStatusLookup      = true,
            SupportsHealthCheck       = true,
            SupportsCostEstimate      = true,
            SupportsRegionalRouting   = false,  // no native region selection in classic API
            SupportsTenantOwnedConfig = true,
            SupportsPlatformConfig    = true,
            SupportedCountries        = null,   // worldwide
            DefaultCurrency           = "USD",
            Notes                     = "Full capability. Existing platform default. " +
                                        "Status lookup via Twilio REST API. " +
                                        "Health check via account endpoint.",
        },
        new()
        {
            ProviderType              = "vonage",
            DisplayName               = "Vonage (Nexmo Classic SMS API)",
            SupportsSend              = true,
            SupportsStatusLookup      = false,  // webhook-only; active pull not implemented
            SupportsHealthCheck       = false,  // no safe zero-cost probe endpoint
            SupportsCostEstimate      = true,   // estimated cost configured in SmsCostAnalytics
            SupportsRegionalRouting   = false,
            SupportsTenantOwnedConfig = true,
            SupportsPlatformConfig    = false,  // no platform env-var config in this version
            SupportedCountries        = null,   // worldwide
            DefaultCurrency           = "USD",
            Notes                     = "Send-only. Status lookup not supported (webhook-based). " +
                                        "Reconciliation auto-skips with skipped_unsupported_provider. " +
                                        "Requires tenant-owned TenantProviderConfig. " +
                                        "CredentialsJson: { apiKey, apiSecret }; " +
                                        "SettingsJson: { fromNumber }.",
        },
    };

    private static readonly Dictionary<string, SmsProviderCapabilityDto> Index =
        Registry.ToDictionary(c => c.ProviderType, StringComparer.OrdinalIgnoreCase);

    public SmsProviderCapabilityDto? GetCapability(string providerType)
        => Index.TryGetValue(providerType, out var c) ? c : null;

    public IReadOnlyList<SmsProviderCapabilityDto> GetAll() => Registry;
}
