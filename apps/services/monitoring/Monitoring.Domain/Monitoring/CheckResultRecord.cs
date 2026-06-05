namespace Monitoring.Domain.Monitoring;

/// <summary>
/// Durable, append-only history row for a single executed check.
///
/// <para>This is the persistence-side counterpart of the transient
/// check-result value produced by per-entity executors. They are kept
/// as separate types deliberately:
/// <list type="bullet">
///   <item>The transient result has no identity, no audit fields, and
///     no rowversion — it is a value object used during a single cycle.</item>
///   <item>This record has its own identity, immutable fields, and an
///     explicit insert-time <see cref="CreatedAtUtc"/>.</item>
/// </list>
/// </para>
///
/// <para><b>Immutability</b>: rows are insert-only. There is no edit
/// surface. <c>IAuditableEntity</c> is intentionally <i>not</i>
/// implemented because that interface is for mutable entities whose
/// last-update timestamp moves over time; here it would just be a
/// duplicate column that never changes. Insert time is set once by the
/// writer at the moment the row is constructed.</para>
///
/// <para><b>Scope/relationship</b>: <see cref="MonitoredEntityId"/> is
/// stored as a plain Guid foreign-key column with no navigation property.
/// We deliberately do not introduce a navigation collection on
/// <see cref="MonitoredEntity"/> because (a) results will be queried
/// separately (paged history, filtered by outcome/time, etc.) and (b)
/// loading a monitored entity should never accidentally hydrate its
/// history. The FK constraint configured in the EF mapping prevents
/// orphan rows.</para>
///
/// <para><b>Safety</b>: <see cref="Message"/> and <see cref="ErrorType"/>
/// inherit the same sanitization guarantees as the transient model — they
/// are short, stable, operator-facing strings produced by per-executor
/// classification. Per-field length caps below also act as a defensive
/// bound against accidentally oversized payloads.</para>
/// </summary>
public class CheckResultRecord
{
    public const int EntityNameMaxLength = 200;
    public const int TargetMaxLength = 1000;
    public const int MessageMaxLength = 500;
    public const int ErrorTypeMaxLength = 100;

    public Guid Id { get; private set; }
    public Guid MonitoredEntityId { get; private set; }
    public string EntityName { get; private set; } = string.Empty;
    public MonitoringType MonitoringType { get; private set; }
    public string Target { get; private set; } = string.Empty;
    public bool Succeeded { get; private set; }
    public CheckOutcome Outcome { get; private set; }
    public int? StatusCode { get; private set; }
    public long ElapsedMs { get; private set; }
    public DateTime CheckedAtUtc { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public string? ErrorType { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>EF Core constructor. Do not use from application code.</summary>
    private CheckResultRecord() { }

    /// <summary>
    /// Creates a durable history row. The caller (the persistence writer)
    /// is responsible for stamping <paramref name="createdAtUtc"/> once at
    /// the persistence boundary so every row has a consistent insert time.
    /// </summary>
    public CheckResultRecord(
        Guid id,
        Guid monitoredEntityId,
        string entityName,
        MonitoringType monitoringType,
        string target,
        bool succeeded,
        CheckOutcome outcome,
        int? statusCode,
        long elapsedMs,
        DateTime checkedAtUtc,
        string message,
        string? errorType,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id must be a non-empty Guid.", nameof(id));
        }

        if (monitoredEntityId == Guid.Empty)
        {
            throw new ArgumentException(
                "MonitoredEntityId must be a non-empty Guid.", nameof(monitoredEntityId));
        }

        Id = id;
        MonitoredEntityId = monitoredEntityId;
        EntityName = Truncate(entityName ?? string.Empty, EntityNameMaxLength);
        MonitoringType = monitoringType;
        Target = Truncate(target ?? string.Empty, TargetMaxLength);
        Succeeded = succeeded;
        Outcome = outcome;
        StatusCode = statusCode;
        ElapsedMs = elapsedMs;
        CheckedAtUtc = checkedAtUtc;
        Message = Truncate(message ?? string.Empty, MessageMaxLength);
        ErrorType = errorType is null ? null : Truncate(errorType, ErrorTypeMaxLength);
        CreatedAtUtc = createdAtUtc;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
