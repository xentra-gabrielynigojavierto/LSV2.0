using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Flow.Api.Middleware;
using Flow.Api.Services;
using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.Adapters.NotificationAdapter;
using Flow.Application.Events;
using Flow.Domain.Interfaces;
using Flow.Infrastructure;
using Flow.Infrastructure.Adapters;
using Flow.Infrastructure.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// MVC + JSON
// ---------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .SelectMany(e => e.Value!.Errors.Select(err => err.ErrorMessage))
            .ToList();

        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new
        {
            error = "Validation failed.",
            errors
        });
    };
});

// ---------------------------------------------------------------------------
// Identity / JWT (LegalSynq Identity v2 conventions)
// ---------------------------------------------------------------------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"] ?? string.Empty;

// LS-FLOW-MERGE-P5 — two JwtBearer schemes coexist:
//   - "Bearer" (default): user tokens issued by Identity v2.
//   - "ServiceToken":     HS256 machine-to-machine tokens minted by
//                         products via BuildingBlocks ServiceTokenIssuer.
// A policy scheme ("MultiAuth") inspects the inbound token's `aud` claim
// and forwards to whichever bearer is appropriate. Endpoints stay
// configured against the default scheme; both token shapes authenticate.
const string MultiScheme = "MultiAuth";

var serviceTokenSection = builder.Configuration.GetSection(ServiceTokenOptions.SectionName);
var serviceTokenAudience = serviceTokenSection["Audience"] ?? ServiceTokenAuthenticationDefaults.DefaultAudience;

var authBuilder = builder.Services
    .AddAuthentication(MultiScheme)
    .AddPolicyScheme(MultiScheme, MultiScheme, options =>
    {
        options.ForwardDefaultSelector = ctx =>
        {
            var auth = ctx.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return JwtBearerDefaults.AuthenticationScheme;
            var token = auth.Substring("Bearer ".Length).Trim();
            try
            {
                var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
                if (jwt.Audiences.Any(a => string.Equals(a, serviceTokenAudience, StringComparison.Ordinal)))
                    return ServiceTokenAuthenticationDefaults.Scheme;
            }
            catch
            {
                // Not a parseable JWT — let the user scheme reject it.
            }
            return JwtBearerDefaults.AuthenticationScheme;
        };
    });

if (!string.IsNullOrWhiteSpace(signingKey))
{
    authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidAudience            = jwtSection["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            RoleClaimType            = "role",
            ClockSkew                = TimeSpan.Zero
        };
    });
}
else
{
    // Allow boot in environments without a configured signing key. Protected
    // endpoints will reject requests because no token will validate.
    authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });
}

// LS-FLOW-MERGE-P5 — register the service-token scheme.
// LS-FLOW-HARDEN-A1 — outside Development, fail fast on startup if the
// signing secret is missing/short. Development hosts still self-disable
// (no token can validate) so local dev keeps working out of the box.
authBuilder.AddServiceTokenBearer(
    builder.Configuration,
    failFastIfMissingSecret: !builder.Environment.IsDevelopment());

// LS-FLOW-E13.1 — register the service-token ISSUER too. Flow needs to
// mint M2M tokens when it calls the audit service from background work
// (e.g. the outbox processor) where no caller bearer exists. When the
// signing secret is unconfigured the issuer self-disables and the audit
// adapters fall back to anonymous calls, which keeps local dev working
// against an audit service running in QueryAuth:Mode=None.
builder.Services.AddServiceTokenIssuer(builder.Configuration, "flow");

