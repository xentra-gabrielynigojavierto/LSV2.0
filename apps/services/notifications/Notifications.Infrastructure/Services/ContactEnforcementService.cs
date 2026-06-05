using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

public class ContactEnforcementService : IContactEnforcementService
{
    private readonly IContactSuppressionRepository _suppressionRepo;
    private readonly ITenantContactPolicyRepository _policyRepo;
    private readonly IRecipientContactHealthRepository _healthRepo;
    private readonly ISmsPreferenceRepository _smsPreferenceRepo;
    private readonly ILogger<ContactEnforcementService> _logger;

    private static readonly HashSet<string> NonOverrideable = new() { "unsubscribe", "complaint", "system_protection" };

    private static readonly Dictionary<string, string> HealthStatusPolicyMap = new()
    {
        ["bounced"] = "BlockBouncedContacts",
        ["complained"] = "BlockComplainedContacts",
        ["unsubscribed"] = "BlockUnsubscribedContacts",
        ["suppressed"] = "BlockSuppressedContacts",
        ["invalid"] = "BlockInvalidContacts",
        ["carrier_rejected"] = "BlockCarrierRejectedContacts",
        ["opted_out"] = "BlockUnsubscribedContacts"
    };

    private static readonly Dictionary<string, string> SuppressionTypePolicyMap = new()
    {
        ["manual"] = "BlockSuppressedContacts",
        ["bounce"] = "BlockBouncedContacts",
        ["unsubscribe"] = "BlockUnsubscribedContacts",
        ["complaint"] = "BlockComplainedContacts",
        ["invalid_contact"] = "BlockInvalidContacts",
        ["carrier_rejection"] = "BlockCarrierRejectedContacts",
        ["system_protection"] = "BlockSuppressedContacts"
    };

    public ContactEnforcementService(
        IContactSuppressionRepository suppressionRepo,
        ITenantContactPolicyRepository policyRepo,
        IRecipientContactHealthRepository healthRepo,
        ISmsPreferenceRepository smsPreferenceRepo,
        ILogger<ContactEnforcementService> logger)
    {
        _suppressionRepo    = suppressionRepo;
        _policyRepo         = policyRepo;
        _healthRepo         = healthRepo;
        _smsPreferenceRepo  = smsPreferenceRepo;
        _logger             = logger;
    }

