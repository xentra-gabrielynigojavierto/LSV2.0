using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-018: SMS Template Governance engine.
///
/// Performs deterministic local governance checks (no AI/ML, no external APIs):
///   1. Template registry lookup and approval status validation
///   2. Variable/token validation on rendered body
///   3. Content length enforcement
///   4. Content classification and restricted-category enforcement
///   5. Prohibited-content pattern matching
///
/// Safe degradation:
/// - FailOpenOnEvaluationError = true (default): exceptions return allow.
/// - FailOpenOnEvaluationError = false: exceptions return block.
/// - Disabled master switch returns allow immediately.
/// - Raw phone numbers are NEVER persisted.
/// </summary>
public sealed partial class SmsTemplateGovernanceService : ISmsTemplateGovernanceService
{
    private readonly NotificationsDbContext             _db;
    private readonly SmsTemplateGovernanceOptions       _options;
    private readonly ILogger<SmsTemplateGovernanceService> _logger;

    /// <summary>LS-019: Optional dynamic rule engine — null when not registered.</summary>
    private readonly ISmsGovernanceRuleEngine?          _ruleEngine;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Regex: detect unresolved {{token}} patterns
    [GeneratedRegex(@"\{\{[^}]+\}\}", RegexOptions.Compiled)]
    private static partial Regex UnresolvedTokenRegex();

