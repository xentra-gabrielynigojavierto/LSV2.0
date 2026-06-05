using FluentValidation;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Validators;

/// <summary>
/// Validates the <see cref="AuditEventScopeDto"/> nested object within an ingest request.
///
/// ScopeType drives conditional field requirements:
///   Tenant       → TenantId required
///   Organization → TenantId + OrganizationId required
///   User         → TenantId + UserId required
///   Service      → no ID fields required (SourceSystem serves as scope)
///   Global/Platform → no ID fields required
/// </summary>
public sealed class AuditEventScopeDtoValidator : AbstractValidator<AuditEventScopeDto>
{
    public AuditEventScopeDtoValidator()
    {
        // ── ScopeType ──────────────────────────────────────────────────────────
        RuleFor(x => x.ScopeType)
            .IsInEnum()
            .WithMessage("ScopeType must be a valid scope: Global, Platform, Tenant, Organization, User, or Service.");

        // ── PlatformId ─────────────────────────────────────────────────────────
        RuleFor(x => x.PlatformId)
            .Must(id => id is null || Guid.TryParse(id, out _))
            .WithMessage("PlatformId must be a valid GUID format when provided.");

        // ── TenantId — required when scope implies a tenant boundary ───────────
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required when ScopeType is Tenant, Organization, or User.")
            .When(x => x.ScopeType is ScopeType.Tenant or ScopeType.Organization or ScopeType.User);

        RuleFor(x => x.TenantId)
            .MaximumLength(100)
            .WithMessage("TenantId must not exceed 100 characters.")
            .When(x => x.TenantId is not null);

        // ── OrganizationId — required when scope is Organization ───────────────
        RuleFor(x => x.OrganizationId)
            .NotEmpty()
            .WithMessage("OrganizationId is required when ScopeType is Organization.")
            .When(x => x.ScopeType == ScopeType.Organization);

        RuleFor(x => x.OrganizationId)
            .MaximumLength(100)
            .WithMessage("OrganizationId must not exceed 100 characters.")
            .When(x => x.OrganizationId is not null);

        // ── UserId — required when scope is User ───────────────────────────────
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required when ScopeType is User.")
            .When(x => x.ScopeType == ScopeType.User);

        RuleFor(x => x.UserId)
            .MaximumLength(200)
            .WithMessage("UserId must not exceed 200 characters.")
            .When(x => x.UserId is not null);
    }
}
