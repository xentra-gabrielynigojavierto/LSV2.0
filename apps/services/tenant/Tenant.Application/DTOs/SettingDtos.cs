namespace Tenant.Application.DTOs;

// ── Settings management DTOs ──────────────────────────────────────────────────

/// <summary>Response returned for a single TenantSetting record.</summary>
public record SettingResponse(
    Guid     Id,
    Guid     TenantId,
    string   SettingKey,
    string   SettingValue,
    string   ValueType,
    string?  ProductKey,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Request body for upserting a setting by (TenantId, SettingKey, ProductKey).</summary>
public record UpsertSettingRequest(
    string  SettingKey,
    string  SettingValue,
    string  ValueType,
    string? ProductKey = null);
