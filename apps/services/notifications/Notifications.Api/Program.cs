using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Notifications.Api.Authorization;
using Notifications.Api.Endpoints;
using Notifications.Api.Middleware;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ── JWT Authentication ────────────────────────────────────────────────────────

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

// LS-NOTIF-CORE-021: service token signing key (shared platform secret).
// Preferred from FLOW_SERVICE_TOKEN_SECRET env var, then ServiceTokens:SigningKey config.
var serviceTokenKey =
    Environment.GetEnvironmentVariable(ServiceTokenAuthenticationDefaults.SecretEnvVar)
    ?? builder.Configuration[$"{ServiceTokenOptions.SectionName}:SigningKey"]
    ?? string.Empty;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    // ── Scheme 1: user JWTs from Identity ────────────────────────────────
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
    // ── Scheme 2: service-to-service JWTs (LS-NOTIF-CORE-021) ────────────
    // Accepts tokens minted by ServiceTokenIssuer from any producer service.
    // Validates: issuer=legalsynq-service-tokens, audience=notifications-service
    // OR flow-service (for Flow's existing issuer config), subject=service:*
    .AddJwtBearer(ServiceTokenAuthenticationDefaults.Scheme, options =>
    {
        options.MapInboundClaims    = false;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(serviceTokenKey),
            RequireSignedTokens      = true,
            RequireExpirationTime    = true,
            ValidIssuer              = ServiceTokenAuthenticationDefaults.DefaultIssuer,
            // Accept notifications-service (new preferred) + flow-service
            // (Flow's existing issuer defaults) + legalsynq-services (future).
            ValidAudiences           = ["notifications-service", "flow-service", "legalsynq-services"],
            IssuerSigningKey         = string.IsNullOrWhiteSpace(serviceTokenKey)
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(serviceTokenKey)),
            NameClaimType            = "sub",
            RoleClaimType            = "role",
            ClockSkew                = TimeSpan.FromSeconds(30),
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var sub = ctx.Principal?.FindFirst("sub")?.Value;
                if (string.IsNullOrWhiteSpace(sub) ||
                    !sub.StartsWith("service:", StringComparison.Ordinal))
                {
                    ctx.Fail("Service token must have a subject starting with 'service:'.");
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = ctx =>
            {
                var log = ctx.HttpContext.RequestServices
                    .GetService<ILoggerFactory>()
                    ?.CreateLogger(ServiceTokenAuthenticationDefaults.Scheme);
                log?.LogWarning(ctx.Exception,
                    "ServiceToken authentication failed. Path={Path}",
                    ctx.HttpContext.Request.Path);
                return Task.CompletedTask;
            },
        };
    });

// ── HTTP context accessor (required by ServiceSubmissionHandler) ──────────────
builder.Services.AddHttpContextAccessor();

// ── Authorization ─────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));

    // LS-NOTIF-CORE-021 — service submission gate on POST /v1/notifications.
    // Tries both the user JWT scheme and the ServiceToken scheme;
    // the custom handler also allows legacy X-Tenant-Id header requests.
    options.AddPolicy(Policies.ServiceSubmission, policy =>
        policy
            .AddAuthenticationSchemes(
                JwtBearerDefaults.AuthenticationScheme,
                ServiceTokenAuthenticationDefaults.Scheme)
            .AddRequirements(new ServiceSubmissionRequirement()));
});

// Register the custom authorization handler for ServiceSubmission.
builder.Services.AddSingleton<IAuthorizationHandler, ServiceSubmissionHandler>();

// ── Application services ─────────────────────────────────────────────────────

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ── Database startup ──────────────────────────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

    try
    {
        await SchemaRenamer.RenameSchemaAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Schema rename step failed — tables/columns may already be renamed");
    }

    try
    {
        // If the schema already existed before EF migrations were introduced the
        // __EFMigrationsHistory table may be empty even though InitialCreate (and
        // AddRetryFields) have already been applied.  MigrateAsync() would then
        // try to re-run InitialCreate, fail with "table already exists", and
        // abort — leaving AddCategoryAndSeverity (and any future migrations) never
        // applied.  We detect this condition and seed the history so that
        // MigrateAsync() only executes the genuinely pending migrations.
        await SeedMigrationHistoryIfNeededAsync(db, app.Logger);
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Notifications database migrated successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not apply Notifications database migrations on startup — schema may be out of sync.");
    }

    // Safety net: ensure columns added by AddCategoryAndSeverity actually exist
    // in the database even if EF's history already records the migration as applied
    // (which can happen when the migration was aborted mid-run but still committed
    // to __EFMigrationsHistory).
    try
    {
        await EnsureNotificationsSchemaColumnsAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not ensure notification schema columns — queries may fail");
    }

    try
    {
        await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
    }
}

