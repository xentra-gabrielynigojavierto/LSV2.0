using System.Text.Json;
using PlatformAuditEventService.DTOs.Query;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Mapping;

/// <summary>
/// Stateless mapper between <see cref="AuditEventRecord"/> (persistence model)
/// and <see cref="AuditEventRecordResponse"/> (API response DTO).
///
/// Design notes:
/// - Hash is only populated when <c>exposeHash</c> is true on the <see cref="ToResponse"/> call.
/// - IpAddress and UserAgent are always passed through; redaction is the caller's
///   responsibility (applied in the query service based on <c>QueryAuthOptions</c>).
/// - TagsJson is deserialized defensively: a null or malformed value yields an empty list.
/// - Before/After/Metadata are returned as raw JSON strings — no re-serialisation.
/// </summary>
public static class AuditEventRecordMapper
{
    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Map a single <see cref="AuditEventRecord"/> to its API response representation.
    /// </summary>
    /// <param name="record">The source entity.</param>
    /// <param name="exposeHash">
    /// When true, the <see cref="AuditEventRecordResponse.Hash"/> field is populated.
    /// Controlled by <c>QueryAuth:ExposeIntegrityHash</c>.
    /// </param>
    /// <param name="redactNetworkIdentifiers">
    /// When true, <c>Actor.IpAddress</c> and <c>Actor.UserAgent</c> are redacted to null.
    /// Apply for callers whose role does not grant access to network-level PII.
    /// </param>
    public static AuditEventRecordResponse ToResponse(
        AuditEventRecord record,
        bool exposeHash                = false,
        bool redactNetworkIdentifiers  = false)
    {
        var entity = (record.EntityType is not null || record.EntityId is not null)
            ? new AuditEventEntityResponseDto
            {
                Type = record.EntityType,
                Id   = record.EntityId,
            }
            : null;

        return new AuditEventRecordResponse
        {
            AuditId         = record.AuditId,
            EventId         = record.EventId,
            EventType       = record.EventType,
            EventCategory   = record.EventCategory,
            SourceSystem    = record.SourceSystem,
            SourceService   = record.SourceService,
            SourceEnvironment = record.SourceEnvironment,

            Scope = new AuditEventScopeResponseDto
            {
                ScopeType      = record.ScopeType,
                PlatformId     = record.PlatformId?.ToString(),
                TenantId       = record.TenantId,
                OrganizationId = record.OrganizationId,
                UserScopeId    = record.UserScopeId,
            },

            Actor = new AuditEventActorResponseDto
            {
                Id        = record.ActorId,
                Type      = record.ActorType,
                Name      = record.ActorName,
                IpAddress = redactNetworkIdentifiers ? null : record.ActorIpAddress,
                UserAgent = redactNetworkIdentifiers ? null : record.ActorUserAgent,
            },

            Entity = entity,

            Action      = record.Action,
            Description = record.Description,
            Before      = record.BeforeJson,
            After       = record.AfterJson,
            Metadata    = record.MetadataJson,

            CorrelationId = record.CorrelationId,
            RequestId     = record.RequestId,
            SessionId     = record.SessionId,

            Visibility = record.VisibilityScope,
            Severity   = record.Severity,

            OccurredAtUtc = record.OccurredAtUtc,
            RecordedAtUtc = record.RecordedAtUtc,

            Hash      = exposeHash ? record.Hash : null,
            IsReplay  = record.IsReplay,

            Tags = DeserializeTags(record.TagsJson),
        };
    }

    /// <summary>
    /// Map a collection of records to response DTOs.
    /// </summary>
    public static IReadOnlyList<AuditEventRecordResponse> ToResponseList(
        IEnumerable<AuditEventRecord> records,
        bool exposeHash               = false,
        bool redactNetworkIdentifiers = false)
    {
        return records
            .Select(r => ToResponse(r, exposeHash, redactNetworkIdentifiers))
            .ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static IReadOnlyList<string> DeserializeTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson, _jsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
