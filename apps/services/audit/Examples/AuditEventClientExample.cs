// ─────────────────────────────────────────────────────────────────────────────
// AuditEventClientExample.cs
//
// Reference implementation: how a .NET 8 upstream service calls the Platform
// Audit Event Service ingest API.
//
// Copy the IAuditEventClient interface + HttpAuditEventClient implementation
// into a shared infrastructure library (or NuGet package). Register via
// AddAuditEventClient() in each producer service's Program.cs.
//
// Endpoints exercised:
//   POST /internal/audit/events          — single event ingest
//   POST /internal/audit/events/batch    — batch event ingest (up to 500)
//
// Auth: x-service-token header (ServiceToken mode).
//   In Mode=None (dev), the header is ignored — safe to omit locally.
// ─────────────────────────────────────────────────────────────────────────────

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Examples;

// ─────────────────────────────────────────────────────────────────────────────
// Configuration
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Bound from "AuditClient" in appsettings.json of the *producer* service.
///
/// Example appsettings.json:
/// <code>
/// "AuditClient": {
///   "BaseUrl":       "http://platform-audit-event-service:5007",
///   "ServiceToken":  "",   // inject via env: AuditClient__ServiceToken
///   "SourceSystem":  "identity-service",
///   "SourceService": "auth-api",
///   "TimeoutSeconds": 5
/// }
/// </code>
/// </summary>
public sealed class AuditClientOptions
{
    public const string SectionName = "AuditClient";

    /// <summary>Base URL of the Platform Audit Event Service. No trailing slash.</summary>
    public string BaseUrl { get; set; } = "http://platform-audit-event-service:5007";

    /// <summary>
    /// Service token for x-service-token header auth.
    /// Inject via environment variable — never commit.
    /// Empty string = skip header (for Mode=None development environments).
    /// </summary>
    public string ServiceToken { get; set; } = string.Empty;

    /// <summary>Value sent in x-source-system header.</summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>Value sent in x-source-service header.</summary>
    public string SourceService { get; set; } = string.Empty;

    /// <summary>Per-request timeout in seconds. Default: 5.</summary>
    public int TimeoutSeconds { get; set; } = 5;
}

// ─────────────────────────────────────────────────────────────────────────────
// Result types
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Outcome of a single event ingest call.</summary>
public sealed record IngestResult(
    bool    Accepted,
    Guid?   AuditId,
    string? RejectionReason,
    int     StatusCode);

/// <summary>Outcome of a batch ingest call, plus per-item details.</summary>
public sealed record BatchIngestResult(
    int                  Submitted,
    int                  Accepted,
    int                  Rejected,
    IReadOnlyList<IngestItemResult> Results,
    int                  StatusCode);

// ─────────────────────────────────────────────────────────────────────────────
// Interface — producers code against this, not the concrete HTTP client
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Audit event submission contract for producer services.
///
/// Usage:
///   Inject <see cref="IAuditEventClient"/> into any service that needs to emit
///   audit events. Both methods are fire-and-observe — they never throw for
///   delivery failures (they return <see cref="IngestResult.Accepted"/> = false
///   instead). Persist-first, audit-second: do not gate business operations on
///   the audit call's success.
/// </summary>
public interface IAuditEventClient
{
    /// <summary>
    /// Submit a single audit event. Returns the assigned <c>AuditId</c> on success.
    /// Idempotent when <see cref="IngestAuditEventRequest.IdempotencyKey"/> is supplied.
    /// </summary>
    Task<IngestResult> IngestAsync(
        IngestAuditEventRequest request,
        CancellationToken       ct = default);

