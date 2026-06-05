using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

/// <summary>
/// Block 5 — Dual Write Preparation (TENANT-E00-S05).
///
/// Abstraction for propagating Identity tenant changes into the Tenant service.
/// Registered as a no-op by default; wire-up to a real implementation is deferred to Block 6.
///
/// Controlled by Features:TenantDualWriteEnabled (default: false).
/// When the flag is false, the registered NoOpTenantSyncAdapter discards all calls silently.
/// </summary>
public interface ITenantSyncAdapter
{
    /// <summary>
    /// Propagates a tenant create or update from Identity into the Tenant service.
    /// Must be idempotent and safe to call multiple times.
    /// </summary>
    Task SyncAsync(TenantSyncRequest request, CancellationToken ct = default);
}
