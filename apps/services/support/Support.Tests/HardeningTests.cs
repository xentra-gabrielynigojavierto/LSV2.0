using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Support.Api.Data;
using Support.Api.Domain;
using Support.Api.Dtos;
using Support.Api.Endpoints;

namespace Support.Tests;

// ── Low-limit rate-limit factory ─────────────────────────────────────────────
// Uses the same Production-like JWT setup as SupportApiProdFactory, but
// overrides the rate-limit permit to 3 so tests can verify 429 behavior
// without making 61+ real requests.

public class RateLimitTestFactory : WebApplicationFactory<Program>
{
    public string DbName { get; } = $"support-tests-ratelimit-{Guid.NewGuid()}";

    static RateLimitTestFactory()
    {
        // Env vars are read by the default config providers BEFORE Program.cs
        // queries builder.Configuration — the ONLY reliable injection point for
        // WebApplication.CreateBuilder + WebApplicationFactory (same pattern as
        // SupportApiProdFactory for JWT settings).
        Environment.SetEnvironmentVariable("Jwt__Issuer",
            "https://test-issuer.local");
        Environment.SetEnvironmentVariable("Jwt__Audience",
            "support-api-tests");
        Environment.SetEnvironmentVariable("Jwt__SigningKey",
            "test-only-symmetric-signing-key-for-prod-like-tests-32+chars!!");

        // Override rate-limit permit to 3 so tests can verify 429 without
        // making 61 real requests.  Each test uses a unique customer GUID,
        // so buckets are isolated and existing tests are unaffected.
        Environment.SetEnvironmentVariable("Support__RateLimit__CustomerPermitLimit", "3");
        Environment.SetEnvironmentVariable("Support__RateLimit__CustomerWindowSeconds", "60");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<SupportDbContext>) ||
                d.ServiceType == typeof(SupportDbContext)).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<SupportDbContext>(o => o.UseInMemoryDatabase(DbName));
        });
    }
}

/// <summary>
/// SUP-TNT-05: API Hardening tests.
///
/// Covers:
///   - Page/pageSize input validation on customer list endpoint
///   - Comment body validation (required, max length)
///   - Security headers on all responses
///   - Rate limiting: exceeding the per-customer limit returns 429 with Retry-After
///   - Existing functionality unchanged (all original assertions preserved)
/// </summary>
public class HardeningTests : IClassFixture<SupportApiProdFactory>
{
    private readonly SupportApiProdFactory _factory;

