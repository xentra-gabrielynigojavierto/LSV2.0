namespace Notifications.Application.DTOs;

// ── Recipient Reputation Snapshot ────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-016: API DTO for recipient reputation snapshot.
/// RecipientHash is an opaque 64-char hex token. Never a raw phone number.
/// </summary>
public sealed class SmsRecipientReputationDto
{
    public Guid    Id             { get; set; }
    /// <summary>Opaque HMAC-SHA256 token. Never a raw phone number.</summary>
    public string  RecipientHash  { get; set; } = string.Empty;
    public Guid?   TenantId       { get; set; }
    public string? ProviderType   { get; set; }
    public string? CountryCode    { get; set; }
    public string? Region         { get; set; }

    // Counts
    public int TotalAttempts              { get; set; }
    public int DeliveredAttempts          { get; set; }
    public int FailedAttempts             { get; set; }
    public int RetryAttempts              { get; set; }
    public int DeadLetterAttempts         { get; set; }
    public int CarrierRejectedAttempts    { get; set; }
    public int InvalidDestinationAttempts { get; set; }

    // Rates
    public decimal DeliverySuccessRate { get; set; }
    public decimal FailureRate         { get; set; }
    public decimal RetryRate           { get; set; }
    public decimal DeadLetterRate      { get; set; }
    public decimal CarrierFailureRate  { get; set; }

    // Scores
    public decimal InvalidNumberRisk    { get; set; }
    public decimal RetrySuppressionRisk { get; set; }
    public decimal QualityScore         { get; set; }

    // Classification
    public string DestinationRiskLevel { get; set; } = "low";

    public DateTime? LastAttemptAt { get; set; }
    public DateTime  CalculatedAt  { get; set; }
}

public sealed class SmsRecipientReputationListResult
{
    public IReadOnlyList<SmsRecipientReputationDto> Items { get; set; } = Array.Empty<SmsRecipientReputationDto>();
    public int Total  { get; set; }
    public int Limit  { get; set; }
    public int Offset { get; set; }
}

// ── Suppression Decision ──────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-016: API DTO for suppression decision audit.
/// RecipientHash is an opaque token. Never a raw phone number.
/// </summary>
public sealed class SmsSuppressionDecisionDto
{
    public Guid    Id             { get; set; }
    public string  RecipientHash  { get; set; } = string.Empty;
    public Guid?   TenantId       { get; set; }
    public Guid?   NotificationId { get; set; }
    public Guid?   AttemptId      { get; set; }
    public string  DecisionType   { get; set; } = string.Empty;
    public string  ReasonCode     { get; set; } = string.Empty;
    public decimal? RiskScore     { get; set; }
    public decimal? QualityScore  { get; set; }
    public int     RetryCount     { get; set; }
    public string? ProviderType   { get; set; }
    public string? CountryCode    { get; set; }
    public string? Region         { get; set; }
    public DateTime CreatedAt     { get; set; }
}

public sealed class SmsSuppressionDecisionListResult
{
    public IReadOnlyList<SmsSuppressionDecisionDto> Items { get; set; } = Array.Empty<SmsSuppressionDecisionDto>();
    public int Total  { get; set; }
    public int Limit  { get; set; }
    public int Offset { get; set; }
}

// ── Risk Distribution ─────────────────────────────────────────────────────────

public sealed class SmsDestinationRiskSummaryDto
{
    public long LowRiskCount        { get; set; }
    public long MediumRiskCount     { get; set; }
    public long HighRiskCount       { get; set; }
    public long SuppressedCount     { get; set; }
    public long TotalRecipients     { get; set; }
    public Dictionary<string, long> ByCountry   { get; set; } = new();
    public Dictionary<string, long> ByProvider  { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

// ── Failure Analytics ─────────────────────────────────────────────────────────

public sealed class SmsRecipientFailureDto
{
    public string  RecipientHash          { get; set; } = string.Empty;
    public Guid?   TenantId               { get; set; }
    public string? CountryCode            { get; set; }
    public decimal FailureRate            { get; set; }
    public decimal CarrierFailureRate     { get; set; }
    public decimal InvalidNumberRisk      { get; set; }
    public decimal RetrySuppressionRisk   { get; set; }
    public int     TotalAttempts          { get; set; }
    public int     FailedAttempts         { get; set; }
    public int     CarrierRejectedAttempts { get; set; }
    public int     InvalidDestinationAttempts { get; set; }
    public string  DestinationRiskLevel   { get; set; } = string.Empty;
    public DateTime? LastAttemptAt        { get; set; }
    public DateTime  CalculatedAt         { get; set; }
}

public sealed class SmsRecipientFailureListResult
{
    public IReadOnlyList<SmsRecipientFailureDto> Items { get; set; } = Array.Empty<SmsRecipientFailureDto>();
    public int Total  { get; set; }
    public int Limit  { get; set; }
    public int Offset { get; set; }
}

// ── Trend ─────────────────────────────────────────────────────────────────────

public sealed class SmsRecipientTrendPoint
{
    public DateTime WindowDate             { get; set; }
    public long     TotalRecipients        { get; set; }
    public decimal  AverageDeliveryRate    { get; set; }
    public decimal  AverageFailureRate     { get; set; }
    public decimal  AverageQualityScore    { get; set; }
    public long     SuppressedCount        { get; set; }
    public long     HighRiskCount          { get; set; }
}

public sealed class SmsRecipientTrendResult
{
    public IReadOnlyList<SmsRecipientTrendPoint> Points { get; set; } = Array.Empty<SmsRecipientTrendPoint>();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
