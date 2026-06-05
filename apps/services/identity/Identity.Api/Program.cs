using System.Text;
using System.Threading.RateLimiting;
using BuildingBlocks;
using Contracts;
using Identity.Api;
using Identity.Api.Endpoints;
using Identity.Infrastructure;
using Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "identity";
const string Version = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

builder.Services.AddInfrastructure(builder.Configuration);

// ── JWT authentication (for GET /api/auth/me) ─────────────────────────────
// Identity.Api both ISSUES and VALIDATES JWTs.
// Validation here is used only for the /auth/me endpoint (called by the Next.js BFF).
// The gateway handles JWT validation for all other downstream service routes.
var jwtSection   = builder.Configuration.GetSection("Jwt");
var signingKey   = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
var issuer       = jwtSection["Issuer"]   ?? "legalsynq-identity";
var audience     = jwtSection["Audience"] ?? "legalsynq-platform";

// ── BLK-OPS-01: Production fail-fast (supersedes BLK-SEC-01 inline checks) ────
// Both email values must be present for invitation and password-reset emails to work.
// Failing at startup prevents silent runtime drops where emails are never sent.
if (!builder.Environment.IsDevelopment())
{
    var v = new RuntimeConfigValidator(builder.Configuration, "identity");
    v
        // JWT signing key must be real — not a placeholder
        .RequireNotPlaceholder("Jwt:SigningKey")
        // Provisioning secret gates all internal provisioning and membership endpoints
        .RequireNonEmpty("TenantService:ProvisioningSecret")
        // Notifications service — required for invitation and password-reset emails
        .RequireAbsoluteUrl("NotificationsService:BaseUrl")
        .RequireNonEmpty("NotificationsService:PortalBaseUrl")
        // LS-ID-TNT-016-01: tenant-subdomain-aware portal links require a base domain in
        // non-development environments. Missing this causes BuildBaseUrl to silently return
        // null and breaks all invite/reset email links. Set via
        // NotificationsService__PortalBaseDomain (e.g. "portal.legalsynq.com").
        .RequireNonEmpty("NotificationsService:PortalBaseDomain")
        // Database connection string
        .RequireConnectionString("ConnectionStrings:IdentityDb");
}

// ── LS-ID-TNT-016-01: Log portal URL-building mode at startup ─────────────
// NotificationsService:PortalBaseDomain (env: NotificationsService__PortalBaseDomain)
// enables tenant-subdomain-aware email links: https://{slug}.{baseDomain}/{path}?token=...
// When absent, PortalBaseUrl is used as a fallback (generic, non-subdomain URL).
// Set NotificationsService__PortalBaseDomain to your deployment base domain (e.g. example.com).
{
    var startupLogger     = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Identity.Startup");
    var portalBaseDomain  = builder.Configuration["NotificationsService:PortalBaseDomain"];
    var portalBaseUrl     = builder.Configuration["NotificationsService:PortalBaseUrl"];

    if (!string.IsNullOrWhiteSpace(portalBaseDomain))
    {
        startupLogger.LogInformation(
            "[LS-ID-TNT-016-01] Portal URL mode: SUBDOMAIN — invite/reset links will use " +
            "https://{{slug}}.{BaseDomain}/{{path}}?token=... Set via NotificationsService__PortalBaseDomain.",
            portalBaseDomain);
    }
    else if (!string.IsNullOrWhiteSpace(portalBaseUrl))
    {
        startupLogger.LogWarning(
            "[LS-ID-TNT-016-01] Portal URL mode: FALLBACK — NotificationsService:PortalBaseDomain is not set; " +
            "invite/reset links will use the generic PortalBaseUrl ({PortalBaseUrl}). " +
            "Set NotificationsService__PortalBaseDomain to enable tenant-subdomain-aware links.",
            portalBaseUrl);
    }
    else
    {
        startupLogger.LogError(
            "[LS-ID-TNT-016-01] Portal URL mode: UNCONFIGURED — neither PortalBaseDomain nor PortalBaseUrl is set. " +
            "Invite and password-reset emails cannot be sent. " +
            "Set NotificationsService__PortalBaseDomain (required) and NotificationsService__PortalBaseUrl (fallback).");
    }
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep claim names as-is (sub, email, etc.)
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = issuer,
            ValidateAudience         = true,
            ValidAudience            = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero,
            RoleClaimType            = "role",
            NameClaimType            = System.Security.Claims.ClaimTypes.NameIdentifier,
        };
    });

