using System.Net;
using FluentAssertions;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.Tests.Helpers;

namespace PlatformAuditEventService.Tests.IntegrationTests;

/// <summary>
/// Integration tests for POST /internal/audit/events/batch.
///
/// Scenarios:
///   - All valid events → 200 (all accepted)
///   - Empty events list → 400 (batch-level validation fails before pipeline)
///   - Null events list → 400
///   - Mixed: one duplicate in batch → 207 Multi-Status
///   - All duplicates → 422 Unprocessable Entity
///   - StopOnFirstError = true halts processing after first failure
/// </summary>
public class BatchIngestTests(AuditServiceFactory factory)
    : IClassFixture<AuditServiceFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string IngestUrl      = "/internal/audit/events";
    private const string BatchUrl       = "/internal/audit/events/batch";

    // ── All accepted → 200 ───────────────────────────────────────────────────

    [Fact]
    public async Task IngestBatch_AllValid_Returns200()
    {
        var request = AuditRequestBuilder.ValidBatch(count: 3);

        var response = await _client.PostServiceJsonAsync(BatchUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IngestBatch_AllValid_AllItemsAccepted()
    {
        var request = AuditRequestBuilder.ValidBatch(count: 3);

        var response = await _client.PostServiceJsonAsync(BatchUrl, request);
        var body     = await response.ReadApiResponseAsync<BatchIngestResponse>();

        body.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Submitted.Should().Be(3);
        body.Data.Accepted.Should().Be(3);
        body.Data.Rejected.Should().Be(0);
    }

    [Fact]
    public async Task IngestBatch_AllValid_EachResultHasAuditId()
    {
        var request = AuditRequestBuilder.ValidBatch(count: 2);

        var response = await _client.PostServiceJsonAsync(BatchUrl, request);
        var body     = await response.ReadApiResponseAsync<BatchIngestResponse>();

        body.Data!.Results.Should().HaveCount(2);
        body.Data.Results.Should().AllSatisfy(r =>
        {
            r.Accepted.Should().BeTrue();
            r.AuditId.Should().NotBeNull();
            r.AuditId.Should().NotBe(Guid.Empty);
        });
    }

    // ── Batch-level validation failures → 400 ────────────────────────────────

    [Fact]
    public async Task IngestBatch_EmptyEventsList_Returns400()
    {
        var request = new BatchIngestRequest { Events = [] };

        var response = await _client.PostServiceJsonAsync(BatchUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IngestBatch_EmptyEventsList_BodyContainsErrors()
    {
        var request = new BatchIngestRequest { Events = [] };

        var response = await _client.PostServiceJsonAsync(BatchUrl, request);
        var body     = await response.ReadApiResponseAsync<object>();

        body.Success.Should().BeFalse();
        body.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task IngestBatch_OneItemMissingEventType_Returns400()
    {
        var valid   = AuditRequestBuilder.MinimalValid(idempotencyKey: Guid.NewGuid().ToString());
        var invalid = AuditRequestBuilder.MinimalValid();
        invalid.EventType = string.Empty;

        var request = new BatchIngestRequest { Events = [valid, invalid] };

        var response = await _client.PostServiceJsonAsync(BatchUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Duplicate key in batch → 207 ─────────────────────────────────────────

    [Fact]
    public async Task IngestBatch_OneDuplicate_Returns207()
    {
        var dupKey = $"dup-{Guid.NewGuid():N}";

        // First: ingest the key individually so it exists in the store.
        var single = AuditRequestBuilder.MinimalValid(idempotencyKey: dupKey);
        await _client.PostServiceJsonAsync(IngestUrl, single);

        // Now batch: two fresh events + one duplicate key.
        var batch = new BatchIngestRequest
        {
            Events =
            [
                AuditRequestBuilder.MinimalValid(idempotencyKey: $"fresh-{Guid.NewGuid():N}"),
                AuditRequestBuilder.MinimalValid(idempotencyKey: dupKey),
                AuditRequestBuilder.MinimalValid(idempotencyKey: $"fresh-{Guid.NewGuid():N}"),
            ],
        };

        var response = await _client.PostServiceJsonAsync(BatchUrl, batch);

        response.StatusCode.Should().Be(HttpStatusCode.MultiStatus);
    }

    [Fact]
    public async Task IngestBatch_OneDuplicate_ResultsShowMixedOutcome()
    {
        var dupKey = $"dup-{Guid.NewGuid():N}";
        var single = AuditRequestBuilder.MinimalValid(idempotencyKey: dupKey);
        await _client.PostServiceJsonAsync(IngestUrl, single);

        var batch = new BatchIngestRequest
        {
            Events =
            [
                AuditRequestBuilder.MinimalValid(idempotencyKey: $"f1-{Guid.NewGuid():N}"),
                AuditRequestBuilder.MinimalValid(idempotencyKey: dupKey),
                AuditRequestBuilder.MinimalValid(idempotencyKey: $"f2-{Guid.NewGuid():N}"),
            ],
        };

        var response = await _client.PostServiceJsonAsync(BatchUrl, batch);
        var body     = await response.ReadApiResponseAsync<BatchIngestResponse>();

        body.Data.Should().NotBeNull();
        body.Data!.Accepted.Should().Be(2);
        body.Data.Rejected.Should().Be(1);

        var rejected = body.Data.Results.Single(r => !r.Accepted);
        rejected.RejectionReason.Should().Be("DuplicateIdempotencyKey");
    }

    // ── All rejected → 422 ───────────────────────────────────────────────────

    [Fact]
    public async Task IngestBatch_AllDuplicates_Returns422()
    {
        var key1 = $"dup-{Guid.NewGuid():N}";
        var key2 = $"dup-{Guid.NewGuid():N}";

        // Pre-ingest both keys.
        await _client.PostServiceJsonAsync(IngestUrl, AuditRequestBuilder.MinimalValid(idempotencyKey: key1));
        await _client.PostServiceJsonAsync(IngestUrl, AuditRequestBuilder.MinimalValid(idempotencyKey: key2));

        var batch = new BatchIngestRequest
        {
            Events =
            [
                AuditRequestBuilder.MinimalValid(idempotencyKey: key1),
                AuditRequestBuilder.MinimalValid(idempotencyKey: key2),
            ],
        };

        var response = await _client.PostServiceJsonAsync(BatchUrl, batch);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── StopOnFirstError ──────────────────────────────────────────────────────

    [Fact]
    public async Task IngestBatch_StopOnFirstError_LaterItemsAreSkipped()
    {
        var dupKey = $"dup-{Guid.NewGuid():N}";
        await _client.PostServiceJsonAsync(IngestUrl, AuditRequestBuilder.MinimalValid(idempotencyKey: dupKey));

        var batch = new BatchIngestRequest
        {
            StopOnFirstError = true,
            Events =
            [
                AuditRequestBuilder.MinimalValid(idempotencyKey: dupKey),     // rejected → stops here
                AuditRequestBuilder.MinimalValid(idempotencyKey: $"skip-{Guid.NewGuid():N}"), // skipped
            ],
        };

        var response = await _client.PostServiceJsonAsync(BatchUrl, batch);
        var body     = await response.ReadApiResponseAsync<BatchIngestResponse>();

        var skipped = body.Data!.Results.Where(r => r.RejectionReason == "Skipped").ToList();
        skipped.Should().HaveCount(1);
    }
}
