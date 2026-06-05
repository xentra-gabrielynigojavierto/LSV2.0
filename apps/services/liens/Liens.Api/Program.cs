using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using BuildingBlocks.FlowClient;
using Contracts;
using Liens.Api.Endpoints;
using Liens.Api.Middleware;
using Liens.Domain;
using Liens.Infrastructure;
using Liens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "liens";
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

builder.Services.AddLiensServices(builder.Configuration);
// LS-FLOW-MERGE-P4 — shared Flow HTTP adapter (bearer pass-through, retry, 503 mapping).
builder.Services.AddFlowClient(builder.Configuration, serviceName: "synqlien");

var app = builder.Build();

var env = app.Environment.EnvironmentName;
app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

// Auto-migrate — apply pending EF Core migrations in all environments (not just Development).
// __EFMigrationsHistory tracks applied migrations; idempotent across restarts and re-deploys.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Liens database migrations applied successfully.");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not apply Liens database migrations on startup — schema may be out of sync.");
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
    var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
}

// ── Schema safety-net ─────────────────────────────────────────────────────
// Creates any tables that were pre-seeded in __EFMigrationsHistory without
// their DDL actually running in production (same pattern as the notifications
// fix). Idempotent: uses CREATE TABLE IF NOT EXISTS so safe on every restart.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
    await EnsureLiensSchemaTablesAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "EnsureLiensSchemaTablesAsync could not run — schema may be missing tables.");
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
        capabilities = new
        {
            sell = ctx.CanSellLiens(),
            manageInternal = ctx.CanManageLiensInternal(),
            resolved = ctx.GetLiensCapabilities(),
        },
    });
})
.RequireAuthorization(Policies.AuthenticatedUser);

app.MapLienEndpoints();
app.MapLienOfferEndpoints();
app.MapBillOfSaleEndpoints();
app.MapCaseEndpoints();
// LS-LIENS-CASE-005 — Case Notes Backend & Persistence.
app.MapCaseNoteEndpoints();
app.MapServicingEndpoints();
app.MapContactEndpoints();
// Lookup reference data (states, accident types, contact types, lien statuses, etc.)
app.MapLookupEndpoints();
// LS-FLOW-MERGE-P4 — product → Flow integration endpoints.
app.MapWorkflowEndpoints();
// LS-LIENS-FLOW-001 — My Tasks + Workflow Configuration.
app.MapTaskEndpoints();
app.MapWorkflowConfigEndpoints();
// LS-LIENS-FLOW-002 — Contextual Task Intelligence: Task Templates.
app.MapTaskTemplateEndpoints();
// LS-LIENS-FLOW-003 — Event-Driven Task Generation.
app.MapTaskGenerationRuleEndpoints();
// LS-LIENS-FLOW-004 — Task Notes & Collaboration.
app.MapTaskNoteEndpoints();
// LS-LIENS-FLOW-006 — Task Creation Governance + Email Notifications.
app.MapTaskGovernanceEndpoints();
// LS-LIENS-FLOW-009 — Flow Event Consumption (internal event ingestion).
app.MapFlowEventsEndpoints();
// TASK-B04 — one-shot admin task backfill (internal, shared-secret protected).
app.MapLienTaskBackfillEndpoints();

app.Run();

// ── EnsureLiensSchemaTablesAsync ─────────────────────────────────────────
// Idempotently creates tables that are defined in EF migrations but may not
// have been applied to the live DB (production schema-drift guard).
static async Task EnsureLiensSchemaTablesAsync(LiensDbContext db, ILogger logger)
{
    var conn = db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open)
        await conn.OpenAsync();

    // liens_WorkflowTransitions — added by migration 20260418200000
    const string createWorkflowTransitions = """
        CREATE TABLE IF NOT EXISTS `liens_WorkflowTransitions` (
            `Id`               char(36)    NOT NULL COLLATE ascii_general_ci,
            `WorkflowConfigId` char(36)    NOT NULL COLLATE ascii_general_ci,
            `FromStageId`      char(36)    NOT NULL COLLATE ascii_general_ci,
            `ToStageId`        char(36)    NOT NULL COLLATE ascii_general_ci,
            `IsActive`         tinyint(1)  NOT NULL,
            `SortOrder`        int         NOT NULL,
            `CreatedByUserId`  char(36)    NOT NULL COLLATE ascii_general_ci,
            `UpdatedByUserId`  char(36)    NULL     COLLATE ascii_general_ci,
            `CreatedAtUtc`     datetime(6) NOT NULL,
            `UpdatedAtUtc`     datetime(6) NOT NULL,
            PRIMARY KEY (`Id`),
            CONSTRAINT `FK_lWT_WorkflowConfigId`
                FOREIGN KEY (`WorkflowConfigId`) REFERENCES `liens_WorkflowConfigs` (`Id`) ON DELETE CASCADE,
            CONSTRAINT `FK_lWT_FromStageId`
                FOREIGN KEY (`FromStageId`) REFERENCES `liens_WorkflowStages` (`Id`) ON DELETE RESTRICT,
            CONSTRAINT `FK_lWT_ToStageId`
                FOREIGN KEY (`ToStageId`) REFERENCES `liens_WorkflowStages` (`Id`) ON DELETE RESTRICT
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """;

    // TASK-MIG-09: liens_TaskGovernanceSettings removed — table dropped by migration 20260421000002.
    // TASK-MIG-09: liens_TaskTemplates removed — table dropped by migration 20260421000002.

    using var cmd = conn.CreateCommand();
    cmd.CommandText = createWorkflowTransitions;
    await cmd.ExecuteNonQueryAsync();
    logger.LogInformation("EnsureLiensSchemaTablesAsync: liens_WorkflowTransitions ensured.");

    // LS-LIENS-FLOW-007 — liens_Tasks.WorkflowInstanceId + WorkflowStepKey columns
    // Added by migration 20260420000002_AddTaskFlowLinkage. MySQL does not support
    // ADD COLUMN IF NOT EXISTS, so we guard via information_schema.
    cmd.CommandText = """
        SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME   = 'liens_Tasks'
          AND COLUMN_NAME  = 'WorkflowInstanceId'
        """;
    var hasWorkflowInstanceId = Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;

    if (!hasWorkflowInstanceId)
    {
        cmd.CommandText = """
            ALTER TABLE `liens_Tasks`
                ADD COLUMN `WorkflowInstanceId` char(36)     NULL COLLATE ascii_general_ci,
                ADD COLUMN `WorkflowStepKey`    varchar(200) NULL
            """;
        await cmd.ExecuteNonQueryAsync();
        logger.LogInformation("EnsureLiensSchemaTablesAsync: Added WorkflowInstanceId/WorkflowStepKey to liens_Tasks.");

        try
        {
            cmd.CommandText = "CREATE INDEX `IX_Tasks_TenantId_WorkflowInstanceId` ON `liens_Tasks` (`TenantId`, `WorkflowInstanceId`)";
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("EnsureLiensSchemaTablesAsync: Created IX_Tasks_TenantId_WorkflowInstanceId.");
        }
        catch (Exception idxEx)
        {
            logger.LogWarning(idxEx, "EnsureLiensSchemaTablesAsync: Could not create IX_Tasks_TenantId_WorkflowInstanceId (may already exist).");
        }
    }
    else
    {
        logger.LogInformation("EnsureLiensSchemaTablesAsync: liens_Tasks flow-linkage columns already present.");
    }
}
