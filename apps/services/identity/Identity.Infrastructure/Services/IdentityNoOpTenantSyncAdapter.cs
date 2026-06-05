using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

/// <summary>
/// TENANT-B07 — No-op implementation of the Identity-side ITenantSyncAdapter.
///
/// Registered when Features:TenantDualWriteEnabled = false (the default).
/// Discards all calls with a Debug log entry; has zero runtime cost.
/// </summary>
public sealed class IdentityNoOpTenantSyncAdapter : ITenantSyncAdapter
{
    private readonly ILogger<IdentityNoOpTenantSyncAdapter> _logger;

    public IdentityNoOpTenantSyncAdapter(ILogger<IdentityNoOpTenantSyncAdapter> logger)
    {
        _logger = logger;
    }

    public Task SyncAsync(IdentityTenantSyncRequest request, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "[TenantDualWrite] Skipped (disabled) — {EventType} for TenantId={TenantId} Code={Code}. " +
            "Set Features:TenantDualWriteEnabled=true to activate.",
            request.EventType,
            request.TenantId,
            request.Code);

        return Task.CompletedTask;
    }
}
