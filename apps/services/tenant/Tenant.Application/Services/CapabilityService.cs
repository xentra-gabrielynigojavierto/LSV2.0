using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public class CapabilityService : ICapabilityService
{
    private readonly ICapabilityRepository  _capabilities;
    private readonly ITenantRepository      _tenants;
    private readonly IEntitlementRepository _entitlements;

    public CapabilityService(
        ICapabilityRepository  capabilities,
        ITenantRepository      tenants,
        IEntitlementRepository entitlements)
    {
        _capabilities = capabilities;
        _tenants      = tenants;
        _entitlements = entitlements;
    }

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<List<CapabilityResponse>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var records = await _capabilities.ListByTenantAsync(tenantId, ct);
        return records.Select(ToResponse).ToList();
    }

    // ── Get ───────────────────────────────────────────────────────────────────

    public async Task<CapabilityResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var record = await RequireCapabilityAsync(tenantId, id, ct);
        return ToResponse(record);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<CapabilityResponse> CreateAsync(
        Guid                    tenantId,
        CreateCapabilityRequest request,
        CancellationToken       ct = default)
    {
        await RequireTenantAsync(tenantId, ct);

        var normalizedKey = TenantCapability.NormalizeKey(request.CapabilityKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            throw new ValidationException("CapabilityKey is required.",
                new Dictionary<string, string[]> { ["capabilityKey"] = ["CapabilityKey cannot be empty."] });

        // If product-scoped, verify the entitlement exists and belongs to this tenant.
        if (request.ProductEntitlementId.HasValue)
            await RequireEntitlementForTenantAsync(tenantId, request.ProductEntitlementId.Value, ct);

        // Uniqueness: (tenant, key, productEntitlementId)
        var existing = await _capabilities.GetByKeyAsync(tenantId, normalizedKey, request.ProductEntitlementId, ct);
        if (existing is not null)
            throw new ConflictException($"Capability '{normalizedKey}' already exists for this tenant/scope.");

        var capability = TenantCapability.Create(tenantId, normalizedKey, request.IsEnabled, request.ProductEntitlementId);
        await _capabilities.AddAsync(capability, ct);
        return ToResponse(capability);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<CapabilityResponse> UpdateAsync(
        Guid                    tenantId,
        Guid                    id,
        UpdateCapabilityRequest request,
        CancellationToken       ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var capability = await RequireCapabilityAsync(tenantId, id, ct);

        var normalizedKey = TenantCapability.NormalizeKey(request.CapabilityKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            throw new ValidationException("CapabilityKey is required.",
                new Dictionary<string, string[]> { ["capabilityKey"] = ["CapabilityKey cannot be empty."] });

        // If product-scoped, verify the entitlement exists and belongs to this tenant.
        if (request.ProductEntitlementId.HasValue)
            await RequireEntitlementForTenantAsync(tenantId, request.ProductEntitlementId.Value, ct);

        // Uniqueness: reject if another capability already occupies the new key+scope.
        var keyOrScopeChanged =
            !string.Equals(capability.CapabilityKey, normalizedKey, StringComparison.Ordinal) ||
            capability.ProductEntitlementId != request.ProductEntitlementId;

        if (keyOrScopeChanged)
        {
            var conflict = await _capabilities.GetByKeyAsync(tenantId, normalizedKey, request.ProductEntitlementId, ct);
            if (conflict is not null && conflict.Id != id)
                throw new ConflictException($"Capability '{normalizedKey}' already exists for this tenant/scope.");
        }

        capability.Update(normalizedKey, request.IsEnabled, request.ProductEntitlementId);
        await _capabilities.UpdateAsync(capability, ct);
        return ToResponse(capability);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var capability = await RequireCapabilityAsync(tenantId, id, ct);
        await _capabilities.DeleteAsync(capability, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task RequireTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            throw new NotFoundException($"Tenant '{tenantId}' was not found.");
    }

    private async Task<TenantCapability> RequireCapabilityAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var record = await _capabilities.GetByIdAsync(id, ct);
        if (record is null || record.TenantId != tenantId)
            throw new NotFoundException($"Capability '{id}' was not found.");
        return record;
    }

    private async Task RequireEntitlementForTenantAsync(Guid tenantId, Guid entitlementId, CancellationToken ct)
    {
        var entitlement = await _entitlements.GetByIdAsync(entitlementId, ct);
        if (entitlement is null || entitlement.TenantId != tenantId)
            throw new ValidationException(
                $"ProductEntitlementId '{entitlementId}' is not valid for this tenant.",
                new Dictionary<string, string[]>
                {
                    ["productEntitlementId"] = [$"Entitlement '{entitlementId}' was not found for this tenant."]
                });
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    internal static CapabilityResponse ToResponse(TenantCapability c) => new(
        c.Id,
        c.TenantId,
        c.ProductEntitlementId,
        c.CapabilityKey,
        c.IsEnabled,
        c.CreatedAtUtc,
        c.UpdatedAtUtc);
}
