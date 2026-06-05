using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.DTOs.LegalHold;
using PlatformAuditEventService.Tests.Helpers;

namespace PlatformAuditEventService.Tests.IntegrationTests;

/// <summary>
/// Integration tests for legal hold CRUD endpoints and compliance invariants.
///
/// Uses the base <see cref="AuditServiceFactory"/> (Mode=None) because legal hold
/// endpoints are unauthenticated in the Development environment, allowing us to
/// focus purely on the business logic.
///
/// Scenarios covered:
///   - Create a hold on a valid AuditId → 201
///   - Create a hold on non-existent AuditId → 404
///   - List holds by AuditId (empty + after create)
///   - Release an active hold → 200 + ReleasedAtUtc populated
///   - Release an already-released hold → 409 Conflict
///   - Release a non-existent hold → 404
/// </summary>
public class LegalHoldRetentionTests : IClassFixture<AuditServiceFactory>
{
    private readonly AuditServiceFactory _factory;

    private const string IngestUrl     = "/internal/audit/events";
    private const string LegalHoldBase = "/audit/legal-holds";

    public LegalHoldRetentionTests(AuditServiceFactory factory)
    {
        _factory = factory;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ingests a minimal audit event and returns its AuditId.
    /// </summary>
    private async Task<Guid> IngestOneAndGetAuditIdAsync(HttpClient client)
    {
        var request  = AuditRequestBuilder.MinimalValid();
        var response = await client.PostServiceJsonAsync(IngestUrl, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body  = await response.ReadApiResponseAsync<IngestItemResult>();
        body.Data.Should().NotBeNull();
        body.Data!.AuditId.Should().NotBeNull("ingest should return a valid AuditId");
        return body.Data.AuditId!.Value;
    }

    // ── Create hold ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateHold_ValidAuditId_Returns201()
    {
        using var client = _factory.CreateClient();
        var auditId      = await IngestOneAndGetAuditIdAsync(client);

        var holdReq  = new CreateLegalHoldRequest { LegalAuthority = "FRCP 34" };
        var response = await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{auditId}", holdReq);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateHold_ValidAuditId_ResponseContainsHoldId()
    {
        using var client = _factory.CreateClient();
        var auditId      = await IngestOneAndGetAuditIdAsync(client);

        var holdReq  = new CreateLegalHoldRequest
        {
            LegalAuthority = "SOX 404",
            Notes          = "Pre-litigation hold — FY2024 audit cycle",
        };
        var response = await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{auditId}", holdReq);

        var body = await response.ReadApiResponseAsync<LegalHoldResponse>();
        body.Success.Should().BeTrue();
        body.Data!.AuditId.Should().Be(auditId);
        body.Data.HoldId.Should().NotBe(Guid.Empty);
        body.Data.LegalAuthority.Should().Be("SOX 404");
        body.Data.ReleasedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task CreateHold_NonExistentAuditId_Returns404()
    {
        using var client = _factory.CreateClient();
        var missing      = Guid.NewGuid();

        var holdReq  = new CreateLegalHoldRequest { LegalAuthority = "FRCP 34" };
        var response = await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{missing}", holdReq);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── List holds by AuditId ─────────────────────────────────────────────────

    [Fact]
    public async Task ListByAuditId_NoHolds_ReturnsEmptyList()
    {
        using var client = _factory.CreateClient();
        var auditId      = await IngestOneAndGetAuditIdAsync(client);

        var response = await client.GetAsync(
            $"{LegalHoldBase}/record/{auditId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadApiResponseAsync<IReadOnlyList<LegalHoldResponse>>();
        body.Success.Should().BeTrue();
        body.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task ListByAuditId_AfterCreatingHold_ReturnsOneHold()
    {
        using var client = _factory.CreateClient();
        var auditId      = await IngestOneAndGetAuditIdAsync(client);

        // Create a hold
        var holdReq = new CreateLegalHoldRequest { LegalAuthority = "GDPR Art.17" };
        var createResp = await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{auditId}", holdReq);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // List holds
        var listResp = await client.GetAsync(
            $"{LegalHoldBase}/record/{auditId}");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResp.ReadApiResponseAsync<IReadOnlyList<LegalHoldResponse>>();
        body.Data.Should().HaveCount(1);
        body.Data![0].AuditId.Should().Be(auditId);
        body.Data[0].LegalAuthority.Should().Be("GDPR Art.17");
        body.Data[0].ReleasedAtUtc.Should().BeNull("hold is still active");
    }

    // ── Release hold ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ReleaseHold_ActiveHold_Returns200()
    {
        using var client = _factory.CreateClient();
        var auditId      = await IngestOneAndGetAuditIdAsync(client);

        // Create the hold
        var createResp = await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{auditId}",
            new CreateLegalHoldRequest { LegalAuthority = "FRCP 34" });
        var createBody = await createResp.ReadApiResponseAsync<LegalHoldResponse>();
        var holdId     = createBody.Data!.HoldId;

        // Release it
        var releaseResp = await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{holdId}/release",
            new ReleaseLegalHoldRequest { ReleaseNotes = "Matter settled." });

        releaseResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReleaseHold_ActiveHold_ResponseShowsReleasedAtUtc()
    {
        using var client = _factory.CreateClient();
        var auditId      = await IngestOneAndGetAuditIdAsync(client);

        var createResp = await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{auditId}",
            new CreateLegalHoldRequest { LegalAuthority = "SOX 906" });
        var holdId = (await createResp.ReadApiResponseAsync<LegalHoldResponse>()).Data!.HoldId;

        var releaseResp = await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{holdId}/release",
            new ReleaseLegalHoldRequest());

        var body = await releaseResp.ReadApiResponseAsync<LegalHoldResponse>();
        body.Success.Should().BeTrue();
        body.Data!.ReleasedAtUtc.Should().NotBeNull("hold was just released");
        body.Data.HoldId.Should().Be(holdId);
    }

    [Fact]
    public async Task ReleaseHold_AlreadyReleased_Returns409Conflict()
    {
        using var client = _factory.CreateClient();
        var auditId      = await IngestOneAndGetAuditIdAsync(client);

        var createResp = await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{auditId}",
            new CreateLegalHoldRequest { LegalAuthority = "FRCP 34" });
        var holdId = (await createResp.ReadApiResponseAsync<LegalHoldResponse>()).Data!.HoldId;

        // First release — OK
        await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{holdId}/release",
            new ReleaseLegalHoldRequest());

        // Second release — should be 409
        var secondRelease = await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{holdId}/release",
            new ReleaseLegalHoldRequest());

        secondRelease.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReleaseHold_NonExistentHoldId_Returns404()
    {
        using var client = _factory.CreateClient();
        var missing      = Guid.NewGuid();

        var response = await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{missing}/release",
            new ReleaseLegalHoldRequest { ReleaseNotes = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Legal hold compliance invariant ───────────────────────────────────────

    [Fact]
    public async Task CreateTwoHolds_BothAppearInList()
    {
        using var client = _factory.CreateClient();
        var auditId      = await IngestOneAndGetAuditIdAsync(client);

        await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{auditId}",
            new CreateLegalHoldRequest { LegalAuthority = "FRCP 34" });

        await client.PostServiceJsonAsync(
            $"{LegalHoldBase}/{auditId}",
            new CreateLegalHoldRequest { LegalAuthority = "SOX 302" });

        var listResp = await client.GetAsync(
            $"{LegalHoldBase}/record/{auditId}");
        var body = await listResp.ReadApiResponseAsync<IReadOnlyList<LegalHoldResponse>>();

        body.Data.Should().HaveCount(2);
        body.Data.Should().Contain(h => h.LegalAuthority == "FRCP 34");
        body.Data.Should().Contain(h => h.LegalAuthority == "SOX 302");
    }
}