    public HardeningTests(SupportApiProdFactory factory) => _factory = factory;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private HttpClient CustomerClient(string tenantId, Guid customerId, string email = "customer@example.com")
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.IssueCustomer(tenantId, customerId, email: email));
        return c;
    }

    private HttpClient ManagerClient(string tenantId)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Issue(tenantId: tenantId, roles: new[] { "SupportManager" }));
        return c;
    }

    private async Task EnableCustomerSupportAsync(string tenantId)
    {
        var manager = ManagerClient(tenantId);
        var resp = await manager.PutAsJsonAsync(
            "/support/api/admin/tenant-settings",
            new { supportMode = "TenantCustomerSupport", customerPortalEnabled = true });
        resp.IsSuccessStatusCode.Should().BeTrue(
            $"enabling customer support for tenant '{tenantId}' failed: {resp.StatusCode}");
    }

    // ── Tests: Pagination validation ─────────────────────────────────────────

    [Fact]
    public async Task CustomerList_PageZero_Returns400()
    {
        var tenant     = $"hard-page0-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var customer   = CustomerClient(tenant, customerId);

        var resp = await customer.GetAsync("/support/api/customer/tickets?page=0");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "page=0 violates the page ≥ 1 constraint");
    }

    [Fact]
    public async Task CustomerList_NegativePage_Returns400()
    {
        var tenant     = $"hard-pageneg-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var customer   = CustomerClient(tenant, customerId);

        var resp = await customer.GetAsync("/support/api/customer/tickets?page=-5");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "negative page values must be rejected");
    }

    [Fact]
    public async Task CustomerList_PageSizeZero_Returns400()
    {
        var tenant     = $"hard-ps0-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var customer   = CustomerClient(tenant, customerId);

        var resp = await customer.GetAsync("/support/api/customer/tickets?page_size=0");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "page_size=0 is outside the valid 1–100 range");
    }

    [Fact]
    public async Task CustomerList_PageSizeTooLarge_Returns400()
    {
        var tenant     = $"hard-pslarge-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var customer   = CustomerClient(tenant, customerId);

        var resp = await customer.GetAsync("/support/api/customer/tickets?page_size=101");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "page_size=101 exceeds the maximum of 100");
    }

    [Fact]
    public async Task CustomerList_MaxPageSize_PassesValidation()
    {
        var tenant     = $"hard-psmax-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();

        await EnableCustomerSupportAsync(tenant);

        var customer = CustomerClient(tenant, customerId);
        var resp     = await customer.GetAsync("/support/api/customer/tickets?page=1&page_size=100");

        // 200 (no tickets, empty list) — passes parameter validation and mode gate.
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "page_size=100 is the maximum allowed value and must succeed");
    }

    // ── Tests: Comment body validation ───────────────────────────────────────

    [Fact]
    public async Task CustomerComment_EmptyBody_Returns400()
    {
        var tenant     = $"hard-cempty-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var ticketId   = Guid.NewGuid();
        var customer   = CustomerClient(tenant, customerId);

        var resp = await customer.PostAsJsonAsync(
            $"/support/api/customer/tickets/{ticketId}/comments",
            new { body = "" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "empty comment body must be rejected with 400");
    }

    [Fact]
    public async Task CustomerComment_WhitespaceOnlyBody_Returns400()
    {
        var tenant     = $"hard-cws-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var ticketId   = Guid.NewGuid();
        var customer   = CustomerClient(tenant, customerId);

        var resp = await customer.PostAsJsonAsync(
            $"/support/api/customer/tickets/{ticketId}/comments",
            new { body = "   \t\n  " });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "whitespace-only comment body must be rejected with 400");
    }

    [Fact]
    public async Task CustomerComment_BodyExceedsMaxLength_Returns400()
    {
        var tenant     = $"hard-clong-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var ticketId   = Guid.NewGuid();
        var customer   = CustomerClient(tenant, customerId);

        var tooLong = new string('x', 8_001);
        var resp = await customer.PostAsJsonAsync(
            $"/support/api/customer/tickets/{ticketId}/comments",
            new { body = tooLong });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "comment body exceeding 8000 characters must be rejected with 400");
    }

    [Fact]
    public async Task CustomerComment_BodyAtMaxLength_PassesValidation()
    {
        // Validation passes (body is 8000 chars) but the mode gate will return 403
        // (mode not enabled for this tenant). We only care that the response is NOT
        // 400 — confirming the length is accepted.
        var tenant     = $"hard-cmaxlen-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var ticketId   = Guid.NewGuid();
        var customer   = CustomerClient(tenant, customerId);

        var maxBody = new string('a', 8_000);
        var resp = await customer.PostAsJsonAsync(
            $"/support/api/customer/tickets/{ticketId}/comments",
            new { body = maxBody });

        // Mode is InternalOnly → 403. NOT 400 (validation passed).
        resp.StatusCode.Should().NotBe(HttpStatusCode.BadRequest,
            "a body of exactly 8000 characters must pass length validation");
    }

    // ── Tests: Security headers ───────────────────────────────────────────────

    [Fact]
    public async Task SecurityHeaders_PresentOnCustomerListResponse()
    {
        // Even a 401 response (no token) should carry the security headers,
        // because SecurityHeadersMiddleware runs before authentication.
        var c    = _factory.CreateClient();
        var resp = await c.GetAsync("/support/api/customer/tickets");

        resp.Headers.TryGetValues("X-Content-Type-Options", out var xct).Should().BeTrue(
            "X-Content-Type-Options must be present on every response");
        xct!.Should().ContainSingle(v => v == "nosniff");

        resp.Headers.TryGetValues("X-Frame-Options", out var xfo).Should().BeTrue(
            "X-Frame-Options must be present on every response");
        xfo!.Should().ContainSingle(v => v == "DENY");

        resp.Headers.TryGetValues("X-XSS-Protection", out var xxss).Should().BeTrue(
            "X-XSS-Protection must be present on every response");
        xxss!.Should().ContainSingle(v => v == "0");
    }

    [Fact]
    public async Task SecurityHeaders_PresentOnHealthEndpoint()
    {
        var c    = _factory.CreateClient();
        var resp = await c.GetAsync("/support/api/health");

        resp.Headers.TryGetValues("X-Content-Type-Options", out var xct).Should().BeTrue();
        xct!.Should().ContainSingle(v => v == "nosniff");
    }
}

