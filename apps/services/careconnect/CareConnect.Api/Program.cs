using System.Security.Claims;
using System.Threading.RateLimiting;
using BuildingBlocks;
using BuildingBlocks.Authorization;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Context;
using BuildingBlocks.FlowClient;
using CareConnect.Api.Endpoints;
using CareConnect.Api.Middleware;
using CareConnect.Api.Options;
using CareConnect.Infrastructure;
using CareConnect.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            RoleClaimType            = "role"
        };
    })
    // M2M service-token bearer — validates HS256 tokens minted by platform services.
    // Secret is read from FLOW_SERVICE_TOKEN_SECRET env var (see ServiceTokenAuthenticationDefaults).
    .AddServiceTokenBearer(builder.Configuration);

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin));

    // Internal M2M endpoints — only accept service tokens (not user JWTs).
    options.AddPolicy("ServiceOnly", policy =>
        policy
            .AddAuthenticationSchemes(ServiceTokenAuthenticationDefaults.Scheme)
            .RequireRole(ServiceTokenAuthenticationDefaults.ServiceRole));
});

// Infrastructure (DbContext + repositories + services)
builder.Services.AddInfrastructure(builder.Configuration);
// LS-FLOW-MERGE-P4 — shared Flow HTTP adapter (bearer pass-through, retry, 503 mapping).
builder.Services.AddFlowClient(builder.Configuration, serviceName: "careconnect");

// Upload validation limits — bound from "AttachmentUpload" section of appsettings.json.
builder.Services.Configure<AttachmentUploadOptions>(
    builder.Configuration.GetSection(AttachmentUploadOptions.SectionName));

// Set Kestrel's request body size limit and ASP.NET's multipart body length limit
// well above the configured upload ceiling so that oversized-but-realistic uploads
// always reach our handler and receive a custom 400 error, rather than being cut
// off by the framework with a bare 413/400.  The application-level check in
// AttachmentEndpoints is the authoritative gate.
// A hard backstop of 512 MB still protects against truly absurd payloads.
{
    var uploadSection = builder.Configuration.GetSection(AttachmentUploadOptions.SectionName);
    var configuredMax = uploadSection.GetValue<long?>("MaxFileSizeBytes")
                        ?? new AttachmentUploadOptions().MaxFileSizeBytes;
    const long backstopBytes = 512L * 1024 * 1024;
    var effectiveLimit = Math.Max(configuredMax * 10, backstopBytes);

    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        kestrel.Limits.MaxRequestBodySize = effectiveLimit;
    });

    // ASP.NET Core's multipart parser enforces its own separate length limit
    // (default ~128 MB). Align it with the same backstop so it doesn't reject
    // uploads before endpoint code can return a meaningful error.
    builder.Services.Configure<FormOptions>(form =>
    {
        form.MultipartBodyLengthLimit = effectiveLimit;
    });
}

// Request context
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

// CC2-INT-B08: Rate limiting for the public referral endpoint.
// Fixed window: 10 submissions per minute per IP address.
// Rejected requests receive 429 Too Many Requests.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("public-referral-limit", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 10,
                Window               = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── BLK-OPS-01: Production fail-fast (supersedes BLK-SEC-01 inline checks) ────
// Validates all required secrets and service URLs before any requests are accepted.
// Uses RuntimeConfigValidator for consistent error messages and placeholder detection.
if (!builder.Environment.IsDevelopment())
{
    var v = new RuntimeConfigValidator(builder.Configuration, "careconnect");
    v
        // JWT signing key must be real — not a placeholder
        .RequireNotPlaceholder("Jwt:SigningKey")
        // Trust boundary secret — must match Gateway and Web BFF
        .RequireNonEmpty("PublicTrustBoundary:InternalRequestSecret")
        .RequireNotPlaceholder("PublicTrustBoundary:InternalRequestSecret")
        // Service URLs — must be absolute URLs in production
        .RequireAbsoluteUrl("TenantService:BaseUrl")
        .RequireAbsoluteUrl("IdentityService:BaseUrl")
        // Provisioning tokens — required for CareConnect → Tenant and Identity calls
        .RequireNonEmpty("TenantService:ProvisioningToken")
        .RequireNonEmpty("IdentityService:ProvisioningToken")
        // Database connection string — must not contain placeholder password
        .RequireConnectionString("ConnectionStrings:CareConnectDb");
}

