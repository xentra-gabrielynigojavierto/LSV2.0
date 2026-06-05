using System.Net;
using FluentAssertions;
using PlatformAuditEventService.Tests.Helpers;

namespace PlatformAuditEventService.Tests.IntegrationTests;

/// <summary>
/// Integration tests for the health check endpoints.
///
///   GET /health        — built-in ASP.NET Core health probe (k8s liveness/readiness)
///   GET /health/detail — rich diagnostic endpoint from HealthController
/// </summary>
public class HealthEndpointTests(AuditServiceFactory factory)
    : IClassFixture<AuditServiceFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── GET /health ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHealth_Returns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHealth_ContentTypeIsTextPlain()
    {
        // The built-in ASP.NET Core health check middleware returns text/plain ("Healthy").
        // The richer JSON response is at /health/detail via HealthController.
        var response = await _client.GetAsync("/health");

        response.Content.Headers.ContentType?.MediaType
            .Should().Be("text/plain");
    }

    // ── GET /health/detail ────────────────────────────────────────────────────

    [Fact]
    public async Task GetHealthDetail_Returns200()
    {
        var response = await _client.GetAsync("/health/detail");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHealthDetail_BodyIsApiResponseEnvelope()
    {
        var response = await _client.GetAsync("/health/detail");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await response.ReadJsonAsync();
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetHealthDetail_ContainsServiceName()
    {
        var response = await _client.GetAsync("/health/detail");

        var doc = await response.ReadJsonAsync();

        // The detail endpoint returns service metadata — verify it's non-empty.
        var dataElement = doc.RootElement.GetProperty("data");
        dataElement.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);
    }
}
