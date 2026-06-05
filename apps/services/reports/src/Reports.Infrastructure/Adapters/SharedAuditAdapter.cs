using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Audit;

namespace Reports.Infrastructure.Adapters;

public sealed class SharedAuditAdapter : IAuditAdapter
{
    private readonly IAuditEventClient _client;
    private readonly ILogger<SharedAuditAdapter> _log;

    public SharedAuditAdapter(IAuditEventClient client, ILogger<SharedAuditAdapter> log)
    {
        _client = client;
        _log = log;
    }

    public bool IsRealIntegration => true;

    public async Task<AdapterResult<bool>> RecordEventAsync(AuditEventDto auditEvent, CancellationToken ct)
    {
        var request = MapToIngestRequest(auditEvent);

        try
        {
            var result = await _client.IngestAsync(request, ct);

            if (result.Accepted)
            {
                _log.LogDebug(
                    "SharedAuditAdapter: event ingested — EventType={EventType} AuditId={AuditId}",
                    auditEvent.EventType, result.AuditId);
                return AdapterResult<bool>.Ok(true);
            }

            _log.LogWarning(
                "SharedAuditAdapter: event rejected — EventType={EventType} Reason={Reason} StatusCode={StatusCode}",
                auditEvent.EventType, result.RejectionReason, result.StatusCode);
            return AdapterResult<bool>.Fail("AuditRejected", result.RejectionReason ?? "Event rejected by audit service");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "SharedAuditAdapter: transport error — EventType={EventType}",
                auditEvent.EventType);
            return AdapterResult<bool>.Fail("AuditTransportError", ex.Message);
        }
    }

    private static IngestAuditEventRequest MapToIngestRequest(AuditEventDto dto)
    {
        return new IngestAuditEventRequest
        {
            EventType = dto.EventType,
            EventCategory = EventCategory.Business,
            SourceSystem = "legalsynq-platform",
            SourceService = "reports-service",
            Visibility = string.IsNullOrEmpty(dto.TenantId) || dto.TenantId == "system"
                ? VisibilityScope.Platform
                : VisibilityScope.Tenant,
            Severity = dto.Outcome == "Failure" ? SeverityLevel.Warn : SeverityLevel.Info,
            OccurredAtUtc = dto.OccurredAtUtc,
            Scope = new AuditEventScopeDto
            {
                ScopeType = string.IsNullOrEmpty(dto.TenantId) || dto.TenantId == "system"
                    ? ScopeType.Platform
                    : ScopeType.Tenant,
                TenantId = dto.TenantId,
                UserId = dto.ActorUserId,
            },
            Actor = new AuditEventActorDto
            {
                Id = dto.ActorUserId,
                Type = dto.ActorUserId == "system" ? ActorType.System : ActorType.User,
            },
            Entity = new AuditEventEntityDto
            {
                Type = dto.EntityType,
                Id = dto.EntityId,
            },
            Action = dto.Action,
            Description = dto.Description,
            Outcome = dto.Outcome,
            Metadata = dto.MetadataJson,
            CorrelationId = dto.CorrelationId,
            RequestId = dto.RequestId,
            Tags = BuildTags(dto),
        };
    }

    private static List<string>? BuildTags(AuditEventDto dto)
    {
        var tags = new List<string> { "source:reports-service" };
        if (!string.IsNullOrEmpty(dto.ProductCode))
            tags.Add($"product:{dto.ProductCode}");
        if (!string.IsNullOrEmpty(dto.EntityType))
            tags.Add($"entity:{dto.EntityType}");
        return tags;
    }
}
