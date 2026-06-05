using FluentValidation;
using PlatformAuditEventService.DTOs.Ingest;

namespace PlatformAuditEventService.Validators;

/// <summary>
/// Validates the optional <see cref="AuditEventEntityDto"/> nested object.
///
/// Both fields are optional — the object itself is optional in the ingest request.
/// When provided, at least one of Type or Id should be set; however, this is not
/// enforced here to keep validation generic and domain-neutral.
/// </summary>
public sealed class AuditEventEntityDtoValidator : AbstractValidator<AuditEventEntityDto>
{
    public AuditEventEntityDtoValidator()
    {
        // ── Type ───────────────────────────────────────────────────────────────
        RuleFor(x => x.Type)
            .MaximumLength(200)
            .WithMessage("Entity.Type must not exceed 200 characters.")
            .When(x => x.Type is not null);

        // ── Id ─────────────────────────────────────────────────────────────────
        RuleFor(x => x.Id)
            .MaximumLength(200)
            .WithMessage("Entity.Id must not exceed 200 characters.")
            .When(x => x.Id is not null);
    }
}
