using FluentValidation;
using PlatformAuditEventService.DTOs.Ingest;

namespace PlatformAuditEventService.Validators;

/// <summary>
/// Validates the <see cref="AuditEventActorDto"/> nested object within an ingest request.
///
/// ActorId is optional at the DTO level to support anonymous callers (ActorType.Anonymous),
/// but consumers should populate it whenever identity is known.
/// </summary>
public sealed class AuditEventActorDtoValidator : AbstractValidator<AuditEventActorDto>
{
    public AuditEventActorDtoValidator()
    {
        // ── ActorType ──────────────────────────────────────────────────────────
        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Actor.Type must be a valid actor type: User, ServiceAccount, System, Api, Scheduler, Anonymous, or Support.");

        // ── Id ─────────────────────────────────────────────────────────────────
        RuleFor(x => x.Id)
            .MaximumLength(200)
            .WithMessage("Actor.Id must not exceed 200 characters.")
            .When(x => x.Id is not null);

        // ── Name ───────────────────────────────────────────────────────────────
        RuleFor(x => x.Name)
            .MaximumLength(300)
            .WithMessage("Actor.Name must not exceed 300 characters.")
            .When(x => x.Name is not null);

        // ── IpAddress ──────────────────────────────────────────────────────────
        // Max 45 chars covers the full IPv6 notation including zone ID.
        RuleFor(x => x.IpAddress)
            .MaximumLength(45)
            .WithMessage("Actor.IpAddress must not exceed 45 characters (IPv6 max).")
            .When(x => x.IpAddress is not null);

        // ── UserAgent ──────────────────────────────────────────────────────────
        RuleFor(x => x.UserAgent)
            .MaximumLength(500)
            .WithMessage("Actor.UserAgent must not exceed 500 characters.")
            .When(x => x.UserAgent is not null);
    }
}
