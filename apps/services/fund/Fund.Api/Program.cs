using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authorization;
using BuildingBlocks.FlowClient;
using Contracts;
using Fund.Api.Endpoints;
using Fund.Api.Middleware;
using Fund.Infrastructure;
using Fund.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "fund";
const string Version = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
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

    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin));

    // SynqFund product-role gates — LS-COR-AUT-006A: check product_roles claims
    // in unified PRODUCT:Role format. Endpoints should prefer RequireProductAccess/
    // RequireProductRole filters for consistency; these policies exist as fallback.
    options.AddPolicy(Policies.CanReferFund, policy =>
        policy.RequireClaim("product_roles",
            $"{ProductCodes.SynqFund}:{ProductRoleCodes.SynqFundReferrer}"));

    options.AddPolicy(Policies.CanFundApplications, policy =>
        policy.RequireClaim("product_roles",
            $"{ProductCodes.SynqFund}:{ProductRoleCodes.SynqFundFunder}"));
});

builder.Services.AddInfrastructure(builder.Configuration);
// LS-FLOW-MERGE-P4 — shared Flow HTTP adapter (bearer pass-through, retry, 503 mapping).
builder.Services.AddFlowClient(builder.Configuration, serviceName: "synqfund");

var app = builder.Build();

var env = app.Environment.EnvironmentName;
app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

// Auto-migrate — apply pending EF Core migrations in all environments (not just Development).
// __EFMigrationsHistory tracks applied migrations; idempotent across restarts and re-deploys.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FundDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Fund database migrations applied successfully.");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not apply Fund database migrations on startup — schema may be out of sync.");
}

// ── Migration coverage self-test ─────────────────────────────────────────
// Compares every EF-mapped column against the live schema and logs an ERROR
// if any are missing. Catches the class of bug behind Task #58: a migration
// committed without its [Migration] attribute (or otherwise un-applied)
// leaves the EF model and the live schema out of sync, which previously
// surfaced only as runtime "Unknown column" SQL errors.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FundDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
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

app.MapApplicationEndpoints();
// LS-FLOW-MERGE-P4 — product → Flow integration endpoints.
app.MapWorkflowEndpoints();

app.Run();
