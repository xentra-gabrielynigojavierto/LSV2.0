using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using PlatformAuditEventService.Tests.Helpers;

namespace PlatformAuditEventService.Tests.IntegrationTests;

/// <summary>
/// Integration tests for query endpoint authorization in Bearer (JWT) mode.
///
/// Uses <see cref="BearerAuditFactory"/> which:
///   - Activates QueryAuth:Mode = Bearer
///   - Registers ClaimsCallerResolver as IQueryCallerResolver
///   - Configures JWT validation against an in-process RSA key (no live OIDC needed)
///   - Issues test tokens via <see cref="BearerAuditFactory.IssueToken"/>
///
/// Scenarios covered:
///   - No Bearer token → 401
///   - Invalid / tampered token → 401
///   - Expired token → 401
///   - Wrong audience → 401
///   - Valid PlatformAdmin token → 200
///   - Valid TenantAdmin token → 200 (tenant-scoped)
///   - Valid token, no matching role → 403 (UserSelf or Unknown scope)
/// </summary>
public class QueryAuthBearerTests(BearerAuditFactory factory)
    : IClassFixture<BearerAuditFactory>
{
    private const string QueryUrl = "/audit/events";

    // ── No token → 401 ────────────────────────────────────────────────────────

    [Fact]
    public async Task Query_NoToken_Returns401()
    {
        using var client   = factory.CreateClient();
        var response       = await client.GetAsync(QueryUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Query_NoToken_BodyIsApiResponseEnvelope_WithMessage()
    {
        using var client   = factory.CreateClient();
        var response       = await client.GetAsync(QueryUrl);
        var body           = await response.ReadApiResponseAsync<object>();

        body.Success.Should().BeFalse();
        body.Message.Should().NotBeNullOrWhiteSpace();
    }

    // ── Malformed / tampered token → 401 ──────────────────────────────────────

    [Fact]
    public async Task Query_MalformedBearerToken_Returns401()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "this.is.not.a.jwt");

        var response = await client.GetAsync(QueryUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Query_TamperedSignature_Returns401()
    {
        var token  = factory.IssueToken();
        // Corrupt the signature portion (last segment after the final '.')
        var parts  = token.Split('.');
        parts[2]   = "invalidsignaturexxx";
        var tampered = string.Join('.', parts);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync(QueryUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Expired token → 401 ───────────────────────────────────────────────────

    [Fact]
    public async Task Query_ExpiredToken_Returns401()
    {
        // Issue a token that expired 60 seconds ago.
        var token  = factory.IssueToken(expMinutes: -1);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(QueryUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Wrong audience → 401 ─────────────────────────────────────────────────

    [Fact]
    public async Task Query_WrongAudience_Returns401()
    {
        var token  = factory.IssueToken(audience: "completely-wrong-audience");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(QueryUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Valid PlatformAdmin token → 200 ───────────────────────────────────────

    [Fact]
    public async Task Query_ValidPlatformAdminToken_Returns200()
    {
        using var client   = factory.CreateBearerClient(role: BearerAuditFactory.PlatformAdminRole);
        var response       = await client.GetAsync(QueryUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Query_ValidPlatformAdminToken_ResponseBodyIsSuccess()
    {
        using var client = factory.CreateBearerClient(role: BearerAuditFactory.PlatformAdminRole);
        var response     = await client.GetAsync(QueryUrl);
        var body         = await response.ReadApiResponseAsync<object>();

        body.Success.Should().BeTrue();
    }

    // ── Valid TenantAdmin token → 200 (own tenant) ────────────────────────────

    [Fact]
    public async Task Query_ValidTenantAdminToken_Returns200()
    {
        using var client = factory.CreateBearerClient(
            role:     BearerAuditFactory.TenantAdminRole,
            tenantId: BearerAuditFactory.TestTenantId);

        var response = await client.GetAsync(QueryUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Query_ValidTenantAdminToken_TenantQueryReturns200()
    {
        using var client = factory.CreateBearerClient(
            role:     BearerAuditFactory.TenantAdminRole,
            tenantId: BearerAuditFactory.TestTenantId);

        var url      = $"/audit/tenant/{BearerAuditFactory.TestTenantId}";
        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Token with no role → TenantUser scope → allowed (user-level constraints) ──

    [Fact]
    public async Task Query_TokenWithNoRole_TenantUserScopeAllowed()
    {
        // ClaimsCallerResolver maps authenticated tokens with no role claims to
        // CallerScope.TenantUser — the minimum safe fallback. TenantUser callers
        // CAN query but see only their own records (constrained by userId).
        // The query endpoint therefore returns 200, not 401/403.
        var token = factory.IssueToken(role: null);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(QueryUrl);

        // 200: authenticated TenantUser is allowed; results are user-scoped.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Non-query paths bypass QueryAuth ─────────────────────────────────────

    [Fact]
    public async Task Health_WithoutToken_Returns200()
    {
        using var client = factory.CreateClient();
        var response     = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
