using System.Net.Mail;
using System.Text.RegularExpressions;
using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public class TenantService : ITenantService
{
    private readonly ITenantRepository _repository;

    public TenantService(ITenantRepository repository) => _repository = repository;

    // ── BLK-TS-01: Tenant code format rules ──────────────────────────────────

    /// <summary>
    /// Valid code: lowercase alphanumeric + hyphens, no leading/trailing hyphens, 2–50 chars.
    /// Examples: "acme", "liens-company", "abc123"
    /// </summary>
    private static readonly Regex CodeFormatRegex = new(
        @"^[a-z0-9]([a-z0-9\-]*[a-z0-9])?$",
        RegexOptions.Compiled);

    private const int CodeMinLength = 2;
    private const int CodeMaxLength = 50;

    private static bool IsValidCodeFormat(string normalizedCode, out string error)
    {
        if (normalizedCode.Length < CodeMinLength)
        {
            error = $"Code must be at least {CodeMinLength} characters.";
            return false;
        }
        if (normalizedCode.Length > CodeMaxLength)
        {
            error = $"Code must be at most {CodeMaxLength} characters.";
            return false;
        }
        if (!CodeFormatRegex.IsMatch(normalizedCode))
        {
            error = "Code must contain only lowercase letters, digits, and hyphens, and must not start or end with a hyphen.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    // ── BLK-TS-01: Check code availability ───────────────────────────────────

    public async Task<CheckCodeResponse> CheckCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new CheckCodeResponse(false, string.Empty, "Code cannot be empty.");

        var normalized = code.Trim().ToLowerInvariant();

        if (!IsValidCodeFormat(normalized, out var formatError))
            return new CheckCodeResponse(false, normalized, formatError);

        if (await _repository.ExistsByCodeAsync(normalized, ct))
            return new CheckCodeResponse(false, normalized, $"The code '{normalized}' is already taken.");

        return new CheckCodeResponse(true, normalized);
    }

    // ── BLK-TS-01: Minimal provision ─────────────────────────────────────────

    public async Task<ProvisionResponse> ProvisionAsync(ProvisionRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantName, nameof(request.TenantName));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantCode, nameof(request.TenantCode));

        var code = request.TenantCode.Trim().ToLowerInvariant();

        if (!IsValidCodeFormat(code, out var formatError))
            throw new ValidationException("Invalid tenant code.",
                new Dictionary<string, string[]> { ["tenantCode"] = [formatError] });

        if (await _repository.ExistsByCodeAsync(code, ct))
            throw new ConflictException($"A tenant with code '{code}' already exists.");

        var subdomain = code;
        if (await _repository.ExistsBySubdomainAsync(subdomain, null, ct))
            throw new ConflictException($"The subdomain '{subdomain}' is already taken.");

        var tenant = Domain.Tenant.Create(
            code:        code,
            displayName: request.TenantName.Trim(),
            subdomain:   subdomain);

        await _repository.AddAsync(tenant, ct);

        return new ProvisionResponse(tenant.Id, tenant.Code, tenant.Subdomain ?? subdomain);
    }

    public async Task<TenantResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _repository.GetByIdAsync(id, ct);
        return tenant is null ? null : ToResponse(tenant);
    }

    public async Task<TenantResponse?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var tenant = await _repository.GetByCodeAsync(code.ToLowerInvariant(), ct);
        return tenant is null ? null : ToResponse(tenant);
    }

    public async Task<(List<TenantResponse> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1)       page     = 1;
        if (pageSize < 1)   pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var (items, total) = await _repository.ListAsync(page, pageSize, ct);
        return (items.Select(ToResponse).ToList(), total);
    }

    public async Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code,        nameof(request.Code));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DisplayName, nameof(request.DisplayName));

        var errors = new Dictionary<string, string[]>();

        var code = request.Code.Trim().ToLowerInvariant();

        if (!IsValidCodeFormat(code, out var codeFormatError))
            throw new ValidationException("Invalid tenant code.",
                new Dictionary<string, string[]> { ["code"] = [codeFormatError] });

        if (await _repository.ExistsByCodeAsync(code, ct))
            throw new ConflictException($"A tenant with code '{code}' already exists.");

        if (request.Subdomain is not null)
        {
            var sub = request.Subdomain.Trim().ToLowerInvariant();
            if (await _repository.ExistsBySubdomainAsync(sub, null, ct))
                throw new ConflictException($"The subdomain '{sub}' is already taken.");
        }

        ValidateOptionalEmail(request.SupportEmail, "supportEmail", errors);
        ValidateOptionalUrl(request.WebsiteUrl,     "websiteUrl",   errors);
        ValidateOptionalCountryCode(request.CountryCode, "countryCode", errors);

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);

        var tenant = Domain.Tenant.Create(
            code,
            request.DisplayName,
            request.LegalName,
            request.Subdomain,
            request.Description,
            request.WebsiteUrl,
            request.TimeZone,
            request.Locale,
            request.SupportEmail,
            request.SupportPhone,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.StateOrProvince,
            request.PostalCode,
            request.CountryCode);

        await _repository.AddAsync(tenant, ct);
        return ToResponse(tenant);
    }

    public async Task<TenantResponse> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DisplayName, nameof(request.DisplayName));

        var tenant = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Tenant '{id}' was not found.");

        var errors = new Dictionary<string, string[]>();

        if (request.Subdomain is not null)
        {
            var sub = request.Subdomain.Trim().ToLowerInvariant();
            if (await _repository.ExistsBySubdomainAsync(sub, id, ct))
                throw new ConflictException($"The subdomain '{sub}' is already taken.");
        }

        ValidateOptionalEmail(request.SupportEmail, "supportEmail", errors);
        ValidateOptionalUrl(request.WebsiteUrl,     "websiteUrl",   errors);
        ValidateOptionalCountryCode(request.CountryCode, "countryCode", errors);

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);

        tenant.UpdateProfile(
            request.DisplayName,
            request.LegalName,
            request.Description,
            request.WebsiteUrl,
            request.TimeZone,
            request.Locale,
            request.SupportEmail,
            request.SupportPhone);

        tenant.UpdateAddress(
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.StateOrProvince,
            request.PostalCode,
            request.CountryCode);

        if (request.Subdomain is not null)
            tenant.SetSubdomain(request.Subdomain);

        if (request.Status is not null)
        {
            if (!Enum.TryParse<TenantStatus>(request.Status, ignoreCase: true, out var status))
                throw new ValidationException($"Invalid status '{request.Status}'.",
                    new Dictionary<string, string[]> { ["status"] = [$"'{request.Status}' is not a valid status value."] });
            tenant.SetStatus(status);
        }

        if (request.LogoDocumentId is not null)
            tenant.SetLogo(request.LogoDocumentId);

        if (request.LogoWhiteDocumentId is not null)
            tenant.SetLogoWhite(request.LogoWhiteDocumentId);

        await _repository.UpdateAsync(tenant, ct);
        return ToResponse(tenant);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Tenant '{id}' was not found.");

        tenant.SetStatus(TenantStatus.Inactive);
        await _repository.UpdateAsync(tenant, ct);
    }

    /// <summary>
    /// TENANT-B07 — Idempotent upsert from an Identity dual-write sync event.
    ///
    /// If the tenant already exists in the Tenant service (matched by Id), it is
    /// updated with the incoming payload fields. If it does not exist, a minimal
    /// record is created from the payload so the Tenant service can serve it as
    /// a runtime read source without requiring a full migration run first.
    ///
    /// Fields not present in the sync payload (profile metadata, address, etc.)
    /// are left unchanged on update or left null on create — they will be populated
    /// by the migration execute endpoint or a subsequent operator update.
    /// </summary>
    public async Task UpsertFromSyncAsync(TenantSyncRequest request, CancellationToken ct = default)
    {
        var existing = await _repository.GetByIdAsync(request.TenantId, ct);

        if (existing is null)
        {
            // Create a minimal record so Tenant service can serve runtime reads.
            var code = request.Code.Trim().ToLowerInvariant();

            // If the code is already taken by a *different* tenant, skip creation
            // (this should not happen in practice but guards against race conditions).
            var byCode = await _repository.GetByCodeAsync(code, ct);
            if (byCode is not null && byCode.Id != request.TenantId)
                return;

            if (byCode is null)
            {
                var created = Domain.Tenant.Rehydrate(
                    id:                 request.TenantId,
                    code:               code,
                    displayName:        request.DisplayName,
                    status:             ParseStatus(request.Status),
                    subdomain:          request.Subdomain,
                    logoDocumentId:     request.LogoDocumentId,
                    logoWhiteDocumentId: request.LogoWhiteDocumentId,
                    createdAtUtc:       request.SourceCreatedAtUtc,
                    updatedAtUtc:       request.SourceUpdatedAtUtc);

                await _repository.AddAsync(created, ct);
            }
        }
        else
        {
            // Update the fields the sync event carries.
            existing.UpdateProfile(
                existing.DisplayName != request.DisplayName ? request.DisplayName : existing.DisplayName,
                existing.LegalName,
                existing.Description,
                existing.WebsiteUrl,
                existing.TimeZone,
                existing.Locale,
                existing.SupportEmail,
                existing.SupportPhone);

            if (request.Subdomain is not null)
                existing.SetSubdomain(request.Subdomain);

            existing.SetStatus(ParseStatus(request.Status));

            if (request.LogoDocumentId.HasValue)
                existing.SetLogo(request.LogoDocumentId);

            if (request.LogoWhiteDocumentId.HasValue)
                existing.SetLogoWhite(request.LogoWhiteDocumentId);

            await _repository.UpdateAsync(existing, ct);
        }
    }

    private static TenantStatus ParseStatus(string? status) =>
        Enum.TryParse<TenantStatus>(status, ignoreCase: true, out var s) ? s : TenantStatus.Active;

    // ── Validation helpers ────────────────────────────────────────────────────

    private static void ValidateOptionalEmail(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        try { _ = new MailAddress(value); }
        catch { errors[field] = [$"'{value}' is not a valid email address."]; }
    }

    private static void ValidateOptionalUrl(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            errors[field] = [$"'{value}' is not a valid http/https URL."];
    }

    private static void ValidateOptionalCountryCode(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.Trim().Length != 2)
            errors[field] = ["Country code must be a 2-character ISO 3166-1 alpha-2 value."];
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    internal static TenantResponse ToResponse(Domain.Tenant t) => new(
        t.Id,
        t.Code,
        t.DisplayName,
        t.LegalName,
        t.Description,
        t.Status.ToString(),
        t.Subdomain,
        t.LogoDocumentId,
        t.LogoWhiteDocumentId,
        t.WebsiteUrl,
        t.TimeZone,
        t.Locale,
        t.SupportEmail,
        t.SupportPhone,
        t.AddressLine1,
        t.AddressLine2,
        t.City,
        t.StateOrProvince,
        t.PostalCode,
        t.CountryCode,
        t.CreatedAtUtc,
        t.UpdatedAtUtc,
        // BLK-TS-02 — provisioning state
        ProvisioningStatus:    t.ProvisioningStatus.ToString(),
        ProvisionedAtUtc:      t.ProvisionedAtUtc,
        LastProvisioningError: t.LastProvisioningError);
}