builder.Services.AddAuthorization();

// Resolves the real client IP from X-Forwarded-For (set by the gateway/proxy)
// before falling back to the direct TCP remote address. This prevents all
// traffic from appearing to originate from the gateway IP, which would cause
// a single bad actor to exhaust the shared limit for all legitimate callers.
static string ResolveClientIp(HttpContext ctx)
{
    var xff = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(xff))
    {
        var first = xff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        if (!string.IsNullOrWhiteSpace(first))
            return first;
    }
    return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ResolveClientIp(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 20,
                Window               = TimeSpan.FromMinutes(5),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
            }));

    options.AddPolicy("auth-forgot-password", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ResolveClientIp(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 5,
                Window               = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
            }));

    options.AddPolicy("auth-token-exchange", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ResolveClientIp(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 10,
                Window               = TimeSpan.FromMinutes(5),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
            }));
});

var app = builder.Build();
var env = app.Environment.EnvironmentName;

app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

// ── LS-ID-TNT-011 pre-migration guard ────────────────────────────────────────
// MySQL auto-commits DDL (ALTER TABLE, CREATE TABLE) immediately, even inside a
// failed migration transaction. If 20260418230627_AddTenantPermissionCatalog ran
// its DDL but failed on the data-seed step, the schema is already correct but EF
// has no record of the migration. On the next startup EF tries to re-apply it,
// the AddColumn calls fail with "Duplicate column name", and the service can never
// start. This guard detects that scenario, runs the idempotent data seeds, and
// inserts the migration record so EF's Migrate() finds nothing to do.
try
{
    using var scope = app.Services.CreateScope();
    var db    = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var conn  = db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open) conn.Open();
    using var cmd = conn.CreateCommand();

    Identity.Infrastructure.StartupMigrationGuard.ApplyIfMissing(
        cmd,
        migrationId: "20260418230627_AddTenantPermissionCatalog",
        efVersion:   "8.0.7",
        logger:      app.Logger,
        guardLabel:  "LS-ID-TNT-011",
        apply: c =>
        {
            // Seed SYNQ_PLATFORM product (idempotent)
            c.CommandText = @"
                INSERT IGNORE INTO `idt_Products` (`Id`, `Code`, `CreatedAtUtc`, `Description`, `IsActive`, `Name`)
                VALUES ('10000000-0000-0000-0000-000000000006','SYNQ_PLATFORM','2025-01-01 00:00:00.000000',
                        'Platform/tenant operation capabilities',1,'SynqPlatform');";
            c.ExecuteNonQuery();

            // Seed 8 TENANT.* permissions (idempotent)
            c.CommandText = @"
                INSERT IGNORE INTO `idt_Capabilities`
                    (`Id`,`ProductId`,`Code`,`Name`,`Description`,`Category`,`IsActive`,`CreatedAtUtc`,`UpdatedAtUtc`,`CreatedBy`,`UpdatedBy`)
                VALUES
                    ('60000000-0000-0000-0000-000000000030','10000000-0000-0000-0000-000000000006','TENANT.users:view',        'View Tenant Users',       'View the list of users in the tenant',                  'Users',      1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000031','10000000-0000-0000-0000-000000000006','TENANT.users:manage',      'Manage Tenant Users',     'Create, edit, and deactivate users in the tenant',      'Users',      1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000032','10000000-0000-0000-0000-000000000006','TENANT.groups:manage',     'Manage Access Groups',    'Create, edit, and delete tenant access groups',         'Groups',     1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000033','10000000-0000-0000-0000-000000000006','TENANT.roles:assign',      'Assign Roles',            'Assign or revoke roles for tenant users',               'Roles',      1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000034','10000000-0000-0000-0000-000000000006','TENANT.products:assign',   'Assign Product Access',   'Assign or revoke product access for tenant users',      'Products',   1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000035','10000000-0000-0000-0000-000000000006','TENANT.settings:manage',   'Manage Tenant Settings',  'Update tenant configuration and preferences',           'Settings',   1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000036','10000000-0000-0000-0000-000000000006','TENANT.audit:view',        'View Audit Logs',         'View identity and access audit events for the tenant',  'Audit',      1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000037','10000000-0000-0000-0000-000000000006','TENANT.invitations:manage','Manage User Invitations', 'Send, resend, and revoke user invitations',             'Invitations',1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL);";
            c.ExecuteNonQuery();

            // Seed role → capability assignments (idempotent). Column is CapabilityId (not PermissionId).
            c.CommandText = @"
                INSERT IGNORE INTO `idt_RoleCapabilityAssignments` (`RoleId`,`CapabilityId`,`AssignedAtUtc`,`AssignedByUserId`)
                VALUES
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000030','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000031','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000032','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000033','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000034','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000035','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000036','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000037','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000003','60000000-0000-0000-0000-000000000030','2025-01-01 00:00:00.000000',NULL);";
            c.ExecuteNonQuery();
        },
        prerequisite: c =>
        {
            // Only proceed if the DDL column was already applied (partial-commit scenario).
            c.CommandText = @"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME   = 'idt_Capabilities'
                  AND COLUMN_NAME  = 'Category';";
            return Convert.ToInt64(c.ExecuteScalar()) > 0;
        },
        warningMessage:
            "LS-ID-TNT-011: AddTenantPermissionCatalog DDL was partially committed " +
            "by a prior run. Seeding data idempotently and marking migration as applied.");

    conn.Close();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "LS-ID-TNT-011 pre-migration guard failed — proceeding with normal migration");
}

