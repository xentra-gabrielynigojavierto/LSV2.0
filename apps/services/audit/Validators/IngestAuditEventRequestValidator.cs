using FluentValidation;
using PlatformAuditEventService.DTOs.Ingest;

namespace PlatformAuditEventService.Validators;

/// <summary>
/// Validates a single <see cref="IngestAuditEventRequest"/> submitted to the ingest API.
///
/// Required fields (must be present and non-empty):
///   EventType, EventCategory, SourceSystem, SourceService,
///   Visibility (VisibilityScope), Severity (SeverityLevel), OccurredAtUtc
///
/// Optional fields (validated only when provided):
///   SourceEnvironment, Scope, Actor, Entity, Action, Description,
///   Before, After, Metadata, CorrelationId, RequestId, SessionId,
///   IdempotencyKey, Tags, EventId, IsReplay
///
/// Nested objects use child validators registered separately:
///   AuditEventScopeDtoValidator, AuditEventActorDtoValidator, AuditEventEntityDtoValidator
///
/// Design note: Validation is intentionally domain-neutral — no business rule
/// enforcement (e.g. "Security events must have an ActorId"). That belongs in
/// the domain service, not the structural validator.
/// </summary>
public sealed class IngestAuditEventRequestValidator : AbstractValidator<IngestAuditEventRequest>
{
    // ── Max length constants ───────────────────────────────────────────────────
    // These match the column definitions in AuditEventRecordConfiguration.

    private const int MaxEventType         = 200;
    private const int MaxSourceSystem      = 200;
    private const int MaxSourceService     = 200;
    private const int MaxSourceEnvironment = 100;
    private const int MaxAction            = 200;
    private const int MaxDescription       = 2000;
    private const int MaxCorrelationId     = 200;
    private const int MaxRequestId         = 200;
    private const int MaxSessionId         = 200;
    private const int MaxIdempotencyKey    = 300;
    private const int MaxJsonField         = 1_048_576;  // 1 MB — generous; mediumtext cap is 16 MB
    private const int MaxTagCount          = 20;
    private const int MaxTagLength         = 100;

    // OccurredAtUtc clock-skew tolerance (allow events slightly ahead of server time)
    private static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    // Reject timestamps implausibly far in the past (older than this are suspicious)
    private static readonly TimeSpan MaxEventAge = TimeSpan.FromDays(365 * 7);   // 7 years

