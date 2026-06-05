namespace Notifications.Application.DTOs;

// ── Provider Capability ───────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-014: Static metadata describing what an SMS provider supports.
/// Config-driven initially; no external calls made.
/// </summary>
public sealed class SmsProviderCapabilityDto
{
    public string ProviderType              { get; set; } = string.Empty;
    public string DisplayName               { get; set; } = string.Empty;
    public bool   SupportsSend              { get; set; }
    public bool   SupportsStatusLookup      { get; set; }
    public bool   SupportsHealthCheck       { get; set; }
    public bool   SupportsCostEstimate      { get; set; }
    public bool   SupportsRegionalRouting   { get; set; }
    public bool   SupportsTenantOwnedConfig { get; set; }
    public bool   SupportsPlatformConfig    { get; set; }

    /// <summary>ISO country code list (comma-separated) or null for worldwide.</summary>
    public string? SupportedCountries { get; set; }

    public string? DefaultCurrency { get; set; }
    public string? Notes            { get; set; }
}

// ── Routing Policy ────────────────────────────────────────────────────────────

public sealed class SmsRoutingPolicyDto
{
    public Guid    Id              { get; set; }
    public Guid?   TenantId        { get; set; }
    public string  Name            { get; set; } = string.Empty;
    public bool    Enabled         { get; set; }
    public string? Region          { get; set; }
    public string? CountryCode     { get; set; }
    public string  RoutingMode     { get; set; } = string.Empty;

    /// <summary>Ordered list of preferred providers (JSON string array).</summary>
    public string? PreferredProvidersJson { get; set; }

    /// <summary>Providers to exclude from candidate list (JSON string array).</summary>
    public string? ExcludedProvidersJson  { get; set; }

    public decimal? MaxEstimatedCostPerMessage { get; set; }
    public bool     RequireHealthyProvider     { get; set; }
    public bool     FallbackToPlatform         { get; set; }
    public int      Priority                   { get; set; }

    public DateTime  CreatedAt  { get; set; }
    public DateTime  UpdatedAt  { get; set; }
    public string?   CreatedBy  { get; set; }
    public string?   UpdatedBy  { get; set; }
}

public sealed class CreateSmsRoutingPolicyRequest
{
    public Guid?   TenantId    { get; set; }
    public string  Name        { get; set; } = string.Empty;
    public bool    Enabled     { get; set; } = true;
    public string? Region      { get; set; }
    public string? CountryCode { get; set; }

    /// <summary>priority | cost_optimized | health_optimized | hybrid | regional</summary>
    public string  RoutingMode { get; set; } = "priority";

    public string? PreferredProvidersJson      { get; set; }
    public string? ExcludedProvidersJson       { get; set; }
    public decimal? MaxEstimatedCostPerMessage { get; set; }
    public bool    RequireHealthyProvider      { get; set; } = false;
    public bool    FallbackToPlatform          { get; set; } = true;
    public int     Priority                    { get; set; } = 0;
}

public sealed class UpdateSmsRoutingPolicyRequest
{
    public string  Name        { get; set; } = string.Empty;
    public bool    Enabled     { get; set; }
    public string? Region      { get; set; }
    public string? CountryCode { get; set; }
    public string  RoutingMode { get; set; } = "priority";
    public string? PreferredProvidersJson      { get; set; }
    public string? ExcludedProvidersJson       { get; set; }
    public decimal? MaxEstimatedCostPerMessage { get; set; }
    public bool    RequireHealthyProvider      { get; set; }
    public bool    FallbackToPlatform          { get; set; }
    public int     Priority                    { get; set; }
}

public sealed class SmsRoutingPolicyListResult
{
    public IReadOnlyList<SmsRoutingPolicyDto> Items { get; set; } = Array.Empty<SmsRoutingPolicyDto>();
    public int Total  { get; set; }
    public int Limit  { get; set; }
    public int Offset { get; set; }
}

public sealed class SmsRoutingPolicyQuery
{
    public Guid?   TenantId    { get; set; }
    public bool?   Enabled     { get; set; }
    public string? RoutingMode { get; set; }
    public int     Limit       { get; set; } = 50;
    public int     Offset      { get; set; } = 0;
}

// ── Routing Decision ──────────────────────────────────────────────────────────

public sealed class SmsRoutingDecisionDto
{
    public Guid    Id                        { get; set; }
    public Guid?   TenantId                  { get; set; }
    public Guid?   NotificationId            { get; set; }
    public Guid?   AttemptId                 { get; set; }
    public Guid?   RoutingPolicyId           { get; set; }
    public string  RoutingMode               { get; set; } = string.Empty;
    public string  SelectedProvider          { get; set; } = string.Empty;
    public Guid?   SelectedProviderConfigId  { get; set; }
    public string? ProviderOwnershipMode     { get; set; }
    public string? CandidateProvidersJson    { get; set; }
    public string? ExcludedProvidersJson     { get; set; }
    public string  DecisionReason            { get; set; } = string.Empty;
    public decimal? EstimatedCostAmount      { get; set; }
    public string?  CostCurrency             { get; set; }
    public string?  Region                   { get; set; }
    public string?  CountryCode              { get; set; }
    public DateTime CreatedAt                { get; set; }
}

public sealed class SmsRoutingDecisionListResult
{
    public IReadOnlyList<SmsRoutingDecisionDto> Items { get; set; } = Array.Empty<SmsRoutingDecisionDto>();
    public int Total  { get; set; }
    public int Limit  { get; set; }
    public int Offset { get; set; }
}

public sealed class SmsRoutingDecisionQuery
{
    public Guid?   TenantId        { get; set; }
    public Guid?   NotificationId  { get; set; }
    public string? Provider        { get; set; }
    public string? RoutingMode     { get; set; }
    public Guid?   PolicyId        { get; set; }
    public DateTime? From          { get; set; }
    public DateTime? To            { get; set; }
    public int     Limit           { get; set; } = 50;
    public int     Offset          { get; set; } = 0;
}

public sealed class SmsRoutingDecisionSummaryDto
{
    public long   TotalDecisions        { get; set; }
    public Dictionary<string, long> ByMode     { get; set; } = new();
    public Dictionary<string, long> ByProvider { get; set; } = new();
    public long   PriorityModeCount     { get; set; }
    public long   CostOptimizedCount    { get; set; }
    public long   HealthOptimizedCount  { get; set; }
    public long   HybridCount           { get; set; }
    public long   RegionalCount         { get; set; }
    public long   NoRouteCount          { get; set; }
}

// ── Provider Health for routing admin view ────────────────────────────────────

public sealed class SmsProviderHealthDto
{
    public string  ProviderType      { get; set; } = string.Empty;
    public string? OwnershipMode     { get; set; }
    public Guid?   ProviderConfigId  { get; set; }
    public string  HealthStatus      { get; set; } = "unknown";
    public int?    LatencyMs         { get; set; }
    public DateTime? CheckedAt       { get; set; }
}
