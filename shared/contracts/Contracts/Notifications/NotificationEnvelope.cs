namespace Contracts.Notifications;

/// <summary>
/// Canonical, channel-neutral notification contract (E12.1).
///
/// <para>
/// Any producer (Flow workflow engine, task lifecycle, SLA evaluator, admin
/// action handler) builds one of these and hands it to the notifications
/// adapter. The adapter is responsible for fanning out to per-channel
/// providers; producers never reach into the channel layer directly.
/// </para>
///
/// <para>
/// Design rules:
/// <list type="bullet">
///   <item><description>Must be safe to serialise to JSON (records + primitives only).</description></item>
///   <item><description>Must be additive — new fields are appended with sensible defaults so old producers keep compiling.</description></item>
///   <item><description>Channel-neutral — neither the envelope nor the template registry assumes email.</description></item>
///   <item><description>Carries enough metadata (correlationId, outboxId, productKey) for downstream dedupe and audit correlation.</description></item>
/// </list>
/// </para>
/// </summary>
public sealed record NotificationEnvelope
{
    /// <summary>
    /// Stable, well-known template key (see <see cref="NotificationTemplateKeys"/>).
    /// The notifications service uses this to look up a <see cref="NotificationTemplate"/>
    /// in the registry and to resolve the tenant-overridden body/subject.
    /// </summary>
    public required string TemplateKey { get; init; }

    /// <summary>
    /// Owning tenant for the notification. Optional only for system-level
    /// notifications that are deliberately tenant-agnostic (e.g. platform health).
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Product key the originating event belongs to (e.g. <c>flow</c>, <c>tasks</c>,
    /// <c>liens</c>). Used for branding selection and per-product template overrides.
    /// </summary>
    public string? ProductKey { get; init; }

    /// <summary>Type of business entity the notification refers to (e.g. <c>WorkflowInstance</c>, <c>Task</c>).</summary>
    public string? EntityType { get; init; }

    /// <summary>Identifier of the entity (string-encoded so any id type is supported).</summary>
    public string? EntityId { get; init; }

    /// <summary>Recipient addressing — see <see cref="NotificationRecipient"/>.</summary>
    public required NotificationRecipient Recipient { get; init; }

    /// <summary>
    /// Optional pre-rendered subject. Producers SHOULD leave this null and
    /// rely on the template's subject template; supplying a literal value
    /// here is reserved for events that have no registered template.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Optional pre-rendered body. Same guidance as <see cref="Subject"/>.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// Token values that fill the template's <c>{{tokens}}</c>. Keys must
    /// match <see cref="NotificationTemplate.Tokens"/> for required tokens.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? BodyVariables { get; init; }

    /// <summary>One of <see cref="NotificationSeverity"/> — defaults to <see cref="NotificationSeverity.Info"/>.</summary>
    public string Severity { get; init; } = NotificationSeverity.Info;

    /// <summary>One of <see cref="NotificationCategory"/> — defaults to <see cref="NotificationCategory.System"/>.</summary>
    public string Category { get; init; } = NotificationCategory.System;

    /// <summary>
    /// Logical correlation id for cross-service tracing (e.g. workflow
    /// instance id, task id, user-initiated request id). Distinct from
    /// <see cref="OutboxId"/>, which is the dedupe key.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Originating outbox row id (when emitted via Flow's outbox). Acts as
    /// the canonical idempotency key downstream — the notifications service
    /// will refuse a second submit with the same OutboxId for the same tenant.
    /// </summary>
    public string? OutboxId { get; init; }

    /// <summary>
    /// Optional channel hints. When omitted, the notifications service uses
    /// the channels declared on the template definition. When set, it is
    /// treated as the producer's preferred subset; channels not in the
    /// template's applicability list are ignored.
    /// </summary>
    public IReadOnlyList<string>? ChannelHints { get; init; }

    /// <summary>Free-form metadata persisted alongside the notification for audit / debugging.</summary>
    public IReadOnlyDictionary<string, string?>? Metadata { get; init; }
}