// ── Platform provider seeding ─────────────────────────────────────────────────
// On every startup, ensure the platform-level SendGrid provider config exists.
// This is stored with the sentinel TenantId 00000000-0000-0000-0000-000000000001
// so the control center can list/use it without a real tenant context.
try
{
    using var seedScope = app.Services.CreateScope();
    await SeedPlatformSendGridProviderAsync(
        seedScope.ServiceProvider,
        app.Configuration,
        app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Platform SendGrid provider seeding failed — providers page may show empty");
}

// ── Support email template seeding ────────────────────────────────────────────
// On every startup, ensure the four global support email templates exist so the
// support service can deliver email notifications without manual DB setup.
try
{
    using var seedScope = app.Services.CreateScope();
    await SeedSupportEmailTemplatesAsync(seedScope.ServiceProvider, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Support email template seeding failed — support notifications may not render");
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
// Order matters: Authentication → Authorization → custom middleware → endpoints.
// TenantMiddleware is placed AFTER UseAuthentication so it can read context.User
// to extract tenant_id from JWT claims for authenticated requests.

app.UseMiddleware<RawBodyMiddleware>();
app.UseMiddleware<InternalTokenMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();

// ── Endpoints ─────────────────────────────────────────────────────────────────

app.MapHealthEndpoints();
app.MapNotificationEndpoints();
app.MapAdminNotificationEndpoints();
app.MapTemplateEndpoints();
app.MapGlobalTemplateEndpoints();
app.MapProviderEndpoints();
app.MapWebhookEndpoints();
app.MapBillingEndpoints();
app.MapContactEndpoints();
app.MapBrandingEndpoints();
app.MapInternalEndpoints();
app.MapSmsPreferenceEndpoints();
app.MapSmsReconciliationEndpoints();
app.MapSmsActivityEndpoints();
app.MapSmsDashboardEndpoints();
app.MapSmsCostEndpoints();
app.MapSmsAlertEndpoints();
app.MapSmsEscalationEndpoints();
app.MapSmsRoutingEndpoints();
app.MapSmsOptimizationEndpoints(); // LS-NOTIF-SMS-015
app.MapSmsRecipientIntelligenceEndpoints(); // LS-NOTIF-SMS-016
app.MapSmsGovernanceEndpoints();             // LS-NOTIF-SMS-017
app.MapSmsTemplateGovernanceEndpoints();     // LS-NOTIF-SMS-018
app.MapSmsGovernanceDynamicRuleEndpoints();  // LS-NOTIF-SMS-019
app.MapSmsGovernanceLifecycleEndpoints();   // LS-NOTIF-SMS-020
app.MapSmsGovernanceReleaseEndpoints();     // LS-NOTIF-SMS-021
app.MapSmsGovernanceRolloutEndpoints();       // LS-NOTIF-SMS-022
app.MapSmsGovernanceTenantScopingEndpoints(); // LS-NOTIF-SMS-023
app.MapGovernanceFederationEndpoints();       // LS-NOTIF-SMS-024
app.MapGovernanceRuntimeEndpoints();          // LS-NOTIF-SMS-025

app.Run();

// ── Helpers ───────────────────────────────────────────────────────────────────

static async Task EnsureNotificationsSchemaColumnsAsync(NotificationsDbContext db, ILogger logger)
{
    // Use raw ADO.NET so we stay in control of the SQL and avoid EF query-wrapping quirks.
    var conn = db.Database.GetDbConnection();
    var dbName = conn.Database;
    var opened = false;

    try
    {
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
            opened = true;
        }

        // All columns that may be missing: from AddRetryFields and AddCategoryAndSeverity.
        var columnsToAdd = new[]
        {
            ("RetryCount",  "int NOT NULL DEFAULT 0"),
            ("MaxRetries",  "int NOT NULL DEFAULT 3"),
            ("NextRetryAt", "datetime(6) NULL"),
            ("Category",    "varchar(100) CHARACTER SET utf8mb4 NULL"),
            ("Severity",    "varchar(50)  CHARACTER SET utf8mb4 NULL"),
        };

        foreach (var (col, colDef) in columnsToAdd)
        {
            // Check column existence via INFORMATION_SCHEMA.
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText =
                $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = 'ntf_Notifications' AND COLUMN_NAME = '{col}'";
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (count == 0)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = $"ALTER TABLE `ntf_Notifications` ADD COLUMN `{col}` {colDef}";
                await alterCmd.ExecuteNonQueryAsync();
                logger.LogInformation("Added missing column ntf_Notifications.{Column}", col);
            }
            else
            {
                logger.LogDebug("Column ntf_Notifications.{Column} already exists", col);
            }
        }

        // Also ensure the retry index exists (idempotent via INFORMATION_SCHEMA check).
        using var idxCheckCmd = conn.CreateCommand();
        idxCheckCmd.CommandText =
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS " +
            $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = 'ntf_Notifications' " +
            $"AND INDEX_NAME = 'IX_Notifications_Status_NextRetryAt'";
        var idxCount = Convert.ToInt32(await idxCheckCmd.ExecuteScalarAsync());
        if (idxCount == 0)
        {
            using var idxCmd = conn.CreateCommand();
            idxCmd.CommandText =
                "CREATE INDEX `IX_Notifications_Status_NextRetryAt` ON `ntf_Notifications` (`Status`, `NextRetryAt`)";
            await idxCmd.ExecuteNonQueryAsync();
            logger.LogInformation("Created missing index IX_Notifications_Status_NextRetryAt");
        }

        // ── LS-NOTIF-SMS-006: composite index for SMS activity queries ────────
        // Covers: WHERE Channel='sms' AND TenantId=? ORDER BY CreatedAt DESC
        using var smsIdxCheckCmd = conn.CreateCommand();
        smsIdxCheckCmd.CommandText =
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS " +
            $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = 'ntf_NotificationAttempts' " +
            $"AND INDEX_NAME = 'IX_NotificationAttempts_Channel_TenantId_CreatedAt'";
        var smsIdxCount = Convert.ToInt32(await smsIdxCheckCmd.ExecuteScalarAsync());
        if (smsIdxCount == 0)
        {
            using var smsIdxCmd = conn.CreateCommand();
            smsIdxCmd.CommandText =
                "CREATE INDEX `IX_NotificationAttempts_Channel_TenantId_CreatedAt` " +
                "ON `ntf_NotificationAttempts` (`Channel`, `TenantId`, `CreatedAt`)";
            await smsIdxCmd.ExecuteNonQueryAsync();
            logger.LogInformation("Created index IX_NotificationAttempts_Channel_TenantId_CreatedAt");
        }

        // Ensure columns exist on ntf_Templates and ntf_TemplateVersions — may be absent on DBs
        // whose InitialCreate migration was pre-seeded without actually running DDL.
        // TEXT / LONGTEXT columns cannot have a DEFAULT on all MySQL versions, so use NULL for those.
        var templateColumnsToAdd = new[]
        {
            ("ntf_Templates",        "Scope",           "varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'tenant'"),
            ("ntf_Templates",        "ProductType",     "varchar(50) CHARACTER SET utf8mb4 NULL"),
            ("ntf_Templates",        "Description",     "text CHARACTER SET utf8mb4 NULL"),
            ("ntf_TemplateVersions", "SubjectTemplate", "text CHARACTER SET utf8mb4 NULL"),
            ("ntf_TemplateVersions", "TextTemplate",    "text CHARACTER SET utf8mb4 NULL"),
            ("ntf_TemplateVersions", "EditorType",      "varchar(20) CHARACTER SET utf8mb4 NULL"),
            ("ntf_TemplateVersions", "IsPublished",     "tinyint(1) NOT NULL DEFAULT 0"),
            ("ntf_TemplateVersions", "PublishedBy",     "varchar(255) CHARACTER SET utf8mb4 NULL"),
            ("ntf_TemplateVersions", "PublishedAt",     "datetime(6) NULL"),
        };

        foreach (var (table, col, colDef) in templateColumnsToAdd)
        {
            using var checkCmdT = conn.CreateCommand();
            checkCmdT.CommandText =
                $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{col}'";
            var countT = Convert.ToInt32(await checkCmdT.ExecuteScalarAsync());

            if (countT == 0)
            {
                using var alterCmdT = conn.CreateCommand();
                alterCmdT.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{col}` {colDef}";
                await alterCmdT.ExecuteNonQueryAsync();
                logger.LogInformation("Added missing column {Table}.{Column}", table, col);
            }
        }

        // Ensure all columns exist on ntf_TenantProviderConfigs — some may be missing on DBs
        // where the migration was pre-seeded as already-applied without actually running DDL.
        // Note: TEXT columns cannot have DEFAULT values on all MySQL versions, so use NULL for those.
        var providerColumnsToAdd = new[]
        {
            ("ntf_TenantProviderConfigs", "CredentialsJson",     "longtext CHARACTER SET utf8mb4 NULL"),
            ("ntf_TenantProviderConfigs", "SettingsJson",        "longtext CHARACTER SET utf8mb4 NULL"),
            ("ntf_TenantProviderConfigs", "ValidationStatus",    "varchar(30) CHARACTER SET utf8mb4 NULL"),
            ("ntf_TenantProviderConfigs", "ValidationMessage",   "text CHARACTER SET utf8mb4 NULL"),
            ("ntf_TenantProviderConfigs", "LastValidatedAt",     "datetime(6) NULL"),
            ("ntf_TenantProviderConfigs", "HealthStatus",        "varchar(20) CHARACTER SET utf8mb4 NULL"),
            ("ntf_TenantProviderConfigs", "LastHealthCheckAt",   "datetime(6) NULL"),
            ("ntf_TenantProviderConfigs", "HealthCheckLatencyMs","int NULL"),
            ("ntf_TenantProviderConfigs", "OwnershipMode",       "varchar(20) CHARACTER SET utf8mb4 NULL"),
            ("ntf_TenantProviderConfigs", "Priority",            "int NULL"),
        };

        foreach (var (table, col, colDef) in providerColumnsToAdd)
        {
            using var checkCmd2 = conn.CreateCommand();
            checkCmd2.CommandText =
                $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{col}'";
            var count2 = Convert.ToInt32(await checkCmd2.ExecuteScalarAsync());

            if (count2 == 0)
            {
                using var alterCmd2 = conn.CreateCommand();
                alterCmd2.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{col}` {colDef}";
                await alterCmd2.ExecuteNonQueryAsync();
                logger.LogInformation("Added missing column {Table}.{Column}", table, col);
            }
        }

        // ── LS-NOTIF-SMS-002: BlockUnknownSmsPreference on ntf_TenantContactPolicies ──
        var contactPolicyColumnsToAdd = new[]
        {
            ("ntf_TenantContactPolicies", "BlockUnknownSmsPreference", "tinyint(1) NOT NULL DEFAULT 1"),
        };

        foreach (var (table, col, colDef) in contactPolicyColumnsToAdd)
        {
            using var checkCmd3 = conn.CreateCommand();
            checkCmd3.CommandText =
                $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{col}'";
            var count3 = Convert.ToInt32(await checkCmd3.ExecuteScalarAsync());

            if (count3 == 0)
            {
                using var alterCmd3 = conn.CreateCommand();
                alterCmd3.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{col}` {colDef}";
                await alterCmd3.ExecuteNonQueryAsync();
                logger.LogInformation("Added missing column {Table}.{Column}", table, col);
            }
        }

        // ── LS-NOTIF-SMS-007: Reconciliation tracking columns on ntf_NotificationAttempts ──
        var reconciliationColumnsToAdd = new[]
        {
            ("ntf_NotificationAttempts", "LastReconciliationOutcome",          "varchar(100) CHARACTER SET utf8mb4 NULL"),
            ("ntf_NotificationAttempts", "LastReconciledAt",                   "datetime(6) NULL"),
            ("ntf_NotificationAttempts", "LastReconciliationErrorCode",        "varchar(100) CHARACTER SET utf8mb4 NULL"),
            ("ntf_NotificationAttempts", "LastReconciliationProviderStatus",   "varchar(100) CHARACTER SET utf8mb4 NULL"),
            ("ntf_NotificationAttempts", "LastReconciliationNormalizedStatus", "varchar(100) CHARACTER SET utf8mb4 NULL"),
            ("ntf_NotificationAttempts", "ReconciliationAttemptCount",         "int NOT NULL DEFAULT 0"),
        };

        foreach (var (table, col, colDef) in reconciliationColumnsToAdd)
        {
            using var checkCmdR = conn.CreateCommand();
            checkCmdR.CommandText =
                $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{col}'";
            var countR = Convert.ToInt32(await checkCmdR.ExecuteScalarAsync());

            if (countR == 0)
            {
                using var alterCmdR = conn.CreateCommand();
                alterCmdR.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{col}` {colDef}";
                await alterCmdR.ExecuteNonQueryAsync();
                logger.LogInformation("Added missing column {Table}.{Column}", table, col);
            }
        }

        // ── LS-NOTIF-SMS-007: composite index for reconciliation outcome queries ──
        using var reconIdxCheckCmd = conn.CreateCommand();
        reconIdxCheckCmd.CommandText =
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS " +
            $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = 'ntf_NotificationAttempts' " +
            $"AND INDEX_NAME = 'IX_NotificationAttempts_Channel_TenantId_LastReconciliationOutcome'";
        var reconIdxCount = Convert.ToInt32(await reconIdxCheckCmd.ExecuteScalarAsync());
        if (reconIdxCount == 0)
        {
            using var reconIdxCmd = conn.CreateCommand();
            reconIdxCmd.CommandText =
                "CREATE INDEX `IX_NotificationAttempts_Channel_TenantId_LastReconciliationOutcome` " +
                "ON `ntf_NotificationAttempts` (`Channel`, `TenantId`, `LastReconciliationOutcome`)";
            await reconIdxCmd.ExecuteNonQueryAsync();
            logger.LogInformation("Created index IX_NotificationAttempts_Channel_TenantId_LastReconciliationOutcome");
        }

        // ── LS-NOTIF-SMS-002: Ensure ntf_SmsContactPreferences table exists ──────
        // Safety net: if the migration ran but DDL failed (or was pre-seeded), ensure the table exists.
        using var tableCheckCmd = conn.CreateCommand();
        tableCheckCmd.CommandText =
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES " +
            $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = 'ntf_SmsContactPreferences'";
        var tableExists = Convert.ToInt32(await tableCheckCmd.ExecuteScalarAsync()) > 0;

        if (!tableExists)
        {
            using var createTableCmd = conn.CreateCommand();
            createTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS `ntf_SmsContactPreferences` (
                    `Id`                char(36)        CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `TenantId`          char(36)        CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `Phone`             varchar(50)     CHARACTER SET utf8mb4 NOT NULL,
                    `PreferenceState`   varchar(20)     CHARACTER SET utf8mb4 NOT NULL DEFAULT 'unknown',
                    `Source`            varchar(50)     CHARACTER SET utf8mb4 NULL,
                    `Reason`            text            CHARACTER SET utf8mb4 NULL,
                    `KeywordReceived`   varchar(50)     CHARACTER SET utf8mb4 NULL,
                    `ProviderMessageId` varchar(255)    CHARACTER SET utf8mb4 NULL,
                    `UpdatedBy`         varchar(255)    CHARACTER SET utf8mb4 NULL,
                    `CreatedAt`         datetime(6)     NOT NULL,
                    `UpdatedAt`         datetime(6)     NOT NULL,
                    PRIMARY KEY (`Id`),
                    UNIQUE KEY `UX_SmsContactPreferences_TenantId_Phone` (`TenantId`, `Phone`)
                ) CHARACTER SET=utf8mb4;";
            await createTableCmd.ExecuteNonQueryAsync();
            logger.LogInformation("Created missing table ntf_SmsContactPreferences");
        }

        // ── LS-NOTIF-SMS-003: Ensure ntf_SmsPreferenceHistories table exists ────
        using var histTableCheckCmd = conn.CreateCommand();
        histTableCheckCmd.CommandText =
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES " +
            $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = 'ntf_SmsPreferenceHistories'";
        var histTableExists = Convert.ToInt32(await histTableCheckCmd.ExecuteScalarAsync()) > 0;

        if (!histTableExists)
        {
            using var createHistCmd = conn.CreateCommand();
            createHistCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS `ntf_SmsPreferenceHistories` (
                    `Id`                char(36)        CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `TenantId`          char(36)        CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `Phone`             varchar(50)     CHARACTER SET utf8mb4 NOT NULL,
                    `PreviousState`     varchar(20)     CHARACTER SET utf8mb4 NULL,
                    `NewState`          varchar(20)     CHARACTER SET utf8mb4 NOT NULL,
                    `Source`            varchar(50)     CHARACTER SET utf8mb4 NOT NULL,
                    `Reason`            text            CHARACTER SET utf8mb4 NULL,
                    `KeywordReceived`   varchar(50)     CHARACTER SET utf8mb4 NULL,
                    `Provider`          varchar(50)     CHARACTER SET utf8mb4 NULL,
                    `ProviderMessageId` varchar(255)    CHARACTER SET utf8mb4 NULL,
                    `ProviderConfigId`  char(36)        CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `InboundToNumber`   varchar(50)     CHARACTER SET utf8mb4 NULL,
                    `CreatedBy`         varchar(255)    CHARACTER SET utf8mb4 NULL,
                    `MetadataJson`      text            CHARACTER SET utf8mb4 NULL,
                    `CreatedAt`         datetime(6)     NOT NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_SmsPreferenceHistories_TenantId_Phone` (`TenantId`, `Phone`),
                    KEY `IX_SmsPreferenceHistories_Phone` (`Phone`)
                ) CHARACTER SET=utf8mb4;";
            await createHistCmd.ExecuteNonQueryAsync();
            logger.LogInformation("Created missing table ntf_SmsPreferenceHistories");
        }

        // ── LS-NOTIF-SMS-010: Ensure ntf_SmsOperationalAlerts table exists ────────
        // Safety net: if the migration ran but DDL failed (or was pre-seeded), ensure the table exists.
        using var alertTableCheckCmd = conn.CreateCommand();
        alertTableCheckCmd.CommandText =
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES " +
            $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = 'ntf_SmsOperationalAlerts'";
        var alertTableExists = Convert.ToInt32(await alertTableCheckCmd.ExecuteScalarAsync()) > 0;

        if (!alertTableExists)
        {
            using var createAlertTableCmd = conn.CreateCommand();
            createAlertTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS `ntf_SmsOperationalAlerts` (
                    `Id`                    char(36)        CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `AlertType`             varchar(100)    CHARACTER SET utf8mb4 NOT NULL,
                    `Severity`              varchar(20)     CHARACTER SET utf8mb4 NOT NULL DEFAULT 'warning',
                    `TenantId`              char(36)        CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `Provider`              varchar(100)    CHARACTER SET utf8mb4 NULL,
                    `ProviderConfigId`      char(36)        CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `MetricValue`           decimal(18,6)   NOT NULL,
                    `ThresholdValue`        decimal(18,6)   NOT NULL,
                    `Message`               text            CHARACTER SET utf8mb4 NOT NULL,
                    `EvaluationWindowStart` datetime(6)     NOT NULL,
                    `EvaluationWindowEnd`   datetime(6)     NOT NULL,
                    `Status`                varchar(20)     CHARACTER SET utf8mb4 NOT NULL DEFAULT 'active',
                    `OccurrenceCount`       int             NOT NULL DEFAULT 1,
                    `FirstObservedAt`       datetime(6)     NOT NULL,
                    `LastObservedAt`        datetime(6)     NOT NULL,
                    `ResolvedAt`            datetime(6)     NULL,
                    `ResolvedBy`            varchar(255)    CHARACTER SET utf8mb4 NULL,
                    `ResolutionNote`        text            CHARACTER SET utf8mb4 NULL,
                    `SuppressedUntil`       datetime(6)     NULL,
                    `CreatedAt`             datetime(6)     NOT NULL,
                    `UpdatedAt`             datetime(6)     NOT NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_SmsOperationalAlerts_Status_LastObservedAt` (`Status`, `LastObservedAt`),
                    KEY `IX_SmsOperationalAlerts_AlertType_Status_Scope` (`AlertType`, `Status`, `TenantId`, `Provider`, `ProviderConfigId`),
                    KEY `IX_SmsOperationalAlerts_TenantId_Status_CreatedAt` (`TenantId`, `Status`, `CreatedAt`)
                ) CHARACTER SET=utf8mb4;";
            await createAlertTableCmd.ExecuteNonQueryAsync();
            logger.LogInformation("Created missing table ntf_SmsOperationalAlerts");
        }

        // ── LS-NOTIF-SMS-011: Ensure ntf_SmsEscalationPolicies table exists ─────
        using var polTableCheckCmd = conn.CreateCommand();
        polTableCheckCmd.CommandText =
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES " +
            $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = 'ntf_SmsEscalationPolicies'";
        var polTableExists = Convert.ToInt32(await polTableCheckCmd.ExecuteScalarAsync()) > 0;

        if (!polTableExists)
        {
            using var createPolCmd = conn.CreateCommand();
            createPolCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS `ntf_SmsEscalationPolicies` (
                    `Id`              char(36)       CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `Name`            varchar(200)   CHARACTER SET utf8mb4 NOT NULL,
                    `Enabled`         tinyint(1)     NOT NULL DEFAULT 1,
                    `AlertType`       varchar(100)   CHARACTER SET utf8mb4 NULL,
                    `Severity`        varchar(20)    CHARACTER SET utf8mb4 NULL,
                    `TenantId`        char(36)       CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `Provider`        varchar(100)   CHARACTER SET utf8mb4 NULL,
                    `ProviderConfigId` char(36)      CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `ChannelType`     varchar(50)    CHARACTER SET utf8mb4 NOT NULL,
                    `Target`          text           CHARACTER SET utf8mb4 NOT NULL,
                    `TargetDisplay`   varchar(500)   CHARACTER SET utf8mb4 NULL,
                    `CooldownMinutes` int            NOT NULL DEFAULT 60,
                    `RetryEnabled`    tinyint(1)     NOT NULL DEFAULT 0,
                    `MaxRetryCount`   int            NOT NULL DEFAULT 3,
                    `CreatedAt`       datetime(6)    NOT NULL,
                    `UpdatedAt`       datetime(6)    NOT NULL,
                    `CreatedBy`       varchar(255)   CHARACTER SET utf8mb4 NULL,
                    `UpdatedBy`       varchar(255)   CHARACTER SET utf8mb4 NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_SmsEscalationPolicies_Enabled_AlertType`   (`Enabled`, `AlertType`),
                    KEY `IX_SmsEscalationPolicies_Enabled_ChannelType` (`Enabled`, `ChannelType`)
                ) CHARACTER SET=utf8mb4;";
            await createPolCmd.ExecuteNonQueryAsync();
            logger.LogInformation("Created missing table ntf_SmsEscalationPolicies");
        }

        // ── LS-NOTIF-SMS-011: Ensure ntf_SmsAlertEscalations table exists ──────
        using var escTableCheckCmd = conn.CreateCommand();
        escTableCheckCmd.CommandText =
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES " +
            $"WHERE TABLE_SCHEMA = '{dbName}' AND TABLE_NAME = 'ntf_SmsAlertEscalations'";
        var escTableExists = Convert.ToInt32(await escTableCheckCmd.ExecuteScalarAsync()) > 0;

        if (!escTableExists)
        {
            using var createEscCmd = conn.CreateCommand();
            createEscCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS `ntf_SmsAlertEscalations` (
                    `Id`              char(36)       CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `AlertId`         char(36)       CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `PolicyId`        char(36)       CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `ChannelType`     varchar(50)    CHARACTER SET utf8mb4 NOT NULL,
                    `TargetMasked`    varchar(500)   CHARACTER SET utf8mb4 NULL,
                    `Severity`        varchar(20)    CHARACTER SET utf8mb4 NOT NULL DEFAULT 'warning',
                    `Status`          varchar(30)    CHARACTER SET utf8mb4 NOT NULL DEFAULT 'pending',
                    `AttemptCount`    int            NOT NULL DEFAULT 0,
                    `LastAttemptAt`   datetime(6)    NULL,
                    `SentAt`          datetime(6)    NULL,
                    `FailureReason`   text           CHARACTER SET utf8mb4 NULL,
                    `NextRetryAt`     datetime(6)    NULL,
                    `SuppressedUntil` datetime(6)    NULL,
                    `PayloadHash`     varchar(64)    CHARACTER SET utf8mb4 NULL,
                    `MetadataJson`    text           CHARACTER SET utf8mb4 NULL,
                    `CreatedAt`       datetime(6)    NOT NULL,
                    `UpdatedAt`       datetime(6)    NOT NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_SmsAlertEscalations_AlertId`                    (`AlertId`),
                    KEY `IX_SmsAlertEscalations_Status_NextRetryAt`          (`Status`, `NextRetryAt`),
                    KEY `IX_SmsAlertEscalations_AlertId_PolicyId_PayloadHash` (`AlertId`, `PolicyId`, `PayloadHash`),
                    KEY `IX_SmsAlertEscalations_CreatedAt`                  (`CreatedAt`)
                ) CHARACTER SET=utf8mb4;";
            await createEscCmd.ExecuteNonQueryAsync();
            logger.LogInformation("Created missing table ntf_SmsAlertEscalations");
        }

        logger.LogInformation("EnsureNotificationsSchemaColumns complete");
    }
    finally
    {
        if (opened) await conn.CloseAsync();
    }
}

static async Task SeedMigrationHistoryIfNeededAsync(NotificationsDbContext db, ILogger logger)
{
    // These are the migrations whose DDL was applied to the DB before EF
    // migrations were tracking history.  If the history table exists but does
    // not contain them we insert them so MigrateAsync skips re-running them.
    var alreadyApplied = new[]
    {
        ("20260418043535_InitialCreate",        "8.0.2"),
        ("20260419000001_AddRetryFields",       "8.0.2"),
        ("20260508000001_AddSmsPreference",     "8.0.2"),
        ("20260508000002_AddSmsPreferenceHistory",      "8.0.2"),
        ("20260509000001_AddSmsReconciliationTracking", "8.0.2"),
        ("20260510000001_AddSmsOperationalAlerts",      "8.0.2"),
        ("20260510000002_AddSmsEscalation",             "8.0.2"),
    };

    try
    {
        // Ensure the history table exists (idempotent).
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (" +
            "`MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL," +
            "`ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL," +
            "PRIMARY KEY (`MigrationId`)) CHARACTER SET=utf8mb4;");

        foreach (var (id, ver) in alreadyApplied)
        {
            var inserted = await db.Database.ExecuteSqlRawAsync(
                "INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ({0}, {1})",
                id, ver);
            if (inserted > 0)
                logger.LogInformation("Seeded migration history for {MigrationId}", id);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not seed migration history — proceeding anyway");
    }
}

// ── Platform SendGrid provider seeder ─────────────────────────────────────────
// Ensures a single platform-level SendGrid config exists so the control-center
// "Test Outbound Message" page and the providers list work for platform admins
// without any manual setup step.
static async Task SeedPlatformSendGridProviderAsync(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger logger)
{
    var sgApiKey = configuration["SENDGRID_API_KEY"];
    if (string.IsNullOrWhiteSpace(sgApiKey))
    {
        logger.LogInformation("SENDGRID_API_KEY not set — skipping platform provider seed");
        return;
    }

    var repo = services.GetRequiredService<Notifications.Application.Interfaces.ITenantProviderConfigRepository>();

    var platformId   = Notifications.Application.Constants.PlatformProvider.PlatformTenantId;
    var existing     = await repo.GetByTenantAndChannelAsync(platformId, "email");
    var alreadyHasSg = existing.Any(c => c.ProviderType.Equals("sendgrid", StringComparison.OrdinalIgnoreCase));

    if (alreadyHasSg)
    {
        logger.LogInformation("Platform SendGrid provider config already exists — skipping seed");
        return;
    }

    var fromEmail = configuration["SENDGRID_FROM_EMAIL"] ?? "noreply@legalsynq.com";
    var fromName  = configuration["SENDGRID_FROM_NAME"]  ?? "LegalSynq";

    var config = new Notifications.Domain.TenantProviderConfig
    {
        Id              = Guid.NewGuid(),
        TenantId        = platformId,
        Channel         = "email",
        ProviderType    = "sendgrid",
        DisplayName     = "SendGrid (Platform Default)",
        CredentialsJson = JsonSerializer.Serialize(new { apiKey = sgApiKey }),
        SettingsJson    = JsonSerializer.Serialize(new { fromEmail, fromName }),
        Status          = "active",
        ValidationStatus = "valid",
        HealthStatus    = "unknown",
        Priority        = 1,
    };

    await repo.CreateAsync(config);
    logger.LogInformation("Platform SendGrid provider config seeded with id={Id}", config.Id);
}

// ─── Support email template seeder ────────────────────────────────────────────

static async Task SeedSupportEmailTemplatesAsync(IServiceProvider services, ILogger logger)
{
    var templateRepo = services.GetRequiredService<Notifications.Application.Interfaces.ITemplateRepository>();
    var versionRepo  = services.GetRequiredService<Notifications.Application.Interfaces.ITemplateVersionRepository>();

    var templates = new[]
    {
        new SupportEmailTemplateSeed(
            Key:         "support-ticket-created-email",
            Name:        "Support: Ticket Created",
            Subject:     "Support Ticket {{ticket_number}} Submitted: {{title}}",
            HtmlBody:    """
                         <p>Hi,</p>
                         <p>Your support ticket has been received and is being reviewed by our team.</p>
                         <table cellpadding="4" cellspacing="0" style="margin:0">
                           <tr><td style="padding-right:16px"><strong>Ticket</strong></td><td>{{ticket_number}}</td></tr>
                           <tr><td style="padding-right:16px"><strong>Subject</strong></td><td>{{title}}</td></tr>
                           <tr><td style="padding-right:16px"><strong>Priority</strong></td><td>{{priority}}</td></tr>
                           <tr><td style="padding-right:16px"><strong>Status</strong></td><td>{{status}}</td></tr>
                         </table>
                         <p style="margin-top:24px">
                           <a href="{{deeplink_url}}" style="background:#2563eb;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:600">View Ticket</a>
                         </p>
                         """,
            TextBody:    "Your support ticket {{ticket_number}} has been submitted.\nSubject: {{title}}\nPriority: {{priority}}\n\nView it here: {{deeplink_url}}"),

        new SupportEmailTemplateSeed(
            Key:         "support-ticket-status-changed-email",
            Name:        "Support: Ticket Status Changed",
            Subject:     "Ticket {{ticket_number}} Status Updated: {{new_status}}",
            HtmlBody:    """
                         <p>Hi,</p>
                         <p>The status of your support ticket has been updated.</p>
                         <table cellpadding="4" cellspacing="0" style="margin:0">
                           <tr><td style="padding-right:16px"><strong>Ticket</strong></td><td>{{ticket_number}}</td></tr>
                           <tr><td style="padding-right:16px"><strong>Subject</strong></td><td>{{title}}</td></tr>
                           <tr><td style="padding-right:16px"><strong>Previous Status</strong></td><td>{{previous_status}}</td></tr>
                           <tr><td style="padding-right:16px"><strong>New Status</strong></td><td>{{new_status}}</td></tr>
                         </table>
                         <p style="margin-top:24px">
                           <a href="{{deeplink_url}}" style="background:#2563eb;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:600">View Ticket</a>
                         </p>
                         """,
            TextBody:    "Ticket {{ticket_number}} status changed from {{previous_status}} to {{new_status}}.\n\nView it here: {{deeplink_url}}"),

        new SupportEmailTemplateSeed(
            Key:         "support-ticket-comment-added-email",
            Name:        "Support: New Reply",
            Subject:     "New Reply on Ticket {{ticket_number}}: {{title}}",
            HtmlBody:    """
                         <p>Hi,</p>
                         <p>A new reply has been posted on your support ticket.</p>
                         <table cellpadding="4" cellspacing="0" style="margin:0 0 16px">
                           <tr><td style="padding-right:16px"><strong>Ticket</strong></td><td>{{ticket_number}}</td></tr>
                           <tr><td style="padding-right:16px"><strong>Subject</strong></td><td>{{title}}</td></tr>
                           <tr><td style="padding-right:16px"><strong>From</strong></td><td>{{author_display}}</td></tr>
                         </table>
                         <div style="margin:16px 0;padding:16px 20px;background:#f8fafc;border-left:4px solid #2563eb;border-radius:4px;color:#374151;white-space:pre-wrap;word-break:break-word">{{comment_body}}</div>
                         <p style="margin-top:24px">
                           <a href="{{deeplink_url}}" style="background:#2563eb;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:600">View Ticket</a>
                         </p>
                         """,
            TextBody:    "New reply on ticket {{ticket_number}}: {{title}}\nFrom: {{author_display}}\n\n{{comment_body}}\n\nView it here: {{deeplink_url}}"),

        new SupportEmailTemplateSeed(
            Key:         "support-ticket-assigned-email",
            Name:        "Support: Ticket Assigned",
            Subject:     "Support Ticket {{ticket_number}} Has Been Assigned to You",
            HtmlBody:    """
                         <p>Hi,</p>
                         <p>A support ticket has been assigned to you.</p>
                         <table cellpadding="4" cellspacing="0" style="margin:0">
                           <tr><td style="padding-right:16px"><strong>Ticket</strong></td><td>{{ticket_number}}</td></tr>
                           <tr><td style="padding-right:16px"><strong>Subject</strong></td><td>{{title}}</td></tr>
                           <tr><td style="padding-right:16px"><strong>Priority</strong></td><td>{{priority}}</td></tr>
                         </table>
                         <p style="margin-top:24px">
                           <a href="{{deeplink_url}}" style="background:#2563eb;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:600">View Ticket</a>
                         </p>
                         """,
            TextBody:    "Support ticket {{ticket_number}} ({{title}}) has been assigned to you.\n\nView it here: {{deeplink_url}}"),

        new SupportEmailTemplateSeed(
            Key:         "support-ticket-updated-email",
            Name:        "Support: Ticket Updated",
            Subject:     "Support Ticket {{ticket_number}} Updated",
            HtmlBody:    """
                         <p>Hi,</p>
                         <p>Your support ticket has been updated.</p>
                         <table cellpadding="4" cellspacing="0" style="margin:0">
                           <tr><td style="padding-right:16px"><strong>Ticket</strong></td><td>{{ticket_number}}</td></tr>
                           <tr><td style="padding-right:16px"><strong>Subject</strong></td><td>{{title}}</td></tr>
                           <tr><td style="padding-right:16px"><strong>Status</strong></td><td>{{status}}</td></tr>
                         </table>
                         <p style="margin-top:24px">
                           <a href="{{deeplink_url}}" style="background:#2563eb;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:600">View Ticket</a>
                         </p>
                         """,
            TextBody:    "Ticket {{ticket_number}} ({{title}}) has been updated. Status: {{status}}.\n\nView it here: {{deeplink_url}}"),
    };

    foreach (var seed in templates)
    {
        var existing = await templateRepo.FindByKeyAsync(seed.Key, "email", null);
        Guid templateId;

        if (existing != null)
        {
            templateId = existing.Id;
            // Template record exists — check whether a published version was also created.
            var existingVersion = await versionRepo.FindPublishedByTemplateIdAsync(templateId);
            if (existingVersion != null)
            {
                // Update in-place when the seed content has changed (e.g. new tokens added).
                if (existingVersion.SubjectTemplate != seed.Subject
                    || existingVersion.BodyTemplate  != seed.HtmlBody
                    || existingVersion.TextTemplate  != seed.TextBody)
                {
                    existingVersion.SubjectTemplate = seed.Subject;
                    existingVersion.BodyTemplate    = seed.HtmlBody;
                    existingVersion.TextTemplate    = seed.TextBody;
                    await versionRepo.UpdateAsync(existingVersion);
                    logger.LogInformation("Updated support email template: {Key}", seed.Key);
                }
                else
                {
                    logger.LogDebug("Support email template already fully seeded, skipping: {Key}", seed.Key);
                }
                continue;
            }
            logger.LogDebug("Support email template exists but has no published version, creating version: {Key}", seed.Key);
        }
        else
        {
            var template = await templateRepo.CreateAsync(new Notifications.Domain.Template
            {
                Id          = Guid.NewGuid(),
                TenantId    = null,
                TemplateKey = seed.Key,
                Channel     = "email",
                Name        = seed.Name,
                Description = $"Auto-seeded global template for support event {seed.Key}",
                Status      = "active",
                Scope       = "global",
                ProductType = "support",
            });
            templateId = template.Id;
        }

        await versionRepo.CreateAsync(new Notifications.Domain.TemplateVersion
        {
            Id              = Guid.NewGuid(),
            TemplateId      = templateId,
            VersionNumber   = 1,
            SubjectTemplate = seed.Subject,
            BodyTemplate    = seed.HtmlBody,
            TextTemplate    = seed.TextBody,
            EditorType      = "html",
            IsPublished     = true,
            PublishedBy     = "system-seed",
            PublishedAt     = DateTime.UtcNow,
        });

        logger.LogInformation("Seeded support email template: {Key}", seed.Key);
    }
}

file sealed record SupportEmailTemplateSeed(
    string Key,
    string Name,
    string Subject,
    string HtmlBody,
    string TextBody);
