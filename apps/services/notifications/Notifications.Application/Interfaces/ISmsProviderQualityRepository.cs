using Notifications.Domain;

namespace Notifications.Application.Interfaces;

/// <summary>LS-NOTIF-SMS-015: Quality snapshot query parameters.</summary>
public sealed class SmsQualitySnapshotQuery
{
    public string? ProviderType         { get; set; }
    public Guid?   ProviderConfigId     { get; set; }
    public string? ProviderOwnershipMode { get; set; }
    public Guid?   TenantId             { get; set; }
    public string? CountryCode          { get; set; }
    public string? Region               { get; set; }
    public DateTime? From               { get; set; }
    public DateTime? To                 { get; set; }
    public int Limit                    { get; set; } = 100;
    public int Offset                   { get; set; } = 0;
}

/// <summary>
/// LS-NOTIF-SMS-015: Repository for SmsProviderQualitySnapshot.
/// Read-only query surface plus snapshot upsert.
/// </summary>
public interface ISmsProviderQualityRepository
{
    /// <summary>Persist a batch of snapshots (insert or replace by natural key).</summary>
    Task SaveSnapshotsAsync(IReadOnlyList<SmsProviderQualitySnapshot> snapshots, CancellationToken ct);

    /// <summary>Get the single latest snapshot for a provider/tenant/country combination.</summary>
    Task<SmsProviderQualitySnapshot?> GetLatestAsync(
        string providerType,
        Guid?  tenantId,
        Guid?  providerConfigId,
        string? countryCode,
        CancellationToken ct);

    /// <summary>Query snapshots with optional filters. Returns most-recent first.</summary>
    Task<IReadOnlyList<SmsProviderQualitySnapshot>> QueryAsync(
        SmsQualitySnapshotQuery query,
        CancellationToken ct);

    /// <summary>
    /// Get the latest snapshot per distinct provider (for quality leaderboard / dashboard card).
    /// Optionally scoped to a tenant and/or country.
    /// </summary>
    Task<IReadOnlyList<SmsProviderQualitySnapshot>> GetLatestPerProviderAsync(
        Guid?   tenantId,
        string? countryCode,
        CancellationToken ct);
}