    // Regex: extract {{token}} variable names
    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.Compiled)]
    private static partial Regex VariableTokenRegex();

    // ─── Classification keyword maps ──────────────────────────────────────────

    private static readonly string[] TransactionalKeywords =
    [
        "otp", "verification code", "one-time", "confirm your", "account alert",
        "password reset", "login attempt", "sign in", "your code", "auth code",
        "delivery notification", "order confirmed", "shipment", "your appointment",
        "account has been", "security alert", "your account",
    ];

    private static readonly string[] OperationalKeywords =
    [
        "system notification", "maintenance", "service update", "scheduled downtime",
        "incident update", "service alert", "reminder:", "action required",
        "system alert", "platform update",
    ];

    private static readonly string[] EscalationKeywords =
    [
        "escalation", "on-call", "critical alert", "page:", "paging",
        "urgent incident", "critical incident", "sev1", "sev2",
        "p1 alert", "p2 alert",
    ];

    private static readonly string[] ComplianceKeywords =
    [
        "compliance notice", "regulatory", "legal notice", "required notice",
        "gdpr", "hipaa", "data breach", "required by law",
    ];

    private static readonly string[] MarketingRestrictedKeywords =
    [
        "limited time", "act now", "special offer", "discount", "sale ends",
        "promo", "coupon", "save 10", "save 20", "save 30", "save 50",
        "subscribe now", "unsubscribe", "opt out", "reply stop",
        "free gift", "winner", "you've been selected", "claim your",
        "click here to buy", "buy now", "shop now", "order now",
        "marketing", "campaign",
    ];

    private static readonly string[] ProhibitedKeywords =
    [
        "guaranteed winner", "you have won", "lottery", "jackpot",
        "casino bonus", "online casino", "gambling offer",
        "work from home earn", "make money fast", "get rich",
        "mlm", "pyramid", "unsolicited", "bulk sms", "mass text",
        "blast sms", "click here to claim prize",
    ];

    // Prohibited URL-spam pattern: 3+ URLs in a single short message
    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UrlRegex();

    public SmsTemplateGovernanceService(
        NotificationsDbContext                  db,
        IOptions<SmsTemplateGovernanceOptions>  options,
        ILogger<SmsTemplateGovernanceService>   logger,
        ISmsGovernanceRuleEngine?               ruleEngine = null)
    {
        _db         = db;
        _options    = options.Value;
        _logger     = logger;
        _ruleEngine = ruleEngine;
    }

    // ─── Primary evaluation entry point ──────────────────────────────────────

    public async Task<SmsTemplateGovernanceResult> EvaluateAsync(
        SmsTemplateGovernanceRequest request,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Allow("governance_disabled");

        try
        {
            // Step 1: Template registry + approval check
            SmsTemplate?        template        = null;
            SmsTemplateVersion? approvedVersion = null;

            if (!string.IsNullOrEmpty(request.TemplateKey))
            {
                template = await _db.SmsTemplates
                    .AsNoTracking()
                    .Where(t => t.TemplateKey == request.TemplateKey &&
                                (t.TenantId == request.TenantId || t.TenantId == null) &&
                                t.Enabled)
                    .OrderBy(t => t.TenantId == null ? 1 : 0) // tenant-specific first
                    .FirstOrDefaultAsync(ct);

                if (template == null)
                {
                    // Template key not found in SMS governance registry
                    // If RequireApprovedTemplates is false, allow with warn
                    if (!_options.RequireApprovedTemplates)
                    {
                        _logger.LogDebug(
                            "SmsTemplateGovernance: template key '{Key}' not in registry — allowing (RequireApprovedTemplates=false)",
                            request.TemplateKey);
                        return Allow("template_not_registered");
                    }

                    return await BlockAndPersist(request, "block", "template_not_found",
                        classification: null, variableValidationPassed: true);
                }

                if (template.Status == "archived")
                    return await BlockAndPersist(request, "block", "template_archived",
                        templateId: template.Id, classification: template.ContentClassification);

                if (_options.RequireApprovedTemplates && template.Status != "approved")
                {
                    return await BlockAndPersist(request, "review_required", "template_not_approved",
                        templateId: template.Id, classification: template.ContentClassification);
                }

                if (_options.RequireApprovedTemplates && template.LatestApprovedVersion.HasValue)
                {
                    approvedVersion = await _db.SmsTemplateVersions
                        .AsNoTracking()
                        .Where(v => v.TemplateId == template.Id &&
                                    v.VersionNumber == template.LatestApprovedVersion &&
                                    v.ApprovalStatus == "approved")
                        .FirstOrDefaultAsync(ct);
                }
            }
            else if (!_options.AllowInlineUntemplatedMessages)
            {
                return await BlockAndPersist(request, "block", "inline_body_not_allowed");
            }

            // Step 2: Variable validation on rendered body
            var renderedBody = request.RenderedBody ?? request.InlineBody ?? string.Empty;
            var (varPassed, varErrors) = await ValidateVariablesAsync(
                new ValidateTemplateVariablesRequest
                {
                    TemplateBody       = renderedBody,
                    Variables          = request.VariablesUsed,
                    VariableSchemaJson = approvedVersion?.VariableSchemaJson,
                }, ct);

            if (!varPassed)
            {
                await PersistDecision(request, "block", "invalid_variables",
                    templateId: template?.Id,
                    templateVersionId: approvedVersion?.Id,
                    classification: template?.ContentClassification,
                    variableValidationPassed: false,
                    metadata: new { errors = varErrors });
                return new SmsTemplateGovernanceResult
                {
                    DecisionType = "block", ReasonCode = "invalid_variables",
                    ShouldProceed = false, ShouldBlock = true,
                    TemplateId = template?.Id, TemplateVersionId = approvedVersion?.Id,
                    Classification = template?.ContentClassification,
                    VariableValidationPassed = false, ValidationErrors = varErrors,
                };
            }

            // Step 3: Content length check
            if (renderedBody.Length > _options.MaxTemplateLength)
            {
                return await BlockAndPersist(request, "block", "body_too_long",
                    templateId: template?.Id, templateVersionId: approvedVersion?.Id,
                    classification: template?.ContentClassification,
                    variableValidationPassed: true,
                    metadata: new { length = renderedBody.Length, max = _options.MaxTemplateLength });
            }

            // Step 4: Content classification
            var classifyReq = new ClassifyTemplateRequest
            {
                TemplateBody    = renderedBody,
                TemplateKey     = request.TemplateKey,
                CurrentCategory = template?.Category,
            };
            var classification = ClassifyContent(classifyReq);

            // Step 5: Restricted category enforcement
            if (_options.RestrictedCategories.Contains(classification))
            {
                var decisionType = classification == "prohibited" ? "block" : "review_required";
                var reasonCode   = classification == "prohibited" ? "prohibited_content" : "marketing_restricted";

                return await BlockAndPersist(request, decisionType, reasonCode,
                    templateId: template?.Id, templateVersionId: approvedVersion?.Id,
                    classification: classification, variableValidationPassed: true);
            }

            // Step 6: Classification mismatch check (registered vs detected)
            if (template != null &&
                !string.IsNullOrEmpty(template.ContentClassification) &&
                template.ContentClassification != classification &&
                template.ContentClassification != "transactional") // transactional is always safe
            {
                // Warn but allow — classification mismatch is informational for audit
                await PersistDecision(request, "warn", "classification_mismatch",
                    templateId: template.Id, templateVersionId: approvedVersion?.Id,
                    classification: classification, variableValidationPassed: true,
                    metadata: new { registered = template.ContentClassification, detected = classification });

                _logger.LogWarning(
                    "SmsTemplateGovernance: classification mismatch for template '{Key}' " +
                    "— registered={Registered}, detected={Detected} (warn+allow)",
                    request.TemplateKey, template.ContentClassification, classification);

                return new SmsTemplateGovernanceResult
                {
                    DecisionType = "warn", ReasonCode = "classification_mismatch",
                    ShouldProceed = true, ShouldBlock = false,
                    TemplateId = template.Id, TemplateVersionId = approvedVersion?.Id,
                    Classification = classification,
                    VariableValidationPassed = true,
                    RenderedBody = renderedBody,
                };
            }

            // Step 7: LS-019 dynamic rule engine evaluation (augments LS-018; never replaces)
            if (_ruleEngine != null)
            {
                try
                {
                    var dynReq = new SmsGovernanceRuleEvaluationRequest
                    {
                        TenantId             = request.TenantId == Guid.Empty ? null : request.TenantId,
                        NotificationId       = request.NotificationId,
                        TemplateId           = template?.Id,
                        TemplateVersionId    = approvedVersion?.Id,
                        RenderedBody         = renderedBody,
                        TemplateBody         = request.TemplateBody,
                        Variables            = request.VariablesUsed,
                        ContentClassification = classification,
                        Context              = "content",
                        IsDryRun             = request.IsDryRun,
                        NowUtc               = request.NowUtc,
                    };

                    var dynResult = await _ruleEngine.EvaluateContentAsync(dynReq, ct);

                    // Update effective classification if dynamic engine overrode it
                    if (!string.IsNullOrEmpty(dynResult.EffectiveClassification))
                        classification = dynResult.EffectiveClassification;

                    if (dynResult.ShouldBlock)
                    {
                        if (!request.IsDryRun)
                        {
                            await PersistDecision(request, dynResult.DecisionType, dynResult.ReasonCode,
                                templateId: template?.Id, templateVersionId: approvedVersion?.Id,
                                classification: classification, variableValidationPassed: true,
                                metadata: new
                                {
                                    source            = "dynamic_rule_engine",
                                    matchedRulesCount = dynResult.MatchedRules.Count,
                                    enforcementMode   = dynResult.EnforcementMode,
                                });
                        }

                        return new SmsTemplateGovernanceResult
                        {
                            DecisionType             = dynResult.DecisionType,
                            ReasonCode               = dynResult.ReasonCode,
                            ShouldProceed            = false,
                            ShouldBlock              = true,
                            TemplateId               = template?.Id,
                            TemplateVersionId        = approvedVersion?.Id,
                            Classification           = classification,
                            VariableValidationPassed = true,
                            RenderedBody             = renderedBody,
                        };
                    }

                    if (dynResult.DecisionType == "warn" && dynResult.MatchedRules.Count > 0)
                    {
                        _logger.LogInformation(
                            "SmsTemplateGovernance: dynamic rule warn for template '{Key}' — {Count} rule(s) matched, allowing",
                            request.TemplateKey, dynResult.MatchedRules.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "SmsTemplateGovernanceService: LS-019 dynamic rule engine threw for template '{Key}' — failing open",
                        request.TemplateKey);
                    // Fail open: dynamic rule errors never block delivery
                }
            }

            // All checks passed — allow
            return new SmsTemplateGovernanceResult
            {
                DecisionType = "allow", ReasonCode = "governance_passed",
                ShouldProceed = true, ShouldBlock = false,
                TemplateId = template?.Id, TemplateVersionId = approvedVersion?.Id,
                Classification = classification ?? template?.ContentClassification,
                VariableValidationPassed = true,
                RenderedBody = renderedBody,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SmsTemplateGovernanceService: EvaluateAsync threw for template '{Key}' / notification {NotifId} — {Behavior}",
                request.TemplateKey, request.NotificationId,
                _options.FailOpenOnEvaluationError ? "defaulting to allow" : "defaulting to block");

            if (_options.FailOpenOnEvaluationError)
                return Allow("governance_evaluation_error");

            return new SmsTemplateGovernanceResult
            {
                DecisionType = "block", ReasonCode = "governance_evaluation_error",
                ShouldProceed = false, ShouldBlock = true,
            };
        }
    }

    // ─── Variable validation ──────────────────────────────────────────────────

    public async Task<(bool Passed, List<string> Errors)> ValidateVariablesAsync(
        ValidateTemplateVariablesRequest request,
        CancellationToken ct = default)
    {
        await Task.CompletedTask; // sync logic only — Task for interface consistency

        var errors = new List<string>();

        // Detect unresolved {{token}} patterns in the rendered body
        var unresolvedMatches = UnresolvedTokenRegex().Matches(request.TemplateBody);
        if (unresolvedMatches.Count > 0)
        {
            errors.Add($"Unresolved variable tokens detected: {string.Join(", ", unresolvedMatches.Select(m => m.Value).Distinct())}");
        }

        // Enforce MaxVariableCount on variables used
        if (request.Variables != null && request.Variables.Count > _options.MaxVariableCount)
        {
            errors.Add($"Variable count {request.Variables.Count} exceeds maximum {_options.MaxVariableCount}");
        }

        // Validate required variables from schema if provided
        if (!string.IsNullOrEmpty(request.VariableSchemaJson))
        {
            try
            {
                var schema = JsonSerializer.Deserialize<List<VariableSchemaEntry>>(
                    request.VariableSchemaJson, _jsonOpts);

                if (schema != null)
                {
                    foreach (var entry in schema.Where(e => e.Required))
                    {
                        var hasValue = request.Variables != null &&
                                       request.Variables.TryGetValue(entry.Name, out var v) &&
                                       !string.IsNullOrEmpty(v);
                        if (!hasValue)
                            errors.Add($"Required variable '{{{{ {entry.Name} }}}}' is missing or empty");
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "SmsTemplateGovernanceService: malformed VariableSchemaJson — skipping required-variable check");
            }
        }

        return (errors.Count == 0, errors);
    }

    // ─── Content classification ───────────────────────────────────────────────

    public string ClassifyContent(ClassifyTemplateRequest request)
    {
        var body = (request.TemplateBody ?? string.Empty).ToLowerInvariant();

        // Prohibited check first (highest priority)
        if (ProhibitedKeywords.Any(k => body.Contains(k)))
            return "prohibited";

        // Spam-like URL pattern check
        var urlMatches = UrlRegex().Matches(request.TemplateBody ?? string.Empty);
        if (urlMatches.Count >= 3)
            return "prohibited";

        // Marketing restricted
        if (MarketingRestrictedKeywords.Any(k => body.Contains(k)))
            return "marketing_restricted";

        // Escalation
        if (EscalationKeywords.Any(k => body.Contains(k)))
            return "escalation";

        // Compliance
        if (ComplianceKeywords.Any(k => body.Contains(k)))
            return "compliance";

        // Operational
        if (OperationalKeywords.Any(k => body.Contains(k)))
            return "operational";

        // Transactional (also the default fallback)
        if (TransactionalKeywords.Any(k => body.Contains(k)))
            return "transactional";

        // Default: transactional (safest default for unknown content)
        return "transactional";
    }

    // ─── Template registry CRUD ───────────────────────────────────────────────

    public async Task<Guid> CreateTemplateAsync(CreateSmsTemplateRequest request, CancellationToken ct = default)
    {
        var template = new SmsTemplate
        {
            Id                    = Guid.NewGuid(),
            TenantId              = request.TenantId,
            TemplateKey           = request.TemplateKey,
            Name                  = request.Name,
            Description           = request.Description,
            Category              = request.Category,
            Status                = "draft",
            CurrentVersion        = 0,
            ContentClassification = request.ContentClassification,
            RequiresApproval      = request.RequiresApproval,
            Enabled               = true,
            CreatedAt             = DateTime.UtcNow,
            UpdatedAt             = DateTime.UtcNow,
            CreatedBy             = request.RequestedBy,
            UpdatedBy             = request.RequestedBy,
        };

        _db.SmsTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return template.Id;
    }

    public async Task<bool> UpdateTemplateAsync(UpdateSmsTemplateRequest request, CancellationToken ct = default)
    {
        var template = await _db.SmsTemplates.FindAsync([request.Id], ct);
        if (template == null) return false;
        if (template.Status == "archived") return false;

        if (request.Name                  != null) template.Name                  = request.Name;
        if (request.Description           != null) template.Description           = request.Description;
        if (request.Category              != null) template.Category              = request.Category;
        if (request.ContentClassification != null) template.ContentClassification = request.ContentClassification;
        if (request.RequiresApproval      != null) template.RequiresApproval      = request.RequiresApproval.Value;
        if (request.Enabled               != null) template.Enabled               = request.Enabled.Value;

        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = request.RequestedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ArchiveTemplateAsync(Guid templateId, string? requestedBy, CancellationToken ct = default)
    {
        var template = await _db.SmsTemplates.FindAsync([templateId], ct);
        if (template == null) return false;

        template.Status    = "archived";
        template.Enabled   = false;
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = requestedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ─── Version lifecycle ────────────────────────────────────────────────────

    public async Task<Guid> CreateVersionAsync(CreateSmsTemplateVersionRequest request, CancellationToken ct = default)
    {
        var template = await _db.SmsTemplates.FindAsync([request.TemplateId], ct)
                       ?? throw new InvalidOperationException($"SmsTemplate {request.TemplateId} not found");

        // Classify before creating
        var classification = ClassifyContent(new ClassifyTemplateRequest
        {
            TemplateBody = request.TemplateBody,
        });

        var newVersion = template.CurrentVersion + 1;
        var version = new SmsTemplateVersion
        {
            Id                    = Guid.NewGuid(),
            TemplateId            = request.TemplateId,
            VersionNumber         = newVersion,
            TemplateBody          = request.TemplateBody,
            VariableSchemaJson    = request.VariableSchemaJson,
            ContentClassification = classification,
            ApprovalStatus        = "draft",
            CreatedAt             = DateTime.UtcNow,
            CreatedBy             = request.RequestedBy,
        };

        template.CurrentVersion = newVersion;
        if (template.Status == "approved" || template.Status == "rejected")
            template.Status = "draft"; // new version resets to draft
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = request.RequestedBy;

        _db.SmsTemplateVersions.Add(version);
        await _db.SaveChangesAsync(ct);
        return version.Id;
    }

    public async Task<bool> SubmitForReviewAsync(Guid templateId, string? requestedBy, CancellationToken ct = default)
    {
        var template = await _db.SmsTemplates.FindAsync([templateId], ct);
        if (template == null) return false;
        if (template.Status != "draft" && template.Status != "rejected") return false;

        // Find latest draft version
        var version = await _db.SmsTemplateVersions
            .Where(v => v.TemplateId == templateId && v.VersionNumber == template.CurrentVersion)
            .FirstOrDefaultAsync(ct);
        if (version == null) return false;

        version.ApprovalStatus = "pending_review";
        template.Status        = "pending_review";
        template.UpdatedAt     = DateTime.UtcNow;
        template.UpdatedBy     = requestedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ApproveVersionAsync(Guid templateId, string approvedBy, CancellationToken ct = default)
    {
        var template = await _db.SmsTemplates.FindAsync([templateId], ct);
        if (template == null) return false;
        if (template.Status != "pending_review") return false;

        var version = await _db.SmsTemplateVersions
            .Where(v => v.TemplateId == templateId &&
                        v.VersionNumber == template.CurrentVersion &&
                        v.ApprovalStatus == "pending_review")
            .FirstOrDefaultAsync(ct);
        if (version == null) return false;

        var now                        = DateTime.UtcNow;
        version.ApprovalStatus         = "approved";
        version.ApprovedBy             = approvedBy;
        version.ApprovedAt             = now;
        template.Status                = "approved";
        template.LatestApprovedVersion = version.VersionNumber;
        template.UpdatedAt             = now;
        template.UpdatedBy             = approvedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RejectVersionAsync(Guid templateId, string rejectedBy, string reason, CancellationToken ct = default)
    {
        var template = await _db.SmsTemplates.FindAsync([templateId], ct);
        if (template == null) return false;
        if (template.Status != "pending_review") return false;

        var version = await _db.SmsTemplateVersions
            .Where(v => v.TemplateId == templateId &&
                        v.VersionNumber == template.CurrentVersion &&
                        v.ApprovalStatus == "pending_review")
            .FirstOrDefaultAsync(ct);
        if (version == null) return false;

        version.ApprovalStatus  = "rejected";
        version.RejectionReason = reason;
        template.Status         = "rejected";
        template.UpdatedAt      = DateTime.UtcNow;
        template.UpdatedBy      = rejectedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ─── Audit / query ────────────────────────────────────────────────────────

    public async Task<(int Total, IReadOnlyList<SmsTemplate> Items)> GetTemplatesAsync(
        TemplateGovernancePolicyQuery query, CancellationToken ct = default)
    {
        var q = _db.SmsTemplates.AsNoTracking().AsQueryable();

        if (query.TenantId.HasValue)
            q = q.Where(t => t.TenantId == query.TenantId || t.TenantId == null);
        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(t => t.Status == query.Status);
        if (!string.IsNullOrEmpty(query.Classification))
            q = q.Where(t => t.ContentClassification == query.Classification);
        if (query.Enabled.HasValue)
            q = q.Where(t => t.Enabled == query.Enabled.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(t => t.TemplateKey)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<(int Total, IReadOnlyList<SmsTemplateGovernanceDecision> Items)> GetDecisionsAsync(
        TemplateGovernanceDecisionQuery query, CancellationToken ct = default)
    {
        var q = _db.SmsTemplateGovernanceDecisions.AsNoTracking().AsQueryable();

        if (query.TenantId.HasValue)   q = q.Where(d => d.TenantId   == query.TenantId);
        if (query.TemplateId.HasValue)  q = q.Where(d => d.TemplateId == query.TemplateId);
        if (!string.IsNullOrEmpty(query.DecisionType)) q = q.Where(d => d.DecisionType == query.DecisionType);
        if (!string.IsNullOrEmpty(query.ReasonCode))   q = q.Where(d => d.ReasonCode   == query.ReasonCode);

        if (DateTime.TryParse(query.From, out var from)) q = q.Where(d => d.CreatedAt >= from);
        if (DateTime.TryParse(query.To,   out var to))   q = q.Where(d => d.CreatedAt <= to);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(d => d.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return (total, items);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static SmsTemplateGovernanceResult Allow(string reasonCode) =>
        new()
        {
            DecisionType             = "allow",
            ReasonCode               = reasonCode,
            ShouldProceed            = true,
            ShouldBlock              = false,
            VariableValidationPassed = true,
        };

    private async Task<SmsTemplateGovernanceResult> BlockAndPersist(
        SmsTemplateGovernanceRequest request,
        string  decisionType,
        string  reasonCode,
        Guid?   templateId              = null,
        Guid?   templateVersionId       = null,
        string? classification          = null,
        bool    variableValidationPassed = true,
        object? metadata                = null)
    {
        await PersistDecision(request, decisionType, reasonCode,
            templateId, templateVersionId, classification, variableValidationPassed, metadata);

        return new SmsTemplateGovernanceResult
        {
            DecisionType             = decisionType,
            ReasonCode               = reasonCode,
            ShouldProceed            = false,
            ShouldBlock              = true,
            TemplateId               = templateId,
            TemplateVersionId        = templateVersionId,
            Classification           = classification,
            VariableValidationPassed = variableValidationPassed,
        };
    }

    private async Task PersistDecision(
        SmsTemplateGovernanceRequest request,
        string  decisionType,
        string  reasonCode,
        Guid?   templateId              = null,
        Guid?   templateVersionId       = null,
        string? classification          = null,
        bool    variableValidationPassed = true,
        object? metadata                = null)
    {
        try
        {
            var decision = new SmsTemplateGovernanceDecision
            {
                Id                       = Guid.NewGuid(),
                NotificationId           = request.NotificationId,
                AttemptId                = request.AttemptId,
                TemplateId               = templateId,
                TemplateVersionId        = templateVersionId,
                TenantId                 = request.TenantId,
                DecisionType             = decisionType,
                ReasonCode               = reasonCode,
                ContentClassification    = classification,
                VariableValidationPassed = variableValidationPassed,
                DecisionMetadataJson     = metadata != null
                    ? JsonSerializer.Serialize(metadata, _jsonOpts)
                    : null,
                CreatedAt                = DateTime.UtcNow,
            };

            _db.SmsTemplateGovernanceDecisions.Add(decision);
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SmsTemplateGovernanceService: failed to persist governance decision {Decision}/{Reason}",
                decisionType, reasonCode);
        }
    }

    // ─── Variable schema entry (for JSON deserialization) ─────────────────────

    private sealed class VariableSchemaEntry
    {
        public string Name        { get; set; } = string.Empty;
        public bool   Required    { get; set; }
        public string? Description { get; set; }
    }
}
