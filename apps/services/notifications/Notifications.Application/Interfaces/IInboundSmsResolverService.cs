namespace Notifications.Application.Interfaces;

/// <summary>
/// Resolves an inbound Twilio SMS `To` number to the tenant and provider config
/// that owns that number. Used to correctly scope inbound STOP/START/HELP keywords.
/// </summary>
public interface IInboundSmsResolverService
{
    /// <summary>
    /// Attempt to resolve the inbound Twilio webhook `To` number to a tenant/provider config.
    /// Returns a result where <see cref="InboundSmsResolutionResult.Resolved"/> indicates success.
    /// Never throws — returns unresolved on any error.
    /// </summary>
    Task<InboundSmsResolutionResult> ResolveAsync(string inboundToNumber);
}

public class InboundSmsResolutionResult
{
    /// <summary>True if the inbound `To` number was matched to a tenant/provider config.</summary>
    public bool Resolved { get; set; }

    /// <summary>Resolved tenant ID. Null if unresolved or platform-level match.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Resolved TenantProviderConfig ID. Null if not matched to a specific config.</summary>
    public Guid? ProviderConfigId { get; set; }

    /// <summary>Provider name (twilio). Null if unresolved.</summary>
    public string? Provider { get; set; }

    /// <summary>Normalized inbound `To` number.</summary>
    public string? NormalizedToNumber { get; set; }

    public static InboundSmsResolutionResult Unresolved(string? normalizedToNumber = null) => new()
    {
        Resolved          = false,
        NormalizedToNumber = normalizedToNumber,
    };
}

/// <summary>Context passed to <see cref="ISmsPreferenceService.ProcessInboundKeywordWithContextAsync"/>.</summary>
public class InboundSmsKeywordContext
{
    /// <summary>Resolved tenant ID. Null if unresolved.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Sender (contact) phone number — E.164 format from Twilio `From` field.</summary>
    public string FromPhone { get; set; } = string.Empty;

    /// <summary>Our platform/tenant Twilio number from Twilio `To` field — normalized.</summary>
    public string ToPhone { get; set; } = string.Empty;

    /// <summary>Classified keyword category: "opt_out" | "opt_in" | "help".</summary>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>Exact text body from the inbound SMS.</summary>
    public string RawKeyword { get; set; } = string.Empty;

    /// <summary>Twilio MessageSid for traceability.</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>Resolved TenantProviderConfig ID. Null if unresolved.</summary>
    public Guid? ProviderConfigId { get; set; }

    /// <summary>Provider name (twilio).</summary>
    public string Provider { get; set; } = "twilio";

    /// <summary>Whether the tenant was successfully resolved from the `To` number.</summary>
    public bool TenantResolved { get; set; }
}
