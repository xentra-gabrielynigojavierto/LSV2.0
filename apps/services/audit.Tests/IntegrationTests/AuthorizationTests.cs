using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Tests.Helpers;

namespace PlatformAuditEventService.Tests.IntegrationTests;

/// <summary>
/// Integration tests for IngestAuthMiddleware in ServiceToken mode.
///
/// Uses <see cref="ServiceTokenAuditFactory"/> which overrides:
///   IngestAuth:Mode = "ServiceToken"
///   IngestAuth:ServiceTokens[0].Token = ServiceTokenAuditFactory.ValidToken
///
/// Protected path: /internal/audit/*
/// Unprotected paths (health, swagger, query) are NOT affected by IngestAuth.
///
/// Scenarios:
///   - No x-service-token header → 401
///   - Invalid/wrong token → 401
///   - Valid token → 201 (request proceeds to pipeline)
///   - Non-ingest paths are unaffected by IngestAuth
/// </summary>
public class AuthorizationTests(ServiceTokenAuditFactory factory)
    : IClassFixture<ServiceTokenAuditFactory>
{
    private const string IngestUrl = "/internal/audit/events";

    // ── No token → 401 ────────────────────────────────────────────────────────

    [Fact]
    public async Task IngestSingle_NoToken_Returns401()
    {
        using var client = factory.CreateClient();

        var request  = AuditRequestBuilder.MinimalValid();
        var response = await client.PostServiceJsonAsync(IngestUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IngestSingle_NoToken_BodyIsApiResponseEnvelope()
    {
        using var client = factory.CreateClient();

        var request  = AuditRequestBuilder.MinimalValid();
        var response = await client.PostServiceJsonAsync(IngestUrl, request);
        var body     = await response.ReadApiResponseAsync<object>();

        body.Success.Should().BeFalse();
        body.Message.Should().NotBeNullOrWhiteSpace();
    }

    // ── Wrong token → 401 ────────────────────────────────────────────────────

    [Fact]
    public async Task IngestSingle_WrongToken_Returns401()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(IngestAuthHeaders.ServiceToken, "wrong-token-value");

        var request  = AuditRequestBuilder.MinimalValid();
        var response = await client.PostServiceJsonAsync(IngestUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Valid token → request proceeds ────────────────────────────────────────

    [Fact]
    public async Task IngestSingle_ValidToken_Returns201()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(IngestAuthHeaders.ServiceToken, ServiceTokenAuditFactory.ValidToken);

        var request  = AuditRequestBuilder.MinimalValid();
        var response = await client.PostServiceJsonAsync(IngestUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task IngestSingle_ValidToken_ResponseBodyShowsSuccess()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(IngestAuthHeaders.ServiceToken, ServiceTokenAuditFactory.ValidToken);

        var request  = AuditRequestBuilder.MinimalValid();
        var response = await client.PostServiceJsonAsync(IngestUrl, request);
        var body     = await response.ReadApiResponseAsync<PlatformAuditEventService.DTOs.Ingest.IngestItemResult>();

        body.Success.Should().BeTrue();
        body.Data!.Accepted.Should().BeTrue();
    }

    // ── Non-ingest paths bypass IngestAuth ────────────────────────────────────

    [Fact]
    public async Task GetHealth_WithoutToken_Returns200()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAuditEvents_WithoutToken_Returns200()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/audit/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
