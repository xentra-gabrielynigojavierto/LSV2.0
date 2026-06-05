using Microsoft.Extensions.Logging;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Infrastructure.Services;

/// <summary>
/// Block 5 — Dual Write Preparation (TENANT-E00-S05).
///
/// No-op implementation of ITenantSyncAdapter.
/// Registered by default; discards all calls silently unless
/// Features:TenantDualWriteEnabled is true.
///
/// Wire-up from Identity is deferred to Block 6.
/// </summary>
public class NoOpTenantSyncAdapter : ITenantSyncAdapter
{
    private readonly ILogger<NoOpTenantSyncAdapter> _logger;

    public NoOpTenantSyncAdapter(ILogger<NoOpTenantSyncAdapter> logger)
    {
        _logger = logger;
    }

    public Task SyncAsync(TenantSyncRequest request, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "TenantSyncAdapter (no-op): received {EventType} for TenantId={TenantId} Code={Code}. " +
            "Dual-write is disabled. Wire-up deferred to Block 6.",
            request.EventType,
            request.TenantId,
            request.Code);

        return Task.CompletedTask;
    }
}