var app = builder.Build();

// Auto-migrate — apply pending EF Core migrations on startup in all environments.
// CareConnect uses MySQL (RDS) and the __EFMigrationsHistory table tracks which
// migrations have already been applied, so this is safe and idempotent.
// Fail fast if migrations cannot be applied — serving traffic with an incompatible
// schema causes silent 500s and process crashes that are harder to diagnose.
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CareConnectDbContext>();

    // ── Schema divergence repair (CC2-INT-B07) ────────────────────────────
    // Guards against a known RDS state where __EFMigrationsHistory records
    // migrations as applied but the actual DDL was never executed.
    // Uses idempotent DDL (CREATE TABLE IF NOT EXISTS / ADD COLUMN IF NOT EXISTS)
    // to guarantee the B06+ schema objects exist before handing off to EF.
    // Wrapped in try/catch so a transient DB error during repair does not
    // prevent EF Core Migrate() from running (schema repair is advisory).
    try
    {
        await EnsureSchemaObjectsAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "EnsureSchemaObjects schema repair failed — proceeding with EF Core Migrate()");
    }

    db.Database.Migrate();
    app.Logger.LogInformation("CareConnect database migrations applied successfully.");
}

// ── Migration coverage self-test ─────────────────────────────────────────
// Compares every EF-mapped column against the live schema and logs an ERROR
// if any are missing. Guards against the regression behind Task #58 —
// a migration committed without its [Migration] attribute (or otherwise
// un-applied) leaves the EF model and the live schema out of sync, which
// previously surfaced only as runtime "Unknown column" SQL errors.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CareConnectDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
}

// ── Phase H startup diagnostic: provider/facility Identity linkage health ─────
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CareConnectDbContext>();

    var totalProviders           = await db.Providers.CountAsync(p => p.IsActive);
    var providersWithoutOrgLink  = await db.Providers.CountAsync(p => p.IsActive && p.OrganizationId == null);
    var totalFacilities          = await db.Facilities.CountAsync(f => f.IsActive);
    var facilitiesWithoutOrgLink = await db.Facilities.CountAsync(f => f.IsActive && f.OrganizationId == null);

    if (providersWithoutOrgLink > 0)
        app.Logger.LogWarning(
            "Linkage health: {Count}/{Total} active Provider(s) have no Identity Organization link (OrganizationId is null). " +
            "These providers cannot participate in cross-service org-scoped authorization.",
            providersWithoutOrgLink, totalProviders);
    else
        app.Logger.LogInformation(
            "Linkage health: all {Total} active Provider(s) have an Identity Organization link.",
            totalProviders);

    if (facilitiesWithoutOrgLink > 0)
        app.Logger.LogWarning(
            "Linkage health: {Count}/{Total} active Facility(ies) have no Identity Organization link (OrganizationId is null). " +
            "These facilities cannot participate in cross-service org-scoped authorization.",
            facilitiesWithoutOrgLink, totalFacilities);
    else
        app.Logger.LogInformation(
            "Linkage health: all {Total} active Facility(ies) have an Identity Organization link.",
            totalFacilities);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex,
        "CareConnect Phase H startup diagnostic skipped — could not query the database at startup.");
}

app.UseMiddleware<CorrelationIdMiddleware>();    // BLK-OBS-01: assign X-Correlation-Id first
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter(); // CC2-INT-B08: rate limit public referral endpoint

