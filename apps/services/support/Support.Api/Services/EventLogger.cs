using System.Text.Json;
using Support.Api.Data;
using Support.Api.Domain;

namespace Support.Api.Services;

public interface IEventLogger
{
    /// <summary>Adds an event to the change tracker. Caller is responsible for SaveChangesAsync.</summary>
    SupportTicketEvent Log(Guid ticketId, string tenantId, string eventType, string summary,
        object? metadata = null, string? actorUserId = null);
}

public class EventLogger : IEventLogger
{
    private readonly SupportDbContext _db;
    private readonly ILogger<EventLogger> _log;

    public EventLogger(SupportDbContext db, ILogger<EventLogger> log)
    {
        _db = db;
        _log = log;
    }

    public SupportTicketEvent Log(Guid ticketId, string tenantId, string eventType, string summary,
        object? metadata = null, string? actorUserId = null)
    {
        var ev = new SupportTicketEvent
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            TenantId = tenantId,
            EventType = eventType,
            Summary = summary,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata),
            ActorUserId = actorUserId,
            CreatedAt = DateTime.UtcNow,
        };
        _db.TicketEvents.Add(ev);
        _log.LogDebug("Event {EventType} queued for ticket {TicketId} tenant={TenantId}", eventType, ticketId, tenantId);
        return ev;
    }
}
