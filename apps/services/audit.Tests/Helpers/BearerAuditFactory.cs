using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.Configuration;

namespace PlatformAuditEventService.Tests.Helpers;

/// <summary>
/// Integration test factory with <c>QueryAuth:Mode = Bearer</c> active.
///
/// Uses an in-process RSA key so tests never need a live OIDC authority endpoint.
///
/// Design mirrors <see cref="ServiceTokenAuditFactory"/>:
///   Program.cs registers <see cref="IQueryCallerResolver"/> via a factory lambda that
///   captures the raw <c>QueryAuth:Mode</c> config value at build time ("None" in
///   Development). Options-layer overrides arrive too late to affect that capture.
///
///   Fix: remove all existing <see cref="IQueryCallerResolver"/> registrations and
///   register <see cref="TestJwtCallerResolver"/> — a test-native resolver that reads
///   the Authorization Bearer header and validates the JWT directly with the test RSA key,
///   without going through the ASP.NET Core JWT Bearer middleware at all.
///
///   <c>QueryAuthMiddleware._mode</c> is set to "Bearer" via
///   <see cref="QueryAuthOptions"/> override so that unauthenticated requests (unknown
///   scope) correctly receive 401.
///
/// Usage:
///   <list type="bullet">
///     <item>Call <see cref="CreateBearerClient"/> for a client pre-loaded with a valid token.</item>
///     <item>Call <see cref="IssueToken"/> for arbitrary tokens (expired, wrong audience, …).</item>
///   </list>
///
/// Constants:
///   Issuer   = "test-issuer"
///   Audience = "test-audience"
///   Sub      = "test-user-id"
///   TenantId = "tenant-abc123"
/// </summary>
public sealed class BearerAuditFactory : AuditServiceFactory
{
    // ── Test PKI ──────────────────────────────────────────────────────────────
    private static readonly RSA              _rsa;
    private static readonly RsaSecurityKey   _privateKey;
    private static readonly RsaSecurityKey   _publicKey;
    private static readonly SigningCredentials _signingCredentials;

    public const string TestIssuer        = "test-issuer";
    public const string TestAudience      = "test-audience";
    public const string TestUserId        = "test-user-id";
    public const string TestTenantId      = "tenant-abc123";
    public const string PlatformAdminRole = "platform-admin";
    public const string TenantAdminRole   = "tenant-admin";

