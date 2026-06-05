using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using Contracts;
using Task.Api.Endpoints;
using Task.Api.Middleware;
using Task.Infrastructure;
using Task.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "task";
const string Version     = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey  = jwtSection["SigningKey"]
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
            ClockSkew                = TimeSpan.Zero,
        };
    })
    // TASK-B05 (TASK-013) — second scheme for machine-to-machine service tokens.
    // Used exclusively on /api/tasks/internal/* endpoints.
    // Secret sourced from FLOW_SERVICE_TOKEN_SECRET env var (same shared secret
    // used by Flow, Notifications, and all other platform services).
    .AddServiceTokenBearer(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    // Accepts only standard user JWTs.
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser());

    // Accepts either a user JWT (JwtBearer) or an internal service token (ServiceToken).
    // Used on /api/tasks endpoints so platform services (e.g. Liens, Flow) can query
    // and mutate tasks on behalf of their tenants using signed service tokens while
    // portal users continue to use their standard user JWTs.
    options.AddPolicy("AuthenticatedUserOrService", policy =>
        policy
            .AddAuthenticationSchemes(
                JwtBearerDefaults.AuthenticationScheme,
                ServiceTokenAuthenticationDefaults.Scheme)
            .RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));

    // Restricted to real admin roles only; service tokens are not granted
    // admin-tier access because they can carry an arbitrary tenant claim and
    // would otherwise bypass tenant-scoped authorization on admin endpoints.
    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireRole(
                Roles.PlatformAdmin,
                Roles.TenantAdmin));

    // TASK-B05 (TASK-013) — internal service-to-service endpoint gate.
    // Only accepts tokens with scheme=ServiceToken and role=service.
    // Rejects user JWTs, tokens with missing tenant claim, and unsigned tokens.
    options.AddPolicy("InternalService", policy =>
        policy
            .AddAuthenticationSchemes(ServiceTokenAuthenticationDefaults.Scheme)
            .RequireRole(ServiceTokenAuthenticationDefaults.ServiceRole));
});

builder.Services.AddTaskServices(builder.Configuration);

var app = builder.Build();
var env = app.Environment.EnvironmentName;

app.Logger.LogInformation(
    "Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TasksDbContext>();
    await db.Database.MigrateAsync();

    // Self-heal: verify that the core table actually exists.
    // EF can report "up to date" if __EFMigrationsHistory has stale entries
    // but the DDL was never committed (e.g. after a crashed first-run).
    bool coreTableExists = false;
    try
    {
        await db.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM `tasks_Tasks` LIMIT 1");
        coreTableExists = true;
    }
    catch { /* table absent */ }

    if (!coreTableExists)
    {
        app.Logger.LogWarning(
            "tasks_Tasks table missing despite migration history — " +
            "clearing __EFMigrationsHistory and re-applying all migrations.");
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM `__EFMigrationsHistory`");
        await db.Database.MigrateAsync();
        app.Logger.LogInformation(
            "Schema rebuilt from scratch — all migrations re-applied.");
    }
    else
    {
        app.Logger.LogInformation("Task database migrations applied successfully.");
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex,
        "Could not apply Task database migrations on startup — schema may be out of sync.");
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

app.MapTaskEndpoints();
app.MapTaskNoteEndpoints();
app.MapTaskStageEndpoints();
app.MapTaskStageTransitionEndpoints();
app.MapTaskGovernanceEndpoints();
app.MapTaskTemplateEndpoints();
app.MapTaskReminderEndpoints();
app.MapTaskFlowEndpoints();
app.MapTaskAnalyticsEndpoints();
app.MapTaskWorkloadEndpoints();
app.MapTaskLinkedEntityEndpoints();

app.Run();
