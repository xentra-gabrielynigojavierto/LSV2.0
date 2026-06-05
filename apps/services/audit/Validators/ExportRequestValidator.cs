using FluentValidation;
using PlatformAuditEventService.DTOs.Export;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Validators;

/// <summary>
/// Validates an <see cref="ExportRequest"/> submitted to create an async export job.
///
/// Key constraints:
/// - Format is required and must be one of the supported values.
/// - ScopeId is required when ScopeType implies a bounded scope (Tenant, Organization, User, Service).
/// - When both From and To are provided, From must precede To; the span must not exceed 1 year.
/// - A time range (at minimum, either From or To) is strongly encouraged for exports to
///   prevent unbounded result sets, but is not enforced at validation to allow
///   compliance officers to export complete historical records when needed.
/// </summary>
public sealed class ExportRequestValidator : AbstractValidator<ExportRequest>
{
    /// <summary>Supported output format identifiers (case-sensitive).</summary>
    public static readonly string[] SupportedFormats = ["Json", "Csv", "Ndjson"];

    private const int MaxScopeId        = 200;
    private const int MaxActorId        = 200;
    private const int MaxEntityType     = 200;
    private const int MaxEntityId       = 200;
    private const int MaxCorrelationId  = 200;
    private const int MaxEventTypeItem  = 200;
    private const int MaxEventTypeCount = 20;
    private const int MaxFormatLength   = 20;
    private static readonly TimeSpan MaxExportSpan = TimeSpan.FromDays(366); // ~1 year

    public ExportRequestValidator()
    {
        // ── ScopeType ──────────────────────────────────────────────────────────
        RuleFor(x => x.ScopeType)
            .IsInEnum()
            .WithMessage("ScopeType must be a valid scope: Global, Platform, Tenant, Organization, User, or Service.");

        // ── ScopeId — required for bounded scopes ──────────────────────────────
        RuleFor(x => x.ScopeId)
            .NotEmpty()
            .WithMessage("ScopeId is required when ScopeType is Tenant, Organization, User, or Service.")
            .When(x => x.ScopeType is ScopeType.Tenant or ScopeType.Organization
                                    or ScopeType.User  or ScopeType.Service);

        RuleFor(x => x.ScopeId)
            .MaximumLength(MaxScopeId)
            .WithMessage($"ScopeId must not exceed {MaxScopeId} characters.")
            .When(x => x.ScopeId is not null);

        // ── Format ─────────────────────────────────────────────────────────────
        RuleFor(x => x.Format)
            .NotEmpty()
            .WithMessage("Format is required.")
            .MaximumLength(MaxFormatLength)
            .WithMessage($"Format must not exceed {MaxFormatLength} characters.")
            .Must(f => SupportedFormats.Contains(f, StringComparer.Ordinal))
            .WithMessage($"Format must be one of: {string.Join(", ", SupportedFormats)}.");

        // ── Classification filters ─────────────────────────────────────────────
        RuleFor(x => x.Category)
            .IsInEnum()
            .WithMessage("Category must be a valid EventCategory when provided.")
            .When(x => x.Category.HasValue);

        RuleFor(x => x.MinSeverity)
            .IsInEnum()
            .WithMessage("MinSeverity must be a valid SeverityLevel when provided.")
            .When(x => x.MinSeverity.HasValue);

        // ── EventTypes ─────────────────────────────────────────────────────────
        RuleFor(x => x.EventTypes)
            .Must(types => types!.Count <= MaxEventTypeCount)
            .WithMessage($"EventTypes must not contain more than {MaxEventTypeCount} values.")
            .When(x => x.EventTypes is not null);

        RuleForEach(x => x.EventTypes)
            .NotEmpty()
            .WithMessage("Each EventType filter value must be non-empty.")
            .MaximumLength(MaxEventTypeItem)
            .WithMessage($"Each EventType filter value must not exceed {MaxEventTypeItem} characters.")
            .When(x => x.EventTypes is not null);

        // ── Actor / entity filters ─────────────────────────────────────────────
        RuleFor(x => x.ActorId)
            .MaximumLength(MaxActorId)
            .WithMessage($"ActorId must not exceed {MaxActorId} characters.")
            .When(x => x.ActorId is not null);

        RuleFor(x => x.EntityType)
            .MaximumLength(MaxEntityType)
            .WithMessage($"EntityType must not exceed {MaxEntityType} characters.")
            .When(x => x.EntityType is not null);

        RuleFor(x => x.EntityId)
            .MaximumLength(MaxEntityId)
            .WithMessage($"EntityId must not exceed {MaxEntityId} characters.")
            .When(x => x.EntityId is not null);

        RuleFor(x => x.CorrelationId)
            .MaximumLength(MaxCorrelationId)
            .WithMessage($"CorrelationId must not exceed {MaxCorrelationId} characters.")
            .When(x => x.CorrelationId is not null);

        // ── Time range ─────────────────────────────────────────────────────────
        // From must precede To when both are supplied
        RuleFor(x => x.From)
            .Must((req, from) => from < req.To)
            .WithMessage("From must be earlier than To.")
            .When(x => x.From.HasValue && x.To.HasValue);

        // Maximum export span: 1 year. Larger ranges require multiple export jobs.
        RuleFor(x => x)
            .Must(req => (req.To!.Value - req.From!.Value) <= MaxExportSpan)
            .WithMessage($"Export time range must not exceed {MaxExportSpan.TotalDays:0} days (~1 year). " +
                         "Split the request into multiple smaller export jobs.")
            .When(x => x.From.HasValue && x.To.HasValue);
    }
}
