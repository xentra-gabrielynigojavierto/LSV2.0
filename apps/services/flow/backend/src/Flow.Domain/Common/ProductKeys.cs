namespace Flow.Domain.Common;

/// <summary>
/// LS-FLOW-020-A — Centralised, controlled product key constants and helpers.
///
/// Flow is a multi-tenant, multi-product platform service. Every core entity
/// (FlowDefinition, TaskItem, WorkflowAutomationHook) is scoped not just to a
/// tenant but also to a product within that tenant. This avoids cross-product
/// query leakage and keeps each product's workflows/tasks isolated even when
/// a single tenant runs multiple LegalSynq products.
///
/// Allowed values are intentionally a small whitelist. New products should be
/// added here (and only here) before being accepted by the API.
/// </summary>
public static class ProductKeys
{
    /// <summary>
    /// Transitional default used during the LS-FLOW-020-A migration to
    /// backfill existing rows and as a fallback when callers omit
    /// productKey from create/update requests.
    /// </summary>
    public const string FlowGeneric = "FLOW_GENERIC";

    public const string SynqLiens = "SYNQ_LIENS";
    public const string SynqFund = "SYNQ_FUND";
    public const string CareConnect = "CARE_CONNECT";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        FlowGeneric,
        SynqLiens,
        SynqFund,
        CareConnect,
    };

    /// <summary>True when <paramref name="value"/> is a non-empty supported product key.</summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value);

    /// <summary>
    /// Normalises a (possibly null/blank) caller-supplied value to a valid
    /// product key. Returns <see cref="FlowGeneric"/> for null/blank input
    /// (transitional backward-compatibility default). Throws when the value
    /// is non-blank but not in the whitelist.
    /// </summary>
    public static string NormalizeOrDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return FlowGeneric;
        var trimmed = value.Trim();
        if (!All.Contains(trimmed))
            throw new ArgumentException($"Unsupported productKey: {trimmed}");
        return trimmed;
    }
}
