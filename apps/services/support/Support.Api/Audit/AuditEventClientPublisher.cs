using System.Text.Json;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Options;

namespace Support.Api.Audit;

/// <summary>
/// LS-SUP-INT-05 — <see cref="IAuditPublisher"/> implementation that maps
/// <see cref="SupportAuditEvent"/> records to the platform
/// <see cref="IngestAuditEventRequest"/> wire contract and delegates
/// delivery to the shared <see cref="IAuditEventClient"/>.
///
/// <para>
/// The <see cref="IAuditEventClient"/> is fire-and-observe: it never throws
/// on transport failures, returning an <c>IngestResult</c> instead.
/// This publisher logs non-accepted results and returns without rethrowing
/// so that Support Service write paths are never blocked by audit transport issues.
/// </para>
///
/// Registered by <c>Program.cs</c> when <c>Support:Audit:Mode = Http</c>.
/// The client's own configuration lives in the <c>AuditClient</c> section.
/// </summary>
public sealed class AuditEventClientPublisher : IAuditPublisher
{
    private static readonly JsonSerializerOptions MetadataJsonOpts = new()
    {
        WriteIndented = false,
    };

    private readonly IAuditEventClient _client;
    private readonly IOptionsMonitor<AuditOptions> _options;
    private readonly ILogger<AuditEventClientPublisher> _log;

    public AuditEventClientPublisher(
        IAuditEventClient client,
        IOptionsMonitor<AuditOptions> options,
        ILogger<AuditEventClientPublisher> log)
    {
        _client  = client;
        _options = options;
        _log     = log;
    }

    public async Task PublishAsync(SupportAuditEvent auditEvent, CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            _log.LogDebug(
                "Audit disabled; suppressing HTTP dispatch event={EventType} resource={ResourceId}",
                auditEvent.EventType, auditEvent.ResourceId);
            return;
        }

        var occurred = new DateTimeOffset(auditEvent.OccurredAt, TimeSpan.Zero);

        var request = new IngestAuditEventRequest
        {
            EventType     = auditEvent.EventType,
            EventCategory = EventCategory.Business,
            SourceSystem  = "support-service",
            SourceService = "support-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = MapSeverity(auditEvent.Outcome),
            OccurredAtUtc = occurred,

            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = auditEvent.TenantId,
            },

            Actor = new AuditEventActorDto
            {
                Type      = auditEvent.ActorUserId is not null ? ActorType.User : ActorType.System,
                Id        = auditEvent.ActorUserId,
                Name      = auditEvent.ActorEmail,
                IpAddress = auditEvent.IpAddress,
                UserAgent = auditEvent.UserAgent,
            },

            Entity = new AuditEventEntityDto
            {
                Type = auditEvent.ResourceType,
                Id   = auditEvent.ResourceId,
            },

            Action      = ToPascalCase(auditEvent.Action),
            Outcome     = auditEvent.Outcome,
            Description = BuildDescription(auditEvent),
            Metadata    = SerializeMetadata(auditEvent.Metadata),

            CorrelationId  = auditEvent.CorrelationId,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                occurred,
                "support-service",
                auditEvent.EventType,
                auditEvent.ResourceId),

            Tags = BuildTags(auditEvent),
        };

        var result = await _client.IngestAsync(request, ct);

        if (!result.Accepted)
        {
            _log.LogWarning(
                "Audit ingest not accepted: reason={Reason} status={Status} event={EventType} resource={ResourceId}",
                result.RejectionReason, result.StatusCode, auditEvent.EventType, auditEvent.ResourceId);
        }
        else
        {
            _log.LogDebug(
                "Audit event ingested: auditId={AuditId} event={EventType} resource={ResourceId}",
                result.AuditId, auditEvent.EventType, auditEvent.ResourceId);
        }
    }

    private static SeverityLevel MapSeverity(string outcome)
        => outcome.Equals("failure", StringComparison.OrdinalIgnoreCase)
            ? SeverityLevel.Warn
            : SeverityLevel.Info;

    private static string ToPascalCase(string action)
    {
        if (string.IsNullOrWhiteSpace(action)) return action;
        var parts = action.Split('_', '-', ' ');
        return string.Concat(parts.Select(p =>
            p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private static string BuildDescription(SupportAuditEvent evt)
    {
        var actor = evt.ActorEmail ?? evt.ActorUserId ?? "system";
        return $"{actor} performed {evt.Action} on {evt.ResourceType} {evt.ResourceNumber ?? evt.ResourceId}";
    }

    private static string? SerializeMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        if (metadata.Count == 0) return null;
        try
        {
            return JsonSerializer.Serialize(metadata, MetadataJsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static List<string> BuildTags(SupportAuditEvent evt)
    {
        var tags = new List<string> { "support", evt.ResourceType };
        if (!string.IsNullOrWhiteSpace(evt.ActorUserId))
            tags.Add("user-action");
        return tags;
    }
}