    public async Task<ContactEnforcementResult> EvaluateAsync(ContactEnforcementInput input)
    {
        var normalizedContact = NormalizeContact(input.Channel, input.ContactValue);
        var defaultResult = new ContactEnforcementResult
        {
            Allowed = true, ReasonMessage = "Contact is allowed to receive notifications"
        };

        try
        {
            var dbPolicy = await _policyRepo.FindEffectivePolicyAsync(input.TenantId, input.Channel);
            var blockSuppressed = dbPolicy?.BlockSuppressedContacts ?? true;
            var blockUnsubscribed = dbPolicy?.BlockUnsubscribedContacts ?? true;
            var blockComplained = dbPolicy?.BlockComplainedContacts ?? true;
            var blockBounced = dbPolicy?.BlockBouncedContacts ?? false;
            var blockInvalid = dbPolicy?.BlockInvalidContacts ?? false;
            var blockCarrierRejected = dbPolicy?.BlockCarrierRejectedContacts ?? false;
            var allowOverride = dbPolicy?.AllowManualOverride ?? false;
            var blockUnknownSms = dbPolicy?.BlockUnknownSmsPreference ?? true;

            // ── SMS preference check (LS-NOTIF-SMS-002) ─────────────────────────
            // Runs before generic suppression checks. Only applies to the SMS channel.
            if (input.Channel == "sms")
            {
                try
                {
                    var smsPref = await _smsPreferenceRepo.FindAsync(input.TenantId, normalizedContact);
                    var prefState = smsPref?.PreferenceState ?? "unknown";

                    if (prefState == "opted_out")
                    {
                        _logger.LogDebug("SMS enforcement: opted_out for contact in tenant {TenantId}", input.TenantId);
                        return new ContactEnforcementResult
                        {
                            Allowed        = false,
                            ReasonCode     = "sms_opted_out",
                            ReasonMessage  = "Recipient has opted out of SMS messages",
                            OverrideAllowed = false,
                        };
                    }

                    if (prefState == "unknown" && blockUnknownSms)
                    {
                        _logger.LogDebug("SMS enforcement: unknown preference blocked by policy for tenant {TenantId}", input.TenantId);
                        return new ContactEnforcementResult
                        {
                            Allowed        = false,
                            ReasonCode     = "sms_preference_unknown_blocked_by_policy",
                            ReasonMessage  = "SMS preference is unknown and tenant policy blocks sends to contacts without explicit opt-in",
                            OverrideAllowed = false,
                        };
                    }
                    // opted_in (or unknown+policy-allows) → fall through to existing suppression checks
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SMS preference check failed — falling through to suppression checks");
                }
            }

            var suppressions = await _suppressionRepo.FindActiveAsync(input.TenantId, input.Channel, normalizedContact);
            foreach (var suppression in suppressions)
            {
                var isBlocked = IsPolicyBlocking(suppression.SuppressionType, blockSuppressed, blockUnsubscribed, blockComplained, blockBounced, blockInvalid, blockCarrierRejected);
                if (!isBlocked) continue;

                var isOverrideable = !NonOverrideable.Contains(suppression.SuppressionType);
                var overrideAttempted = input.OverrideSuppression && !string.IsNullOrEmpty(input.OverrideReason);
                var overrideGranted = isOverrideable && allowOverride && overrideAttempted;

                if (overrideGranted)
                    return new ContactEnforcementResult { Allowed = true, ReasonCode = $"override_{suppression.SuppressionType}", ReasonMessage = $"Override applied for suppression type: {suppression.SuppressionType}", MatchedSuppressionId = suppression.Id, OverrideAllowed = true, OverrideUsed = true };

                return new ContactEnforcementResult { Allowed = false, ReasonCode = $"suppressed_{suppression.SuppressionType}", ReasonMessage = $"Contact is suppressed: {suppression.SuppressionType} ({suppression.Reason})", MatchedSuppressionId = suppression.Id, OverrideAllowed = isOverrideable && allowOverride };
            }

            var health = await _healthRepo.FindByContactAsync(input.TenantId, input.Channel, normalizedContact);
            if (health != null && health.HealthStatus is not "valid" and not "unreachable")
            {
                var isBlocked = IsHealthBlocked(health.HealthStatus, blockSuppressed, blockUnsubscribed, blockComplained, blockBounced, blockInvalid, blockCarrierRejected);
                if (isBlocked)
                {
                    var isOverrideable = !NonOverrideable.Contains(health.HealthStatus);
                    var overrideAttempted = input.OverrideSuppression && !string.IsNullOrEmpty(input.OverrideReason);
                    var overrideGranted = isOverrideable && allowOverride && overrideAttempted;

                    if (overrideGranted)
                        return new ContactEnforcementResult { Allowed = true, ReasonCode = $"override_health_{health.HealthStatus}", ReasonMessage = $"Override applied for contact health: {health.HealthStatus}", MatchedHealthStatus = health.HealthStatus, OverrideAllowed = true, OverrideUsed = true };

                    return new ContactEnforcementResult { Allowed = false, ReasonCode = $"health_{health.HealthStatus}", ReasonMessage = $"Contact health status is '{health.HealthStatus}' and is blocked by policy", MatchedHealthStatus = health.HealthStatus, OverrideAllowed = isOverrideable && allowOverride };
                }
            }

            return defaultResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContactEnforcement: check failed, allowing send: {TenantId} {Channel}", input.TenantId, input.Channel);
            return new ContactEnforcementResult { Allowed = true, ReasonMessage = "Contact enforcement check failed - defaulting to allow" };
        }
    }

    private static bool IsPolicyBlocking(string suppressionType, bool blockSuppressed, bool blockUnsubscribed, bool blockComplained, bool blockBounced, bool blockInvalid, bool blockCarrierRejected)
    {
        if (!SuppressionTypePolicyMap.TryGetValue(suppressionType, out var field)) return blockSuppressed;
        return field switch
        {
            "BlockSuppressedContacts" => blockSuppressed, "BlockBouncedContacts" => blockBounced,
            "BlockUnsubscribedContacts" => blockUnsubscribed, "BlockComplainedContacts" => blockComplained,
            "BlockInvalidContacts" => blockInvalid, "BlockCarrierRejectedContacts" => blockCarrierRejected,
            _ => blockSuppressed
        };
    }

    private static bool IsHealthBlocked(string healthStatus, bool blockSuppressed, bool blockUnsubscribed, bool blockComplained, bool blockBounced, bool blockInvalid, bool blockCarrierRejected)
    {
        if (!HealthStatusPolicyMap.TryGetValue(healthStatus, out var field)) return false;
        return field switch
        {
            "BlockSuppressedContacts" => blockSuppressed, "BlockBouncedContacts" => blockBounced,
            "BlockUnsubscribedContacts" => blockUnsubscribed, "BlockComplainedContacts" => blockComplained,
            "BlockInvalidContacts" => blockInvalid, "BlockCarrierRejectedContacts" => blockCarrierRejected,
            _ => false
        };
    }

    private static string NormalizeContact(string channel, string value)
    {
        if (channel == "email") return value.Trim().ToLowerInvariant();
        if (channel == "sms") return System.Text.RegularExpressions.Regex.Replace(value.Trim(), @"[^\d+]", "");
        return value.Trim();
    }
}
