using Monitoring.Domain.Common;

namespace Monitoring.Domain.Monitoring;

/// <summary>
/// Durable, mutable, **one-row-per-monitored-entity** projection of the
/// most recent execution outcome plus the evaluated <see cref="EntityStatus"/>.
///
/// <para>This is the persistence-side companion to <see cref="StatusEvaluator"/>.
/// It is updated in-place every cycle (upsert) so a current-state read
/// is O(1) — no scan over the append-only <see cref="CheckResultRecord"/>
/// history table.</para>
///
/// <para><b>Mutability vs. history</b>: unlike <see cref="CheckResultRecord"/>
/// (insert-only), this row is overwritten each time the entity executes,
/// so it implements <see cref="IAuditableEntity"/> and inherits the
/// existing SaveChanges audit-stamping (<c>CreatedAtUtc</c> on first
/// upsert, <c>UpdatedAtUtc</c> on every change).</para>
///
/// <para><b>Scope/relationship</b>: <see cref="MonitoredEntityId"/> is
/// both the primary key and the foreign key to <c>monitored_entities(id)</c>.
/// One monitored entity therefore has at most one current-status row.
/// No navigation property exists on either side — current-status is
/// queried directly when needed.</para>
///
/// <para><b>Defensive truncation</b>: per-field length caps mirror the
/// EF mapping so a producer that drifts can never blow a column.</para>
/// </summary>
public class EntityCurrentStatus : IAuditableEntity
{
    public const int LastMessageMaxLength = 500;
    public const int LastErrorTypeMaxLength = 100;

    public Guid MonitoredEntityId { get; private set; }
    public EntityStatus CurrentStatus { get; private set; }
    public CheckOutcome LastOutcome { get; private set; }
    public int? LastStatusCode { get; private set; }
    public long LastElapsedMs { get; private set; }
    public DateTime LastCheckedAtUtc { get; private set; }
    public string LastMessage { get; private set; } = string.Empty;
    public string? LastErrorType { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>EF Core constructor. Do not use from application code.</summary>
    private EntityCurrentStatus() { }

    /// <summary>
    /// Creates a fresh current-status row. Used by the persistence writer
    /// the first time an entity is executed. Subsequent updates flow
    /// through <see cref="ApplyResult"/>.
    /// </summary>
    public EntityCurrentStatus(
        Guid monitoredEntityId,
        EntityStatus currentStatus,
        CheckOutcome lastOutcome,
        int? lastStatusCode,
        long lastElapsedMs,
        DateTime lastCheckedAtUtc,
        string lastMessage,
        string? lastErrorType)
    {
        if (monitoredEntityId == Guid.Empty)
        {
            throw new ArgumentException(
                "MonitoredEntityId must be a non-empty Guid.", nameof(monitoredEntityId));
        }

        MonitoredEntityId = monitoredEntityId;
        Apply(currentStatus, lastOutcome, lastStatusCode, lastElapsedMs,
              lastCheckedAtUtc, lastMessage, lastErrorType);
    }

    /// <summary>
    /// Applies a newer evaluated state on top of the existing row.
    /// Audit timestamps are stamped by the SaveChanges interceptor.
    /// </summary>
    public void ApplyResult(
        EntityStatus currentStatus,
        CheckOutcome lastOutcome,
        int? lastStatusCode,
        long lastElapsedMs,
        DateTime lastCheckedAtUtc,
        string lastMessage,
        string? lastErrorType)
    {
        Apply(currentStatus, lastOutcome, lastStatusCode, lastElapsedMs,
              lastCheckedAtUtc, lastMessage, lastErrorType);
    }

    private void Apply(
        EntityStatus currentStatus,
        CheckOutcome lastOutcome,
        int? lastStatusCode,
        long lastElapsedMs,
        DateTime lastCheckedAtUtc,
        string lastMessage,
        string? lastErrorType)
    {
        CurrentStatus = currentStatus;
        LastOutcome = lastOutcome;
        LastStatusCode = lastStatusCode;
        LastElapsedMs = lastElapsedMs;
        LastCheckedAtUtc = lastCheckedAtUtc;
        LastMessage = Truncate(lastMessage ?? string.Empty, LastMessageMaxLength);
        LastErrorType = lastErrorType is null
            ? null
            : Truncate(lastErrorType, LastErrorTypeMaxLength);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    void IAuditableEntity.SetCreatedAt(DateTime utcNow) => CreatedAtUtc = utcNow;
    void IAuditableEntity.SetUpdatedAt(DateTime utcNow) => UpdatedAtUtc = utcNow;
}
