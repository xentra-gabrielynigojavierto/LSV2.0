using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Contracts;
using Comms.Api.Endpoints;
using Comms.Api.Middleware;
using Comms.Infrastructure;
using Comms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "comms";
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
});

builder.Services.AddCommsServices(builder.Configuration);

var app = builder.Build();

var env = app.Environment.EnvironmentName;
app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommsDbContext>();
        db.Database.Migrate();
        app.Logger.LogInformation("Database migrations applied");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not apply migrations on startup");
    }
}

// ── Migration coverage self-test ─────────────────────────────────────────
// Compares every EF-mapped column against information_schema. If a model
// property has no backing column on the live database, log an ERROR so the
// regression is loud at boot. Catches the class of bug behind Task #58:
// a migration committed without its [Migration] attribute (or otherwise
// un-applied) leaves the EF model and the live schema out of sync, which
// previously surfaced only as runtime "Unknown column" SQL errors.

// Auto-migrate — apply pending EF Core migrations in all environments.
// __EFMigrationsHistory tracks applied migrations; idempotent across restarts.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CommsDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Comms database migrations applied successfully.");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not apply Comms database migrations on startup — schema may be out of sync.");
}

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CommsDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<InternalServiceTokenMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceName)))
    .AllowAnonymous();

app.MapGet("/info", () =>
    Results.Ok(new InfoResponse(ServiceName, env, Version)))
    .AllowAnonymous();

app.MapGet("/context", (ICurrentRequestContext ctx) =>
{
    if (!ctx.IsAuthenticated)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        authenticated = true,
        userId        = ctx.UserId,
        tenantId      = ctx.TenantId,
        tenantCode    = ctx.TenantCode,
        email         = ctx.Email,
        orgId         = ctx.OrgId,
        orgType       = ctx.OrgType,
        orgTypeId     = ctx.OrgTypeId,
        roles         = ctx.Roles,
        productRoles  = ctx.ProductRoles,
        permissions   = ctx.Permissions,
        isPlatformAdmin = ctx.IsPlatformAdmin,
    });
})
.RequireAuthorization(Policies.AuthenticatedUser);

app.MapConversationEndpoints();
app.MapMessageEndpoints();
app.MapParticipantEndpoints();
app.MapAttachmentEndpoints();
app.MapEmailIntakeEndpoints();
app.MapOutboundEmailEndpoints();
app.MapSenderConfigEndpoints();
app.MapEmailTemplateEndpoints();
app.MapQueueEndpoints();
app.MapOperationalEndpoints();
app.MapSlaTriggersEndpoints();
app.MapTimelineEndpoints();

app.Run();
