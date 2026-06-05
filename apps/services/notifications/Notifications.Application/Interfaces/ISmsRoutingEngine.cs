namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-014/015: Input to the SMS routing engine.
/// Built from ProviderRoutingService candidate routes + notification context.
/// Does NOT include credentials or raw recipient data — except the transient inference field below.
/// </summary>
public sealed class SmsRoutingRequest
{
    public Guid   TenantId       { get; set; }
    public Guid?  NotificationId { get; set; }

    /// <summary>Candidate routes from ProviderRoutingService, already ordered by base priority.</summary>
    public IReadOnlyList<ProviderRoute> CandidateRoutes { get; set; } = Array.Empty<ProviderRoute>();

    /// <summary>Optional country code derived from recipient phone number (E.164 prefix parsing).
    /// Null if country code cannot be safely derived.</summary>
    public string? CountryCode { get; set; }

    /// <summary>Optional region hint (e.g., "us-east-1", "eu-west-1").</summary>
    public string? Region { get; set; }

    /// <summary>
    /// LS-NOTIF-SMS-015: Transient phone number for E.164 country-code inference ONLY.
    /// NEVER persisted, NEVER logged, NEVER included in routing decisions or quality snapshots.
    /// Used exclusively inside SmsRoutingEngine to infer InferredCountryCode before route selection.
    /// Set to null immediately after inference.
    /// </summary>
    public string? RecipientPhoneForInferenceOnly { get; set; }
}

/// <summary>
/// Result from the SMS routing engine's SelectRouteAsync.
/// Routing decisions are persisted by the caller (NotificationServiceImpl).
/// </summary>
public sealed class SmsRoutingDecisionResult
{
    public bool   Success    { get; set; }

    /// <summary>The selected route. Null when no route could be selected.</summary>
    public ProviderRoute? SelectedRoute { get; set; }

    public string  RoutingMode             { get; set; } = "priority";
    public string  SelectedProvider        { get; set; } = string.Empty;
    public Guid?   SelectedProviderConfigId { get; set; }
    public string? ProviderOwnershipMode   { get; set; }

    /// <summary>Human-readable reason for the selection decision.</summary>
    public string DecisionReason { get; set; } = string.Empty;

    /// <summary>All candidate providers considered (provider type strings).</summary>
    public IReadOnlyList<string> CandidateProviders { get; set; } = Array.Empty<string>();

    /// <summary>Providers excluded by policy or health constraints.</summary>
    public IReadOnlyList<string> ExcludedProviders { get; set; } = Array.Empty<string>();

    /// <summary>Matched routing policy ID, if any.</summary>
    public Guid?   MatchedPolicyId { get; set; }

    public decimal? EstimatedCostAmount { get; set; }
    public string?  CostCurrency        { get; set; }
    public string?  CountryCode         { get; set; }
    public string?  Region              { get; set; }

    /// <summary>Failure code when Success = false (no_route, no_healthy_provider).</summary>
    public string? FailureCode    { get; set; }
    public string? FailureMessage { get; set; }

    // ── LS-NOTIF-SMS-015: Adaptive routing metadata ──────────────────────────
    /// <summary>Country code inferred from RecipientPhoneForInferenceOnly (never a raw phone).</summary>
    public string?  InferredCountryCode  { get; set; }
    /// <summary>Region derived from InferredCountryCode.</summary>
    public string?  InferredRegion       { get; set; }
    /// <summary>Quality score (0-100) of the selected provider. Null for non-adaptive modes.</summary>
    public decimal? ProviderQualityScore { get; set; }
    /// <summary>Composite adaptive score. Null for non-adaptive modes.</summary>
    public decimal? AdaptiveScore        { get; set; }
    /// <summary>JSON inputs used by adaptive routing. Null for non-adaptive modes.</summary>
    public string?  AdaptiveInputsJson   { get; set; }

    // Factory helpers
    public static SmsRoutingDecisionResult NoRoute(
        string routingMode,
        IReadOnlyList<string> candidates,
        string reason = "no_route")
        => new()
        {
            Success         = false,
            RoutingMode     = routingMode,
            FailureCode     = "no_route",
            FailureMessage  = reason,
            DecisionReason  = reason,
            CandidateProviders = candidates,
            ExcludedProviders  = Array.Empty<string>(),
        };
}

/// <summary>
/// LS-NOTIF-SMS-014: SMS routing engine.
/// Selects the best provider route from a candidate list based on the active routing policy.
/// Never calls external providers. Uses only locally persisted health/cost data.
/// </summary>
public interface ISmsRoutingEngine
{
    /// <summary>
    /// Select the best provider route for an outbound SMS based on tenant routing policy.
    /// Always returns a result — failures indicated by Success=false + FailureCode.
    /// Never throws.
    /// </summary>
    Task<SmsRoutingDecisionResult> SelectRouteAsync(SmsRoutingRequest request, CancellationToken ct = default);
}
