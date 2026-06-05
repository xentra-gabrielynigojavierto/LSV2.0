using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public class EntitlementService : IEntitlementService
{
    private readonly IEntitlementRepository _entitlements;
    private readonly ITenantRepository      _tenants;

    public EntitlementService(IEntitlementRepository entitlements, ITenantRepository tenants)
    {
        _entitlements = entitlements;
        _tenants      = tenants;
    }

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<List<EntitlementResponse>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var records = await _entitlements.ListByTenantAsync(tenantId, ct);
        return records.Select(ToResponse).ToList();
    }

    // ── Get ───────────────────────────────────────────────────────────────────

    public async Task<EntitlementResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var record = await RequireEntitlementAsync(tenantId, id, ct);
        return ToResponse(record);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<EntitlementResponse> CreateAsync(
        Guid                     tenantId,
        CreateEntitlementRequest request,
        CancellationToken        ct = default)
    {
        await RequireTenantAsync(tenantId, ct);

        var normalizedKey = TenantProductEntitlement.NormalizeKey(request.ProductKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            throw new ValidationException("ProductKey is required.",
                new Dictionary<string, string[]> { ["productKey"] = ["ProductKey cannot be empty."] });

        var existing = await _entitlements.GetByTenantAndProductKeyAsync(tenantId, normalizedKey, ct);
        if (existing is not null)
            throw new ConflictException($"An entitlement for product '{normalizedKey}' already exists for this tenant.");

        ValidateEffectiveDates(request.EffectiveFromUtc, request.EffectiveToUtc);

        // If this new entitlement should be default, auto-demote prior defaults first.
        if (request.IsDefault)
            await DemotePriorDefaultsAsync(tenantId, null, ct);

        var entitlement = TenantProductEntitlement.Create(
            tenantId,
            normalizedKey,
            request.ProductDisplayName,
            request.IsEnabled,
            request.IsDefault,
            request.PlanCode,
            request.EffectiveFromUtc,
            request.EffectiveToUtc);

        await _entitlements.AddAsync(entitlement, ct);
        return ToResponse(entitlement);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<EntitlementResponse> UpdateAsync(
        Guid                     tenantId,
        Guid                     id,
        UpdateEntitlementRequest request,
        CancellationToken        ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var entitlement = await RequireEntitlementAsync(tenantId, id, ct);

        var normalizedKey = TenantProductEntitlement.NormalizeKey(request.ProductKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            throw new ValidationException("ProductKey is required.",
                new Dictionary<string, string[]> { ["productKey"] = ["ProductKey cannot be empty."] });

        // Reject key change if another entitlement already owns the new key.
        if (!string.Equals(entitlement.ProductKey, normalizedKey, StringComparison.Ordinal))
        {
            var conflict = await _entitlements.GetByTenantAndProductKeyAsync(tenantId, normalizedKey, ct);
            if (conflict is not null)
                throw new ConflictException($"An entitlement for product '{normalizedKey}' already exists for this tenant.");
        }

        ValidateEffectiveDates(request.EffectiveFromUtc, request.EffectiveToUtc);

        // If disabling and this was the default, clear default.
        if (!request.IsEnabled && entitlement.IsDefault)
            entitlement.ClearDefault();

        entitlement.Update(normalizedKey, request.ProductDisplayName, request.IsEnabled,
            request.PlanCode, request.EffectiveFromUtc, request.EffectiveToUtc);

        await _entitlements.UpdateAsync(entitlement, ct);
        return ToResponse(entitlement);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var entitlement = await RequireEntitlementAsync(tenantId, id, ct);
        await _entitlements.DeleteAsync(entitlement, ct);
    }

    // ── BLK-TS-02: Idempotent activate ───────────────────────────────────────

    public async Task<EntitlementResponse> ActivateProductAsync(
        Guid              tenantId,
        string            productKey,
        string?           displayName = null,
        CancellationToken ct          = default)
    {
        await RequireTenantAsync(tenantId, ct);

        var normalizedKey = TenantProductEntitlement.NormalizeKey(productKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            throw new ValidationException("ProductKey is required.",
                new Dictionary<string, string[]> { ["productKey"] = ["ProductKey cannot be empty."] });

        var existing = await _entitlements.GetByTenantAndProductKeyAsync(tenantId, normalizedKey, ct);

        if (existing is not null)
        {
            // Idempotent: already exists. Enable it if disabled.
            if (!existing.IsEnabled)
            {
                existing.Enable();
                await _entitlements.UpdateAsync(existing, ct);
            }
            return ToResponse(existing);
        }

        // Create new entitlement — enabled immediately.
        var entitlement = TenantProductEntitlement.Create(
            tenantId:           tenantId,
            productKey:         normalizedKey,
            productDisplayName: displayName?.Trim(),
            isEnabled:          true,
            isDefault:          false,
            effectiveFromUtc:   DateTime.UtcNow);

        await _entitlements.AddAsync(entitlement, ct);
        return ToResponse(entitlement);
    }

    // ── BLK-TS-02: Active check ───────────────────────────────────────────────

    public async Task<bool> IsProductActiveAsync(
        Guid              tenantId,
        string            productKey,
        CancellationToken ct = default)
    {
        var normalizedKey = TenantProductEntitlement.NormalizeKey(productKey);
        var existing = await _entitlements.GetByTenantAndProductKeyAsync(tenantId, normalizedKey, ct);
        return existing is { IsEnabled: true };
    }

    // ── Set default ───────────────────────────────────────────────────────────

    public async Task<EntitlementResponse> SetDefaultAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var entitlement = await RequireEntitlementAsync(tenantId, id, ct);

        if (!entitlement.IsEnabled)
            throw new ValidationException("Cannot set a disabled entitlement as the default product.",
                new Dictionary<string, string[]> { ["isEnabled"] = ["The entitlement must be enabled before it can be set as default."] });

        // Auto-demote prior defaults (excluding this one).
        await DemotePriorDefaultsAsync(tenantId, id, ct);

        entitlement.SetDefault();
        await _entitlements.UpdateAsync(entitlement, ct);
        return ToResponse(entitlement);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task RequireTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            throw new NotFoundException($"Tenant '{tenantId}' was not found.");
    }

    private async Task<TenantProductEntitlement> RequireEntitlementAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var record = await _entitlements.GetByIdAsync(id, ct);
        if (record is null || record.TenantId != tenantId)
            throw new NotFoundException($"Entitlement '{id}' was not found.");
        return record;
    }

    /// <summary>
    /// Clears IsDefault on any entitlements for the tenant except <paramref name="excludeId"/>.
    /// Enforces the single-default-per-tenant invariant.
    /// </summary>
    private async Task DemotePriorDefaultsAsync(Guid tenantId, Guid? excludeId, CancellationToken ct)
    {
        var defaults = await _entitlements.GetDefaultsForTenantAsync(tenantId, ct);
        var toDemote = defaults
            .Where(e => excludeId == null || e.Id != excludeId)
            .ToList();

        if (toDemote.Count == 0) return;

        foreach (var e in toDemote)
            e.ClearDefault();

        await _entitlements.UpdateRangeAsync(toDemote, ct);
    }

    private static void ValidateEffectiveDates(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue && to.Value <= from.Value)
            throw new ValidationException("EffectiveToUtc must be after EffectiveFromUtc.",
                new Dictionary<string, string[]>
                {
                    ["effectiveToUtc"] = ["EffectiveToUtc must be later than EffectiveFromUtc."]
                });
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    internal static EntitlementResponse ToResponse(TenantProductEntitlement e) => new(
        e.Id,
        e.TenantId,
        e.ProductKey,
        e.ProductDisplayName,
        e.IsEnabled,
        e.IsDefault,
        e.PlanCode,
        e.EffectiveFromUtc,
        e.EffectiveToUtc,
        e.CreatedAtUtc,
        e.UpdatedAtUtc);
}
