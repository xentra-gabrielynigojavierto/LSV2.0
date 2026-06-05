namespace Notifications.Application.Options;

/// <summary>
/// LS-NOTIF-SMS-013 / LS-NOTIF-SMS-014: Configuration for SMS cost analytics.
///
/// These are operational cost estimates, not invoice-grade billing values.
/// Estimated costs are configurable assumptions per provider.
/// Actual provider billing data requires future provider adapter extension.
///
/// LS-NOTIF-SMS-014: Added ProviderEstimates dictionary for multi-provider cost config.
/// GetEstimatedCost() checks ProviderEstimates first, then falls back to
/// legacy per-name properties, then DefaultEstimatedOutboundSmsCost.
///
/// Safe defaults: if a provider cost is unconfigured, CostSource = "unavailable"
/// and no monetary amount is recorded — this avoids implying false spend.
/// </summary>
public sealed class SmsCostAnalyticsOptions
{
    public const string SectionName = "SmsCostAnalytics";

    /// <summary>When false, cost recording in the send path is skipped entirely.
    /// Existing cost analytics queries still work against any data already recorded.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>ISO 4217 currency code for all cost records. Default: "USD".</summary>
    public string DefaultCurrency { get; set; } = "USD";

    /// <summary>
    /// LS-NOTIF-SMS-014: Per-provider estimated outbound SMS cost dictionary.
    /// Keys are lowercase provider type names (e.g., "twilio", "vonage", "telnyx").
    /// Checked first by GetEstimatedCost() before falling back to legacy per-name properties.
    ///
    /// Example appsettings.json:
    ///   "SmsCostAnalytics": {
    ///     "ProviderEstimates": {
    ///       "twilio": 0.0075,
    ///       "vonage": 0.0065,
    ///       "telnyx": 0.0055
    ///     }
    ///   }
    /// </summary>
    public Dictionary<string, decimal> ProviderEstimates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Estimated outbound SMS cost when provider is not in ProviderEstimates and
    /// is not twilio (or TwilioEstimatedOutboundSmsCost is unset).
    /// Set to null to record CostSource = "unavailable" for unknown providers.
    /// </summary>
    public decimal? DefaultEstimatedOutboundSmsCost { get; set; } = null;

    /// <summary>
    /// Legacy: Estimated outbound SMS cost for Twilio. Superseded by ProviderEstimates["twilio"]
    /// when both are configured (ProviderEstimates takes precedence).
    /// Typical Twilio US SMS rate ~$0.0075.
    /// </summary>
    public decimal? TwilioEstimatedOutboundSmsCost { get; set; } = 0.0075m;

    /// <summary>
    /// Cost policy for failed messages.
    /// "count_estimated_when_provider_accepted": cost only when attempt has a ProviderMessageId
    ///   (provider accepted the message before it failed/timed out).
    /// Default: count_estimated_when_provider_accepted.
    /// </summary>
    public string FailedMessageCostPolicy { get; set; } = "count_estimated_when_provider_accepted";

    /// <summary>
    /// Cost policy for retry/failover attempts.
    /// "per_attempt": each attempt hop is costed independently.
    /// Default: per_attempt.
    /// </summary>
    public string RetryCostPolicy { get; set; } = "per_attempt";

    /// <summary>
    /// Returns the estimated outbound cost for the given provider name, or null if unavailable.
    /// Provider name comparison is case-insensitive.
    ///
    /// Resolution order:
    ///   1. ProviderEstimates dictionary (multi-provider config, LS-NOTIF-SMS-014)
    ///   2. TwilioEstimatedOutboundSmsCost (legacy, for "twilio" only)
    ///   3. DefaultEstimatedOutboundSmsCost
    /// </summary>
    public decimal? GetEstimatedCost(string provider)
    {
        // 1. Check ProviderEstimates dictionary first
        if (ProviderEstimates.TryGetValue(provider, out var dictCost))
            return dictCost;

        // 2. Legacy Twilio property (backward compat when dict not configured)
        if (string.Equals(provider, "twilio", StringComparison.OrdinalIgnoreCase))
            return TwilioEstimatedOutboundSmsCost;

        // 3. Default fallback (null = CostSource "unavailable")
        return DefaultEstimatedOutboundSmsCost;
    }
}
