using System.Net;
using FluentAssertions;
using PlatformAuditEventService.DTOs.Query;
using PlatformAuditEventService.Tests.Helpers;

namespace PlatformAuditEventService.Tests.IntegrationTests;

/// <summary>
/// Integration tests for GET /audit/events (paginated query).
///
/// All tests run with QueryAuth:Mode = "None" so all callers are treated as
/// PlatformAdmin, meaning no tenant-scope restriction is enforced.
///
/// Scenarios:
///   - Empty store returns 200 with zero results
///   - After ingestion, all events are queryable
///   - TenantId filter returns only matching events
///   - SourceSystem filter returns only matching events
///   - Pagination: page/pageSize params are honoured
///   - Invalid query params (bad enum) return 400
/// </summary>
public class QueryEndpointTests(AuditServiceFactory factory)
    : IClassFixture<AuditServiceFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string QueryUrl  = "/audit/events";
    private const string IngestUrl = "/internal/audit/events";

    // ── Empty store ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Query_UnseenTenant_Returns200WithZeroResults()
    {
        // Scope to a globally-unique tenant ID that no other test uses,
        // so the result is always empty regardless of shared factory state.
        var isolatedTenant = $"tenant-zero-{Guid.NewGuid():N}";

        var response = await _client.GetAsync($"{QueryUrl}?tenantId={isolatedTenant}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadApiResponseAsync<AuditEventQueryResponse>();
        body.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.TotalCount.Should().Be(0);
        body.Data.Items.Should().BeEmpty();
    }

    // ── Basic query after ingest ──────────────────────────────────────────────

    [Fact]
    public async Task Query_AfterIngest_ReturnsPersistedEvents()
    {
        await IngestEventAsync("query.test.created", "tenant-query-basic");
        await IngestEventAsync("query.test.updated", "tenant-query-basic");

        var response = await _client.GetAsync($"{QueryUrl}?tenantId=tenant-query-basic");
        var body     = await response.ReadApiResponseAsync<AuditEventQueryResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Data!.TotalCount.Should().BeGreaterOrEqualTo(2);
        body.Data.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Query_AfterIngest_ItemsHaveExpectedShape()
    {
        var tenantId = $"tenant-shape-{Guid.NewGuid():N}";
        await IngestEventAsync("query.shape.test", tenantId);

        var response = await _client.GetAsync($"{QueryUrl}?tenantId={tenantId}");
        var body     = await response.ReadApiResponseAsync<AuditEventQueryResponse>();

        var item = body.Data!.Items.Should().ContainSingle().Which;
        item.AuditId.Should().NotBe(Guid.Empty);
        item.EventType.Should().Be("query.shape.test");
    }

    // ── TenantId filter ───────────────────────────────────────────────────────

    [Fact]
    public async Task Query_TenantIdFilter_ReturnsOnlyMatchingTenant()
    {
        var tenantA = $"tenant-a-{Guid.NewGuid():N}";
        var tenantB = $"tenant-b-{Guid.NewGuid():N}";

        await IngestEventAsync("event.a", tenantA);
        await IngestEventAsync("event.b", tenantB);

        var responseA = await _client.GetAsync($"{QueryUrl}?tenantId={tenantA}");
        var bodyA     = await responseA.ReadApiResponseAsync<AuditEventQueryResponse>();

        bodyA.Data!.Items.Should().AllSatisfy(item =>
            item.EventType.Should().Be("event.a"));
    }

    // ── SourceSystem filter ───────────────────────────────────────────────────

    [Fact]
    public async Task Query_SourceSystemFilter_ReturnsOnlyMatchingSource()
    {
        var tenantId = $"tenant-src-{Guid.NewGuid():N}";
        var request  = AuditRequestBuilder.MinimalValid(
            tenantId:     tenantId,
            sourceSystem: "fund-service",
            eventType:    "fund.contribution.created");

        await _client.PostServiceJsonAsync(IngestUrl, request);

        var response = await _client.GetAsync(
            $"{QueryUrl}?tenantId={tenantId}&sourceSystem=fund-service");
        var body = await response.ReadApiResponseAsync<AuditEventQueryResponse>();

        body.Data!.TotalCount.Should().Be(1);
        body.Data.Items.Single().EventType.Should().Be("fund.contribution.created");
    }

    // ── Pagination ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Query_Pagination_PageSizeIsRespected()
    {
        var tenantId = $"tenant-page-{Guid.NewGuid():N}";

        for (var i = 0; i < 5; i++)
            await IngestEventAsync($"page.event.{i}", tenantId);

        var response = await _client.GetAsync(
            $"{QueryUrl}?tenantId={tenantId}&page=1&pageSize=2");
        var body = await response.ReadApiResponseAsync<AuditEventQueryResponse>();

        body.Data!.Items.Should().HaveCount(2);
        body.Data.PageSize.Should().Be(2);
        body.Data.TotalCount.Should().Be(5);
        body.Data.HasNext.Should().BeTrue();
    }

    [Fact]
    public async Task Query_Pagination_SecondPageContainsDifferentItems()
    {
        var tenantId = $"tenant-page2-{Guid.NewGuid():N}";

        for (var i = 0; i < 4; i++)
            await IngestEventAsync($"pageable.event.{i}", tenantId);

        var page1 = await (await _client.GetAsync(
            $"{QueryUrl}?tenantId={tenantId}&page=1&pageSize=2"))
            .ReadApiResponseAsync<AuditEventQueryResponse>();

        var page2 = await (await _client.GetAsync(
            $"{QueryUrl}?tenantId={tenantId}&page=2&pageSize=2"))
            .ReadApiResponseAsync<AuditEventQueryResponse>();

        var ids1 = page1.Data!.Items.Select(i => i.AuditId).ToHashSet();
        var ids2 = page2.Data!.Items.Select(i => i.AuditId).ToHashSet();

        ids1.Intersect(ids2).Should().BeEmpty("page 1 and page 2 must not overlap");
    }

    // ── Validation failures → 400 ─────────────────────────────────────────────

    [Fact]
    public async Task Query_InvalidCategoryEnum_Returns400()
    {
        var response = await _client.GetAsync($"{QueryUrl}?category=9999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Query_NegativePageSize_Returns400()
    {
        var response = await _client.GetAsync($"{QueryUrl}?pageSize=-1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task IngestEventAsync(string eventType, string tenantId)
    {
        var request = AuditRequestBuilder.MinimalValid(
            eventType: eventType,
            tenantId:  tenantId,
            idempotencyKey: Guid.NewGuid().ToString());

        var response = await _client.PostServiceJsonAsync(IngestUrl, request);
        response.EnsureSuccessStatusCode();
    }
}
