using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-025: Governance execution telemetry service.
///
/// Persists safe aggregate governance execution records.
/// NEVER persists raw payload text, phone numbers, email addresses, webhook URLs, or credentials.
/// Telemetry failures are non-fatal — logged and swallowed.
/// </summary>
public sealed class GovernanceExecutionTelemetryService : IGovernanceExecutionTelemetryService
{
    private readonly NotificationsDbContext _db;
    private readonly IOptions<GovernanceExecutionRuntimeOptions> _options;
    private readonly ILogger<GovernanceExecutionTelemetryService> _logger;

    public GovernanceExecutionTelemetryService(
        NotificationsDbContext db,
        IOptions<GovernanceExecutionRuntimeOptions> options,
        ILogger<GovernanceExecutionTelemetryService> logger)
    {
        _db      = db;
        _options = options;
        _logger  = logger;
    }

    public async Task RecordExecutionAsync(
        GovernanceExecutionContext context,
        GovernanceExecutionResult result,
        bool isSimulation,
        CancellationToken ct = default)
    {
        try
        {
            var opts = _options.Value;

            // Skip persisting allow decisions when not configured (reduce write volume)
            if (!isSimulation &&
                !opts.PersistAllowDecisions &&
                result.DecisionType == GovernanceDecisionTypes.Allow &&
                result.ReasonCode   == GovernanceReasonCodes.NoApplicableRules)
                return;

            var record = new GovernanceExecutionRecord
            {
                NotificationId           = context.NotificationId,
                AttemptId                = context.AttemptId,
                TenantId                 = context.TenantId,
                ChannelType              = result.ChannelType,
                DecisionType             = result.DecisionType,
                ReasonCode               = result.ReasonCode,
                ContentClassification    = result.ContentClassification,
                TopologyResolutionStatus = result.TopologyResolutionStatus,
                EngineStatus             = result.EngineStatus,
                IsSimulation             = isSimulation,
                CreatedAt                = context.ExecutedAtUtc,
                MatchedRuleIdsJson       = SerializeIds(result.MatchedRuleIds),
                MatchedRulePackIdsJson   = SerializeIds(result.MatchedRulePackIds),
                AppliedOverlayIdsJson    = SerializeIds(result.AppliedOverlayIds),
                SafeMetadataJson         = SerializeSafeMetadata(result.SafeMetadata),
            };

            _db.Set<GovernanceExecutionRecord>().Add(record);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Telemetry failure must never block governance evaluation or delivery
            _logger.LogWarning(ex, "GovernanceExecutionTelemetryService: failed to persist telemetry record for channel {Channel} — continuing", result.ChannelType);
        }
    }