// ── LS-ID-SUP-002 pre-migration guard ────────────────────────────────────────
// The three 20260426 migrations seed support roles and back-fill ScopedRole-
// Assignments. Their SQL is idempotent (INSERT IGNORE / UPDATE … WHERE), so
// re-running it is safe; however EF will attempt to apply them on EVERY startup
// until they appear in __EFMigrationsHistory. If a prior startup ran their SQL
// via the LS-ID-SUP-001 guard but the EF Migrate() call subsequently failed,
// the history never advanced and the cycle repeats.
//
// This guard runs each migration's SQL idempotently, then inserts any missing
// history rows so that EF's Migrate() finds nothing to do for these three entries.
// (LS-ID-SUP-001, the earlier belt-and-suspenders guard, has been removed now that
// these migrations are correctly recorded in __EFMigrationsHistory on every startup.)
try
{
    using var scope = app.Services.CreateScope();
    var db   = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var conn = db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open) conn.Open();
    using var cmd = conn.CreateCommand();

    const string PlatformTenantId  = "20000000-0000-0000-0000-000000000001";
    const string RolePlatformAdmin = "30000000-0000-0000-0000-000000000001";
    const string RoleTenantAdmin   = "30000000-0000-0000-0000-000000000002";
    const string RoleSupportAdmin     = "30000000-0000-0000-0000-000000000011";
    const string RoleSupportManager   = "30000000-0000-0000-0000-000000000012";
    const string RoleSupportAgent     = "30000000-0000-0000-0000-000000000013";
    const string RoleTenantUser       = "30000000-0000-0000-0000-000000000014";
    const string RoleExternalCustomer = "30000000-0000-0000-0000-000000000015";
    const string EfVersion = "8.0.7";

    // ── Migration 20260426000001_SeedSupportRoles ──────────────────────────
    var mig1Recorded = Identity.Infrastructure.StartupMigrationGuard.ApplyIfMissing(
        cmd,
        migrationId: "20260426000001_SeedSupportRoles",
        efVersion:   EfVersion,
        logger:      app.Logger,
        guardLabel:  "LS-ID-SUP-002",
        apply: c =>
        {
            c.CommandText = $@"
INSERT IGNORE INTO `idt_Roles`
    (`Id`, `TenantId`, `Name`, `Description`, `IsSystemRole`, `Scope`, `CreatedAtUtc`, `UpdatedAtUtc`)
VALUES
    ('{RoleSupportAdmin}',    '{PlatformTenantId}', 'SupportAdmin',     'Full support administration — manages queues, SLAs and settings',         1, 'Support',  '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('{RoleSupportManager}',  '{PlatformTenantId}', 'SupportManager',   'Support team manager — escalations, reporting and agent oversight',        1, 'Support',  '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('{RoleSupportAgent}',    '{PlatformTenantId}', 'SupportAgent',     'Frontline support agent — handles and responds to tickets',                1, 'Support',  '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('{RoleTenantUser}',      '{PlatformTenantId}', 'TenantUser',       'Regular authenticated tenant user — read-only support access',            1, 'Tenant',   '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('{RoleExternalCustomer}','{PlatformTenantId}', 'ExternalCustomer', 'External customer — may view and comment on their own support tickets',   0, 'External', '2024-01-01 00:00:00', '2024-01-01 00:00:00');";
            c.ExecuteNonQuery();

            c.CommandText = $@"
INSERT IGNORE INTO `idt_ScopedRoleAssignments`
    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`,
     `OrganizationId`, `OrganizationRelationshipId`, `ProductId`,
     `IsActive`, `AssignedAtUtc`, `UpdatedAtUtc`, `AssignedByUserId`)
SELECT UUID(), u.`Id`,
    CASE WHEN u.`TenantId` = '{PlatformTenantId}' THEN '{RolePlatformAdmin}' ELSE '{RoleTenantAdmin}' END,
    'GLOBAL', u.`TenantId`, NULL, NULL, NULL,
    1, u.`CreatedAtUtc`, u.`CreatedAtUtc`, NULL
FROM `idt_Users` u
WHERE u.`IsActive` = 1
  AND NOT EXISTS (
    SELECT 1 FROM `idt_ScopedRoleAssignments` sra
    WHERE sra.`UserId` = u.`Id` AND sra.`ScopeType` = 'GLOBAL' AND sra.`IsActive` = 1
  );";
            c.ExecuteNonQuery();
        });

    // ── Migration 20260426000002_FixSupportRolesBackfill ──────────────────
    var mig2Recorded = Identity.Infrastructure.StartupMigrationGuard.ApplyIfMissing(
        cmd,
        migrationId: "20260426000002_FixSupportRolesBackfill",
        efVersion:   EfVersion,
        logger:      app.Logger,
        guardLabel:  "LS-ID-SUP-002",
        apply: c =>
        {
            c.CommandText = $@"
INSERT IGNORE INTO `idt_ScopedRoleAssignments`
    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`,
     `OrganizationId`, `OrganizationRelationshipId`, `ProductId`,
     `IsActive`, `AssignedAtUtc`, `UpdatedAtUtc`, `AssignedByUserId`)
SELECT UUID(), u.`Id`,
    CASE WHEN u.`TenantId` = '{PlatformTenantId}' THEN '{RolePlatformAdmin}' ELSE '{RoleTenantAdmin}' END,
    'GLOBAL', u.`TenantId`, NULL, NULL, NULL,
    1, u.`CreatedAtUtc`, u.`CreatedAtUtc`, NULL
FROM `idt_Users` u
WHERE u.`IsActive` = 1
  AND NOT EXISTS (
    SELECT 1 FROM `idt_ScopedRoleAssignments` sra
    WHERE sra.`UserId` = u.`Id` AND sra.`ScopeType` = 'GLOBAL' AND sra.`IsActive` = 1
  );";
            c.ExecuteNonQuery();
        });

    // ── Migration 20260426000003_CorrectPlatformAdminRole ─────────────────
    var mig3Recorded = Identity.Infrastructure.StartupMigrationGuard.ApplyIfMissing(
        cmd,
        migrationId: "20260426000003_CorrectPlatformAdminRole",
        efVersion:   EfVersion,
        logger:      app.Logger,
        guardLabel:  "LS-ID-SUP-002",
        apply: c =>
        {
            c.CommandText = $@"
UPDATE `idt_ScopedRoleAssignments` sra
INNER JOIN `idt_Users` u ON u.`Id` = sra.`UserId`
SET   sra.`RoleId`       = '{RolePlatformAdmin}',
      sra.`UpdatedAtUtc` = UTC_TIMESTAMP()
WHERE u.`TenantId`           = '{PlatformTenantId}'
  AND sra.`ScopeType`        = 'GLOBAL'
  AND sra.`IsActive`         = 1
  AND sra.`RoleId`           = '{RoleTenantAdmin}'
  AND sra.`AssignedByUserId` IS NULL;";
            c.ExecuteNonQuery();

            c.CommandText = $@"
INSERT IGNORE INTO `idt_ScopedRoleAssignments`
    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`,
     `OrganizationId`, `OrganizationRelationshipId`, `ProductId`,
     `IsActive`, `AssignedAtUtc`, `UpdatedAtUtc`, `AssignedByUserId`)
SELECT UUID(), u.`Id`, '{RolePlatformAdmin}', 'GLOBAL', u.`TenantId`,
       NULL, NULL, NULL, 1, u.`CreatedAtUtc`, u.`CreatedAtUtc`, NULL
FROM `idt_Users` u
WHERE u.`TenantId` = '{PlatformTenantId}'
  AND u.`IsActive` = 1
  AND NOT EXISTS (
    SELECT 1 FROM `idt_ScopedRoleAssignments` sra2
    WHERE sra2.`UserId` = u.`Id` AND sra2.`ScopeType` = 'GLOBAL' AND sra2.`IsActive` = 1
  );";
            c.ExecuteNonQuery();
        });

    // ── Migration 20260426100001_SeedPlatformAdminUser ───────────────────
    // Inserts the platform admin user (admin@legalsynq.com / Admin123!) if not
    // already present.  The SeedAdminOrgMembership EF migration ran a SELECT-
    // based INSERT that silently inserted 0 rows when no user existed yet.
    var mig4Recorded = Identity.Infrastructure.StartupMigrationGuard.ApplyIfMissing(
        cmd,
        migrationId: "20260426100001_SeedPlatformAdminUser",
        efVersion:   EfVersion,
        logger:      app.Logger,
        guardLabel:  "LS-ID-ADM-001",
        apply: c =>
        {
            const string AdminUserId   = "50000000-0000-0000-0000-000000000001";
            const string AdminPwHash   = "$2a$12$/wvZFZf.T4qlqcaD9gn5GOKmjvXHCbr3/wUXu4wtRwLzj4W4XXA2a";
            const string OrgId         = "40000000-0000-0000-0000-000000000001";
            const string OrgMemId      = "40000000-0000-0000-0000-000000000003";
            const string SraId         = "90000000-0000-0000-0000-000000000001";

            c.CommandText = $@"
INSERT IGNORE INTO `idt_Users`
    (`Id`, `TenantId`, `Email`, `PasswordHash`,
     `FirstName`, `LastName`, `IsActive`,
     `IsLocked`, `FailedLoginCount`, `UserType`,
     `CreatedAtUtc`, `UpdatedAtUtc`)
VALUES (
    '{AdminUserId}', '{PlatformTenantId}',
    'admin@legalsynq.com', '{AdminPwHash}',
    'Platform', 'Admin', 1, 0, 0, 'PlatformInternal',
    '2024-01-01 00:00:00', '2024-01-01 00:00:00'
);";
            c.ExecuteNonQuery();

            c.CommandText = $@"
INSERT IGNORE INTO `idt_UserOrganizationMemberships`
    (`Id`, `UserId`, `OrganizationId`, `MemberRole`,
     `IsActive`, `JoinedAtUtc`, `GrantedByUserId`)
VALUES (
    '{OrgMemId}', '{AdminUserId}', '{OrgId}',
    'OWNER', 1, '2024-01-01 00:00:00', NULL
);";
            c.ExecuteNonQuery();

            c.CommandText = $@"
INSERT IGNORE INTO `idt_ScopedRoleAssignments`
    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`,
     `OrganizationId`, `OrganizationRelationshipId`, `ProductId`,
     `IsActive`, `AssignedAtUtc`, `UpdatedAtUtc`, `AssignedByUserId`)
VALUES (
    '{SraId}', '{AdminUserId}', '{RolePlatformAdmin}', 'GLOBAL', '{PlatformTenantId}',
    NULL, NULL, NULL, 1,
    '2024-01-01 00:00:00', '2024-01-01 00:00:00', NULL
);";
            c.ExecuteNonQuery();
        });

    conn.Close();

    if (mig1Recorded && mig2Recorded && mig3Recorded && mig4Recorded)
    {
        app.Logger.LogInformation(
            "LS-ID-SUP-002: all four 20260426 migrations already recorded in EF history — no action needed.");
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "LS-ID-SUP-002 pre-migration guard failed — proceeding with normal migration");
}

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    db.Database.Migrate();
    app.Logger.LogInformation("Database migrations applied");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not apply migrations — ensure MySQL is running and connection string is correct");
}

