namespace Tenant.Application.DTOs;

/// <summary>
/// Block 5 — Dual Write Preparation (TENANT-E00-S05).
///
/// Contract for propagating Identity tenant changes into the Tenant service.
/// This DTO is shared between the adapter interface and any future Identity-side caller.
///
/// Usage: Identity triggers ITenantSyncAdapter.SyncAsync(request) after any
/// tenant create/update. The NoOpTenantSyncAdapter ignores all calls until
/// Block 6 wires the real implementation.
///
/// Controlled by Features:TenantDualWriteEnabled (default: false).
/// </summary>
public record TenantSyncRequest(
    Guid    TenantId,
    string  Code,
    string  DisplayName,
    string  Status,
    string? Subdomain,
    Guid?   LogoDocumentId,
    Guid?   LogoWhiteDocumentId,
    DateTime? SourceCreatedAtUtc,
    DateTime? SourceUpdatedAtUtc,

    /// <summary>
    /// "Create" or "Update" — caller's intent. Adapter may override to upsert.
    /// </summary>
    string  EventType = "Update");
