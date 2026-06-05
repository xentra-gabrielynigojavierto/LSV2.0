using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

/// <summary>
/// TENANT-B11/B12 — Admin-focused aggregation service.
///
/// B11: Aggregates tenant + branding + entitlements + domains + capabilities +
///      settings from Tenant repositories, plus Identity compat read-through.
///
/// B12: Adds canonical tenant creation (Tenant-first) and entitlement toggle.
///      CreateTenantAsync: creates canonical Tenant record, then calls
///      IIdentityProvisioningAdapter to handle downstream auth/provisioning work.
///      ToggleEntitlementAsync: upserts TenantProductEntitlement, then best-effort
///      syncs to Identity via IIdentityCompatAdapter/sync path.
/// </summary>
public class TenantAdminService : ITenantAdminService
{
    private readonly ITenantRepository         _tenantRepo;
    private readonly IBrandingRepository       _brandingRepo;
    private readonly IEntitlementRepository    _entitlementRepo;
    private readonly IDomainRepository         _domainRepo;
    private readonly ICapabilityRepository     _capabilityRepo;
    private readonly ISettingRepository        _settingRepo;
    private readonly IIdentityCompatAdapter    _identityCompat;
    private readonly IIdentityProvisioningAdapter _identityProvisioning;

    public TenantAdminService(
        ITenantRepository            tenantRepo,
        IBrandingRepository          brandingRepo,
        IEntitlementRepository       entitlementRepo,
        IDomainRepository            domainRepo,
        ICapabilityRepository        capabilityRepo,
        ISettingRepository           settingRepo,
        IIdentityCompatAdapter       identityCompat,
        IIdentityProvisioningAdapter identityProvisioning)
    {
        _tenantRepo           = tenantRepo;
        _brandingRepo         = brandingRepo;
        _entitlementRepo      = entitlementRepo;
        _domainRepo           = domainRepo;
        _capabilityRepo       = capabilityRepo;
        _settingRepo          = settingRepo;
        _identityCompat       = identityCompat;
        _identityProvisioning = identityProvisioning;
    }

    // ── B11: List ─────────────────────────────────────────────────────────────

