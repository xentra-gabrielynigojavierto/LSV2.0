using System.Text;
using BuildingBlocks;
using BuildingBlocks.Authorization;
using Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Tenant.Application.Configuration;
using Tenant.Api.Endpoints;
using Tenant.Api.Middleware;
using Tenant.Infrastructure;
using Tenant.Infrastructure.Data;

const string ServiceName = "tenant";
const string Version = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

var jwtSection   = builder.Configuration.GetSection("Jwt");
var signingKey   = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));
});

// ── Feature flags ─────────────────────────────────────────────────────────────
builder.Services.Configure<TenantFeatures>(
    builder.Configuration.GetSection(TenantFeatures.SectionName));

builder.Services.AddInfrastructure(builder.Configuration);

// ── BLK-OPS-01: Production fail-fast (supersedes BLK-SEC-01 inline checks) ────
if (!builder.Environment.IsDevelopment())
{
    var v = new RuntimeConfigValidator(builder.Configuration, "tenant");
    v
        // JWT signing key must be real — not a placeholder
        .RequireNotPlaceholder("Jwt:SigningKey")
        // Provisioning secret gates all internal provisioning endpoints
        .RequireNonEmpty("TenantService:ProvisioningSecret")
        // Database connection string
        .RequireConnectionString("ConnectionStrings:TenantDb");
}

var app = builder.Build();

var env = app.Environment.EnvironmentName;
app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

// Auto-migrate — apply pending EF Core migrations on startup (idempotent).
// A failure here is fatal: starting with an out-of-sync schema causes every
// EF query to throw, so we prefer a clean crash over a silently broken service.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Tenant database migrations applied successfully.");
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Tenant database migration failed — service cannot start with an out-of-sync schema.");
    throw;
}

// Migration coverage self-test — detects EF model / live schema drift at startup.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run.");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceName)))
    .AllowAnonymous();

app.MapGet("/info", () =>
    Results.Ok(new InfoResponse(ServiceName, env, Version)))
    .AllowAnonymous();

app.MapTenantEndpoints();
app.MapProvisionEndpoints();
app.MapBrandingEndpoints();
app.MapDomainEndpoints();
app.MapResolutionEndpoints();
app.MapEntitlementEndpoints();
app.MapCapabilityEndpoints();
app.MapSettingEndpoints();
app.MapMigrationEndpoints();
app.MapReadSourceEndpoints();
app.MapSyncEndpoints();
app.MapRuntimeMetricsEndpoints();
app.MapLogoAdminEndpoints();
app.MapTenantAdminEndpoints();
app.MapActivationEndpoints();     // BLK-TS-02

app.Run();
