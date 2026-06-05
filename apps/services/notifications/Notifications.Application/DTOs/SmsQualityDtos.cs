namespace Notifications.Application.DTOs;

/// <summary>LS-NOTIF-SMS-015: Response DTOs for SMS optimization analytics APIs.</summary>

// ── Quality ──────────────────────────────────────────────────────────────────

public sealed class SmsProviderQualityDto
{
    public string   ProviderType           { get; set; } = string.Empty;
    public string?  ProviderOwnershipMode  { get; set; }
    public string?  CountryCode            { get; set; }
    public string?  Region                 { get; set; }
    public decimal  QualityScore           { get; set; }
    public decimal? CostEfficiencyScore    { get; set; }
    public decimal  DeliverySuccessRate    { get; set; }
    public decimal  FailureRate            { get; set; }
    public decimal  RetryRate              { get; set; }
    public decimal  ReconciliationFailureRate { get; set; }
    public decimal? AverageLatencyMs       { get; set; }
    public decimal? AverageEffectiveCost   { get; set; }
    public decimal? CostPerDeliveredMessage { get; set; }
    public int      TotalAttempts          { get; set; }
    public int      DeliveredAttempts      { get; set; }
    public bool     HasSufficientData      { get; set; }
    public DateTime WindowStart            { get; set; }
    public DateTime WindowEnd              { get; set; }
    public DateTime CalculatedAt           { get; set; }
}

public sealed class SmsQualityListResponse
{
    public IReadOnlyList<SmsProviderQualityDto> Items { get; set; } = Array.Empty<SmsProviderQualityDto>();
    public int Total { get; set; }
}

// ── Quality trend ────────────────────────────────────────────────────────────

public sealed class SmsQualityTrendPoint
{
    public string   ProviderType   { get; set; } = string.Empty;
    public string?  CountryCode    { get; set; }
    public decimal  QualityScore   { get; set; }
    public DateTime CalculatedAt   { get; set; }
    public int      TotalAttempts  { get; set; }
}

public sealed class SmsQualityTrendResponse
{
    public IReadOnlyList<SmsQualityTrendPoint> Items { get; set; } = Array.Empty<SmsQualityTrendPoint>();
    public int Total { get; set; }
}

// ── Latency ──────────────────────────────────────────────────────────────────

public sealed class SmsLatencyDto
{
    public string   ProviderType        { get; set; } = string.Empty;
    public string?  CountryCode         { get; set; }
    public string?  Region              { get; set; }
    public decimal? AverageLatencyMs    { get; set; }
    public int      TotalAttempts       { get; set; }
    public DateTime WindowStart         { get; set; }
    public DateTime WindowEnd           { get; set; }
    public DateTime CalculatedAt        { get; set; }
}

public sealed class SmsLatencyListResponse
{
    public IReadOnlyList<SmsLatencyDto> Items { get; set; } = Array.Empty<SmsLatencyDto>();
    public int Total { get; set; }
}

// ── Regional ─────────────────────────────────────────────────────────────────

public sealed class SmsRegionalDto
{
    public string?  CountryCode            { get; set; }
    public string?  Region                 { get; set; }
    public string   ProviderType           { get; set; } = string.Empty;
    public decimal  DeliverySuccessRate    { get; set; }
    public decimal  QualityScore           { get; set; }
    public decimal? AverageLatencyMs       { get; set; }
    public int      TotalAttempts          { get; set; }
    public DateTime CalculatedAt           { get; set; }
}

public sealed class SmsRegionalListResponse
{
    public IReadOnlyList<SmsRegionalDto> Items { get; set; } = Array.Empty<SmsRegionalDto>();
    public int Total { get; set; }
}

// ── Optimization summary ─────────────────────────────────────────────────────

public sealed class SmsOptimizationInsight
{
    public string   ProviderType           { get; set; } = string.Empty;
    public decimal  QualityScore           { get; set; }
    public decimal? CostEfficiencyScore    { get; set; }
    public decimal  DeliverySuccessRate    { get; set; }
    public decimal? AverageLatencyMs       { get; set; }
    public decimal? CostPerDeliveredMessage { get; set; }
    public string   Recommendation         { get; set; } = string.Empty;
    public int      TotalAttempts          { get; set; }
    public bool     HasSufficientData      { get; set; }
}

public sealed class SmsOptimizationResponse
{
    public IReadOnlyList<SmsOptimizationInsight> Providers { get; set; } = Array.Empty<SmsOptimizationInsight>();
    public string? TopQualityProvider              { get; set; }
    public string? TopCostEfficiencyProvider       { get; set; }
    public string? TopBalancedProvider             { get; set; }
    public DateTime GeneratedAt                    { get; set; } = DateTime.UtcNow;
    public string  DataSummary                     { get; set; } = string.Empty;
}
