using System.Text.Json;
using Flow.Application.Interfaces;
using Flow.Application.Outbox;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Outbox;

/// <summary>
/// LS-FLOW-E10.2 — default <see cref="IOutboxWriter"/>. Adds a
/// <see cref="OutboxMessage"/> to the EF change tracker via
/// <c>OutboxMessages.Add</c> so that the caller's subsequent
/// <c>SaveChangesAsync</c> commits the outbox INSERT and the workflow
/// state UPDATE in the same MySQL transaction.
///
/// <para>
/// Does NOT call <c>SaveChangesAsync</c> itself — atomicity is the whole
/// point, and only the caller knows when the surrounding state mutation
/// is ready to commit.
/// </para>
/// </summary>
public sealed class OutboxWriter : IOutboxWriter
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IFlowDbContext _db;
    private readonly ILogger<OutboxWriter> _log;

    public OutboxWriter(IFlowDbContext db, ILogger<OutboxWriter> log)
    {
        _db = db;
        _log = log;
    }

    public OutboxMessage Enqueue(string eventType, Guid? workflowInstanceId, object payload)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("eventType is required.", nameof(eventType));
        if (payload is null) throw new ArgumentNullException(nameof(payload));

        var row = new OutboxMessage
        {
            Id                 = Guid.NewGuid(),
            EventType          = eventType,
            WorkflowInstanceId = workflowInstanceId,
            PayloadJson        = JsonSerializer.Serialize(payload, payload.GetType(), JsonOpts),
            Status             = OutboxStatus.Pending,
            AttemptCount       = 0,
            NextAttemptAt      = DateTime.UtcNow,
        };

        _db.OutboxMessages.Add(row);

        _log.LogInformation(
            "Outbox enqueue id={OutboxId} type={EventType} workflowInstance={WorkflowInstanceId}",
            row.Id, row.EventType, row.WorkflowInstanceId);

        return row;
    }
}