// LS-FLOW-HARDEN-A1 — caller-context accessor used by the atomic
// ownership controller to distinguish user vs service callers.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<
    BuildingBlocks.Authentication.ServiceTokens.ICallerContextAccessor,
    BuildingBlocks.Authentication.ServiceTokens.CallerContextAccessor>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));

    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin));

    // LS-FLOW-MERGE-P3 — product-capability policies for the product-facing
    // workflow endpoints. Each policy requires an authenticated user PLUS
    // either the corresponding permission claim OR product-role access on
    // the matching ProductCode. The "no permissions at all" fallback is
    // restricted to the Development environment so production tokens that
    // legitimately lack a capability claim cannot pass — see
    // merge-phase-3-notes.md.
    var allowMissingPermissions = builder.Environment.IsDevelopment();

    options.AddPolicy(Policies.CanSellLien, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  ctx.User.HasPermission(PermissionCodes.LienSell) ||
                  ctx.User.HasProductAccess(ProductCodes.SynqLiens) ||
                  (allowMissingPermissions && !ctx.User.GetPermissions().Any())));

    options.AddPolicy(Policies.CanReferCareConnect, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  ctx.User.HasPermission(PermissionCodes.ReferralCreate) ||
                  ctx.User.HasProductAccess(ProductCodes.SynqCareConnect) ||
                  (allowMissingPermissions && !ctx.User.GetPermissions().Any())));

    options.AddPolicy(Policies.CanReferFund, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  ctx.User.HasPermission(PermissionCodes.ApplicationRefer) ||
                  ctx.User.HasProductAccess(ProductCodes.SynqFund) ||
                  (allowMissingPermissions && !ctx.User.GetPermissions().Any())));
});

// ---------------------------------------------------------------------------
// Request context, tenant provider, infrastructure, application
// ---------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();
// LS-FLOW-MERGE-P3 — adapt platform request context into Flow.Domain abstraction.
builder.Services.AddScoped<Flow.Domain.Interfaces.IFlowUserContext, Flow.Api.Services.FlowUserContext>();

builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTenantProvider<ClaimsTenantProvider>();
builder.Services.AddApplicationServices();
// LS-FLOW-E18 — work distribution intelligence configuration.
builder.Services.Configure<Flow.Application.Options.WorkDistributionOptions>(
    builder.Configuration.GetSection(Flow.Application.Options.WorkDistributionOptions.SectionKey));

// ---------------------------------------------------------------------------
// Platform integration adapters (audit + notifications) + internal events
// ---------------------------------------------------------------------------
builder.Services.AddFlowPlatformAdapters(builder.Configuration);

// ---------------------------------------------------------------------------
// CORS — environment-driven origins. Local dev defaults preserved in
// appsettings.json; higher environments must supply explicit origins.
// ---------------------------------------------------------------------------
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FlowCors", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            // No origins configured → no cross-origin access. Same-origin
            // (e.g., gateway-fronted) requests are unaffected.
            policy.DisallowCredentials();
        }
    });
});

var app = builder.Build();

// ── Auto-migrate ──────────────────────────────────────────────────────────
// Apply pending EF Core migrations on startup in all environments.
// __EFMigrationsHistory tracks which migrations have already been applied,
// so this is safe and idempotent across restarts and re-deploys.
// Runs before the coverage probe so the probe validates the final schema.
try
{
    using var migrateScope = app.Services.CreateScope();
    var db = migrateScope.ServiceProvider.GetRequiredService<Flow.Infrastructure.Persistence.FlowDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Flow database migrations applied successfully.");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not apply Flow database migrations on startup — schema may be out of sync.");
}

// ── Migration coverage self-test ─────────────────────────────────────────
// Compares every EF-mapped column against the live schema and logs an ERROR
// if any are missing. Guards against the regression behind Task #58 —
// a migration committed without its [Migration] attribute (or otherwise
// un-applied) leaves the EF model and the live schema out of sync, which
// previously surfaced only as runtime "Unknown column" SQL errors.
try
{
    using var probeScope = app.Services.CreateScope();
    var db = probeScope.ServiceProvider.GetRequiredService<Flow.Infrastructure.Persistence.FlowDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
}

// ---------------------------------------------------------------------------
// Pipeline ordering:
//   exception → routing → CORS → auth → authorization → tenant validation → endpoints
// ---------------------------------------------------------------------------
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRouting();
app.UseCors("FlowCors");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantValidationMiddleware>();

app.MapControllers();
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/health"); // LS-FLOW-MERGE-P4: alias for unified product smoke checks

app.Run();

// LS-FLOW-HARDEN-A1.1 — top-level Program needs an explicit partial class so
// WebApplicationFactory<Program> in Flow.IntegrationTests can reach it.
public partial class Program { }
