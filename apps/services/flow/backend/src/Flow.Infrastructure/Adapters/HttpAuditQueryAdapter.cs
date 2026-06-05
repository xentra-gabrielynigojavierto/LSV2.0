using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Application.Adapters.AuditAdapter;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Adapters;

/// <summary>
/// E13.1 — HTTP-backed read-only audit query adapter. Calls the
/// existing audit service entity-scoped query
/// <c>GET /audit/entity/{entityType}/{entityId}</c> and unwraps the
/// <c>ApiResponse&lt;AuditEventQueryResponse&gt;</c> envelope into the
/// flat <see cref="AuditEventRecord"/> list consumed by the timeline
/// normalizer.
///
/// Pagination: walks pages of <c>PageSizeInternal</c> until the
/// response signals no next page, capped at <c>HardCap</c> events. The
/// hard cap keeps the timeline response bounded; a paginated UI can
/// be added later (E13.2/E13.3) without breaking this contract.
///
/// Resilience: any non-success status, network exception, or parse
/// failure degrades to the injected fallback adapter (typically
/// <see cref="EmptyAuditQueryAdapter"/>) so a triage view never
/// breaks because the audit service is degraded.
/// </summary>
public sealed class HttpAuditQueryAdapter : IAuditQueryAdapter
{
    private const int PageSizeInternal = 200;
    private const int HardCap          = 1000;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly HttpClient _http;
    private readonly IAuditQueryAdapter _fallback;
    private readonly AuditAuthHeaderProvider _auth;
    private readonly ILogger<HttpAuditQueryAdapter> _log;

    public HttpAuditQueryAdapter(
        HttpClient http,
        IAuditQueryAdapter fallback,
        AuditAuthHeaderProvider auth,
        ILogger<HttpAuditQueryAdapter> log)
    {
        _http     = http;
        _fallback = fallback;
        _auth     = auth;
        _log      = log;
    }

    public async Task<AuditEventFetchResult> GetEventsForEntityAsync(
        string entityType,
        string entityId,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var collected = new List<AuditEventRecord>(capacity: PageSizeInternal);
        var truncated = false;

        try
        {
            var page = 1;
            while (collected.Count < HardCap)
            {
                // Sort ascending so the wire order is already close to
                // what the normalizer wants; the normalizer re-sorts
                // anyway to be paranoid about cross-page boundaries.
                var url = $"audit/entity/{Uri.EscapeDataString(entityType)}/{Uri.EscapeDataString(entityId)}"
                        + $"?page={page}&pageSize={PageSizeInternal}"
                        + "&sortBy=occurredAtUtc&sortDescending=false";

                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    url += $"&tenantId={Uri.EscapeDataString(tenantId)}";
                }

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                // Forward the operator's bearer when present so the
                // audit service's QueryAuthorizer treats this read as
                // the originating user (tenant/org/visibility floors are
                // enforced upstream); fall back to a minted service
                // token, or to anonymous when the audit service runs in
                // QueryAuth:Mode=None.
                req.Headers.Authorization = _auth.GetHeader(
                    fallbackTenantId: tenantId);

                using var resp = await _http.SendAsync(req, cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    _log.LogWarning(
                        "Audit query GET {Url} returned {StatusCode}; falling back to empty.",
                        url, (int)resp.StatusCode);
                    return await _fallback.GetEventsForEntityAsync(entityType, entityId, tenantId, cancellationToken);
                }

                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                var envelope = JsonSerializer.Deserialize<ApiResponseEnvelope<AuditEventQueryPage>>(body, JsonOpts);
                var data     = envelope?.Data;
                if (data is null)
                {
                    // Unexpected shape — degrade to the safe baseline.
                    _log.LogWarning(
                        "Audit query response missing Data envelope (page={Page}); falling back.",
                        page);
                    return await _fallback.GetEventsForEntityAsync(entityType, entityId, tenantId, cancellationToken);
                }

                if (data.Items is { Count: > 0 })
                {
                    foreach (var item in data.Items)
                    {
                        collected.Add(MapItem(item));
                        if (collected.Count >= HardCap) break;
                    }
                }

                if (!data.HasNext) break;

                // We exited the loop body because we hit HardCap and the
                // upstream STILL says there is a next page → report
                // truncation explicitly so the caller's flag is correct
                // even when the cap aligns with a page boundary.
                if (collected.Count >= HardCap)
                {
                    truncated = true;
                    break;
                }

                page++;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit query GET failed for {EntityType}:{EntityId} tenant={TenantId}; falling back.",
                entityType, entityId, tenantId);
            return await _fallback.GetEventsForEntityAsync(entityType, entityId, tenantId, cancellationToken);
        }

        return new AuditEventFetchResult(collected, truncated);
    }

    private static AuditEventRecord MapItem(AuditEventQueryItem item) => new(
        EventId:       (item.AuditId ?? Guid.Empty).ToString(),
        Action:        item.Action ?? string.Empty,
        OccurredAtUtc: item.OccurredAtUtc ?? default,
        AuditId:       item.AuditId,
        EventCategory: item.EventCategory,
        SourceSystem:  item.SourceSystem,
        SourceService: item.SourceService,
        TenantId:      item.Scope?.TenantId,
        ActorId:       item.Actor?.Id,
        ActorName:     item.Actor?.Name,
        ActorType:     item.Actor?.Type,
        EntityType:    item.Entity?.Type,
        EntityId:      item.Entity?.Id,
        Description:   item.Description,
        CorrelationId: item.CorrelationId,
        RequestId:     item.RequestId,
        SessionId:     item.SessionId,
        Severity:      item.Severity,
        Visibility:    item.Visibility,
        MetadataJson:  item.Metadata);

    // ── Wire DTOs ────────────────────────────────────────────────────────
    // Decoupled from the audit service's CLR types so this assembly does
    // not take a reference on the audit project. Field names follow the
    // audit response (camelCase JSON via Web defaults).

    private sealed record ApiResponseEnvelope<T>(bool Success, T? Data);

    private sealed record AuditEventQueryPage(
        IReadOnlyList<AuditEventQueryItem>? Items,
        long TotalCount,
        int Page,
        int PageSize,
        bool HasNext);

    private sealed record AuditEventQueryItem(
        Guid? AuditId,
        string? EventType,
        string? EventCategory,
        string? SourceSystem,
        string? SourceService,
        AuditEventScopeWire? Scope,
        AuditEventActorWire? Actor,
        AuditEventEntityWire? Entity,
        string? Action,
        string? Description,
        string? Metadata,
        string? CorrelationId,
        string? RequestId,
        string? SessionId,
        string? Visibility,
        string? Severity,
        DateTimeOffset? OccurredAtUtc,
        DateTimeOffset? RecordedAtUtc);

    private sealed record AuditEventScopeWire(string? TenantId, string? OrganizationId);
    private sealed record AuditEventActorWire(string? Id, string? Name, string? Type);
    private sealed record AuditEventEntityWire(string? Type, string? Id);
}