    public async Task<(List<TenantAdminSummaryResponse> Items, int Total)> ListAdminAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1)       page     = 1;
        if (pageSize < 1)   pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var (tenants, total) = await _tenantRepo.ListAsync(page, pageSize, ct);
        var items = tenants.Select(ToSummary).ToList();
        return (items, total);
    }

    // ── B11: Detail ───────────────────────────────────────────────────────────

    public async Task<TenantAdminDetailResponse?> GetAdminDetailAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _tenantRepo.GetByIdAsync(id, ct);
        if (tenant is null) return null;

        var brandingTask       = _brandingRepo.GetByTenantIdAsync(id, ct);
        var entitlementsTask   = _entitlementRepo.ListByTenantAsync(id, ct);
        var domainsTask        = _domainRepo.ListByTenantAsync(id, ct);
        var capabilitiesTask   = _capabilityRepo.ListByTenantAsync(id, ct);
        var settingsTask       = _settingRepo.ListByTenantAsync(id, ct);
        var sessionTimeoutTask = _identityCompat.GetSessionTimeoutMinutesAsync(id, ct);

        await Task.WhenAll(brandingTask, entitlementsTask, domainsTask, capabilitiesTask, settingsTask, sessionTimeoutTask);

        var branding       = await brandingTask;
        var entitlements   = await entitlementsTask;
        var domains        = await domainsTask;
        var capabilities   = await capabilitiesTask;
        var settings       = await settingsTask;
        var sessionTimeout = await sessionTimeoutTask;

        var logoDocumentId      = branding?.LogoDocumentId      ?? tenant.LogoDocumentId;
        var logoWhiteDocumentId = branding?.LogoWhiteDocumentId ?? tenant.LogoWhiteDocumentId;

        var entitlementItems = entitlements
            .Select(e => new AdminEntitlementItem(
                ProductCode:  e.ProductKey,
                ProductName:  e.ProductDisplayName ?? e.ProductKey,
                Enabled:      e.IsEnabled,
                Status:       e.IsEnabled ? "Active" : "Disabled",
                EnabledAtUtc: e.EffectiveFromUtc))
            .ToList<AdminEntitlementItem>();

        var defaultProductSetting = settings.FirstOrDefault(s => s.SettingKey == "default_product");
        var localeSetting         = settings.FirstOrDefault(s => s.SettingKey == "locale");
        var timeZoneSetting       = settings.FirstOrDefault(s => s.SettingKey == "timezone");

        var settingsSummary = new TenantAdminSettingsSummary(
            DefaultProduct: defaultProductSetting?.SettingValue ?? tenant.Locale,
            Locale:         localeSetting?.SettingValue         ?? tenant.Locale,
            TimeZone:       timeZoneSetting?.SettingValue       ?? tenant.TimeZone);

        var brandingSummary = branding is null ? null : new TenantAdminBrandingSummary(
            BrandName:           branding.BrandName,
            PrimaryColor:        branding.PrimaryColor,
            LogoDocumentId:      branding.LogoDocumentId,
            LogoWhiteDocumentId: branding.LogoWhiteDocumentId);

        var compatSource = sessionTimeout.HasValue ? "IdentityCompat" : "Unavailable";

        return new TenantAdminDetailResponse(
            Id:                  tenant.Id,
            Code:                tenant.Code,
            DisplayName:         tenant.DisplayName,
            Status:              tenant.Status.ToString(),
            IsActive:            tenant.Status == TenantStatus.Active,
            Type:                "LawFirm",
            PrimaryContactName:  "",
            UserCount:           0,
            OrgCount:            0,
            ActiveUserCount:     0,
            LinkedOrgCount:      0,
            Email:               tenant.SupportEmail,
            Subdomain:           tenant.Subdomain,
            CreatedAtUtc:        tenant.CreatedAtUtc,
            UpdatedAtUtc:        tenant.UpdatedAtUtc,
            LogoDocumentId:      logoDocumentId,
            LogoWhiteDocumentId: logoWhiteDocumentId,
            SessionTimeoutMinutes: sessionTimeout,
            IdentityCompatSource: compatSource,
            ProductEntitlements:  entitlementItems,
            DomainCount:         domains.Count,
            CapabilityCount:     capabilities.Count,
            SettingsSummary:     settingsSummary,
            BrandingSummary:     brandingSummary);
    }

    // ── B11: Status update ────────────────────────────────────────────────────

    public async Task<TenantAdminSummaryResponse> UpdateStatusAsync(
        Guid id,
        string status,
        CancellationToken ct = default)
    {
        var tenant = await _tenantRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Tenant '{id}' was not found.");

        if (!Enum.TryParse<TenantStatus>(status, ignoreCase: true, out var parsed))
            throw new ValidationException($"Invalid status '{status}'.",
                new Dictionary<string, string[]>
                {
                    ["status"] = [$"'{status}' is not a valid tenant status (Active, Inactive, Suspended)."]
                });

        tenant.SetStatus(parsed);
        await _tenantRepo.UpdateAsync(tenant, ct);
        return ToSummary(tenant);
    }

    // ── B12: Canonical Tenant Create (Tenant-first) ───────────────────────────

    public async Task<AdminCreateTenantResponse> CreateTenantAsync(
        AdminCreateTenantRequest request,
        CancellationToken ct = default)
    {
        // ── Validate inputs ────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Tenant name is required.",
                new Dictionary<string, string[]> { ["name"] = ["Tenant name is required."] });

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException("Tenant code is required.",
                new Dictionary<string, string[]> { ["code"] = ["Tenant code is required."] });

        if (string.IsNullOrWhiteSpace(request.AdminEmail))
            throw new ValidationException("Admin email is required.",
                new Dictionary<string, string[]> { ["adminEmail"] = ["Admin email is required."] });

        if (string.IsNullOrWhiteSpace(request.AdminFirstName))
            throw new ValidationException("Admin first name is required.",
                new Dictionary<string, string[]> { ["adminFirstName"] = ["Admin first name is required."] });

        if (string.IsNullOrWhiteSpace(request.AdminLastName))
            throw new ValidationException("Admin last name is required.",
                new Dictionary<string, string[]> { ["adminLastName"] = ["Admin last name is required."] });

        // Normalize code to slug
        var code = request.Code.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');

        // Check for existing tenant with same code
        var existing = await _tenantRepo.GetByCodeAsync(code, ct);
        if (existing is not null)
            throw new ConflictException($"A tenant with code '{code}' already exists.");

        // ── Step 1: Create canonical Tenant record ─────────────────────────────
        var tenant = Domain.Tenant.Create(
            code:        code,
            displayName: request.Name.Trim(),
            subdomain:   code,
            timeZone:    null,
            locale:      null);

        await _tenantRepo.AddAsync(tenant, ct);

        // ── Step 2: Call Identity provisioning adapter ─────────────────────────
        var provisioningRequest = new IdentityProvisioningRequest(
            TenantId:          tenant.Id,
            Code:              tenant.Code,
            DisplayName:       tenant.DisplayName,
            OrgType:           request.OrgType,
            AdminEmail:        request.AdminEmail.ToLowerInvariant().Trim(),
            AdminFirstName:    request.AdminFirstName.Trim(),
            AdminLastName:     request.AdminLastName.Trim(),
            PreferredSubdomain: code,
            AddressLine1:      request.AddressLine1,
            City:              request.City,
            State:             request.State,
            PostalCode:        request.PostalCode,
            Latitude:          request.Latitude,
            Longitude:         request.Longitude,
            GeoPointSource:    request.GeoPointSource,
            Products:          null);

        var provResult = await _identityProvisioning.ProvisionAsync(provisioningRequest, ct);

        // BLK-TS-02 — Update Tenant-owned provisioning state from Identity result.
        var newProvStatus = provResult.Success
            ? Domain.TenantProvisioningStatus.Provisioned
            : Domain.TenantProvisioningStatus.Failed;
        var provError = provResult.Errors.Count > 0 ? string.Join("; ", provResult.Errors) : null;

        tenant.SetProvisioningStatus(newProvStatus, provError);

        // If provisioning succeeded and returned a subdomain/hostname, update the Tenant record.
        if (provResult.Success && !string.IsNullOrWhiteSpace(provResult.Subdomain))
            tenant.SetSubdomain(provResult.Subdomain);

        await _tenantRepo.UpdateAsync(tenant, ct);

        // ── Step 3: Build response ─────────────────────────────────────────────
        var nextAction = provResult.Success
            ? "None"
            : "RetryProvisioning";

        return new AdminCreateTenantResponse(
            TenantId:            tenant.Id.ToString(),
            DisplayName:         tenant.DisplayName,
            Code:                tenant.Code,
            Status:              tenant.Status.ToString(),
            AdminUserId:         provResult.AdminUserId,
            AdminEmail:          provResult.AdminEmail ?? request.AdminEmail,
            TemporaryPassword:   provResult.TemporaryPassword,
            Subdomain:           provResult.Subdomain ?? tenant.Subdomain,
            ProvisioningStatus:  provResult.ProvisioningStatus ?? (provResult.Success ? "Provisioned" : "Failed"),
            Hostname:            provResult.Hostname,
            TenantCreated:       true,
            IdentityProvisioned: provResult.Success,
            NextAction:          nextAction,
            ProvisioningWarnings: provResult.Warnings,
            ProvisioningErrors:   provResult.Errors);
    }

    // ── B12: Entitlement Toggle (Tenant-first) ─────────────────────────────────

    public async Task<AdminEntitlementToggleResponse> ToggleEntitlementAsync(
        Guid   tenantId,
        string productCode,
        bool   enabled,
        CancellationToken ct = default)
    {
        var tenant = await _tenantRepo.GetByIdAsync(tenantId, ct)
            ?? throw new NotFoundException($"Tenant '{tenantId}' was not found.");

        // Normalize product key (Tenant service uses lowercase normalized keys)
        var normalizedKey = productCode.Trim().ToLowerInvariant();

        var entitlement = await _entitlementRepo.GetByTenantAndProductKeyAsync(tenantId, normalizedKey, ct);
        DateTime? enabledAtUtc = null;

        if (entitlement is null)
        {
            // Create new entitlement
            entitlement = TenantProductEntitlement.Create(
                tenantId:           tenantId,
                productKey:         normalizedKey,
                productDisplayName: productCode,
                isEnabled:          enabled,
                isDefault:          false,
                effectiveFromUtc:   enabled ? DateTime.UtcNow : null);

            await _entitlementRepo.AddAsync(entitlement, ct);
            enabledAtUtc = entitlement.EffectiveFromUtc;
        }
        else
        {
            // Toggle existing
            if (enabled) entitlement.Enable();
            else         entitlement.Disable();

            await _entitlementRepo.UpdateAsync(entitlement, ct);
            enabledAtUtc = entitlement.EffectiveFromUtc;
        }

        return new AdminEntitlementToggleResponse(
            EntitlementId: entitlement.Id,
            TenantId:      tenantId,
            ProductCode:   productCode,
            ProductName:   entitlement.ProductDisplayName ?? productCode,
            Enabled:       enabled,
            Status:        enabled ? "Active" : "Disabled",
            EnabledAtUtc:  enabledAtUtc?.ToString("o"),
            IdentitySynced: false);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static TenantAdminSummaryResponse ToSummary(Domain.Tenant t) =>
        new(
            Id:                 t.Id,
            Code:               t.Code,
            DisplayName:        t.DisplayName,
            Status:             t.Status.ToString(),
            IsActive:           t.Status == TenantStatus.Active,
            Type:               "LawFirm",
            PrimaryContactName: "",
            UserCount:          0,
            OrgCount:           0,
            Subdomain:          t.Subdomain,
            CreatedAtUtc:       t.CreatedAtUtc);
}
