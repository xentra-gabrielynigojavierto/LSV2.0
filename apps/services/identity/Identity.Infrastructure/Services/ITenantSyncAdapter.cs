namespace Identity.Infrastructure.Services;

/// <summary>
/// TENANT-B07 — Identity-side dual-write abstraction.
///
/// Propagates Identity tenant create/update events into the Tenant service.
/// Two implementations:
///   - IdentityNoOpTenantSyncAdapter  : registered when Features:TenantDualWriteEnabled = false (default)
///   - HttpTenantSyncAdapter          : registered when Features:TenantDualWriteEnabled = true
///
/// This interface is intentionally local to Identity.Infrastructure so that
/// Identity does not take a compile-time dependency on Tenant.Application.
/// The wire contract is the HTTP body of POST /api/internal/tenant-sync/upsert
/// on the Tenant service.
/// </summary>
public interface ITenantSyncAdapter
{
    /// <summary>
    /// Propagates a tenant create or update from Identity into the Tenant service.
    /// Must be idempotent and safe to call multiple times for the same TenantId.
    /// </summary>
    Task SyncAsync(IdentityTenantSyncRequest request, CancellationToken ct = default);
}

/// <summary>
/// Data contract for Identity → Tenant sync calls.
/// Must stay compatible with the TenantSyncRequest shape accepted by
/// Tenant service POST /api/internal/tenant-sync/upsert.
/// </summary>
public record IdentityTenantSyncRequest(
    Guid      TenantId,
    string    Code,
    string    DisplayName,
    string    Status,
    string?   Subdomain,
    Guid?     LogoDocumentId,
    Guid?     LogoWhiteDocumentId,
    DateTime? SourceCreatedAtUtc,
    DateTime? SourceUpdatedAtUtc,

    /// <summary>"Create" or "Update". Tenant service treats both as upsert.</summary>
    string    EventType = "Update");