// ── Migration coverage self-test ─────────────────────────────────────────
// Compares every EF-mapped column against information_schema. If a model
// property has no backing column on the live database, log an ERROR so the
// regression is loud at boot (and visible to CI / log aggregation).
// This catches the class of bug behind Task #58: a migration committed
// without its [Migration] attribute (or otherwise un-applied) leaves the
// EF model and the live schema out of sync, which previously surfaced only
// as runtime "Unknown column" SQL errors at login. Runs after Migrate()
// so anything still missing is a true gap.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
}

// ── Startup diagnostic: Phase G authorization status ─────────────────────────
// Phase G COMPLETE: UserRoles + UserRoleAssignments tables dropped.
// Diagnostics verify OrgTypeRule coverage and ScopedRoleAssignment counts.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

    // 1. Verify all restricted ProductRoles have OrgTypeRule coverage.
    var unrestrictedRoleCount = await db.Set<Identity.Domain.ProductRole>()
        .Where(pr => pr.IsActive)
        .Where(pr => !db.Set<Identity.Domain.ProductOrganizationTypeRule>()
            .Any(r => r.ProductRoleId == pr.Id && r.IsActive))
        .CountAsync();

    if (unrestrictedRoleCount > 0)
    {
        app.Logger.LogWarning(
            "{Count} active ProductRole(s) have no ProductOrganizationTypeRule rows and are " +
            "therefore unrestricted. If restriction was intended, add OrgTypeRule seed data.",
            unrestrictedRoleCount);
    }
    else
    {
        var totalActive = await db.Set<Identity.Domain.ProductRole>().CountAsync(pr => pr.IsActive);
        app.Logger.LogInformation(
            "Phase G eligibility check passed — {Count} active ProductRole(s), all OrgTypeRule-covered.",
            totalActive);
    }

    // 2. Log ScopedRoleAssignment totals (Phase G: sole authoritative role source).
    var scopedTotal = await db.ScopedRoleAssignments
        .CountAsync(s => s.IsActive && s.ScopeType == "GLOBAL");
    var scopedUsers = await db.ScopedRoleAssignments
        .Where(s => s.IsActive && s.ScopeType == "GLOBAL")
        .Select(s => s.UserId)
        .Distinct()
        .CountAsync();
    app.Logger.LogInformation(
        "Phase G role check: {Assignments} active GLOBAL ScopedRoleAssignment(s) across {Users} user(s).",
        scopedTotal, scopedUsers);

    // 3. Org type consistency: flag organizations where OrganizationTypeId FK is null
    //    but OrgType string is populated, or where the FK code doesn't match the string.
    var orgTypeCheckRows = await db.Organizations
        .Where(o => o.IsActive)
        .Select(o => new { o.Id, o.OrgType, o.OrganizationTypeId })
        .ToListAsync();

    var orgsWithMissingTypeId = orgTypeCheckRows
        .Count(o => o.OrganizationTypeId == null && !string.IsNullOrWhiteSpace(o.OrgType));

    var orgsWithCodeMismatch = orgTypeCheckRows
        .Count(o =>
        {
            if (o.OrganizationTypeId == null) return false;
            var expectedCode = Identity.Domain.OrgTypeMapper.TryResolveCode(o.OrganizationTypeId);
            return expectedCode is not null &&
                   !string.Equals(expectedCode, o.OrgType, StringComparison.OrdinalIgnoreCase);
        });

    if (orgsWithMissingTypeId > 0)
        app.Logger.LogWarning(
            "OrgType consistency: {Count} active org(s) have OrgType string but no OrganizationTypeId. " +
            "Run a backfill migration (or update via Organization.Create) to populate the FK.",
            orgsWithMissingTypeId);

    if (orgsWithCodeMismatch > 0)
        app.Logger.LogWarning(
            "OrgType consistency: {Count} active org(s) have an OrganizationTypeId whose OrgTypeMapper " +
            "code does not match the stored OrgType string. Investigate and reconcile.",
            orgsWithCodeMismatch);

    if (orgsWithMissingTypeId == 0 && orgsWithCodeMismatch == 0)
        app.Logger.LogInformation(
            "OrgType consistency check passed — {Total} active org(s) all have consistent OrganizationTypeId and OrgType.",
            orgTypeCheckRows.Count);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex,
        "Phase G startup diagnostic skipped — could not query the database at startup.");
}