    /// <summary>
    /// Submit a batch of audit events (up to 500). Returns per-item results.
    /// Partial acceptance is possible — inspect <see cref="BatchIngestResult.Results"/>.
    /// </summary>
    Task<BatchIngestResult> IngestBatchAsync(
        BatchIngestRequest request,
        CancellationToken  ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// HTTP implementation
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// <see cref="IAuditEventClient"/> implementation backed by <see cref="HttpClient"/>.
///
/// Registered via <see cref="AuditClientServiceCollectionExtensions.AddAuditEventClient"/>.
/// Accepts an <see cref="IHttpClientFactory"/>-managed <see cref="HttpClient"/> named
/// "AuditEventClient" to benefit from connection pooling and retry policies.
/// </summary>
public sealed class HttpAuditEventClient : IAuditEventClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Converters                  = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private const string SingleEndpoint = "/internal/audit/events";
    private const string BatchEndpoint  = "/internal/audit/events/batch";

    private readonly HttpClient                     _http;
    private readonly AuditClientOptions             _opts;
    private readonly ILogger<HttpAuditEventClient>  _logger;

    public HttpAuditEventClient(
        HttpClient                    http,
        IOptions<AuditClientOptions>  opts,
        ILogger<HttpAuditEventClient> logger)
    {
        _http   = http;
        _opts   = opts.Value;
        _logger = logger;

        _http.BaseAddress = new Uri(_opts.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout     = TimeSpan.FromSeconds(_opts.TimeoutSeconds);
    }

    /// <inheritdoc/>
    public async Task<IngestResult> IngestAsync(
        IngestAuditEventRequest request,
        CancellationToken       ct = default)
    {
        using var httpRequest = BuildRequest(HttpMethod.Post, SingleEndpoint, request);

        try
        {
            using var response = await _http.SendAsync(httpRequest, ct);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<IngestItemResult>>(
                    JsonOpts, ct);

                var auditId = body?.Data?.AuditId;

                _logger.LogDebug(
                    "AuditEvent ingested: AuditId={AuditId} EventType={EventType}",
                    auditId, request.EventType);

                return new IngestResult(
                    Accepted:        true,
                    AuditId:         auditId,
                    RejectionReason: null,
                    StatusCode:      (int)response.StatusCode);
            }

            // 409 Conflict = duplicate idempotency key — treat as accepted
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogDebug(
                    "AuditEvent already ingested (duplicate IdempotencyKey): EventType={EventType} Key={Key}",
                    request.EventType, request.IdempotencyKey);

                return new IngestResult(
                    Accepted:        true,
                    AuditId:         null,
                    RejectionReason: "DuplicateIdempotencyKey",
                    StatusCode:      (int)response.StatusCode);
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);

            _logger.LogWarning(
                "AuditEvent ingest rejected: StatusCode={Code} EventType={EventType} Body={Body}",
                (int)response.StatusCode, request.EventType, errorBody);

            return new IngestResult(
                Accepted:        false,
                AuditId:         null,
                RejectionReason: $"HTTP{(int)response.StatusCode}",
                StatusCode:      (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "AuditEvent ingest failed (transport): EventType={EventType}",
                request.EventType);

            return new IngestResult(
                Accepted:        false,
                AuditId:         null,
                RejectionReason: "TransportError",
                StatusCode:      0);
        }
    }

    /// <inheritdoc/>
    public async Task<BatchIngestResult> IngestBatchAsync(
        BatchIngestRequest request,
        CancellationToken  ct = default)
    {
        using var httpRequest = BuildRequest(HttpMethod.Post, BatchEndpoint, request);

        try
        {
            using var response = await _http.SendAsync(httpRequest, ct);

            var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<BatchIngestResponseDto>>(
                JsonOpts, ct);

            var data = body?.Data;

            if (data is null)
            {
                _logger.LogWarning(
                    "AuditEvent batch ingest: unexpected empty response body. StatusCode={Code}",
                    (int)response.StatusCode);

                return new BatchIngestResult(
                    Submitted:  request.Events.Count,
                    Accepted:   0,
                    Rejected:   request.Events.Count,
                    Results:    [],
                    StatusCode: (int)response.StatusCode);
            }

            _logger.LogInformation(
                "AuditEvent batch ingest complete: Submitted={Submitted} Accepted={Accepted} " +
                "Rejected={Rejected} BatchCorrelationId={BatchCorrelationId}",
                data.Submitted, data.Accepted, data.Rejected, request.BatchCorrelationId);

            return new BatchIngestResult(
                Submitted:  data.Submitted,
                Accepted:   data.Accepted,
                Rejected:   data.Rejected,
                Results:    data.Results ?? [],
                StatusCode: (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "AuditEvent batch ingest failed (transport): BatchSize={Count}",
                request.Events.Count);

            return new BatchIngestResult(
                Submitted:  request.Events.Count,
                Accepted:   0,
                Rejected:   request.Events.Count,
                Results:    [],
                StatusCode: 0);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private HttpRequestMessage BuildRequest<T>(HttpMethod method, string path, T body)
    {
        var req = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };

        if (!string.IsNullOrEmpty(_opts.ServiceToken))
            req.Headers.Add("x-service-token", _opts.ServiceToken);

        if (!string.IsNullOrEmpty(_opts.SourceSystem))
            req.Headers.Add("x-source-system", _opts.SourceSystem);

        if (!string.IsNullOrEmpty(_opts.SourceService))
            req.Headers.Add("x-source-service", _opts.SourceService);

        return req;
    }

    // Minimal response envelope shapes (mirrors ApiResponse<T> from the audit service)
    private sealed class ApiEnvelope<T> { public T? Data { get; set; } }
    private sealed class BatchIngestResponseDto
    {
        public int Submitted { get; set; }
        public int Accepted  { get; set; }
        public int Rejected  { get; set; }
        public IReadOnlyList<IngestItemResult>? Results { get; set; }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DI Registration Extension
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Service collection extensions for registering the audit event client.
///
/// In the producer service's Program.cs:
/// <code>
/// builder.Services.AddAuditEventClient(builder.Configuration);
/// </code>
///
/// In appsettings.json of the producer service:
/// <code>
/// "AuditClient": {
///   "BaseUrl":       "http://platform-audit-event-service:5007",
///   "ServiceToken":  "",
///   "SourceSystem":  "my-service",
///   "SourceService": "my-api",
///   "TimeoutSeconds": 5
/// }
/// </code>
/// </summary>
public static class AuditClientServiceCollectionExtensions
{
    public static IServiceCollection AddAuditEventClient(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<AuditClientOptions>(
            configuration.GetSection(AuditClientOptions.SectionName));

        services.AddHttpClient<IAuditEventClient, HttpAuditEventClient>("AuditEventClient");

        return services;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Idempotency Key Builder
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Deterministic idempotency key builder.
///
/// Keys are dot-joined segments, URL-safe, and truncated to 280 chars to stay
/// well within the 300-character limit after any prefix additions.
///
/// Usage:
///   var key = IdempotencyKey.For("identity-service", "user.login.succeeded", "user-789", occurredAt);
/// </summary>
public static class IdempotencyKey
{
    private const int MaxLength = 280;

    /// <summary>
    /// Build a deterministic idempotency key from stable event fields.
    /// All segments are lowercased and URL-encoded.
    /// </summary>
    public static string For(params string[] segments)
    {
        var key = string.Join(":", segments
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => Uri.EscapeDataString(s.Trim().ToLowerInvariant())));

        return key.Length > MaxLength ? key[..MaxLength] : key;
    }

    /// <summary>
    /// Build a key including a UTC timestamp rounded to the minute.
    /// Prevents cross-minute collisions for events that may legitimately repeat.
    /// </summary>
    public static string ForWithTimestamp(DateTimeOffset occurredAt, params string[] segments)
    {
        var ts = occurredAt.UtcDateTime.ToString("yyyyMMddTHHmmssZ");
        return For([..segments, ts]);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Event Factories — all 11 example scenarios
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Static factory methods for the 11 canonical producer event patterns.
///
/// Copy and adapt these factories into your domain service. The patterns shown
/// here illustrate the correct field choices for each scenario.
/// </summary>
public static class AuditEventExamples
{
    // ── 1. Login Success ──────────────────────────────────────────────────────

    /// <summary>Successful user authentication.</summary>
    public static IngestAuditEventRequest LoginSuccess(
        string         tenantId,
        string         userId,
        string         userName,
        string         ipAddress,
        string         userAgent,
        string?        sessionId        = null,
        string?        correlationId    = null,
        string?        requestId        = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new IngestAuditEventRequest
        {
            EventType      = "user.login.succeeded",
            EventCategory  = EventCategory.Security,
            SourceSystem   = "identity-service",
            SourceService  = "auth-api",
            Visibility     = VisibilityScope.Tenant,
            Severity       = SeverityLevel.Info,
            OccurredAtUtc  = now,
            Scope          = TenantScope(tenantId),
            Actor          = UserActor(userId, userName, ipAddress, userAgent),
            Action         = "LoginSucceeded",
            Description    = $"User {userName} authenticated successfully.",
            CorrelationId  = correlationId,
            RequestId      = requestId,
            SessionId      = sessionId,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "user.login.succeeded", userId),
            Tags           = ["auth", "login"],
        };
    }

    // ── 2. Login Failure ──────────────────────────────────────────────────────

    /// <summary>Failed authentication attempt.</summary>
    public static IngestAuditEventRequest LoginFailure(
        string         tenantId,
        string         attemptedEmail,
        string         ipAddress,
        string         userAgent,
        string         failureReason,
        int            attemptCount,
        string?        correlationId = null,
        string?        requestId     = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new IngestAuditEventRequest
        {
            EventType      = "user.login.failed",
            EventCategory  = EventCategory.Security,
            SourceSystem   = "identity-service",
            SourceService  = "auth-api",
            Visibility     = VisibilityScope.Tenant,
            Severity       = SeverityLevel.Warn,
            OccurredAtUtc  = now,
            Scope          = TenantScope(tenantId),
            Actor          = new AuditEventActorDto
            {
                Type      = ActorType.Anonymous,
                IpAddress = ipAddress,
                UserAgent = userAgent,
            },
            Action         = "LoginFailed",
            Description    = $"Login attempt failed for {attemptedEmail}: {failureReason}.",
            Metadata       = JsonSerializer.Serialize(new { reason = failureReason, attemptCount }),
            CorrelationId  = correlationId,
            RequestId      = requestId,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "user.login.failed", attemptedEmail),
            Tags           = ["auth", "login-failure"],
        };
    }

    // ── 3. Authorization Denied ───────────────────────────────────────────────

    /// <summary>Access to a protected resource was denied.</summary>
    public static IngestAuditEventRequest AuthorizationDenied(
        string         tenantId,
        string         organizationId,
        string         userId,
        string         userName,
        string         entityType,
        string         entityId,
        string         requiredPermission,
        string?        correlationId = null,
        string?        requestId     = null,
        string?        sessionId     = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new IngestAuditEventRequest
        {
            EventType      = "user.authorization.denied",
            EventCategory  = EventCategory.Security,
            SourceSystem   = "care-connect",
            SourceService  = "authorization-middleware",
            Visibility     = VisibilityScope.Tenant,
            Severity       = SeverityLevel.Warn,
            OccurredAtUtc  = now,
            Scope          = OrganizationScope(tenantId, organizationId),
            Actor          = UserActor(userId, userName),
            Entity         = new AuditEventEntityDto { Type = entityType, Id = entityId },
            Action         = "AuthorizationDenied",
            Description    = $"User {userName} was denied access to {entityType} {entityId}: {requiredPermission} required.",
            Metadata       = JsonSerializer.Serialize(new { requiredPermission }),
            CorrelationId  = correlationId,
            RequestId      = requestId,
            SessionId      = sessionId,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "care-connect", "user.authorization.denied", userId, entityType, entityId),
            Tags           = ["authz", "denied"],
        };
    }

    // ── 4. Tenant Created ─────────────────────────────────────────────────────

    /// <summary>A new tenant was provisioned.</summary>
    public static IngestAuditEventRequest TenantCreated(
        string   tenantId,
        string   tenantName,
        string   plan,
        string   provisionedByServiceId,
        string   workflowRunId)
    {
        var now = DateTimeOffset.UtcNow;
        return new IngestAuditEventRequest
        {
            EventType      = "tenant.created",
            EventCategory  = EventCategory.Administrative,
            SourceSystem   = "identity-service",
            SourceService  = "tenant-provisioning-api",
            Visibility     = VisibilityScope.Platform,
            Severity       = SeverityLevel.Notice,
            OccurredAtUtc  = now,
            Scope          = TenantScope(tenantId),
            Actor          = new AuditEventActorDto
            {
                Type = ActorType.ServiceAccount,
                Id   = provisionedByServiceId,
                Name = "Tenant Provisioning Service",
            },
            Entity         = new AuditEventEntityDto { Type = "Tenant", Id = tenantId },
            Action         = "Created",
            Description    = $"New tenant '{tenantName}' provisioned.",
            After          = JsonSerializer.Serialize(new { tenantId, name = tenantName, plan }),
            Metadata       = JsonSerializer.Serialize(new { provisionedBy = workflowRunId }),
            IdempotencyKey = IdempotencyKey.For("identity-service", "tenant.created", tenantId),
            Tags           = ["tenant-lifecycle", "provisioning"],
        };
    }

    // ── 5. Organization Relationship Created ──────────────────────────────────

    /// <summary>A relationship between two organizations was established.</summary>
    public static IngestAuditEventRequest OrganizationRelationshipCreated(
        string   tenantId,
        string   organizationId,
        string   adminUserId,
        string   adminUserName,
        string   relationshipId,
        string   fromOrgId,
        string   toOrgId,
        string   relationshipType,
        string?  correlationId = null,
        string?  requestId     = null,
        string?  sessionId     = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new IngestAuditEventRequest
        {
            EventType      = "organization.relationship.created",
            EventCategory  = EventCategory.Administrative,
            SourceSystem   = "care-connect",
            SourceService  = "organization-api",
            Visibility     = VisibilityScope.Tenant,
            Severity       = SeverityLevel.Notice,
            OccurredAtUtc  = now,
            Scope          = OrganizationScope(tenantId, organizationId),
            Actor          = UserActor(adminUserId, adminUserName),
            Entity         = new AuditEventEntityDto { Type = "OrganizationRelationship", Id = relationshipId },
            Action         = "Created",
            Description    = $"Relationship '{relationshipType}' established between {fromOrgId} and {toOrgId}.",
            After          = JsonSerializer.Serialize(new { fromOrgId, toOrgId, relationshipType }),
            CorrelationId  = correlationId,
            RequestId      = requestId,
            SessionId      = sessionId,
            IdempotencyKey = IdempotencyKey.For("care-connect", "organization.relationship.created", relationshipId),
            Tags           = ["org-relationship"],
        };
    }

    // ── 6. Role Assignment Changed ────────────────────────────────────────────

    /// <summary>A role was assigned to or revoked from a user.</summary>
    public static IngestAuditEventRequest RoleAssigned(
        string         tenantId,
        string         organizationId,
        string         adminUserId,
        string         adminUserName,
        string         targetUserId,
        string         assignedRole,
        IReadOnlyList<string> rolesBefore,
        IReadOnlyList<string> rolesAfter,
        string?        correlationId = null,
        string?        requestId     = null,
        string?        sessionId     = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new IngestAuditEventRequest
        {
            EventType      = "user.role.assigned",
            EventCategory  = EventCategory.Administrative,
            SourceSystem   = "identity-service",
            SourceService  = "rbac-api",
            Visibility     = VisibilityScope.Tenant,
            Severity       = SeverityLevel.Notice,
            OccurredAtUtc  = now,
            Scope          = OrganizationScope(tenantId, organizationId),
            Actor          = UserActor(adminUserId, adminUserName),
            Entity         = new AuditEventEntityDto { Type = "User", Id = targetUserId },
            Action         = "RoleAssigned",
            Description    = $"Role '{assignedRole}' assigned to user {targetUserId} by {adminUserName}.",
            Before         = JsonSerializer.Serialize(new { roles = rolesBefore }),
            After          = JsonSerializer.Serialize(new { roles = rolesAfter }),
            Metadata       = JsonSerializer.Serialize(new { assignedRole, scope = organizationId }),
            CorrelationId  = correlationId,
            RequestId      = requestId,
            SessionId      = sessionId,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "user.role.assigned", targetUserId, assignedRole),
            Tags           = ["rbac", "role-change"],
        };
    }

    // ── 7. Record Updated (DataChange with Before / After) ────────────────────

    /// <summary>
    /// A domain record was mutated. Captures state snapshots.
    /// Redact PHI from <paramref name="beforeJson"/> and <paramref name="afterJson"/> before calling.
    /// </summary>
    public static IngestAuditEventRequest RecordUpdated(
        string   tenantId,
        string   organizationId,
        string   actorUserId,
        string   actorUserName,
        string   entityType,
        string   entityId,
        string   beforeJson,
        string   afterJson,
        string   changedFieldsSummary,
        string?  correlationId = null,
        string?  requestId     = null,
        string?  sessionId     = null,
        string[] extraTags     = default!)
    {
        extraTags ??= [];
        var now = DateTimeOffset.UtcNow;
        return new IngestAuditEventRequest
        {
            EventType      = $"{entityType.ToLowerInvariant()}.record.updated",
            EventCategory  = EventCategory.DataChange,
            SourceSystem   = "care-connect",
            SourceService  = "patient-api",
            Visibility     = VisibilityScope.Organization,
            Severity       = SeverityLevel.Notice,
            OccurredAtUtc  = now,
            Scope          = OrganizationScope(tenantId, organizationId),
            Actor          = UserActor(actorUserId, actorUserName),
            Entity         = new AuditEventEntityDto { Type = entityType, Id = entityId },
            Action         = "Updated",
            Description    = $"{entityType} {entityId} updated by {actorUserName}: {changedFieldsSummary}.",
            Before         = beforeJson,
            After          = afterJson,
            CorrelationId  = correlationId,
            RequestId      = requestId,
            SessionId      = sessionId,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "care-connect", $"{entityType}.record.updated", entityId),
            Tags           = [..new[] { "data-change" }, ..extraTags],
        };
    }

    // ── 8. Referral Created ───────────────────────────────────────────────────

    /// <summary>A patient referral was created.</summary>
    public static IngestAuditEventRequest ReferralCreated(
        string   tenantId,
        string   organizationId,
        string   clinicianUserId,
        string   clinicianName,
        string   referralId,
        string   toOrgId,
        string   patientId,
        string   specialty,
        string   urgency,
        string?  correlationId = null,
        string?  requestId     = null,
        string?  sessionId     = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new IngestAuditEventRequest
        {
            EventType      = "referral.created",
            EventCategory  = EventCategory.Business,
            SourceSystem   = "care-connect",
            SourceService  = "referral-api",
            Visibility     = VisibilityScope.Organization,
            Severity       = SeverityLevel.Info,
            OccurredAtUtc  = now,
            Scope          = OrganizationScope(tenantId, organizationId),
            Actor          = UserActor(clinicianUserId, clinicianName),
            Entity         = new AuditEventEntityDto { Type = "Referral", Id = referralId },
            Action         = "Created",
            Description    = $"Referral to {toOrgId} ({specialty}, {urgency}) created for patient {patientId}.",
            After          = JsonSerializer.Serialize(new { referralId, toOrgId, urgency }),
            Metadata       = JsonSerializer.Serialize(new { patientId, specialty }),
            CorrelationId  = correlationId,
            RequestId      = requestId,
            SessionId      = sessionId,
            IdempotencyKey = IdempotencyKey.For("care-connect", "referral.created", referralId),
            Tags           = ["referral", "workflow"],
        };
    }

    // ── 9. Appointment Scheduled ──────────────────────────────────────────────

    /// <summary>A patient appointment was scheduled.</summary>
    public static IngestAuditEventRequest AppointmentScheduled(
        string         tenantId,
        string         organizationId,
        string         schedulerUserId,
        string         schedulerName,
        string         appointmentId,
        string         providerId,
        string         patientId,
        string         appointmentType,
        DateTimeOffset scheduledFor,
        string?        correlationId = null,
        string?        requestId     = null,
        string?        sessionId     = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new IngestAuditEventRequest
        {
            EventType      = "appointment.scheduled",
            EventCategory  = EventCategory.Business,
            SourceSystem   = "care-connect",
            SourceService  = "scheduling-api",
            Visibility     = VisibilityScope.Organization,
            Severity       = SeverityLevel.Info,
            OccurredAtUtc  = now,
            Scope          = OrganizationScope(tenantId, organizationId),
            Actor          = UserActor(schedulerUserId, schedulerName),
            Entity         = new AuditEventEntityDto { Type = "Appointment", Id = appointmentId },
            Action         = "Scheduled",
            Description    = $"Appointment ({appointmentType}) scheduled for patient {patientId} on {scheduledFor:yyyy-MM-dd HH:mm UTC}.",
            After          = JsonSerializer.Serialize(new { appointmentId, providerId, scheduledFor }),
            Metadata       = JsonSerializer.Serialize(new { patientId, appointmentType }),
            CorrelationId  = correlationId,
            RequestId      = requestId,
            SessionId      = sessionId,
            IdempotencyKey = IdempotencyKey.For("care-connect", "appointment.scheduled", appointmentId),
            Tags           = ["scheduling", "appointment"],
        };
    }

    // ── 10. Document Viewed ───────────────────────────────────────────────────

    /// <summary>A document was viewed by a user.</summary>
    public static IngestAuditEventRequest DocumentViewed(
        string   tenantId,
        string   organizationId,
        string   userId,
        string   userName,
        string   ipAddress,
        string   userAgent,
        string   documentId,
        string   documentName,
        string   documentType,
        string?  correlationId = null,
        string?  requestId     = null,
        string?  sessionId     = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new IngestAuditEventRequest
        {
            EventType      = "document.viewed",
            EventCategory  = EventCategory.Access,
            SourceSystem   = "care-connect",
            SourceService  = "document-api",
            Visibility     = VisibilityScope.Organization,
            Severity       = SeverityLevel.Info,
            OccurredAtUtc  = now,
            Scope          = OrganizationScope(tenantId, organizationId),
            Actor          = UserActor(userId, userName, ipAddress, userAgent),
            Entity         = new AuditEventEntityDto { Type = "Document", Id = documentId },
            Action         = "Viewed",
            Description    = $"Document '{documentName}' viewed by {userName}.",
            Metadata       = JsonSerializer.Serialize(new { documentName, documentType }),
            CorrelationId  = correlationId,
            RequestId      = requestId,
            SessionId      = sessionId,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "care-connect", "document.viewed", documentId, userId),
            Tags           = ["document-access"],
        };
    }

    // ── 11. Workflow Approved ─────────────────────────────────────────────────

    /// <summary>A workflow instance was approved by an authorized user.</summary>
    public static IngestAuditEventRequest WorkflowApproved(
        string   tenantId,
        string   organizationId,
        string   approverUserId,
        string   approverName,
        string   workflowId,
        string   workflowType,
        string   workflowOutcome,
        string?  additionalMetadataJson = null,
        string?  correlationId          = null,
        string?  requestId              = null,
        string?  sessionId              = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new IngestAuditEventRequest
        {
            EventType      = "workflow.approved",
            EventCategory  = EventCategory.Compliance,
            SourceSystem   = "care-connect",
            SourceService  = "workflow-engine",
            Visibility     = VisibilityScope.Tenant,
            Severity       = SeverityLevel.Notice,
            OccurredAtUtc  = now,
            Scope          = OrganizationScope(tenantId, organizationId),
            Actor          = UserActor(approverUserId, approverName),
            Entity         = new AuditEventEntityDto { Type = "WorkflowInstance", Id = workflowId },
            Action         = "Approved",
            Description    = $"{workflowType} workflow '{workflowId}' approved by {approverName}.",
            After          = JsonSerializer.Serialize(new { workflowId, outcome = workflowOutcome, approvedAt = now }),
            Metadata       = additionalMetadataJson,
            CorrelationId  = correlationId,
            RequestId      = requestId,
            SessionId      = sessionId,
            IdempotencyKey = IdempotencyKey.For("care-connect", "workflow.approved", workflowId),
            Tags           = ["workflow", "compliance"],
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scope + Actor helpers (private)
    // ─────────────────────────────────────────────────────────────────────────

    private static AuditEventScopeDto TenantScope(string tenantId) => new()
    {
        ScopeType = ScopeType.Tenant,
        TenantId  = tenantId,
    };

    private static AuditEventScopeDto OrganizationScope(string tenantId, string organizationId) => new()
    {
        ScopeType      = ScopeType.Organization,
        TenantId       = tenantId,
        OrganizationId = organizationId,
    };

    private static AuditEventActorDto UserActor(
        string  userId,
        string  userName,
        string? ipAddress = null,
        string? userAgent = null) => new()
    {
        Type      = ActorType.User,
        Id        = userId,
        Name      = userName,
        IpAddress = ipAddress,
        UserAgent = userAgent,
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Usage Walkthrough — how a service uses the client and factories together
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Demonstrates end-to-end usage of <see cref="IAuditEventClient"/> + <see cref="AuditEventExamples"/>.
///
/// In a real service this would be injected into the application service layer.
/// </summary>
public sealed class AuditUsageWalkthrough
{
    private readonly IAuditEventClient _audit;

    public AuditUsageWalkthrough(IAuditEventClient audit)
    {
        _audit = audit;
    }

    /// <summary>
    /// Example: emit a login success event from the identity service's auth flow.
    /// Call this after the JWT is issued, not before — audit the fact, not the intent.
    /// </summary>
    public async Task EmitLoginSuccessAsync(
        string tenantId, string userId, string userName,
        string ip, string userAgent, string sessionId,
        string correlationId, string requestId,
        CancellationToken ct = default)
    {
        var request = AuditEventExamples.LoginSuccess(
            tenantId, userId, userName, ip, userAgent,
            sessionId, correlationId, requestId);

        var result = await _audit.IngestAsync(request, ct);

        // 409 Conflict (duplicate key) = already recorded — still "accepted"
        if (!result.Accepted)
        {
            // Log and continue — audit failure must not block the business flow
        }
    }

    /// <summary>
    /// Example: emit a batch of document-viewed events from a bulk-export download.
    /// </summary>
    public async Task EmitDocumentBatchViewedAsync(
        string           tenantId,
        string           organizationId,
        string           userId,
        string           userName,
        string           ip,
        IReadOnlyList<(string Id, string Name, string Type)> documents,
        string           batchCorrelationId,
        CancellationToken ct = default)
    {
        var events = documents
            .Select(d => AuditEventExamples.DocumentViewed(
                tenantId, organizationId, userId, userName, ip,
                userAgent: "bulk-export-worker",
                documentId: d.Id, documentName: d.Name, documentType: d.Type,
                correlationId: batchCorrelationId))
            .ToList();

        var batchRequest = new BatchIngestRequest
        {
            Events             = events,
            BatchCorrelationId = batchCorrelationId,
            StopOnFirstError   = false,   // partial acceptance is fine for audit
        };

        var result = await _audit.IngestBatchAsync(batchRequest, ct);

        // Log partial rejections but do not fail the export operation
        if (result.Rejected > 0)
        {
            var failed = result.Results
                .Where(r => !r.Accepted)
                .Select(r => $"{r.EventType}:{r.RejectionReason}")
                .ToList();
            // logger.LogWarning(...) in real code
            _ = failed;
        }
    }
}
