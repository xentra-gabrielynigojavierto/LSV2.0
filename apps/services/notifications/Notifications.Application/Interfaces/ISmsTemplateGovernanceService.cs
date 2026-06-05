namespace Notifications.Application.Interfaces;

// ─── Request / Result types ───────────────────────────────────────────────────

/// <summary>
/// Input to the template governance evaluation engine.
/// RecipientPhoneTransient must never be persisted.
/// </summary>
public sealed class SmsTemplateGovernanceRequest
{
    public Guid     TenantId                   { get; set; }
    public Guid?    NotificationId             { get; set; }
    public Guid?    AttemptId                  { get; set; }

    /// <summary>
    /// Key of the SMS template. Null if inline (untemplated) body.
    /// </summary>
    public string?  TemplateKey                { get; set; }

    /// <summary>
    /// Specific version to enforce. Null = use latest approved.
    /// </summary>
    public int?     TemplateVersion            { get; set; }

    /// <summary>
    /// Pre-rendered body for inline (untemplated) messages.
    /// Only valid when TemplateKey is null AND AllowInlineUntemplatedMessages is true.
    /// </summary>
    public string?  InlineBody                 { get; set; }

    /// <summary>
    /// Already-rendered body from the notification record.
    /// Used for variable validation and content classification.
    /// </summary>
    public string?  RenderedBody               { get; set; }

    /// <summary>
    /// Original variable substitution data — for variable count checks.
    /// May be null if not available at evaluation time.
    /// </summary>
    public Dictionary<string, string>? VariablesUsed { get; set; }

    public bool     IsRetry                    { get; set; }
    public int      RetryCount                 { get; set; }
    public DateTime NowUtc                     { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Original template body (with {{tokens}}), if available.
    /// Used by LS-019 dynamic variable_rule evaluation.
    /// </summary>
    public string?  TemplateBody               { get; set; }

    /// <summary>
    /// When true, governance decisions are not persisted to the DB.
    /// Used by simulation/dry-run requests only.
    /// </summary>
    public bool     IsDryRun                   { get; set; }
}

/// <summary>
/// Result from the template governance evaluation engine.
/// </summary>
public sealed class SmsTemplateGovernanceResult
{
    // DecisionType: allow | warn | block | review_required
    public string   DecisionType              { get; set; } = "allow";
    public string   ReasonCode                { get; set; } = "no_template_governance";

    public bool     ShouldProceed             { get; set; } = true;
    public bool     ShouldBlock               { get; set; }

    public Guid?    TemplateId                { get; set; }
    public Guid?    TemplateVersionId         { get; set; }
    public string?  Classification            { get; set; }
    public bool     VariableValidationPassed  { get; set; } = true;
    public List<string> ValidationErrors      { get; set; } = [];