// Health & info
app.MapGet("/health", async (CareConnectDbContext db, CancellationToken ct) =>
{
    try
    {
        // Lightweight probe: executes "SELECT 1" to confirm DB connectivity.
        var canConnect = await db.Database.CanConnectAsync(ct);
        var dbStatus   = canConnect ? "connected" : "unreachable";
        return canConnect
            ? Results.Ok(new { status = "healthy", db = dbStatus })
            : Results.Json(new { status = "degraded", db = dbStatus },
                statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception)
    {
        return Results.Json(new { status = "degraded", db = "error" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();
app.MapGet("/info",   () => Results.Ok(new { service = "CareConnect", version = "1.0.0" })).AllowAnonymous();

// Internal service-to-service endpoints
app.MapInternalProvisionEndpoints();

// API endpoints
app.MapCareConnectIntegrityEndpoints();
app.MapProviderAdminEndpoints();
app.MapAdminDashboardEndpoints();   // LSCC-01-004: admin dashboard, blocked queue, referral monitor
app.MapPerformanceEndpoints();      // LSCC-01-005: referral performance metrics
app.MapAdminBackfillEndpoints();
app.MapActivationAdminEndpoints(); // LSCC-009
app.MapAnalyticsEndpoints();      // LSCC-011
// LS-FLOW-MERGE-P4 — product → Flow integration endpoints.
app.MapWorkflowEndpoints();
app.MapProviderEndpoints();
app.MapReferralEndpoints();
app.MapCategoryEndpoints();
app.MapFacilityEndpoints();
app.MapServiceOfferingEndpoints();
app.MapAvailabilityTemplateEndpoints();
app.MapSlotEndpoints();
app.MapAppointmentEndpoints();
app.MapAvailabilityExceptionEndpoints();
app.MapReferralNoteEndpoints();
app.MapAppointmentNoteEndpoints();
app.MapAttachmentEndpoints();
app.MapNotificationEndpoints();
app.MapNetworkEndpoints();             // CC2-INT-B06: provider network management
app.MapPublicNetworkEndpoints();       // CC2-INT-B07: public network surface (anonymous)
app.MapEnrollmentEndpoints();          // CC2-ENROLL: provider self-enrollment (anonymous)
app.MapReferralThreadEndpoints();      // Public referral comment thread (token-authenticated)
app.MapProviderOnboardingEndpoints();  // CC2-INT-B09: provider tenant self-onboarding

app.Run();

// ── Schema repair helper (CC2-INT-B07) ───────────────────────────────────────
// Applies idempotent DDL to guarantee B06+ schema objects exist regardless of the
// __EFMigrationsHistory state. MySQL DDL is non-transactional, so a partially-applied
// migration can leave history rows written but tables absent.
// Notes:
//   - CREATE TABLE IF NOT EXISTS is safe cross-version.
//   - ADD COLUMN IF NOT EXISTS requires MySQL ≥ 8.0.29 and is NOT available on RDS;
//     we therefore check information_schema first, then ADD COLUMN (no IF NOT EXISTS).
//   - FK constraints are omitted from manual DDL to avoid charset/collation mismatches;
//     EF enforces referential integrity at the application layer.
static async Task EnsureSchemaObjectsAsync(
    CareConnect.Infrastructure.Data.CareConnectDbContext db,
    ILogger logger)
{
    var conn = db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open)
        await conn.OpenAsync();

    // Resolve the actual database name from the open connection
    string dbName;
    using (var dbCmd = conn.CreateCommand())
    {
        dbCmd.CommandText = "SELECT DATABASE()";
        dbName = (string)(await dbCmd.ExecuteScalarAsync() ?? "careconnect_db");
    }

    // Helper: returns true if the table exists in the live schema
    async Task<bool> TableExists(string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT COUNT(*) FROM information_schema.tables " +
            $"WHERE table_schema='{dbName}' AND table_name='{table}'";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    // Helper: returns true if the column exists on the given table
    async Task<bool> ColumnExists(string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT COUNT(*) FROM information_schema.columns " +
            $"WHERE table_schema='{dbName}' AND table_name='{table}' AND column_name='{column}'";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    // Helper: execute a DDL statement, log any errors but continue
    async Task<bool> Exec(string sql, string label)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("EnsureSchemaObjects: {Label} — applied.", label);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EnsureSchemaObjects: {Label} — DDL failed.", label);
            return false;
        }
    }

    int applied = 0;

    // ── 20260422000000_AddProviderReassignmentLog ───────────────────────────
    if (!await TableExists("cc_ReferralProviderReassignments"))
        if (await Exec("""
            CREATE TABLE `cc_ReferralProviderReassignments` (
                `Id`                char(36)    NOT NULL,
                `ReferralId`        char(36)    NOT NULL,
                `TenantId`          char(36)    NOT NULL,
                `PreviousProviderId` char(36)   NULL,
                `NewProviderId`     char(36)    NOT NULL,
                `ReassignedByUserId` char(36)  NULL,
                `ReassignedAtUtc`   datetime(6) NOT NULL,
                PRIMARY KEY (`Id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
            """, "cc_ReferralProviderReassignments")) applied++;

    // ── 20260422100000_AddProviderNetworks ──────────────────────────────────
    if (!await TableExists("cc_ProviderNetworks"))
        if (await Exec("""
            CREATE TABLE `cc_ProviderNetworks` (
                `Id`              char(36)      NOT NULL,
                `TenantId`        char(36)      NOT NULL,
                `Name`            varchar(200)  NOT NULL,
                `Description`     varchar(1000) NOT NULL DEFAULT '',
                `IsDeleted`       tinyint(1)    NOT NULL DEFAULT 0,
                `CreatedAtUtc`    datetime(6)   NOT NULL,
                `UpdatedAtUtc`    datetime(6)   NOT NULL,
                `CreatedByUserId` varchar(255)  NULL,
                `UpdatedByUserId` varchar(255)  NULL,
                PRIMARY KEY (`Id`),
                KEY `IX_cc_ProviderNetworks_TenantId_Name` (`TenantId`, `Name`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
            """, "cc_ProviderNetworks")) applied++;

    if (!await TableExists("cc_NetworkProviders"))
        // FK constraints are omitted — column types/collations vary by RDS instance;
        // EF enforces referential integrity at the application layer.
        if (await Exec("""
            CREATE TABLE `cc_NetworkProviders` (
                `Id`                char(36)     NOT NULL,
                `TenantId`          char(36)     NOT NULL,
                `ProviderNetworkId` char(36)     NOT NULL,
                `ProviderId`        char(36)     NOT NULL,
                `CreatedAtUtc`      datetime(6)  NOT NULL,
                `UpdatedAtUtc`      datetime(6)  NOT NULL,
                `CreatedByUserId`   varchar(255) NULL,
                `UpdatedByUserId`   varchar(255) NULL,
                PRIMARY KEY (`Id`),
                UNIQUE KEY `IX_cc_NetworkProviders_ProviderNetworkId_ProviderId` (`ProviderNetworkId`, `ProviderId`),
                KEY `IX_cc_NetworkProviders_TenantId` (`TenantId`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
            """, "cc_NetworkProviders")) applied++;

    // ── 20260422120000_AddProviderNpi ───────────────────────────────────────
    // ADD COLUMN IF NOT EXISTS is only available on MySQL ≥ 8.0.29; check first.
    if (!await ColumnExists("cc_Providers", "Npi"))
        if (await Exec("ALTER TABLE `cc_Providers` ADD COLUMN `Npi` varchar(20) NULL",
            "cc_Providers.Npi")) applied++;

    // ── 20260422130000_AddProviderAccessStage ───────────────────────────────
    if (!await ColumnExists("cc_Providers", "AccessStage"))
        if (await Exec("ALTER TABLE `cc_Providers` ADD COLUMN `AccessStage` varchar(20) NOT NULL DEFAULT 'URL'",
            "cc_Providers.AccessStage")) applied++;

    if (!await ColumnExists("cc_Providers", "IdentityUserId"))
        if (await Exec("ALTER TABLE `cc_Providers` ADD COLUMN `IdentityUserId` char(36) NULL",
            "cc_Providers.IdentityUserId")) applied++;

    if (!await ColumnExists("cc_Providers", "CommonPortalActivatedAtUtc"))
        if (await Exec("ALTER TABLE `cc_Providers` ADD COLUMN `CommonPortalActivatedAtUtc` datetime(6) NULL",
            "cc_Providers.CommonPortalActivatedAtUtc")) applied++;

    if (!await ColumnExists("cc_Providers", "TenantProvisionedAtUtc"))
        if (await Exec("ALTER TABLE `cc_Providers` ADD COLUMN `TenantProvisionedAtUtc` datetime(6) NULL",
            "cc_Providers.TenantProvisionedAtUtc")) applied++;

    // ── 20260429120000_AddReferralComments ──────────────────────────────────
    if (!await TableExists("cc_ReferralComments"))
        if (await Exec("""
            CREATE TABLE `cc_ReferralComments` (
                `Id`         char(36)      NOT NULL,
                `TenantId`   char(36)      NOT NULL,
                `ReferralId` char(36)      NOT NULL,
                `SenderType` varchar(20)   NOT NULL,
                `SenderName` varchar(200)  NOT NULL,
                `Message`    varchar(4000) NOT NULL,
                `CreatedAt`  datetime(6)   NOT NULL,
                PRIMARY KEY (`Id`),
                KEY `IX_ReferralComments_TenantId_ReferralId_CreatedAt` (`TenantId`, `ReferralId`, `CreatedAt`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
            """, "cc_ReferralComments")) applied++;

    // ── 20260429130000_AddTreatmentTypes ────────────────────────────────────
    if (!await TableExists("cc_TreatmentTypes"))
    {
        if (await Exec("""
            CREATE TABLE `cc_TreatmentTypes` (
                `Id`           char(36)     NOT NULL,
                `Name`         varchar(150) NOT NULL,
                `Category`     varchar(100) NULL,
                `DisplayOrder` int          NOT NULL DEFAULT 0,
                `IsActive`     tinyint(1)   NOT NULL DEFAULT 1,
                PRIMARY KEY (`Id`),
                KEY `IX_cc_TreatmentTypes_Category_DisplayOrder` (`Category`, `DisplayOrder`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
            """, "cc_TreatmentTypes")) applied++;

        // Seed default treatment types (deterministic GUIDs — idempotent on re-run via INSERT IGNORE)
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT IGNORE INTO `cc_TreatmentTypes` (`Id`, `Name`, `Category`, `DisplayOrder`, `IsActive`) VALUES
                ('a1000001-0000-0000-0000-000000000001', 'Chiropractic Care',        'Musculoskeletal',    10, 1),
                ('a1000002-0000-0000-0000-000000000001', 'Physical Therapy',         'Rehabilitation',     20, 1),
                ('a1000003-0000-0000-0000-000000000001', 'Occupational Therapy',     'Rehabilitation',     30, 1),
                ('a1000004-0000-0000-0000-000000000001', 'Orthopedic Evaluation',    'Musculoskeletal',    40, 1),
                ('a1000005-0000-0000-0000-000000000001', 'Neurology Evaluation',     'Neurological',       50, 1),
                ('a1000006-0000-0000-0000-000000000001', 'Pain Management',          'Pain',               60, 1),
                ('a1000007-0000-0000-0000-000000000001', 'MRI / Radiology',          'Diagnostic',         70, 1),
                ('a1000008-0000-0000-0000-000000000001', 'X-Ray',                    'Diagnostic',         80, 1),
                ('a1000009-0000-0000-0000-000000000001', 'Acupuncture',              'Alternative',        90, 1),
                ('a1000010-0000-0000-0000-000000000001', 'Psychological Evaluation', 'Mental Health',     100, 1),
                ('a1000011-0000-0000-0000-000000000001', 'Toxicology',               'Diagnostic',        110, 1),
                ('a1000012-0000-0000-0000-000000000001', 'Internal Medicine',        'General',           120, 1),
                ('a1000013-0000-0000-0000-000000000001', 'Podiatry',                 'Musculoskeletal',   130, 1),
                ('a1000014-0000-0000-0000-000000000001', 'Ophthalmology',            'Specialized',       140, 1),
                ('a1000015-0000-0000-0000-000000000001', 'Cardiology',               'Specialized',       150, 1),
                ('a1000016-0000-0000-0000-000000000001', 'Dermatology',              'Specialized',       160, 1),
                ('a1000017-0000-0000-0000-000000000001', 'General Referral',         'General',           999, 1)
                """;
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("EnsureSchemaObjects: cc_TreatmentTypes — seed data inserted.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EnsureSchemaObjects: cc_TreatmentTypes seed — failed (non-fatal).");
        }
    }

    logger.LogInformation("EnsureSchemaObjects: {Count} DDL change(s) applied.", applied);

    // Close the connection so that EF Core's Migrate() can manage its own
    // connection lifecycle cleanly (Pomelo may behave unexpectedly when
    // Migrate() is invoked on a DbContext whose connection is already open).
    if (conn.State == System.Data.ConnectionState.Open)
        await conn.CloseAsync();
}