    public async Task<GovernanceExecutionPageResult> QueryExecutionsAsync(
        GovernanceExecutionQuery query,
        CancellationToken ct = default)
    {
        var page     = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var q = _db.Set<GovernanceExecutionRecord>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.ChannelType))
            q = q.Where(r => r.ChannelType == query.ChannelType);
        if (query.TenantId.HasValue)
            q = q.Where(r => r.TenantId == query.TenantId);
        if (!string.IsNullOrWhiteSpace(query.DecisionType))
            q = q.Where(r => r.DecisionType == query.DecisionType);
        if (query.IsSimulation.HasValue)
            q = q.Where(r => r.IsSimulation == query.IsSimulation.Value);
        if (query.From.HasValue)
            q = q.Where(r => r.CreatedAt >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(r => r.CreatedAt <= query.To.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new GovernanceExecutionRecordDto
            {
                Id                      = r.Id,
                NotificationId          = r.NotificationId,
                AttemptId               = r.AttemptId,
                TenantId                = r.TenantId,
                ChannelType             = r.ChannelType,
                DecisionType            = r.DecisionType,
                ReasonCode              = r.ReasonCode,
                ContentClassification   = r.ContentClassification,
                TopologyResolutionStatus = r.TopologyResolutionStatus,
                EngineStatus            = r.EngineStatus,
                IsSimulation            = r.IsSimulation,
                CreatedAt               = r.CreatedAt
            })
            .ToListAsync(ct);

        return new GovernanceExecutionPageResult
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        };
    }

    public async Task<GovernanceRuntimeTelemetryResult> GetRuntimeTelemetryAsync(
        GovernanceRuntimeTelemetryQuery query,
        CancellationToken ct = default)
    {
        var q = _db.Set<GovernanceExecutionRecord>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.ChannelType))
            q = q.Where(r => r.ChannelType == query.ChannelType);
        if (query.TenantId.HasValue)
            q = q.Where(r => r.TenantId == query.TenantId);
        if (query.IsSimulation.HasValue)
            q = q.Where(r => r.IsSimulation == query.IsSimulation.Value);
        if (query.From.HasValue)
            q = q.Where(r => r.CreatedAt >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(r => r.CreatedAt <= query.To.Value);

        var records = await q.ToListAsync(ct);

        var byChannel = records
            .GroupBy(r => r.ChannelType)
            .Select(g => new GovernanceChannelTelemetry
            {
                ChannelType      = g.Key,
                TotalExecutions  = g.Count(),
                AllowCount       = g.Count(r => r.DecisionType == GovernanceDecisionTypes.Allow),
                WarnCount        = g.Count(r => r.DecisionType == GovernanceDecisionTypes.Warn),
                BlockCount       = g.Count(r => r.DecisionType == GovernanceDecisionTypes.Block),
                ReviewCount      = g.Count(r => r.DecisionType == GovernanceDecisionTypes.ReviewRequired),
                SuppressCount    = g.Count(r => r.DecisionType == GovernanceDecisionTypes.Suppress),
                LiveCount        = g.Count(r => !r.IsSimulation),
                SimulationCount  = g.Count(r => r.IsSimulation),
                TopologyFailures = g.Count(r => r.TopologyResolutionStatus == "error"),
                EngineFailures   = g.Count(r => r.EngineStatus == "fail_open" || r.EngineStatus == "error"),
            })
            .ToList();

        return new GovernanceRuntimeTelemetryResult
        {
            TotalExecutions      = records.Count,
            LiveExecutions       = records.Count(r => !r.IsSimulation),
            SimulationExecutions = records.Count(r => r.IsSimulation),
            AllowCount           = records.Count(r => r.DecisionType == GovernanceDecisionTypes.Allow),
            WarnCount            = records.Count(r => r.DecisionType == GovernanceDecisionTypes.Warn),
            BlockCount           = records.Count(r => r.DecisionType == GovernanceDecisionTypes.Block),
            ReviewCount          = records.Count(r => r.DecisionType == GovernanceDecisionTypes.ReviewRequired),
            SuppressCount        = records.Count(r => r.DecisionType == GovernanceDecisionTypes.Suppress),
            TopologyFailureCount = records.Count(r => r.TopologyResolutionStatus == "error"),
            EngineFailureCount   = records.Count(r => r.EngineStatus == "fail_open" || r.EngineStatus == "error"),
            ByChannel            = byChannel,
            OldestRecord         = records.Count == 0 ? null : records.Min(r => r.CreatedAt),
            NewestRecord         = records.Count == 0 ? null : records.Max(r => r.CreatedAt),
        };
    }

    public async Task<IReadOnlyList<GovernanceChannelTelemetry>> GetChannelStatusAsync(
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var records = await _db.Set<GovernanceExecutionRecord>()
            .AsNoTracking()
            .Where(r => r.CreatedAt >= cutoff)
            .ToListAsync(ct);

        return records
            .GroupBy(r => r.ChannelType)
            .Select(g => new GovernanceChannelTelemetry
            {
                ChannelType      = g.Key,
                TotalExecutions  = g.Count(),
                AllowCount       = g.Count(r => r.DecisionType == GovernanceDecisionTypes.Allow),
                WarnCount        = g.Count(r => r.DecisionType == GovernanceDecisionTypes.Warn),
                BlockCount       = g.Count(r => r.DecisionType == GovernanceDecisionTypes.Block),
                ReviewCount      = g.Count(r => r.DecisionType == GovernanceDecisionTypes.ReviewRequired),
                SuppressCount    = g.Count(r => r.DecisionType == GovernanceDecisionTypes.Suppress),
                LiveCount        = g.Count(r => !r.IsSimulation),
                SimulationCount  = g.Count(r => r.IsSimulation),
                TopologyFailures = g.Count(r => r.TopologyResolutionStatus == "error"),
                EngineFailures   = g.Count(r => r.EngineStatus == "fail_open" || r.EngineStatus == "error"),
            })
            .ToList();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string? SerializeIds(IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0) return null;
        var json = JsonSerializer.Serialize(ids);
        return json.Length > 2000 ? json[..2000] : json;
    }

    private static string? SerializeSafeMetadata(Dictionary<string, string> meta)
    {
        if (meta.Count == 0) return null;
        try
        {
            var json = JsonSerializer.Serialize(meta);
            return json.Length > 2000 ? json[..2000] : json;
        }
        catch { return null; }
    }
}
