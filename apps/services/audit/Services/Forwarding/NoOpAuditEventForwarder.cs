using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Services.Forwarding;

/// <summary>
/// Default implementation of <see cref="IAuditEventForwarder"/>.
///
/// When <c>EventForwarding:Enabled = false</c> (the default), this forwarder
/// returns immediately with zero overhead on every call.
///
/// When enabled, it applies the configured filter rules (category, event type
/// prefix, min severity, replay flag) and — for events that pass — maps the
/// <see cref="AuditEventRecord"/> to an <see cref="AuditRecordIntegrationEvent"/>
/// and delegates to <see cref="IIntegrationEventPublisher"/> for delivery.
///
/// In v1 with <c>BrokerType=NoOp</c>, the publisher logs and returns; no external
/// messages are sent. This validates the full pipeline (filtering, mapping, routing)
/// in any environment without requiring broker connectivity.
///
/// Filter evaluation order:
///   1. <c>Enabled</c> — master switch; false = skip immediately.
///   2. <c>ForwardReplayRecords</c> — false = skip replay records (default).
///   3. <c>ForwardCategories</c> — non-empty = must match one of the listed categories.
///   4. <c>ForwardEventTypePrefixes</c> — non-empty = EventType must start with one of the prefixes.
///   5. <c>MinSeverity</c> — record severity must be ≥ this level.
///
/// Thread safety:
///   This implementation is Singleton-safe. All fields are read-only after construction.
/// </summary>
public sealed class NoOpAuditEventForwarder : IAuditEventForwarder
{
    private readonly EventForwardingOptions        _opts;
    private readonly IIntegrationEventPublisher    _publisher;
    private readonly SeverityLevel                 _minSeverity;
    private readonly ILogger<NoOpAuditEventForwarder> _logger;

    public NoOpAuditEventForwarder(
        IOptions<EventForwardingOptions>       opts,
        IIntegrationEventPublisher             publisher,
        ILogger<NoOpAuditEventForwarder>       logger)
    {
        _opts      = opts.Value;
        _publisher = publisher;
        _logger    = logger;

        _minSeverity = Enum.TryParse<SeverityLevel>(_opts.MinSeverity, ignoreCase: true, out var sv)
            ? sv
            : SeverityLevel.Info;
    }

    /// <inheritdoc/>
    public async ValueTask ForwardAsync(AuditEventRecord record, CancellationToken ct = default)
    {
        // ── Filter 1: Master switch ────────────────────────────────────────────
        if (!_opts.Enabled)
            return;

        // ── Filter 2: Replay records ──────────────────────────────────────────
        if (record.IsReplay && !_opts.ForwardReplayRecords)
        {
            _logger.LogDebug(
                "EventForwarding: skipping replay record AuditId={AuditId} EventType={EventType}",
                record.AuditId, record.EventType);
            return;
        }

        // ── Filter 3: Category allowlist ──────────────────────────────────────
        if (_opts.ForwardCategories.Count > 0)
        {
            var categoryName = record.EventCategory.ToString();
            var categoryMatch = _opts.ForwardCategories.Any(c =>
                string.Equals(c, categoryName, StringComparison.OrdinalIgnoreCase));

            if (!categoryMatch)
            {
                _logger.LogDebug(
                    "EventForwarding: skipping category={Category} (not in ForwardCategories). " +
                    "AuditId={AuditId}",
                    categoryName, record.AuditId);
                return;
            }
        }

        // ── Filter 4: EventType prefix allowlist ──────────────────────────────
        if (_opts.ForwardEventTypePrefixes.Count > 0)
        {
            var prefixMatch = _opts.ForwardEventTypePrefixes.Any(p =>
                record.EventType.StartsWith(p, StringComparison.OrdinalIgnoreCase));

            if (!prefixMatch)
            {
                _logger.LogDebug(
                    "EventForwarding: skipping EventType={EventType} (no matching prefix). " +
                    "AuditId={AuditId}",
                    record.EventType, record.AuditId);
                return;
            }
        }

        // ── Filter 5: Minimum severity ────────────────────────────────────────
        if (record.Severity < _minSeverity)
        {
            _logger.LogDebug(
                "EventForwarding: skipping below-minimum severity. " +
                "Severity={Severity} MinSeverity={Min} AuditId={AuditId}",
                record.Severity, _minSeverity, record.AuditId);
            return;
        }

        // ── Map to integration event payload ──────────────────────────────────
        // Deliberately excludes: Hash, PreviousHash, BeforeJson, AfterJson, Tags.
        var payload = new AuditRecordIntegrationEvent
        {
            AuditId        = record.AuditId,
            EventType      = record.EventType,
            EventCategory  = record.EventCategory.ToString(),
            Severity       = record.Severity.ToString(),
            SourceSystem   = record.SourceSystem,
            TenantId       = record.TenantId,
            OrganizationId = record.OrganizationId,
            ActorId        = record.ActorId,
            ActorType      = record.ActorType.ToString(),
            EntityType     = record.EntityType,
            EntityId       = record.EntityId,
            Action         = record.Action,
            OccurredAtUtc  = record.OccurredAtUtc,
            RecordedAtUtc  = record.RecordedAtUtc,
            CorrelationId  = record.CorrelationId,
            IsReplay       = record.IsReplay,
        };

        // ── Build envelope ────────────────────────────────────────────────────
        var eventType     = $"{_opts.SubjectPrefix}record.ingested";
        var publishedAt   = DateTimeOffset.UtcNow;

        var envelope = new IntegrationEvent<AuditRecordIntegrationEvent>
        {
            EventId        = Guid.NewGuid().ToString(),
            EventType      = eventType,
            SchemaVersion  = "1",
            Payload        = payload,
            PublishedAtUtc = publishedAt,
            CorrelationId  = record.CorrelationId,
            SourceService  = "audit",
        };

        _logger.LogDebug(
            "EventForwarding: publishing AuditId={AuditId} EventType={EventType} " +
            "Broker={Broker}",
            record.AuditId, eventType, _publisher.BrokerName);

        await _publisher.PublishAsync(envelope, ct);
    }
}