    static BearerAuditFactory()
    {
        _rsa              = RSA.Create(2048);
        _privateKey       = new RsaSecurityKey(_rsa);
        _publicKey        = new RsaSecurityKey(RSA.Create(_rsa.ExportParameters(false)));
        _signingCredentials = new SigningCredentials(_privateKey, SecurityAlgorithms.RsaSha256);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // ── QueryAuth:Mode = Bearer ───────────────────────────────────────
            // Sets _mode in QueryAuthMiddleware to "Bearer" so that callers with
            // Unknown scope receive 401 rather than being passed through as Anonymous.
            services.Configure<QueryAuthOptions>(opts =>
            {
                opts.Mode              = "Bearer";
                opts.TenantIdClaimType = "tenant_id";
                opts.UserIdClaimType   = "sub";
                opts.PlatformAdminRoles.Clear();
                opts.PlatformAdminRoles.Add(PlatformAdminRole);
                opts.TenantAdminRoles.Clear();
                opts.TenantAdminRoles.Add(TenantAdminRole);
            });

            // ── Replace IQueryCallerResolver with test-native JWT resolver ────
            // Program.cs registers IQueryCallerResolver using a factory lambda that
            // captured queryAuthMode="None" at startup — options overrides cannot
            // affect which concrete type that lambda returns.
            // Remove the factory and register TestJwtCallerResolver directly so the
            // middleware uses it for the entire test run.
            var existing = services
                .Where(d => d.ServiceType == typeof(IQueryCallerResolver))
                .ToList();
            foreach (var d in existing) services.Remove(d);

            services.AddSingleton<IQueryCallerResolver>(
                new TestJwtCallerResolver(
                    publicKey:         _publicKey,
                    issuer:            TestIssuer,
                    audience:          TestAudience,
                    platformAdminRole: PlatformAdminRole,
                    tenantAdminRole:   TenantAdminRole));
        });
    }

    // ── Token factory ─────────────────────────────────────────────────────────

    /// <summary>
    /// Issues a signed JWT token with the given claims.
    ///
    /// When <paramref name="expMinutes"/> is negative both NotBefore and Expires are
    /// placed in the past so the token is expired at the moment of issuance.
    ///
    /// Role claims use the short type <c>"role"</c> to match
    /// <c>QueryAuthOptions.RoleClaimType = "role"</c> without requiring inbound-claim
    /// mapping in the JWT handler.
    /// </summary>
    public string IssueToken(
        string? subject    = TestUserId,
        string? tenantId   = TestTenantId,
        string? role       = PlatformAdminRole,
        string? issuer     = TestIssuer,
        string? audience   = TestAudience,
        int     expMinutes = 5)
    {
        var claims = new List<Claim>();
        if (subject  is not null) claims.Add(new Claim("sub",       subject));
        if (tenantId is not null) claims.Add(new Claim("tenant_id", tenantId));
        if (role     is not null) claims.Add(new Claim("role",       role));

        var now = DateTime.UtcNow;

        DateTime notBefore, expires;
        if (expMinutes < 0)
        {
            notBefore = now.AddMinutes(expMinutes * 2); // well in the past
            expires   = now.AddMinutes(expMinutes);     // still in the past
        }
        else
        {
            notBefore = now;
            expires   = now.AddMinutes(expMinutes);
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(claims),
            Issuer             = issuer,
            Audience           = audience,
            NotBefore          = notBefore,
            Expires            = expires,
            SigningCredentials = _signingCredentials,
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> with an Authorization Bearer header
    /// pre-set for the given role.
    /// </summary>
    public HttpClient CreateBearerClient(
        string? role     = PlatformAdminRole,
        string? tenantId = TestTenantId,
        string? subject  = TestUserId)
    {
        var token  = IssueToken(subject: subject, tenantId: tenantId, role: role);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── Test-native caller resolver ───────────────────────────────────────────

    /// <summary>
    /// Validates JWT Bearer tokens in-process using the test RSA key.
    /// Bypasses ASP.NET Core JWT Bearer middleware so tests are not affected by
    /// JwtBearerOptions registration order or authority metadata discovery.
    /// </summary>
    private sealed class TestJwtCallerResolver : IQueryCallerResolver
    {
        private readonly TokenValidationParameters _tvp;
        private readonly string _platformAdminRole;
        private readonly string _tenantAdminRole;
        private readonly JwtSecurityTokenHandler _handler;

        public string Mode => "Bearer";

        public TestJwtCallerResolver(
            RsaSecurityKey publicKey,
            string         issuer,
            string         audience,
            string         platformAdminRole,
            string         tenantAdminRole)
        {
            _platformAdminRole = platformAdminRole;
            _tenantAdminRole   = tenantAdminRole;
            _handler           = new JwtSecurityTokenHandler { MapInboundClaims = false };

            _tvp = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidIssuer              = issuer,
                ValidateAudience         = true,
                ValidAudience            = audience,
                ValidateLifetime         = true,
                IssuerSigningKey         = publicKey,
                ValidateIssuerSigningKey = true,
                ClockSkew                = TimeSpan.Zero,
                NameClaimType            = "sub",
                RoleClaimType            = "role",
            };
        }

        public Task<IQueryCallerContext> ResolveAsync(
            Microsoft.AspNetCore.Http.HttpContext context,
            CancellationToken ct = default)
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<IQueryCallerContext>(QueryCallerContext.Failed(Mode));

            var token = authHeader["Bearer ".Length..].Trim();

            ClaimsPrincipal principal;
            try
            {
                principal = _handler.ValidateToken(token, _tvp, out _);
            }
            catch (SecurityTokenException)
            {
                return Task.FromResult<IQueryCallerContext>(QueryCallerContext.Failed(Mode));
            }

            var tenantId = principal.FindFirst("tenant_id")?.Value;
            var userId   = principal.FindFirst("sub")?.Value;
            var roles    = principal.Claims
                .Where(c => c.Type.Equals("role", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .ToList();

            var scope = ResolveScope(roles);

            var ctx = QueryCallerContext.Authenticated(
                scope:          scope,
                tenantId:       tenantId,
                organizationId: null,
                userId:         userId,
                roles:          roles,
                authMode:       Mode);

            return Task.FromResult<IQueryCallerContext>(ctx);
        }

        private CallerScope ResolveScope(IReadOnlyList<string> roles)
        {
            if (roles.Count == 0)
                return CallerScope.TenantUser;

            if (roles.Any(r => r.Equals(_platformAdminRole, StringComparison.OrdinalIgnoreCase)))
                return CallerScope.PlatformAdmin;
            if (roles.Any(r => r.Equals(_tenantAdminRole, StringComparison.OrdinalIgnoreCase)))
                return CallerScope.TenantAdmin;

            return CallerScope.TenantUser;
        }
    }
}