/// <summary>
/// Rate limiting tests — use the low-permit RateLimitTestFactory so we can
/// verify 429 behavior without making 61 requests.
/// </summary>
public class RateLimitTests : IClassFixture<RateLimitTestFactory>
{
    private readonly RateLimitTestFactory _factory;

    public RateLimitTests(RateLimitTestFactory factory) => _factory = factory;

    private HttpClient CustomerClient(string tenantId, Guid customerId)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.IssueCustomer(tenantId, customerId));
        return c;
    }

    [Fact]
    public async Task CustomerEndpoint_Returns429_WhenPermitLimitExceeded()
    {
        // The RateLimitTestFactory configures permit limit = 3 per 60 seconds.
        // The rate limiter runs before the authorization / mode gate, so we can
        // use any customer_id key — no mode setup needed.
        var tenant     = $"rl-test-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var customer   = CustomerClient(tenant, customerId);

        // First 3 requests should NOT be 429 (may be 403 due to mode=InternalOnly,
        // but the rate-limit permit is not yet exhausted).
        for (var i = 1; i <= 3; i++)
        {
            var resp = await customer.GetAsync("/support/api/customer/tickets");
            resp.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                $"request #{i} should not be rate-limited (permit limit is 3)");
        }

        // 4th request exceeds the permit limit → must get 429.
        var limitedResp = await customer.GetAsync("/support/api/customer/tickets");
        limitedResp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "the 4th request within the window must be rate-limited");
    }

    [Fact]
    public async Task CustomerEndpoint_429Response_HasRetryAfterHeader()
    {
        var tenant     = $"rl-header-{Guid.NewGuid():N}";
        var customerId = Guid.NewGuid();
        var customer   = CustomerClient(tenant, customerId);

        // Exhaust the limit (3 requests) then check the 4th response headers.
        for (var i = 0; i < 3; i++)
            await customer.GetAsync("/support/api/customer/tickets");

        var limitedResp = await customer.GetAsync("/support/api/customer/tickets");
        limitedResp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limitedResp.Headers.Contains("Retry-After").Should().BeTrue(
            "rate-limited responses must carry a Retry-After header");
    }

    [Fact]
    public async Task DifferentCustomers_HaveIndependentRateLimits()
    {
        // Customer A exhausts the limit; Customer B (different customer_id) should
        // still be able to make requests — rate limits are per external_customer_id.
        var tenant     = $"rl-iso-{Guid.NewGuid():N}";
        var customerA  = Guid.NewGuid();
        var customerB  = Guid.NewGuid();
        var clientA    = CustomerClient(tenant, customerA);
        var clientB    = CustomerClient(tenant, customerB);

        // Exhaust customer A's limit.
        for (var i = 0; i < 3; i++)
            await clientA.GetAsync("/support/api/customer/tickets");

        // Customer A is now rate-limited.
        var aResp = await clientA.GetAsync("/support/api/customer/tickets");
        aResp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "Customer A should be rate-limited after 3 requests");

        // Customer B should NOT be rate-limited (independent permit bucket).
        var bResp = await clientB.GetAsync("/support/api/customer/tickets");
        bResp.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
            "Customer B has an independent rate-limit bucket and must not be affected");
    }
}
