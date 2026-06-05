using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public class SettingService : ISettingService
{
    private readonly ISettingRepository _settings;
    private readonly ITenantRepository  _tenants;

    public SettingService(ISettingRepository settings, ITenantRepository tenants)
    {
        _settings = settings;
        _tenants  = tenants;
    }

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<List<SettingResponse>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var records = await _settings.ListByTenantAsync(tenantId, ct);
        return records.Select(ToResponse).ToList();
    }

    // ── Get ───────────────────────────────────────────────────────────────────

    public async Task<SettingResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var record = await RequireSettingAsync(tenantId, id, ct);
        return ToResponse(record);
    }

    // ── Upsert ────────────────────────────────────────────────────────────────

    public async Task<SettingResponse> UpsertAsync(
        Guid                 tenantId,
        UpsertSettingRequest request,
        CancellationToken    ct = default)
    {
        await RequireTenantAsync(tenantId, ct);

        var normalizedKey = TenantSetting.NormalizeKey(request.SettingKey);
        if (!TenantSetting.IsValidKey(normalizedKey))
            throw new ValidationException(
                "SettingKey must be dot-namespaced (e.g. 'platform.default-product').",
                new Dictionary<string, string[]> { ["settingKey"] = ["SettingKey must contain at least one dot."] });

        if (!Enum.TryParse<SettingValueType>(request.ValueType, ignoreCase: true, out var valueType))
            throw new ValidationException(
                $"Invalid valueType '{request.ValueType}'.",
                new Dictionary<string, string[]>
                {
                    ["valueType"] = [$"'{request.ValueType}' is not a valid ValueType. Allowed: String, Boolean, Number, Json."]
                });

        var normalizedProductKey = string.IsNullOrWhiteSpace(request.ProductKey)
            ? null
            : request.ProductKey.Trim().ToLowerInvariant();

        // Upsert: find existing or create new.
        var existing = await _settings.GetByKeyAsync(tenantId, normalizedKey, normalizedProductKey, ct);
        if (existing is not null)
        {
            existing.UpdateValue(request.SettingValue ?? string.Empty, valueType);
            await _settings.UpdateAsync(existing, ct);
            return ToResponse(existing);
        }

        var setting = TenantSetting.Create(
            tenantId, normalizedKey, request.SettingValue ?? string.Empty, valueType, normalizedProductKey);
        await _settings.AddAsync(setting, ct);
        return ToResponse(setting);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var setting = await RequireSettingAsync(tenantId, id, ct);
        await _settings.DeleteAsync(setting, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task RequireTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            throw new NotFoundException($"Tenant '{tenantId}' was not found.");
    }

    private async Task<TenantSetting> RequireSettingAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var record = await _settings.GetByIdAsync(id, ct);
        if (record is null || record.TenantId != tenantId)
            throw new NotFoundException($"Setting '{id}' was not found.");
        return record;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    internal static SettingResponse ToResponse(TenantSetting s) => new(
        s.Id,
        s.TenantId,
        s.SettingKey,
        s.SettingValue,
        s.ValueType.ToString(),
        s.ProductKey,
        s.CreatedAtUtc,
        s.UpdatedAtUtc);
}
