using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Identity.Tests;

/// <summary>
/// Integration tests for <c>POST /api/auth/accept-invite</c> covering the
/// LS-ID-TNT-016-01 tenant-subdomain login redirect feature:
///
///   - 200 response includes a non-null <c>tenantPortalUrl</c> when
///     <c>NotificationsService:PortalBaseDomain</c> is configured and the
///     tenant has a subdomain.
///   - 200 response <c>tenantPortalUrl</c> is null when neither
///     <c>PortalBaseDomain</c> nor <c>PortalBaseUrl</c> is configured.
/// </summary>
public class AcceptInviteTenantPortalUrlTests
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    private static WebApplicationFactory<Program> BuildFactory(
        string? portalBaseDomain,
        string? portalBaseUrl = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:IdentityDb"]         = "Server=localhost;Database=identity_test_placeholder;",
                    ["Jwt:SigningKey"]                        = "test-only-signing-key-32-chars-padded-ok",
                    ["Jwt:Issuer"]                           = "test-issuer",
                    ["Jwt:Audience"]                         = "test-audience",
                    ["NotificationsService:BaseUrl"]         = "http://localhost:19999",
                    ["NotificationsService:PortalBaseUrl"]   = portalBaseUrl  ?? "",
                    ["NotificationsService:PortalBaseDomain"]= portalBaseDomain ?? "",
                });
            });

            builder.ConfigureTestServices(services =>
            {
                var hostedSvcs = services
                    .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
                    .ToList();
                foreach (var s in hostedSvcs) services.Remove(s);

                var dbDescriptors = services
                    .Where(d =>
                        d.ServiceType == typeof(IdentityDbContext) ||
                        d.ServiceType == typeof(DbContextOptions<IdentityDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions))
                    .ToList();
                foreach (var d in dbDescriptors) services.Remove(d);

                var dbName = "accept-invite-test-" + Guid.NewGuid();
                services.AddDbContext<IdentityDbContext>(opts =>
                    opts.UseInMemoryDatabase(dbName));

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, AnonAuthHandler>("Test", _ => { });
                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = "Test";
                    opts.DefaultChallengeScheme    = "Test";
                });
            });
        });
    }

    /// <summary>
    /// Seeds a tenant (with an optional subdomain), an invited user, and a pending
    /// invitation.  Returns the raw token to submit in the request body.
    /// </summary>
    private static async Task<string> SeedInviteAsync(
        WebApplicationFactory<Program> factory,
        string? tenantSubdomain = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var tenant = Tenant.Create("Test Tenant", $"tstco-{Guid.NewGuid():N}");
        if (tenantSubdomain is not null)
            tenant.SetSubdomain(tenantSubdomain);

        db.Tenants.Add(tenant);

        var user = User.Create(tenant.Id, $"invited-{Guid.NewGuid():N}@example.com", "placeholder-hash", "Alice", "Tester");
        db.Users.Add(user);

        var rawToken  = Guid.NewGuid().ToString("N");
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var invitation = UserInvitation.Create(user.Id, tenant.Id, tokenHash);
        db.UserInvitations.Add(invitation);

        await db.SaveChangesAsync();
        return rawToken;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// When PortalBaseDomain is configured and the tenant has a subdomain, the
    /// accept-invite 200 response must include a non-null tenantPortalUrl that
    /// uses the tenant's subdomain.
    /// </summary>
    [Fact]
    public async Task AcceptInvite_Returns200_WithTenantPortalUrl_WhenBaseDomainIsConfigured()
    {
        const string baseDomain = "portal.example.com";
        const string subdomain  = "acmefirm";

        using var factory = BuildFactory(portalBaseDomain: baseDomain);
        var rawToken = await SeedInviteAsync(factory, tenantSubdomain: subdomain);
        var client   = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/accept-invite", new
        {
            token       = rawToken,
            newPassword = "SecurePass1!",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AcceptInviteResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.TenantPortalUrl);
        Assert.Contains(subdomain,   body.TenantPortalUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(baseDomain,  body.TenantPortalUrl, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("https://", body.TenantPortalUrl);
    }

    /// <summary>
    /// When neither PortalBaseDomain nor PortalBaseUrl is configured, the
    /// accept-invite response should still return 200 but tenantPortalUrl must be null.
    /// </summary>
    [Fact]
    public async Task AcceptInvite_Returns200_TenantPortalUrlIsNull_WhenNeitherDomainNorBaseUrlConfigured()
    {
        using var factory = BuildFactory(portalBaseDomain: null, portalBaseUrl: null);
        var rawToken = await SeedInviteAsync(factory, tenantSubdomain: "somesubdomain");
        var client   = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/accept-invite", new
        {
            token       = rawToken,
            newPassword = "SecurePass1!",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AcceptInviteResponse>();
        Assert.NotNull(body);
        Assert.Null(body.TenantPortalUrl);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed record AcceptInviteResponse(
        string? Message,
        string? TenantPortalUrl);

    // ── Test doubles ──────────────────────────────────────────────────────────

    private sealed class AnonAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AnonAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }
}