    public IngestAuditEventRequestValidator(
        IValidator<AuditEventScopeDto>   scopeValidator,
        IValidator<AuditEventActorDto>   actorValidator,
        IValidator<AuditEventEntityDto>  entityValidator)
    {
        // ── Required: EventType ───────────────────────────────────────────────
        RuleFor(x => x.EventType)
            .NotEmpty()
            .WithMessage("EventType is required.")
            .MaximumLength(MaxEventType)
            .WithMessage($"EventType must not exceed {MaxEventType} characters.");

        // ── Required: EventCategory (typed enum) ──────────────────────────────
        RuleFor(x => x.EventCategory)
            .IsInEnum()
            .WithMessage("EventCategory must be a valid category: Security, Access, Business, Administrative, System, Compliance, DataChange, Integration, or Performance.");

        // ── Required: SourceSystem ────────────────────────────────────────────
        RuleFor(x => x.SourceSystem)
            .NotEmpty()
            .WithMessage("SourceSystem is required.")
            .MaximumLength(MaxSourceSystem)
            .WithMessage($"SourceSystem must not exceed {MaxSourceSystem} characters.");

        // ── Required: SourceService ───────────────────────────────────────────
        RuleFor(x => x.SourceService)
            .NotEmpty()
            .WithMessage("SourceService is required.")
            .MaximumLength(MaxSourceService)
            .WithMessage($"SourceService must not exceed {MaxSourceService} characters.");

        // ── Required: Visibility ──────────────────────────────────────────────
        RuleFor(x => x.Visibility)
            .IsInEnum()
            .WithMessage("Visibility must be a valid visibility scope: Platform, Tenant, Organization, User, or Internal.");

        // ── Required: Severity ────────────────────────────────────────────────
        RuleFor(x => x.Severity)
            .IsInEnum()
            .WithMessage("Severity must be a valid severity level: Debug, Info, Notice, Warn, Error, Critical, or Alert.");

        // ── Required: OccurredAtUtc ───────────────────────────────────────────
        // The NotNull check is intentionally a separate rule chain from the range guards.
        // Placing .When(x => x.OccurredAtUtc.HasValue) on the NotNull rule would make it
        // unreachable (it only fires when the value is non-null, which negates the null check).
        RuleFor(x => x.OccurredAtUtc)
            .NotNull()
            .WithMessage("OccurredAtUtc is required.");

        RuleFor(x => x.OccurredAtUtc)
            .Must(ts => ts!.Value <= DateTimeOffset.UtcNow.Add(FutureTolerance))
            .WithMessage($"OccurredAtUtc must not be more than {FutureTolerance.TotalMinutes:0} minutes in the future.")
            .Must(ts => ts!.Value >= DateTimeOffset.UtcNow.Subtract(MaxEventAge))
            .WithMessage($"OccurredAtUtc is implausibly old (max age: {MaxEventAge.TotalDays / 365:0} years).")
            .When(x => x.OccurredAtUtc.HasValue);

        // ── Optional: SourceEnvironment ───────────────────────────────────────
        RuleFor(x => x.SourceEnvironment)
            .MaximumLength(MaxSourceEnvironment)
            .WithMessage($"SourceEnvironment must not exceed {MaxSourceEnvironment} characters.")
            .When(x => x.SourceEnvironment is not null);

        // ── Optional: Action ──────────────────────────────────────────────────
        RuleFor(x => x.Action)
            .MaximumLength(MaxAction)
            .WithMessage($"Action must not exceed {MaxAction} characters.")
            .When(x => !string.IsNullOrEmpty(x.Action));

        // ── Optional: Description ─────────────────────────────────────────────
        RuleFor(x => x.Description)
            .MaximumLength(MaxDescription)
            .WithMessage($"Description must not exceed {MaxDescription} characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        // ── Optional: JSON snapshot fields (Before / After / Metadata) ─────────
        RuleFor(x => x.Before)
            .MaximumLength(MaxJsonField)
            .WithMessage($"Before must not exceed {MaxJsonField:N0} characters.")
            .When(x => x.Before is not null);

        RuleFor(x => x.After)
            .MaximumLength(MaxJsonField)
            .WithMessage($"After must not exceed {MaxJsonField:N0} characters.")
            .When(x => x.After is not null);

        RuleFor(x => x.Metadata)
            .MaximumLength(MaxJsonField)
            .WithMessage($"Metadata must not exceed {MaxJsonField:N0} characters.")
            .When(x => x.Metadata is not null);

        // ── Optional: Correlation / tracing fields ────────────────────────────
        RuleFor(x => x.CorrelationId)
            .MaximumLength(MaxCorrelationId)
            .WithMessage($"CorrelationId must not exceed {MaxCorrelationId} characters.")
            .When(x => x.CorrelationId is not null);

        RuleFor(x => x.RequestId)
            .MaximumLength(MaxRequestId)
            .WithMessage($"RequestId must not exceed {MaxRequestId} characters.")
            .When(x => x.RequestId is not null);

        RuleFor(x => x.SessionId)
            .MaximumLength(MaxSessionId)
            .WithMessage($"SessionId must not exceed {MaxSessionId} characters.")
            .When(x => x.SessionId is not null);

        // ── Optional: IdempotencyKey ──────────────────────────────────────────
        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(MaxIdempotencyKey)
            .WithMessage($"IdempotencyKey must not exceed {MaxIdempotencyKey} characters.")
            .When(x => x.IdempotencyKey is not null);

        // ── Optional: Tags ────────────────────────────────────────────────────
        RuleFor(x => x.Tags)
            .Must(tags => tags!.Count <= MaxTagCount)
            .WithMessage($"Tags must not contain more than {MaxTagCount} items.")
            .When(x => x.Tags is not null);

        RuleForEach(x => x.Tags)
            .NotEmpty()
            .WithMessage("Each tag must be a non-empty string.")
            .MaximumLength(MaxTagLength)
            .WithMessage($"Each tag must not exceed {MaxTagLength} characters.")
            .When(x => x.Tags is not null);

        // ── Nested: Scope ─────────────────────────────────────────────────────
        RuleFor(x => x.Scope)
            .SetValidator(scopeValidator);

        // ── Nested: Actor ─────────────────────────────────────────────────────
        RuleFor(x => x.Actor)
            .SetValidator(actorValidator);

        // ── Nested: Entity (optional object) ─────────────────────────────────
        RuleFor(x => x.Entity)
            .SetValidator(entityValidator!)
            .When(x => x.Entity is not null);
    }
}
