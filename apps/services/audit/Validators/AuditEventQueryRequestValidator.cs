using FluentValidation;
using PlatformAuditEventService.DTOs.Query;

namespace PlatformAuditEventService.Validators;

/// <summary>
/// Validates query filter and pagination parameters for listing audit event records.
///
/// All filter fields are optional. Rules only apply when a field is provided.
/// Pagination and sort fields always apply (they have defaults and hard caps).
///
/// The validator does not enforce tenant scope — that is handled by the
/// QueryAuth middleware which overrides TenantId from the caller's JWT claims
/// when EnforceTenantScope=true.
/// </summary>
public sealed class AuditEventQueryRequestValidator : AbstractValidator<AuditEventQueryRequest>
{
    private static readonly string[] ValidSortByValues =
        ["occurredatutc", "recordedatutc", "severity", "sourcesystem"];

    private const int MaxTenantId             = 100;
    private const int MaxOrganizationId       = 100;
    private const int MaxActorId              = 200;
    private const int MaxEntityType           = 200;
    private const int MaxEntityId             = 200;
    private const int MaxCorrelationId        = 200;
    private const int MaxSessionId            = 200;
    private const int MaxSourceSystem         = 200;
    private const int MaxSourceService        = 200;
    private const int MaxEventTypeItem        = 200;
    private const int MaxEventTypeListCount   = 20;
    private const int MaxDescriptionContains  = 500;
    private const int MaxPageSize             = 500;

    public AuditEventQueryRequestValidator()
    {
        // ── Scope filters ──────────────────────────────────────────────────────
        RuleFor(x => x.TenantId)
            .MaximumLength(MaxTenantId)
            .WithMessage($"TenantId must not exceed {MaxTenantId} characters.")
            .When(x => x.TenantId is not null);

        RuleFor(x => x.OrganizationId)
            .MaximumLength(MaxOrganizationId)
            .WithMessage($"OrganizationId must not exceed {MaxOrganizationId} characters.")
            .When(x => x.OrganizationId is not null);

        // ── Classification filters ─────────────────────────────────────────────
        RuleFor(x => x.Category)
            .IsInEnum()
            .WithMessage("Category must be a valid EventCategory when provided.")
            .When(x => x.Category.HasValue);

        RuleFor(x => x.MinSeverity)
            .IsInEnum()
            .WithMessage("MinSeverity must be a valid SeverityLevel when provided.")
            .When(x => x.MinSeverity.HasValue);

        RuleFor(x => x.MaxSeverity)
            .IsInEnum()
            .WithMessage("MaxSeverity must be a valid SeverityLevel when provided.")
            .When(x => x.MaxSeverity.HasValue);

        // MinSeverity ≤ MaxSeverity
        RuleFor(x => x.MinSeverity)
            .Must((req, min) => !req.MaxSeverity.HasValue || min!.Value <= req.MaxSeverity.Value)
            .WithMessage("MinSeverity must be less than or equal to MaxSeverity.")
            .When(x => x.MinSeverity.HasValue && x.MaxSeverity.HasValue);

        RuleFor(x => x.EventTypes)
            .Must(types => types!.Count <= MaxEventTypeListCount)
            .WithMessage($"EventTypes must not contain more than {MaxEventTypeListCount} values.")
            .When(x => x.EventTypes is not null);

        RuleForEach(x => x.EventTypes)
            .NotEmpty()
            .WithMessage("Each EventType filter value must be non-empty.")
            .MaximumLength(MaxEventTypeItem)
            .WithMessage($"Each EventType filter value must not exceed {MaxEventTypeItem} characters.")
            .When(x => x.EventTypes is not null);

        RuleFor(x => x.SourceSystem)
            .MaximumLength(MaxSourceSystem)
            .WithMessage($"SourceSystem must not exceed {MaxSourceSystem} characters.")
            .When(x => x.SourceSystem is not null);

        RuleFor(x => x.SourceService)
            .MaximumLength(MaxSourceService)
            .WithMessage($"SourceService must not exceed {MaxSourceService} characters.")
            .When(x => x.SourceService is not null);

        // ── Actor / identity filters ───────────────────────────────────────────
        RuleFor(x => x.ActorId)
            .MaximumLength(MaxActorId)
            .WithMessage($"ActorId must not exceed {MaxActorId} characters.")
            .When(x => x.ActorId is not null);

        RuleFor(x => x.ActorType)
            .IsInEnum()
            .WithMessage("ActorType must be a valid actor type when provided.")
            .When(x => x.ActorType.HasValue);

        // ── Entity filters ─────────────────────────────────────────────────────
        RuleFor(x => x.EntityType)
            .MaximumLength(MaxEntityType)
            .WithMessage($"EntityType must not exceed {MaxEntityType} characters.")
            .When(x => x.EntityType is not null);

        RuleFor(x => x.EntityId)
            .MaximumLength(MaxEntityId)
            .WithMessage($"EntityId must not exceed {MaxEntityId} characters.")
            .When(x => x.EntityId is not null);

        // ── Correlation filters ────────────────────────────────────────────────
        RuleFor(x => x.CorrelationId)
            .MaximumLength(MaxCorrelationId)
            .WithMessage($"CorrelationId must not exceed {MaxCorrelationId} characters.")
            .When(x => x.CorrelationId is not null);

        RuleFor(x => x.SessionId)
            .MaximumLength(MaxSessionId)
            .WithMessage($"SessionId must not exceed {MaxSessionId} characters.")
            .When(x => x.SessionId is not null);

        // ── Time range ─────────────────────────────────────────────────────────
        // When both From and To are provided: From must be before To
        RuleFor(x => x.From)
            .Must((req, from) => from < req.To)
            .WithMessage("From must be earlier than To when both are specified.")
            .When(x => x.From.HasValue && x.To.HasValue);

        // ── Visibility ─────────────────────────────────────────────────────────
        RuleFor(x => x.MaxVisibility)
            .IsInEnum()
            .WithMessage("MaxVisibility must be a valid VisibilityScope when provided.")
            .When(x => x.MaxVisibility.HasValue);

        // ── Text search ────────────────────────────────────────────────────────
        RuleFor(x => x.DescriptionContains)
            .MaximumLength(MaxDescriptionContains)
            .WithMessage($"DescriptionContains must not exceed {MaxDescriptionContains} characters.")
            .When(x => x.DescriptionContains is not null);

        // ── Pagination ─────────────────────────────────────────────────────────
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage($"PageSize must be between 1 and {MaxPageSize}.");

        // ── Sorting ────────────────────────────────────────────────────────────
        RuleFor(x => x.SortBy)
            .Must(sortBy => ValidSortByValues.Contains(sortBy.ToLowerInvariant()))
            .WithMessage($"SortBy must be one of: {string.Join(", ", ValidSortByValues)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy));
    }
}