    /// <summary>
    /// Rendered body (may be enriched/validated). Null if blocked before rendering.
    /// </summary>
    public string?  RenderedBody              { get; set; }
}

// ─── Sub-request types ────────────────────────────────────────────────────────

public sealed class ValidateTemplateVariablesRequest
{
    public string                       TemplateBody  { get; set; } = string.Empty;
    public Dictionary<string, string>?  Variables     { get; set; }
    public string?                      VariableSchemaJson { get; set; }
}

public sealed class ClassifyTemplateRequest
{
    public string  TemplateBody    { get; set; } = string.Empty;
    public string? TemplateKey     { get; set; }
    public string? CurrentCategory { get; set; }
}

public sealed class RenderTemplateRequest
{
    public Guid    TenantId      { get; set; }
    public string  TemplateKey   { get; set; } = string.Empty;
    public int?    Version       { get; set; }
    public Dictionary<string, string> Variables { get; set; } = [];
}

// ─── Inline create/update request types ──────────────────────────────────────

public sealed class CreateSmsTemplateRequest
{
    public Guid?   TenantId               { get; set; }
    public string  TemplateKey            { get; set; } = string.Empty;
    public string  Name                   { get; set; } = string.Empty;
    public string? Description            { get; set; }
    public string? Category               { get; set; }
    public string  ContentClassification  { get; set; } = "transactional";
    public bool    RequiresApproval       { get; set; } = true;
    public string? RequestedBy            { get; set; }
}

public sealed class UpdateSmsTemplateRequest
{
    public Guid    Id                     { get; set; }
    public string? Name                   { get; set; }
    public string? Description            { get; set; }
    public string? Category               { get; set; }
    public string? ContentClassification  { get; set; }
    public bool?   RequiresApproval       { get; set; }
    public bool?   Enabled                { get; set; }
    public string? RequestedBy            { get; set; }
}

public sealed class CreateSmsTemplateVersionRequest
{
    public Guid    TemplateId          { get; set; }
    public string  TemplateBody        { get; set; } = string.Empty;
    public string? VariableSchemaJson  { get; set; }
    public string? RequestedBy         { get; set; }
}

public sealed class TemplateGovernancePolicyQuery
{
    public Guid?   TenantId         { get; set; }
    public string? Status           { get; set; }
    public string? Classification   { get; set; }
    public bool?   Enabled          { get; set; }
    public int     Page             { get; set; } = 1;
    public int     PageSize         { get; set; } = 50;
}

public sealed class TemplateGovernanceDecisionQuery
{
    public Guid?   TenantId     { get; set; }
    public Guid?   TemplateId   { get; set; }
    public string? DecisionType { get; set; }
    public string? ReasonCode   { get; set; }
    public string? From         { get; set; }
    public string? To           { get; set; }
    public int     Page         { get; set; } = 1;
    public int     PageSize     { get; set; } = 50;
}

// ─── Interface ────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-018: SMS Template Governance — content classification,
/// variable validation, prohibited-content enforcement, and approval lifecycle.
///
/// All evaluation failures degrade safely (fail-open by default).
/// No external APIs or AI/ML models are used.
/// </summary>
public interface ISmsTemplateGovernanceService
{
    /// <summary>
    /// Primary governance evaluation — call from delivery pipeline before provider execution.
    /// Returns ShouldProceed=true for allow/warn, false for block/review_required.
    /// </summary>
    Task<SmsTemplateGovernanceResult> EvaluateAsync(
        SmsTemplateGovernanceRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Validate variable tokens in a template body.
    /// Detects unresolved {{token}} patterns, missing required variables, count overflows.
    /// </summary>
    Task<(bool Passed, List<string> Errors)> ValidateVariablesAsync(
        ValidateTemplateVariablesRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Classify template content using local deterministic rules (no AI/ML/external APIs).
    /// Returns a ContentClassification string.
    /// </summary>
    string ClassifyContent(ClassifyTemplateRequest request);

    // ── Template registry CRUD ────────────────────────────────────────────────

    Task<Guid>  CreateTemplateAsync(CreateSmsTemplateRequest request, CancellationToken ct = default);
    Task<bool>  UpdateTemplateAsync(UpdateSmsTemplateRequest request, CancellationToken ct = default);
    Task<bool>  ArchiveTemplateAsync(Guid templateId, string? requestedBy, CancellationToken ct = default);

    // ── Version lifecycle ─────────────────────────────────────────────────────

    Task<Guid>  CreateVersionAsync(CreateSmsTemplateVersionRequest request, CancellationToken ct = default);
    Task<bool>  SubmitForReviewAsync(Guid templateId, string? requestedBy, CancellationToken ct = default);
    Task<bool>  ApproveVersionAsync(Guid templateId, string approvedBy, CancellationToken ct = default);
    Task<bool>  RejectVersionAsync(Guid templateId, string rejectedBy, string reason, CancellationToken ct = default);

    // ── Audit / query ─────────────────────────────────────────────────────────

    Task<(int Total, IReadOnlyList<Domain.SmsTemplate> Items)> GetTemplatesAsync(
        TemplateGovernancePolicyQuery query, CancellationToken ct = default);

    Task<(int Total, IReadOnlyList<Domain.SmsTemplateGovernanceDecision> Items)> GetDecisionsAsync(
        TemplateGovernanceDecisionQuery query, CancellationToken ct = default);
}
