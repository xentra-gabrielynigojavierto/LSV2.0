using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Models;

namespace PlatformAuditEventService.Utilities;

public static class AuditEventMapper
{
    public static AuditEvent ToModel(IngestAuditEventRequest req, byte[] hmacSecret)
    {
        var now   = DateTimeOffset.UtcNow;
        var occAt = req.OccurredAtUtc ?? now;

        var evt = new AuditEvent
        {
            Id             = Guid.NewGuid(),
            Source         = req.Source.Trim(),
            EventType      = req.EventType.Trim().ToLowerInvariant(),
            Category       = req.Category.Trim().ToLowerInvariant(),
            Severity       = req.Severity.Trim().ToUpperInvariant(),
            TenantId       = req.TenantId?.Trim(),
            ActorId        = req.ActorId?.Trim(),
            ActorLabel     = req.ActorLabel?.Trim(),
            TargetType     = req.TargetType?.Trim(),
            TargetId       = req.TargetId?.Trim(),
            Description    = req.Description.Trim(),
            Outcome        = req.Outcome.Trim().ToUpperInvariant(),
            IpAddress      = req.IpAddress?.Trim(),
            UserAgent      = req.UserAgent?.Trim(),
            CorrelationId  = req.CorrelationId?.Trim(),
            Metadata       = req.Metadata,
            OccurredAtUtc  = occAt,
            IngestedAtUtc  = now,
        };

        return evt with { IntegrityHash = IntegrityHasher.Compute(evt, hmacSecret) };
    }

    public static AuditEventResponse ToResponse(AuditEvent evt) => new()
    {
        Id             = evt.Id,
        Source         = evt.Source,
        EventType      = evt.EventType,
        Category       = evt.Category,
        Severity       = evt.Severity,
        TenantId       = evt.TenantId,
        ActorId        = evt.ActorId,
        ActorLabel     = evt.ActorLabel,
        TargetType     = evt.TargetType,
        TargetId       = evt.TargetId,
        Description    = evt.Description,
        Outcome        = evt.Outcome,
        IpAddress      = evt.IpAddress,
        CorrelationId  = evt.CorrelationId,
        Metadata       = evt.Metadata,
        OccurredAtUtc  = evt.OccurredAtUtc,
        IngestedAtUtc  = evt.IngestedAtUtc,
        IntegrityHash  = evt.IntegrityHash,
    };
}
