using System.Net;
using FluentAssertions;
using PlatformAuditEventService.DTOs.Export;
using PlatformAuditEventService.Enums;
using PlatformAuditEventService.Tests.Helpers;

namespace PlatformAuditEventService.Tests.IntegrationTests;

/// <summary>
/// Integration tests for the export lifecycle endpoints:
///
///   POST /audit/exports         — Submit an export job
///   GET  /audit/exports/{id}    — Poll export job status
///
/// The test factory sets <c>Export:Provider = "None"</c>, which means the service
/// is intentionally unconfigured — both endpoints return 503 with an
/// <c>ApiResponse</c> error envelope.
///
/// This validates the configuration-gate code path (Step 21 hardening).
/// </summary>
public class ExportEndpointTests(AuditServiceFactory factory)
    : IClassFixture<AuditServiceFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string ExportUrl = "/audit/exports";

    // ── POST /audit/exports — provider disabled → 503 ────────────────────────

    [Fact]
    public async Task PostExport_WhenProviderIsNone_Returns503()
    {
        var request = new ExportRequest
        {
            ScopeType = ScopeType.Tenant,
            ScopeId   = "tenant-export-001",
            Format    = "Json",
        };

        var response = await _client.PostServiceJsonAsync(ExportUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task PostExport_WhenProviderIsNone_BodyIsApiResponseEnvelope()
    {
        var request = new ExportRequest
        {
            ScopeType = ScopeType.Tenant,
            ScopeId   = "tenant-export-002",
            Format    = "Json",
        };

        var response = await _client.PostServiceJsonAsync(ExportUrl, request);
        var body     = await response.ReadApiResponseAsync<object>();

        body.Success.Should().BeFalse();
        body.Message.Should().NotBeNullOrWhiteSpace();
    }

    // ── GET /audit/exports/{id} — provider disabled → 503 ────────────────────

    [Fact]
    public async Task GetExportStatus_WhenProviderIsNone_Returns503()
    {
        var exportId = Guid.NewGuid();

        var response = await _client.GetAsync($"{ExportUrl}/{exportId}");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetExportStatus_WhenProviderIsNone_BodyIsApiResponseEnvelope()
    {
        var exportId = Guid.NewGuid();

        var response = await _client.GetAsync($"{ExportUrl}/{exportId}");
        var body     = await response.ReadApiResponseAsync<object>();

        body.Success.Should().BeFalse();
        body.Message.Should().NotBeNullOrWhiteSpace();
    }
}
