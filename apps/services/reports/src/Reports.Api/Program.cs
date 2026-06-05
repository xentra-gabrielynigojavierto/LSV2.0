using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Reports.Api.Configuration;
using Reports.Api.Endpoints;
using Reports.Api.Middleware;
using Reports.Application;
using Reports.Contracts.Configuration;
using Reports.Infrastructure;
using Reports.Worker.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ReportsServiceSettings>(
    builder.Configuration.GetSection(ReportsServiceSettings.SectionName));
builder.Services.Configure<MySqlSettings>(
    builder.Configuration.GetSection(MySqlSettings.SectionName));
builder.Services.Configure<AdapterSettings>(
    builder.Configuration.GetSection(AdapterSettings.SectionName));
builder.Services.Configure<AuditServiceSettings>(
    builder.Configuration.GetSection(AuditServiceSettings.SectionName));
builder.Services.Configure<EmailDeliverySettings>(
    builder.Configuration.GetSection(EmailDeliverySettings.SectionName));
builder.Services.Configure<SftpDeliverySettings>(
    builder.Configuration.GetSection(SftpDeliverySettings.SectionName));
builder.Services.Configure<StorageSettings>(
    builder.Configuration.GetSection(StorageSettings.SectionName));
builder.Services.Configure<LiensDataSettings>(
    builder.Configuration.GetSection(LiensDataSettings.SectionName));

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"] ?? string.Empty;

if (!string.IsNullOrWhiteSpace(signingKey))
{
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
}
else
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));

    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

builder.Services.AddReportsApplication();
builder.Services.AddReportsInfrastructure(builder.Configuration);

builder.Services.AddHostedService<ReportWorkerService>();
builder.Services.AddHostedService<ScheduleWorkerService>();

var app = builder.Build();

// ── Auto-migrate ──────────────────────────────────────────────────────────
// Apply pending EF Core migrations in all environments.
// __EFMigrationsHistory tracks applied migrations; idempotent across restarts.
try
{
    using var migrateScope = app.Services.CreateScope();
    var db = migrateScope.ServiceProvider
        .GetRequiredService<Reports.Infrastructure.Persistence.ReportsDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Reports database migrations applied successfully.");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not apply Reports database migrations on startup — schema may be out of sync.");
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
    var db = probeScope.ServiceProvider
        .GetRequiredService<Reports.Infrastructure.Persistence.ReportsDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantValidationMiddleware>();

app.MapHealthEndpoints();
app.MapTemplateEndpoints();
app.MapAssignmentEndpoints();
app.MapOverrideEndpoints();
app.MapExecutionEndpoints();
app.MapExportEndpoints();
app.MapScheduleEndpoints();
app.MapViewEndpoints();
app.MapMetricsEndpoints();

app.MapGet("/health", () => Results.Redirect("/api/v1/health", permanent: true))
    .ExcludeFromDescription();
app.MapGet("/ready", () => Results.Redirect("/api/v1/ready", permanent: true))
    .ExcludeFromDescription();

app.Run();
