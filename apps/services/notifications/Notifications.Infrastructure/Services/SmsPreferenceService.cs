using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;

namespace Notifications.Infrastructure.Services;

public class SmsPreferenceServiceImpl : ISmsPreferenceService
{
    private readonly ISmsPreferenceRepository _repo;
    private readonly ISmsPreferenceHistoryRepository _historyRepo;
    private readonly IAuditEventClient _auditClient;
    private readonly ILogger<SmsPreferenceServiceImpl> _logger;

    private static readonly HashSet<string> OptOutKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "STOP", "STOPALL", "UNSUBSCRIBE", "CANCEL", "END", "QUIT" };

    private static readonly HashSet<string> OptInKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "START", "YES", "UNSTOP" };

    private const string HelpKeyword = "HELP";

    public SmsPreferenceServiceImpl(
        ISmsPreferenceRepository repo,
        ISmsPreferenceHistoryRepository historyRepo,
        IAuditEventClient auditClient,
        ILogger<SmsPreferenceServiceImpl> logger)
    {
        _repo        = repo;
        _historyRepo = historyRepo;
        _auditClient = auditClient;
        _logger      = logger;
    }

    // ── Keyword classification ────────────────────────────────────────────────

    public string? ClassifyKeyword(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return null;
        var trimmed = rawBody.Trim();
        if (OptOutKeywords.Contains(trimmed)) return "opt_out";
        if (OptInKeywords.Contains(trimmed))  return "opt_in";
        if (string.Equals(trimmed, HelpKeyword, StringComparison.OrdinalIgnoreCase)) return "help";
        return null;
    }

    // ── Current preference state ──────────────────────────────────────────────

    public async Task<string> GetPreferenceStateAsync(Guid tenantId, string phone)
    {
        var normalized = NormalizePhone(phone);
        var pref = await _repo.FindAsync(tenantId, normalized);
        return pref?.PreferenceState ?? "unknown";
    }

    // ── Manual preference update (API-driven) ────────────────────────────────

    public async Task<SmsPreferenceDto> SetPreferenceAsync(Guid tenantId, string phone, string state, string? reason, string? actorUserId)
    {
        if (state is not ("opted_in" or "opted_out"))
            throw new ArgumentException($"Invalid preference state: {state}. Must be 'opted_in' or 'opted_out'.", nameof(state));

        var normalized = NormalizePhone(phone);

        // Capture previous state before upsert for history record.
        var existing = await _repo.FindAsync(tenantId, normalized);
        var previousState = existing?.PreferenceState;

        var pref = await _repo.UpsertAsync(new SmsContactPreference
        {
            TenantId        = tenantId,
            Phone           = normalized,
            PreferenceState = state,
            Source          = "manual_update",
            Reason          = reason ?? $"Manually set to {state} by operator",
            UpdatedBy       = actorUserId,
        });

        // Append history record (best-effort).
        try
        {
            await _historyRepo.AppendAsync(new SmsPreferenceHistory
            {
                TenantId      = tenantId,
                Phone         = normalized,
                PreviousState = previousState,
                NewState      = state,
                Source        = "manual_update",
                Reason        = reason ?? $"Manually set to {state} by operator",
                CreatedBy     = actorUserId,
                MetadataJson  = JsonSerializer.Serialize(new { updated_by = actorUserId }),
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to append SMS preference history for manual update"); }

        // Audit: manual_update event.
        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "sms.preference.manual_update",
                Action       = "sms.preference.manual_update",
                SourceSystem = "notifications",
                Outcome      = "success",
                Description  = $"SMS preference manually set to '{state}' for phone {MaskPhone(normalized)}",
                Scope        = new AuditEventScopeDto { TenantId = tenantId.ToString() },
                Entity       = new AuditEventEntityDto { Type = "SMS_PREFERENCE", Id = pref.Id.ToString() },
                Metadata     = JsonSerializer.Serialize(new
                {
                    phone            = MaskPhone(normalized),
                    preference_state = state,
                    previous_state   = previousState,
                    source           = "manual_update",
                    updated_by       = actorUserId,
                    reason           = reason,
                }),
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to audit SMS preference manual update"); }

        // Audit: opted_in / opted_out state-change event.
        var auditEventType = state == "opted_in" ? "sms.preference.opted_in" : "sms.preference.opted_out";
        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = auditEventType,
                Action       = auditEventType,
                SourceSystem = "notifications",
                Outcome      = "success",
                Description  = $"SMS preference state changed to '{state}' via manual update",
                Scope        = new AuditEventScopeDto { TenantId = tenantId.ToString() },
                Entity       = new AuditEventEntityDto { Type = "SMS_PREFERENCE", Id = pref.Id.ToString() },
                Metadata     = JsonSerializer.Serialize(new
                {
                    phone            = MaskPhone(normalized),
                    preference_state = state,
                    previous_state   = previousState,
                    source           = "manual_update",
                }),
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to audit SMS preference state change event"); }

        return MapToDto(pref);
    }

    // ── LS-NOTIF-SMS-003: Context-rich inbound keyword processing ────────────

    /// <summary>
    /// Process an inbound keyword with full tenant/provider resolution context.
    /// Used by WebhookIngestionService after successful tenant resolution.
    /// Writes both current preference state and an immutable history record.
    /// </summary>
    public async Task ProcessInboundKeywordWithContextAsync(InboundSmsKeywordContext ctx)
    {
        var normalized   = NormalizePhone(ctx.FromPhone);
        var maskedFrom   = MaskPhone(normalized);
        var maskedTo     = MaskPhone(NormalizePhone(ctx.ToPhone));

        string newState;
        string auditEventType;
        string source;

        switch (ctx.Keyword)
        {
            case "opt_out":
                newState       = "opted_out";
                auditEventType = "sms.preference.opted_out";
                source         = "inbound_stop_keyword";
                break;

            case "opt_in":
                newState       = "opted_in";
                auditEventType = "sms.preference.opted_in";
                source         = "inbound_start_keyword";
                break;

            case "help":
                // HELP: history + audit, but do NOT change current preference state.
                _logger.LogInformation("SMS HELP keyword from {Phone} to {To}, TenantId={Tid}",
                    maskedFrom, maskedTo, ctx.TenantId);
                await AppendHelpHistoryAsync(ctx, normalized, maskedFrom, maskedTo);
                await AuditHelpKeywordAsync(ctx, maskedFrom, maskedTo);
                return;

            default:
                _logger.LogWarning("ProcessInboundKeywordWithContextAsync: unrecognized keyword '{Keyword}'", ctx.Keyword);
                return;
        }

        // Load previous state before upsert.
        string? previousState = null;
        if (ctx.TenantId.HasValue)
        {
            try
            {
                var existing = await _repo.FindAsync(ctx.TenantId.Value, normalized);
                previousState = existing?.PreferenceState;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not load previous preference state for history"); }
        }

        // Persist current state.
        try
        {
            await _repo.UpsertAsync(new SmsContactPreference
            {
                TenantId          = ctx.TenantId,
                Phone             = normalized,
                PreferenceState   = newState,
                Source            = source,
                Reason            = $"Inbound SMS keyword: {ctx.RawKeyword}",
                KeywordReceived   = ctx.RawKeyword,
                ProviderMessageId = ctx.ProviderMessageId,
            });

            _logger.LogInformation("SMS preference set to {State} for {Phone} via inbound keyword {Keyword} (TenantId={Tid})",
                newState, maskedFrom, ctx.RawKeyword, ctx.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist SMS preference from inbound keyword for {Phone}", maskedFrom);
        }

        // Append history record (best-effort).
        try
        {
            await _historyRepo.AppendAsync(new SmsPreferenceHistory
            {
                TenantId          = ctx.TenantId,
                Phone             = normalized,
                PreviousState     = previousState,
                NewState          = newState,
                Source            = source,
                Reason            = $"Inbound SMS keyword: {ctx.RawKeyword}",
                KeywordReceived   = ctx.RawKeyword,
                Provider          = ctx.Provider,
                ProviderMessageId = ctx.ProviderMessageId,
                ProviderConfigId  = ctx.ProviderConfigId,
                InboundToNumber   = maskedTo,
                MetadataJson      = JsonSerializer.Serialize(new
                {
                    tenant_resolved    = ctx.TenantResolved,
                    provider_config_id = ctx.ProviderConfigId?.ToString(),
                }),
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to append SMS preference history for inbound keyword"); }

        // Audit event with enriched provider context.
        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = auditEventType,
                Action       = auditEventType,
                SourceSystem = "notifications",
                Outcome      = "success",
                Description  = $"SMS {newState.Replace('_', ' ')} via inbound keyword '{ctx.RawKeyword}' from {maskedFrom}",
                Scope        = new AuditEventScopeDto { TenantId = ctx.TenantId.HasValue ? ctx.TenantId.Value.ToString() : string.Empty },
                Metadata     = JsonSerializer.Serialize(new
                {
                    phone              = maskedFrom,
                    inbound_to_number  = maskedTo,
                    preference_state   = newState,
                    previous_state     = previousState,
                    keyword            = ctx.RawKeyword,
                    source             = source,
                    provider           = ctx.Provider,
                    provider_message_id = ctx.ProviderMessageId,
                    provider_config_id = ctx.ProviderConfigId?.ToString(),
                }),
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to audit SMS preference change from inbound keyword"); }
    }

    // ── LS-NOTIF-SMS-002: Legacy inbound keyword processing (backward compat) ─

    /// <summary>
    /// Legacy inbound keyword processor — now delegates to ProcessInboundKeywordWithContextAsync
    /// with minimal context. Kept for backward compatibility with any callers outside the
    /// updated WebhookIngestionService path.
    /// </summary>
    public Task ProcessInboundKeywordAsync(Guid? tenantId, string fromPhone, string keyword, string rawKeyword, string? providerMessageId)
        => ProcessInboundKeywordWithContextAsync(new InboundSmsKeywordContext
        {
            TenantId          = tenantId,
            FromPhone         = fromPhone,
            ToPhone           = string.Empty,
            Keyword           = keyword,
            RawKeyword        = rawKeyword,
            ProviderMessageId = providerMessageId,
            Provider          = "twilio",
            TenantResolved    = tenantId.HasValue,
        });

    // ── LS-NOTIF-SMS-003: Unresolved inbound audit ───────────────────────────

    public async Task AuditUnresolvedInboundAsync(string fromPhone, string toPhone, string? keyword, string? rawKeyword, string? providerMessageId)
    {
        var maskedFrom = MaskPhone(NormalizePhone(fromPhone));
        var maskedTo   = MaskPhone(NormalizePhone(toPhone));

        _logger.LogWarning(
            "Inbound SMS to unresolved number {To} from {From}: keyword={Keyword}, SID={Sid}",
            maskedTo, maskedFrom, keyword ?? "none", providerMessageId);

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "sms.inbound.unresolved_tenant",
                Action       = "sms.inbound.unresolved_tenant",
                SourceSystem = "notifications",
                Outcome      = "warning",
                Description  = $"Inbound SMS to {maskedTo} could not be resolved to a tenant/provider config",
                Metadata     = JsonSerializer.Serialize(new
                {
                    from_phone          = maskedFrom,
                    to_phone            = maskedTo,
                    keyword_category    = keyword,
                    keyword             = rawKeyword,
                    provider            = "twilio",
                    provider_message_id = providerMessageId,
                    reason              = "unresolved_inbound_sms_tenant",
                }),
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to audit unresolved inbound SMS event"); }
    }

    // ── History query ─────────────────────────────────────────────────────────

    public async Task<SmsPreferenceHistoryResult> GetHistoryAsync(Guid tenantId, string phone, int limit = 50, int offset = 0)
    {
        var normalized = NormalizePhone(phone);
        var items  = await _historyRepo.GetByTenantAndPhoneAsync(tenantId, normalized, limit, offset);
        var total  = await _historyRepo.CountByTenantAndPhoneAsync(tenantId, normalized);

        return new SmsPreferenceHistoryResult
        {
            Items  = items.Select(MapHistoryToDto).ToList(),
            Total  = total,
            Limit  = limit,
            Offset = offset,
        };
    }

    // ── List current preferences ──────────────────────────────────────────────

    public async Task<List<SmsPreferenceDto>> ListAsync(Guid tenantId, int limit = 50, int offset = 0)
    {
        var items = await _repo.GetByTenantAsync(tenantId, limit, offset);
        return items.Select(MapToDto).ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task AppendHelpHistoryAsync(InboundSmsKeywordContext ctx, string normalized, string maskedFrom, string maskedTo)
    {
        try
        {
            await _historyRepo.AppendAsync(new SmsPreferenceHistory
            {
                TenantId          = ctx.TenantId,
                Phone             = normalized,
                PreviousState     = null,
                NewState          = "help_requested",
                Source            = "inbound_help_keyword",
                Reason            = "Inbound SMS HELP keyword received",
                KeywordReceived   = ctx.RawKeyword,
                Provider          = ctx.Provider,
                ProviderMessageId = ctx.ProviderMessageId,
                ProviderConfigId  = ctx.ProviderConfigId,
                InboundToNumber   = maskedTo,
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to append HELP keyword history for {Phone}", maskedFrom); }
    }

    private async Task AuditHelpKeywordAsync(InboundSmsKeywordContext ctx, string maskedFrom, string maskedTo)
    {
        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "sms.preference.help_requested",
                Action       = "sms.preference.help_requested",
                SourceSystem = "notifications",
                Outcome      = "success",
                Description  = $"SMS HELP keyword received from {maskedFrom}",
                Scope        = new AuditEventScopeDto { TenantId = ctx.TenantId.HasValue ? ctx.TenantId.Value.ToString() : string.Empty },
                Metadata     = JsonSerializer.Serialize(new
                {
                    phone               = maskedFrom,
                    inbound_to_number   = maskedTo,
                    keyword             = ctx.RawKeyword,
                    provider            = ctx.Provider,
                    provider_message_id = ctx.ProviderMessageId,
                    provider_config_id  = ctx.ProviderConfigId?.ToString(),
                }),
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to audit SMS HELP keyword"); }
    }

    internal static string NormalizePhone(string phone)
        => System.Text.RegularExpressions.Regex.Replace(phone.Trim(), @"[^\d+]", "");

    private static string MaskPhone(string normalized)
        => normalized.Length > 3 ? normalized[..3] + "***" : "***";

    private static SmsPreferenceDto MapToDto(SmsContactPreference p) => new()
    {
        Id              = p.Id,
        TenantId        = p.TenantId,
        Phone           = p.Phone,
        PreferenceState = p.PreferenceState,
        Source          = p.Source,
        Reason          = p.Reason,
        KeywordReceived = p.KeywordReceived,
        UpdatedBy       = p.UpdatedBy,
        CreatedAt       = p.CreatedAt,
        UpdatedAt       = p.UpdatedAt,
    };

    private static SmsPreferenceHistoryDto MapHistoryToDto(SmsPreferenceHistory h) => new()
    {
        Id                = h.Id,
        TenantId          = h.TenantId,
        Phone             = h.Phone,
        PreviousState     = h.PreviousState,
        NewState          = h.NewState,
        Source            = h.Source,
        Reason            = h.Reason,
        KeywordReceived   = h.KeywordReceived,
        Provider          = h.Provider,
        ProviderMessageId = h.ProviderMessageId,
        ProviderConfigId  = h.ProviderConfigId,
        InboundToNumber   = h.InboundToNumber,
        CreatedBy         = h.CreatedBy,
        CreatedAt         = h.CreatedAt,
    };
}