// ── UIX-002-C: Ensure each active ProductRole has a corresponding entry in the Roles table ──
// Product roles are defined in the ProductRoles table but need corresponding entries in the
// Roles table (with IsSystemRole = false) so they can be assigned through ScopedRoleAssignment.
// This seeder is idempotent — it only creates missing entries.
try
{
    using var prScope = app.Services.CreateScope();
    var prDb = prScope.ServiceProvider.GetRequiredService<IdentityDbContext>();

    var activeProductRoles = await prDb.ProductRoles
        .Include(pr => pr.Product)
        .Where(pr => pr.IsActive)
        .ToListAsync();

    var existingRoleNames = await prDb.Roles
        .Select(r => r.Name)
        .ToListAsync();

    var existingNameSet = new HashSet<string>(existingRoleNames, StringComparer.OrdinalIgnoreCase);

    var platformTenantId = await prDb.Tenants
        .Where(t => t.IsActive)
        .OrderBy(t => t.CreatedAtUtc)
        .Select(t => t.Id)
        .FirstOrDefaultAsync();

    if (platformTenantId == Guid.Empty)
    {
        app.Logger.LogWarning("UIX-002-C: No active tenant found — skipping product role seed.");
    }

    var created = 0;
    foreach (var pr in activeProductRoles)
    {
        if (platformTenantId == Guid.Empty)
            break;

        if (existingNameSet.Contains(pr.Code))
            continue;

        var role = Identity.Domain.Role.Create(
            tenantId: platformTenantId,
            name: pr.Code,
            description: $"[Product] {pr.Name} — {pr.Description ?? pr.Product.Name}",
            isSystemRole: false);
        prDb.Roles.Add(role);
        existingNameSet.Add(pr.Code);
        created++;

        app.Logger.LogInformation(
            "UIX-002-C: Seeded Role '{RoleName}' for ProductRole {ProductRoleCode} (Product: {Product})",
            pr.Code, pr.Code, pr.Product.Name);
    }

    if (created > 0)
        await prDb.SaveChangesAsync();

    app.Logger.LogInformation(
        "UIX-002-C product role sync complete — {Created} new Role(s) seeded, {Total} product roles active.",
        created, activeProductRoles.Count);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "UIX-002-C product role seed encountered an error");
}

