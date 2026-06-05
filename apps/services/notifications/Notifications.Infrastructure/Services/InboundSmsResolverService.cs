using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// Resolves an inbound Twilio webhook `To` number to the tenant and provider config
/// that owns that Twilio sender number. Used by WebhookIngestionService to correctly
/// scope STOP/START/HELP keywords to the right tenant.
///
/// Resolution strategy:
///   1. Load all active SMS/Twilio provider configs from DB.
///   2. For each config, parse SettingsJson for "fromNumber".
///   3. Normalize and compare with the inbound `To` number.
///   4. Return the first matching config (ordered by Priority).
///
/// Phone normalization: same as SmsPreferenceServiceImpl.NormalizePhone
///   → removes all non-digit, non-'+' characters.
/// </summary>
public class InboundSmsResolverService : IInboundSmsResolverService
{
    private readonly ITenantProviderConfigRepository _configRepo;
    private readonly ILogger<InboundSmsResolverService> _logger;

    public InboundSmsResolverService(
        ITenantProviderConfigRepository configRepo,
        ILogger<InboundSmsResolverService> logger)
    {
        _configRepo = configRepo;
        _logger     = logger;
    }

    public async Task<InboundSmsResolutionResult> ResolveAsync(string inboundToNumber)
    {
        var normalizedTo = NormalizePhone(inboundToNumber);
        if (string.IsNullOrWhiteSpace(normalizedTo))
            return InboundSmsResolutionResult.Unresolved(normalizedTo);

        try
        {
            // Load all active Twilio SMS provider configs across all tenants.
            // In practice this is a small set (one per tenant + platform default).
            var configs = await _configRepo.GetActiveSmsProviderConfigsAsync("twilio");

            foreach (var config in configs)
            {
                try
                {
                    var fromNumber = ExtractFromNumber(config.SettingsJson);
                    if (string.IsNullOrWhiteSpace(fromNumber)) continue;

                    var normalizedFrom = NormalizePhone(fromNumber);
                    if (normalizedFrom == normalizedTo)
                    {
                        _logger.LogDebug(
                            "Inbound SMS To={To} resolved to TenantId={TenantId} ProviderConfigId={ConfigId}",
                            MaskPhone(normalizedTo), config.TenantId, config.Id);

                        return new InboundSmsResolutionResult
                        {
                            Resolved          = true,
                            TenantId          = config.TenantId,
                            ProviderConfigId  = config.Id,
                            Provider          = "twilio",
                            NormalizedToNumber = normalizedTo,
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "InboundSmsResolver: failed to parse SettingsJson for config {ConfigId}", config.Id);
                }
            }

            _logger.LogInformation(
                "InboundSmsResolver: To={To} did not match any active Twilio provider config",
                MaskPhone(normalizedTo));

            return InboundSmsResolutionResult.Unresolved(normalizedTo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "InboundSmsResolver: unexpected error resolving To={To}; returning unresolved",
                MaskPhone(normalizedTo));
            return InboundSmsResolutionResult.Unresolved(normalizedTo);
        }
    }

    /// <summary>
    /// Extract the "fromNumber" field from a Twilio provider config's SettingsJson.
    /// The field is written as { "fromNumber": "+15551234567" }.
    /// </summary>
    private static string? ExtractFromNumber(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson)) return null;
        try
        {
            var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.TryGetProperty("fromNumber", out var prop))
                return prop.GetString();
            // Also check snake_case variant for forward compatibility.
            if (doc.RootElement.TryGetProperty("from_number", out var prop2))
                return prop2.GetString();
            return null;
        }
        catch { return null; }
    }

    internal static string NormalizePhone(string phone)
        => System.Text.RegularExpressions.Regex.Replace(phone.Trim(), @"[^\d+]", "");

    private static string MaskPhone(string normalized)
        => normalized.Length > 3 ? normalized[..3] + "***" : "***";
}
