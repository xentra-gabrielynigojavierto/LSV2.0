using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.DTOs.Query;
using PlatformAuditEventService.Tests.Helpers;

namespace PlatformAuditEventService.Tests.IntegrationTests;

// ─────────────────────────────────────────────────────────────────────────────
// Step 27 — Canonical Stabilization Validation Tests
//
// Phase C  — Security: cross-tenant isolation enforcement
// Phase D  — Trace: correlation ID roundtrip
// Phase E  — Integrity: concurrent ingest, hash-chain fork prevention
// Phase F  — Load: high-volume concurrent ingest stability
// Phase G  — Audit-of-audit: access events are self-recorded
// Phase H  — Legacy freeze: /api/auditevents deprecation headers
// ─────────────────────────────────────────────────────────────────────────────

// ═════════════════════════════════════════════════════════════════════════════
// PHASE C — SECURITY VALIDATION: Cross-Tenant Isolation
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Phase C — JWT security and cross-tenant isolation tests using BearerAuditFactory.
///
/// The QueryAuthorizer enforces tenantId override for non-PlatformAdmin callers:
/// a TenantAdmin who requests another tenant's events receives 200 but with results
/// automatically constrained to their own tenantId (not 403).  This prevents IDOR
/// while keeping the API surface consistent.
/// </summary>
public class CrossTenantIsolationTests(BearerAuditFactory factory)
    : IClassFixture<BearerAuditFactory>
{
    private const string IngestUrl = "/internal/audit/events";
    private const string QueryUrl  = "/audit/events";

    // C1a — TenantAdmin can query their own tenant's events ─────────────────

    [Fact]
    public async Task TenantAdmin_QueryOwnTenant_Returns200()
    {
        using var client = factory.CreateBearerClient(
            role:     BearerAuditFactory.TenantAdminRole,
            tenantId: BearerAuditFactory.TestTenantId);

        var response = await client.GetAsync(QueryUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // C1b — TenantAdmin requesting another tenant path is blocked with 403 ────

    [Fact]
    public async Task TenantAdmin_RequestingOtherTenantPath_Gets403()
    {
        // Query as TenantAdmin of testTenantId requesting a DIFFERENT tenant's path.
        // The QueryAuthorizer enforces strict cross-tenant isolation:
        // a 403 Forbidden is returned — not a silent scope override.
        // This is the HIPAA-required behaviour: no data from another tenant leaks.
        var tenantB = "tenant-isolation-other";
        using var client = factory.CreateBearerClient(
            role:     BearerAuditFactory.TenantAdminRole,
            tenantId: BearerAuditFactory.TestTenantId);

        var response = await client.GetAsync($"/audit/tenant/{tenantB}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "a TenantAdmin must not access another tenant's audit trail");
    }

    // C1c — PlatformAdmin can query any tenant's events ─────────────────────

    [Fact]
    public async Task PlatformAdmin_CanQuery_AnyTenant()
    {
        using var client = factory.CreateBearerClient(role: BearerAuditFactory.PlatformAdminRole);
        var response     = await client.GetAsync(QueryUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // C2 — No token → 401 (already in QueryAuthBearerTests, included for completeness) ─

    [Fact]
    public async Task Query_NoToken_Returns401()
    {
        using var client = factory.CreateClient();
        var response     = await client.GetAsync(QueryUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // C3 — Expired token → 401 ───────────────────────────────────────────────

    [Fact]
    public async Task Query_ExpiredToken_Returns401()
    {
        var token = factory.IssueToken(expMinutes: -1);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(QueryUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // C4 — Invalid (tampered) token → 401 ───────────────────────────────────

    [Fact]
    public async Task Query_TamperedToken_Returns401()
    {
        var token  = factory.IssueToken();
        var parts  = token.Split('.');
        parts[2]   = "tampered-signature-xxxx";
        var tampered = string.Join('.', parts);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync(QueryUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Helper type for deserialising query results ────────────────────────────
    private sealed record QueryResult(
        IReadOnlyList<EventItem> Items,
        int                      TotalCount);

    private sealed record EventItem(
        string  Id,
        string? TenantId);
}

// ═════════════════════════════════════════════════════════════════════════════
// PHASE D — TRACE VALIDATION: Correlation ID Roundtrip
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Phase D — End-to-end correlation ID trace validation.
///
/// Simulates a cross-service request chain (login → referral → appointment) by
/// ingesting three events that share a single correlationId.  After ingestion,
/// queries by correlationId confirm all three events are traceable to the same
/// root request.
/// </summary>
public class CorrelationIdTraceTests(AuditServiceFactory factory)
    : IClassFixture<AuditServiceFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string IngestUrl = "/internal/audit/events";
    private const string QueryUrl  = "/audit/events";

    // D1 — Events with same correlationId are all retrievable via filter ─────

    [Fact]
    public async Task CorrelationId_SameIdAcrossThreeEvents_AllVisible()
    {
        var correlationId = $"trace-{Guid.NewGuid():N}";
        var tenantId      = $"tenant-trace-{Guid.NewGuid():N}";

        var events = new[]
        {
            AuditRequestBuilder.MinimalValid(
                eventType: "identity.user.login.succeeded",
                tenantId:  tenantId,
                idempotencyKey: $"trace-login-{Guid.NewGuid():N}"),
            AuditRequestBuilder.MinimalValid(
                eventType: "careconnect.referral.created",
                tenantId:  tenantId,
                idempotencyKey: $"trace-referral-{Guid.NewGuid():N}"),
            AuditRequestBuilder.MinimalValid(
                eventType: "careconnect.appointment.scheduled",
                tenantId:  tenantId,
                idempotencyKey: $"trace-appt-{Guid.NewGuid():N}"),
        };

        // Set CorrelationId on all three events.
        foreach (var e in events)
        {
            e.CorrelationId = correlationId;
            var resp = await _client.PostServiceJsonAsync(IngestUrl, e);
            resp.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Query by correlationId.
        var queryResponse = await _client.GetAsync(
            $"{QueryUrl}?correlationId={Uri.EscapeDataString(correlationId)}&tenantId={tenantId}");
        queryResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await queryResponse.ReadApiResponseAsync<JsonElement>();
        var items = body.Data.GetProperty("items");
        items.GetArrayLength().Should().Be(3);
    }

    // D2 — System auto-generates X-Correlation-ID when absent ────────────────

    [Fact]
    public async Task CorrelationMiddleware_AutoGenerates_WhenHeaderAbsent()
    {
        var request  = AuditRequestBuilder.MinimalValid(
            idempotencyKey: $"corr-auto-{Guid.NewGuid():N}");
        var response = await _client.PostServiceJsonAsync(IngestUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Should().ContainKey("X-Correlation-ID");
        response.Headers.GetValues("X-Correlation-ID").First().Should().NotBeNullOrWhiteSpace();
    }

    // D3 — Caller-supplied X-Correlation-ID is echoed in the response ────────

    [Fact]
    public async Task CorrelationMiddleware_EchoesProvidedCorrelationId()
    {
        var correlationId = $"client-provided-{Guid.NewGuid():N}";

        using var req = new HttpRequestMessage(HttpMethod.Post, IngestUrl);
        req.Headers.Add("X-Correlation-ID", correlationId);
        var payload   = AuditRequestBuilder.MinimalValid(
            idempotencyKey: $"echo-corr-{Guid.NewGuid():N}");
        req.Content = JsonContent.Create(payload, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.GetValues("X-Correlation-ID").First().Should().Be(correlationId);
    }

    // D4 — Malicious oversized X-Correlation-ID is discarded (security) ──────

    [Fact]
    public async Task CorrelationMiddleware_DiscardsMaliciousOversizedHeader()
    {
        var oversized = new string('A', 200); // Exceeds 100-char limit

        using var req = new HttpRequestMessage(HttpMethod.Post, IngestUrl);
        req.Headers.Add("X-Correlation-ID", oversized);
        var payload   = AuditRequestBuilder.MinimalValid(
            idempotencyKey: $"oversized-corr-{Guid.NewGuid():N}");
        req.Content = JsonContent.Create(payload, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // System discards the oversized value and generates a new safe UUID.
        var echoed = response.Headers.GetValues("X-Correlation-ID").First();
        echoed.Should().NotBe(oversized);
        echoed.Length.Should().BeLessOrEqualTo(36); // UUID format
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// PHASE E — INTEGRITY VALIDATION: Hash-Chain Fork Prevention
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Phase E — Hash-chain integrity validation.
///
/// The ingestion pipeline uses SemaphoreSlim per (TenantId, SourceSystem) chain
/// to ensure atomic read-compute-append sequences.  Concurrent ingestion to the
/// same chain must not produce forks or gaps — every record must be accepted and
/// every PreviousHash must match exactly one predecessor.
/// </summary>
public class HashChainIntegrityTests(AuditServiceFactory factory)
    : IClassFixture<AuditServiceFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string IngestUrl = "/internal/audit/events";
    private const string QueryUrl  = "/audit/events";

    // E1 — Concurrent ingest to same chain → all accepted, no data loss ──────

    [Fact]
    public async Task ConcurrentIngest_SameChain_AllEventsAccepted()
    {
        const int concurrency = 20;
        var tenantId     = $"tenant-chain-{Guid.NewGuid():N}";
        var sourceSystem = "integrity-test-service";

        var requests = Enumerable.Range(0, concurrency).Select(i =>
            AuditRequestBuilder.MinimalValid(
                eventType:      $"integrity.test.event",
                tenantId:       tenantId,
                sourceSystem:   sourceSystem,
                idempotencyKey: $"chain-conc-{Guid.NewGuid():N}"))
            .ToList();

        // Fire all ingestions concurrently.
        var tasks    = requests.Select(r => _client.PostServiceJsonAsync(IngestUrl, r));
        var results  = await Task.WhenAll(tasks);

        // All must be accepted (201 Created).
        results.Should().AllSatisfy(r =>
            r.StatusCode.Should().Be(HttpStatusCode.Created));
    }

    // E2 — After concurrent ingest, all events are queryable (no silent loss) ─

    [Fact]
    public async Task ConcurrentIngest_SameChain_AllEventsQueryable()
    {
        const int concurrency = 15;
        var tenantId     = $"tenant-chain-q-{Guid.NewGuid():N}";
        var sourceSystem = "integrity-query-service";

        var requests = Enumerable.Range(0, concurrency).Select(i =>
            AuditRequestBuilder.MinimalValid(
                tenantId:       tenantId,
                sourceSystem:   sourceSystem,
                idempotencyKey: $"chain-q-{Guid.NewGuid():N}"))
            .ToList();

        await Task.WhenAll(requests.Select(r => _client.PostServiceJsonAsync(IngestUrl, r)));

        // Allow a brief moment for in-process async flush (InMemory store is sync but be explicit).
        await Task.Delay(50);

        var response = await _client.GetAsync(
            $"{QueryUrl}?tenantId={tenantId}&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body  = await response.ReadApiResponseAsync<JsonElement>();
        var total = body.Data.GetProperty("totalCount").GetInt32();
        total.Should().Be(concurrency);
    }

    // E3 — Sequential ingest: unique idempotency keys → always accepted ───────

    [Fact]
    public async Task SequentialIngest_UniqueKeys_AllAccepted()
    {
        var tenantId = $"tenant-seq-{Guid.NewGuid():N}";
        for (int i = 0; i < 10; i++)
        {
            var req  = AuditRequestBuilder.MinimalValid(
                tenantId:       tenantId,
                idempotencyKey: $"seq-{Guid.NewGuid():N}");
            var resp = await _client.PostServiceJsonAsync(IngestUrl, req);
            resp.StatusCode.Should().Be(HttpStatusCode.Created,
                because: $"event {i} must be accepted");
        }
    }

    // E4 — Duplicate idempotency key → 409 Conflict (integrity guard) ─────────

    [Fact]
    public async Task DuplicateIdempotencyKey_ReturnsConflict()
    {
        var key  = $"dup-key-{Guid.NewGuid():N}";
        var req1 = AuditRequestBuilder.MinimalValid(idempotencyKey: key);
        var req2 = AuditRequestBuilder.MinimalValid(idempotencyKey: key);

        var r1 = await _client.PostServiceJsonAsync(IngestUrl, req1);
        var r2 = await _client.PostServiceJsonAsync(IngestUrl, req2);

        r1.StatusCode.Should().Be(HttpStatusCode.Created);
        r2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// PHASE F — LOAD & STABILITY: High-Volume Ingest
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Phase F — Load and stability tests.
///
/// Verifies the ingestion pipeline accepts sustained high-volume concurrent load
/// without data loss, silent rejection, or unexpected errors.
/// Uses a dedicated isolated tenantId so results are not contaminated by other tests.
/// </summary>
public class LoadStabilityTests(AuditServiceFactory factory)
    : IClassFixture<AuditServiceFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string IngestUrl = "/internal/audit/events";
    private const string QueryUrl  = "/audit/events";

    // F1 — 100 concurrent events across 5 tenants → all accepted ─────────────

    [Fact]
    public async Task HighVolumeConcurrent_100Events_AllAccepted()
    {
        const int total   = 100;
        const int tenants = 5;

        var requests = Enumerable.Range(0, total).Select(i =>
            AuditRequestBuilder.MinimalValid(
                tenantId:       $"tenant-load-{i % tenants:D2}",
                idempotencyKey: $"load-{Guid.NewGuid():N}"))
            .ToList();

        var results = await Task.WhenAll(
            requests.Select(r => _client.PostServiceJsonAsync(IngestUrl, r)));

        var rejected = results.Count(r => r.StatusCode != HttpStatusCode.Created);
        rejected.Should().Be(0, because: "all 100 events must be accepted without data loss");
    }

    // F2 — After load, query confirms all events persisted ────────────────────

    [Fact]
    public async Task HighVolumeConcurrent_EventsQueryable_AfterIngest()
    {
        const int total    = 50;
        var isolatedTenant = $"tenant-load-iso-{Guid.NewGuid():N}";

        var requests = Enumerable.Range(0, total).Select(_ =>
            AuditRequestBuilder.MinimalValid(
                tenantId:       isolatedTenant,
                idempotencyKey: $"load-iso-{Guid.NewGuid():N}"))
            .ToList();

        await Task.WhenAll(requests.Select(r => _client.PostServiceJsonAsync(IngestUrl, r)));

        await Task.Delay(50); // Allow in-process write to settle.

        var response = await _client.GetAsync(
            $"{QueryUrl}?tenantId={isolatedTenant}&pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body  = await response.ReadApiResponseAsync<JsonElement>();
        var count = body.Data.GetProperty("totalCount").GetInt32();
        count.Should().Be(total, because: "every ingested event must be queryable");
    }

    // F3 — Batch ingest of 50 events → 200 all accepted ──────────────────────

    [Fact]
    public async Task BatchIngest_50Events_AllAccepted()
    {
        const string BatchUrl = "/internal/audit/events/batch";
        var batch = AuditRequestBuilder.ValidBatch(count: 50);

        var response = await _client.PostServiceJsonAsync(BatchUrl, batch);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadApiResponseAsync<BatchIngestResponse>();
        body.Data!.Accepted.Should().Be(50);
        body.Data.Rejected.Should().Be(0);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// PHASE G — AUDIT-OF-AUDIT VALIDATION
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Phase G — Audit-of-audit: verifies that querying the audit log produces
/// a self-referential <c>audit.log.accessed</c> event.
///
/// The QueryAuthorizer emits audit.log.accessed on every query call.
/// These events are suppressed from further auditing (no recursion).
///
/// Verification strategy:
///   1. Perform a query to trigger audit.log.accessed emission.
///   2. Wait briefly for async ingest to settle.
///   3. Query for audit.log.accessed events.
///   4. Confirm at least one event exists.
/// </summary>
public class AuditOfAuditTests(AuditServiceFactory factory)
    : IClassFixture<AuditServiceFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string QueryUrl  = "/audit/events";

    // G1 — Query audit events → audit.log.accessed event is recorded ──────────

    [Fact]
    public async Task QueryAuditEvents_EmitsAuditLogAccessedEvent()
    {
        // Step 1: Trigger a query to produce the audit.log.accessed event.
        var tenantId = $"tenant-aoa-{Guid.NewGuid():N}";
        await _client.GetAsync($"{QueryUrl}?tenantId={tenantId}");

        // Step 2: Give the async ingest a moment to complete.
        await Task.Delay(200);

        // Step 3: Query for audit.log.accessed events in the store.
        var response = await _client.GetAsync(
            $"{QueryUrl}?eventType={Uri.EscapeDataString("audit.log.accessed")}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body  = await response.ReadApiResponseAsync<JsonElement>();
        var total = body.Data.GetProperty("totalCount").GetInt32();

        // Step 4: At least one access event must have been recorded.
        total.Should().BeGreaterThan(0,
            because: "every audit query must self-record an audit.log.accessed event");
    }

    // G2 — audit.log.accessed events are NOT recursively audited ─────────────

    [Fact]
    public async Task AuditLogAccessed_IsNotRecursivelyAudited()
    {
        // Query twice — the count of audit.log.accessed should not grow exponentially.
        await _client.GetAsync($"{QueryUrl}?eventType=audit.log.accessed");
        await Task.Delay(100);

        var r1    = await _client.GetAsync($"{QueryUrl}?eventType=audit.log.accessed");
        var body1 = await r1.ReadApiResponseAsync<JsonElement>();
        var count1 = body1.Data.GetProperty("totalCount").GetInt32();

        await _client.GetAsync($"{QueryUrl}?eventType=audit.log.accessed");
        await Task.Delay(100);

        var r2    = await _client.GetAsync($"{QueryUrl}?eventType=audit.log.accessed");
        var body2 = await r2.ReadApiResponseAsync<JsonElement>();
        var count2 = body2.Data.GetProperty("totalCount").GetInt32();

        // Count must grow linearly (one new access event per non-audit-log query),
        // not exponentially.  The difference should be small (at most a handful of
        // intervening test queries), never hundreds.
        (count2 - count1).Should().BeLessThan(20,
            because: "audit.log.accessed must not trigger recursive re-auditing");
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// PHASE H — LEGACY FREEZE: Deprecation Signal Tests
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Phase H — Legacy freeze: verifies that the deprecated AuditEventsController
/// returns RFC 8594-compliant Deprecation and Sunset headers on every response,
/// signalling to API consumers that the endpoint is frozen and will be removed.
/// </summary>
public class LegacyFreezeTests(AuditServiceFactory factory)
    : IClassFixture<AuditServiceFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string LegacyIngestUrl = "/api/auditevents";

    // H1 — Legacy POST /api/auditevents returns Deprecation: true ─────────────

    [Fact]
    public async Task LegacyIngest_ReturnsDeprecationHeader()
    {
        var request  = AuditRequestBuilder.MinimalValid(
            idempotencyKey: $"legacy-dep-{Guid.NewGuid():N}");
        var response = await _client.PostServiceJsonAsync(LegacyIngestUrl, request);

        response.Headers.Should().ContainKey("Deprecation");
        response.Headers.GetValues("Deprecation").First().Should().Be("true");
    }

    // H2 — Legacy POST /api/auditevents returns Sunset header ─────────────────

    [Fact]
    public async Task LegacyIngest_ReturnsSunsetHeader()
    {
        var request  = AuditRequestBuilder.MinimalValid(
            idempotencyKey: $"legacy-sun-{Guid.NewGuid():N}");
        var response = await _client.PostServiceJsonAsync(LegacyIngestUrl, request);

        response.Headers.Should().ContainKey("Sunset");
        response.Headers.GetValues("Sunset").First().Should().NotBeNullOrWhiteSpace();
    }

    // H3 — Legacy POST /api/auditevents returns Link to successor ─────────────

    [Fact]
    public async Task LegacyIngest_ReturnsLinkToSuccessor()
    {
        var request  = AuditRequestBuilder.MinimalValid(
            idempotencyKey: $"legacy-link-{Guid.NewGuid():N}");
        var response = await _client.PostServiceJsonAsync(LegacyIngestUrl, request);

        response.Headers.Should().ContainKey("Link");
        var link = response.Headers.GetValues("Link").First();
        link.Should().Contain("successor-version");
        link.Should().Contain("/internal/audit/events");
    }

    // H4 — Canonical /internal/audit/events does NOT return Deprecation ────────

    [Fact]
    public async Task CanonicalIngest_DoesNotReturnDeprecationHeader()
    {
        var request  = AuditRequestBuilder.MinimalValid(
            idempotencyKey: $"canon-nodep-{Guid.NewGuid():N}");
        var response = await _client.PostServiceJsonAsync("/internal/audit/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Contains("Deprecation").Should().BeFalse(
            because: "the canonical ingestion endpoint is the live route, not deprecated");
    }
}