// ── Dev-only: ensure every user has a primary org membership ─────────────
// Some tenants were created with the org added separately, leaving the user
// without a UserOrganizationMembership.  This block auto-heals that gap.
if (app.Environment.IsDevelopment())
{
    try
    {
        using var fixScope = app.Services.CreateScope();
        var fixDb = fixScope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var usersWithoutMembership = await fixDb.Users
            .Where(u => u.IsActive)
            .Where(u => !fixDb.UserOrganizationMemberships.Any(m => m.UserId == u.Id))
            .ToListAsync();

        foreach (var orphan in usersWithoutMembership)
        {
            var org = await fixDb.Organizations
                .Where(o => o.TenantId == orphan.TenantId && o.IsActive)
                .OrderBy(o => o.CreatedAtUtc)
                .FirstOrDefaultAsync();

            if (org is null) continue;

            var membership = Identity.Domain.UserOrganizationMembership.Create(
                orphan.Id, org.Id, Identity.Domain.MemberRole.Admin);
            membership.SetPrimary();
            fixDb.UserOrganizationMemberships.Add(membership);

            app.Logger.LogInformation(
                "Dev fixup: created org membership for user {UserId} ({Email}) → org {OrgId} ({OrgName})",
                orphan.Id, orphan.Email, org.Id, org.Name);
        }

        if (usersWithoutMembership.Count > 0)
            await fixDb.SaveChangesAsync();

        var allMemberships = await fixDb.UserOrganizationMemberships
            .Where(m => m.IsActive)
            .ToListAsync();

        var userIdsWithPrimary = allMemberships.Where(m => m.IsPrimary).Select(m => m.UserId).ToHashSet();
        var needsPrimary = allMemberships
            .Where(m => !userIdsWithPrimary.Contains(m.UserId))
            .GroupBy(m => m.UserId)
            .Select(g => g.OrderBy(m => m.JoinedAtUtc).First())
            .ToList();

        foreach (var m in needsPrimary)
        {
            m.SetPrimary();
            app.Logger.LogInformation(
                "Dev fixup: set primary org membership for user {UserId} → org {OrgId}",
                m.UserId, m.OrganizationId);
        }

        if (needsPrimary.Count > 0)
            await fixDb.SaveChangesAsync();

        // Also ensure OrganizationProduct rows exist for every active TenantProduct + Org pair
        var tenantProducts = await fixDb.Set<Identity.Domain.TenantProduct>()
            .Where(tp => tp.IsEnabled)
            .ToListAsync();

        foreach (var tp in tenantProducts)
        {
            var orgs = await fixDb.Organizations
                .Include(o => o.OrganizationProducts)
                .Where(o => o.TenantId == tp.TenantId && o.IsActive)
                .ToListAsync();

            foreach (var org in orgs)
            {
                if (org.OrganizationProducts.Any(op => op.ProductId == tp.ProductId))
                    continue;

                var op = Identity.Domain.OrganizationProduct.Create(org.Id, tp.ProductId);
                fixDb.Set<Identity.Domain.OrganizationProduct>().Add(op);

                app.Logger.LogInformation(
                    "Dev fixup: created OrganizationProduct for org {OrgId} ({OrgName}) → product {ProductId}",
                    org.Id, org.Name, tp.ProductId);
            }
        }

        await fixDb.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Dev fixup for user/org memberships encountered an error");
    }
}

// ── Middleware pipeline ───────────────────────────────────────────────────
// Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]        = "DENY";
    ctx.Response.Headers["X-XSS-Protection"]       = "0";
    ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    await next();
});

app.UseRateLimiter();

// UseAuthentication + UseAuthorization must precede all endpoint maps
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceName)));

app.MapGet("/info", () =>
    Results.Ok(new InfoResponse(ServiceName, env, Version)));

app.MapTenantEndpoints();
app.MapProductEndpoints();
app.MapUserEndpoints();
app.MapAuthEndpoints();
app.MapTenantBrandingEndpoints();
app.MapAdminEndpoints();
app.MapTenantProvisioningEndpoints();
app.MapUserMembershipEndpoints();      // BLK-ID-02
app.MapAccessSourceEndpoints();
app.MapGroupEndpoints();
app.MapPermissionCatalogEndpoints();

app.Run();
