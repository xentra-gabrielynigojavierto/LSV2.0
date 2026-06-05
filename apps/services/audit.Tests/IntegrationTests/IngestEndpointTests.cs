using System.Net;
using FluentAssertions;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.Tests.Helpers;

namespace PlatformAuditEventService.Tests.IntegrationTests;

/// <summary>
/// Integration tests for POST /internal/audit/events (single event ingest).
///
/// Scenarios:
///   - Valid request → 201 Created with AuditId
///   - Missing required fields → 400 Bad Request with error list
///   - Null request body → 400 Bad Request
///   - Duplicate IdempotencyKey → 409 Conflict
///   - Response body always follows ApiResponse envelope
/// </summary>
public class IngestEndpointTests(AuditServiceFactory factory)
    : IClassFixture<AuditServiceFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string IngestUrl = "/internal/audit/events";

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task IngestSingle_ValidRequest_Returns201()
    {
        var request = AuditRequestBuilder.MinimalValid();

        var response = await _client.PostServiceJsonAsync(IngestUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task IngestSingle_ValidRequest_ReturnsApiResponseEnvelope()
    {
        var request = AuditRequestBuilder.MinimalValid();

        var response = await _client.PostServiceJsonAsync(IngestUrl, request);
        var body     = await response.ReadApiResponseAsync<IngestItemResult>();

        body.Success.Should().BeTrue();
        body.Errors.Should().BeEmpty();
        body.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task IngestSingle_ValidRequest_ResponseContainsNonEmptyAuditId()
    {
        var request = AuditRequestBuilder.MinimalValid();

        var response = await _client.PostServiceJsonAsync(IngestUrl, request);
        var body     = await response.ReadApiResponseAsync<IngestItemResult>();

        body.Data.Should().NotBeNull();
        body.Data!.Accepted.Should().BeTrue();
        body.Data.AuditId.Should().NotBeNull();
        body.Data.AuditId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task IngestSingle_ValidRequest_LocationHeaderPointsToResource()
    {
        var request = AuditRequestBuilder.MinimalValid();

        var response = await _client.PostServiceJsonAsync(IngestUrl, request);

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString()
            .Should().StartWith("/internal/audit/events/");
    }

    // ── Validation failures → 400 ─────────────────────────────────────────────

    [Fact]
    public async Task IngestSingle_MissingEventType_Returns400()
    {
        var request = AuditRequestBuilder.MinimalValid();
        request.EventType = string.Empty;

        var response = await _client.PostServiceJsonAsync(IngestUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IngestSingle_MissingEventType_ResponseBodyContainsErrors()
    {
        var request = AuditRequestBuilder.MinimalValid();
        request.EventType = string.Empty;

        var response = await _client.PostServiceJsonAsync(IngestUrl, request);
        var body     = await response.ReadApiResponseAsync<object>();

        body.Success.Should().BeFalse();
        body.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task IngestSingle_MissingSourceSystem_Returns400()
    {
        var request = AuditRequestBuilder.MinimalValid();
        request.SourceSystem = string.Empty;

        var response = await _client.PostServiceJsonAsync(IngestUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IngestSingle_MissingSourceService_Returns400()
    {
        var request = AuditRequestBuilder.MinimalValid();
        request.SourceService = string.Empty;

        var response = await _client.PostServiceJsonAsync(IngestUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IngestSingle_MissingOccurredAtUtc_Returns400()
    {
        var request = AuditRequestBuilder.MinimalValid();
        request.OccurredAtUtc = null;

        var response = await _client.PostServiceJsonAsync(IngestUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IngestSingle_MissingScopeTenantId_Returns400()
    {
        var request = AuditRequestBuilder.MinimalValid();
        request.Scope.TenantId = null;

        var response = await _client.PostServiceJsonAsync(IngestUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Idempotency → 409 ────────────────────────────────────────────────────

    [Fact]
    public async Task IngestSingle_SameIdempotencyKey_SecondSubmissionReturns409()
    {
        var key     = $"idem-{Guid.NewGuid():N}";
        var request = AuditRequestBuilder.MinimalValid(idempotencyKey: key);

        var first  = await _client.PostServiceJsonAsync(IngestUrl, request);
        var second = await _client.PostServiceJsonAsync(IngestUrl, request);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task IngestSingle_SameIdempotencyKey_ConflictBodyIsApiResponseEnvelope()
    {
        var key     = $"idem-{Guid.NewGuid():N}";
        var request = AuditRequestBuilder.MinimalValid(idempotencyKey: key);

        await _client.PostServiceJsonAsync(IngestUrl, request);
        var second = await _client.PostServiceJsonAsync(IngestUrl, request);
        var body   = await second.ReadApiResponseAsync<object>();

        body.Success.Should().BeFalse();
        body.Message.Should().Contain("already been ingested");
    }

    [Fact]
    public async Task IngestSingle_NoIdempotencyKey_SameEventAcceptedTwice()
    {
        var request = AuditRequestBuilder.MinimalValid();

        var first  = await _client.PostServiceJsonAsync(IngestUrl, request);
        var second = await _client.PostServiceJsonAsync(IngestUrl, request);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
