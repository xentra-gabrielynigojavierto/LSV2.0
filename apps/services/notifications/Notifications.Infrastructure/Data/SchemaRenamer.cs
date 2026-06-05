using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Notifications.Infrastructure.Data;

#pragma warning disable EF1002

public static class SchemaRenamer
{
    private static readonly (string OldTable, string NewTable)[] TableRenames =
    {
        ("notifications",                    "ntf_Notifications"),
        ("notification_attempts",            "ntf_NotificationAttempts"),
        ("templates",                        "ntf_Templates"),
        ("template_versions",                "ntf_TemplateVersions"),
        ("notification_events",              "ntf_NotificationEvents"),
        ("recipient_contact_health",         "ntf_RecipientContactHealth"),
        ("delivery_issues",                  "ntf_DeliveryIssues"),
        ("contact_suppressions",             "ntf_ContactSuppressions"),
        ("tenant_billing_plans",             "ntf_TenantBillingPlans"),
        ("tenant_billing_rates",             "ntf_TenantBillingRates"),
        ("tenant_rate_limit_policies",       "ntf_TenantRateLimitPolicies"),
        ("tenant_contact_policies",          "ntf_TenantContactPolicies"),
        ("tenant_brandings",                 "ntf_TenantBrandings"),
        ("usage_meter_events",               "ntf_UsageMeterEvents"),
        ("tenant_provider_configs",          "ntf_TenantProviderConfigs"),
        ("tenant_channel_provider_settings", "ntf_TenantChannelProviderSettings"),
        ("provider_health",                  "ntf_ProviderHealth"),
        ("provider_webhook_logs",            "ntf_ProviderWebhookLogs"),

        ("ntf_notifications",                    "ntf_Notifications"),
        ("ntf_notification_attempts",            "ntf_NotificationAttempts"),
        ("ntf_templates",                        "ntf_Templates"),
        ("ntf_template_versions",                "ntf_TemplateVersions"),
        ("ntf_notification_events",              "ntf_NotificationEvents"),
        ("ntf_recipient_contact_health",         "ntf_RecipientContactHealth"),
        ("ntf_delivery_issues",                  "ntf_DeliveryIssues"),
        ("ntf_contact_suppressions",             "ntf_ContactSuppressions"),
        ("ntf_tenant_billing_plans",             "ntf_TenantBillingPlans"),
        ("ntf_tenant_billing_rates",             "ntf_TenantBillingRates"),
        ("ntf_tenant_rate_limit_policies",       "ntf_TenantRateLimitPolicies"),
        ("ntf_tenant_contact_policies",          "ntf_TenantContactPolicies"),
        ("ntf_tenant_brandings",                 "ntf_TenantBrandings"),
        ("ntf_usage_meter_events",               "ntf_UsageMeterEvents"),
        ("ntf_tenant_provider_configs",          "ntf_TenantProviderConfigs"),
        ("ntf_tenant_channel_provider_settings", "ntf_TenantChannelProviderSettings"),
        ("ntf_provider_health",                  "ntf_ProviderHealth"),
        ("ntf_provider_webhook_logs",            "ntf_ProviderWebhookLogs"),
    };

    private static readonly Dictionary<string, string[]> ColumnRenames = new()
    {
        ["ntf_Notifications"] = new[]
        {
            "tenant_id:TenantId", "recipient_json:RecipientJson", "message_json:MessageJson",
            "metadata_json:MetadataJson", "idempotency_key:IdempotencyKey", "provider_used:ProviderUsed",
            "failure_category:FailureCategory", "last_error_message:LastErrorMessage",
            "template_id:TemplateId", "template_version_id:TemplateVersionId", "template_key:TemplateKey",
            "rendered_subject:RenderedSubject", "rendered_body:RenderedBody", "rendered_text:RenderedText",
            "provider_ownership_mode:ProviderOwnershipMode", "provider_config_id:ProviderConfigId",
            "platform_fallback_used:PlatformFallbackUsed", "blocked_by_policy:BlockedByPolicy",
            "blocked_reason_code:BlockedReasonCode", "override_used:OverrideUsed",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_NotificationAttempts"] = new[]
        {
            "tenant_id:TenantId", "notification_id:NotificationId", "attempt_number:AttemptNumber",
            "provider_message_id:ProviderMessageId", "provider_ownership_mode:ProviderOwnershipMode",
            "provider_config_id:ProviderConfigId", "failure_category:FailureCategory",
            "error_message:ErrorMessage", "is_failover:IsFailover", "completed_at:CompletedAt",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_Templates"] = new[]
        {
            "tenant_id:TenantId", "template_key:TemplateKey", "product_type:ProductType",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_TemplateVersions"] = new[]
        {
            "template_id:TemplateId", "version_number:VersionNumber", "subject_template:SubjectTemplate",
            "body_template:BodyTemplate", "text_template:TextTemplate", "editor_type:EditorType",
            "is_published:IsPublished", "published_by:PublishedBy", "published_at:PublishedAt",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_NotificationEvents"] = new[]
        {
            "tenant_id:TenantId", "notification_id:NotificationId",
            "notification_attempt_id:NotificationAttemptId", "raw_event_type:RawEventType",
            "normalized_event_type:NormalizedEventType", "event_timestamp:EventTimestamp",
            "provider_message_id:ProviderMessageId", "metadata_json:MetadataJson",
            "dedup_key:DedupKey", "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_RecipientContactHealth"] = new[]
        {
            "tenant_id:TenantId", "contact_value:ContactValue", "health_status:HealthStatus",
            "bounce_count:BounceCount", "complaint_count:ComplaintCount", "delivery_count:DeliveryCount",
            "last_bounce_at:LastBounceAt", "last_complaint_at:LastComplaintAt",
            "last_delivery_at:LastDeliveryAt", "last_raw_event_type:LastRawEventType",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_DeliveryIssues"] = new[]
        {
            "tenant_id:TenantId", "notification_id:NotificationId",
            "notification_attempt_id:NotificationAttemptId", "issue_type:IssueType",
            "recommended_action:RecommendedAction", "details_json:DetailsJson",
            "is_resolved:IsResolved", "resolved_at:ResolvedAt",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_ContactSuppressions"] = new[]
        {
            "tenant_id:TenantId", "contact_value:ContactValue", "suppression_type:SuppressionType",
            "expires_at:ExpiresAt", "created_by:CreatedBy",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_TenantBillingPlans"] = new[]
        {
            "tenant_id:TenantId", "plan_name:PlanName", "billing_mode:BillingMode",
            "monthly_flat_rate:MonthlyFlatRate", "effective_from:EffectiveFrom",
            "effective_to:EffectiveTo", "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_TenantBillingRates"] = new[]
        {
            "billing_plan_id:BillingPlanId", "usage_unit:UsageUnit",
            "provider_ownership_mode:ProviderOwnershipMode", "unit_price:UnitPrice",
            "is_billable:IsBillable", "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_TenantRateLimitPolicies"] = new[]
        {
            "tenant_id:TenantId", "max_requests_per_minute:MaxRequestsPerMinute",
            "max_attempts_per_minute:MaxAttemptsPerMinute", "max_daily_usage:MaxDailyUsage",
            "max_monthly_usage:MaxMonthlyUsage", "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_TenantContactPolicies"] = new[]
        {
            "tenant_id:TenantId", "block_suppressed_contacts:BlockSuppressedContacts",
            "block_unsubscribed_contacts:BlockUnsubscribedContacts",
            "block_complained_contacts:BlockComplainedContacts",
            "block_bounced_contacts:BlockBouncedContacts", "block_invalid_contacts:BlockInvalidContacts",
            "block_carrier_rejected_contacts:BlockCarrierRejectedContacts",
            "allow_manual_override:AllowManualOverride",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_TenantBrandings"] = new[]
        {
            "tenant_id:TenantId", "product_type:ProductType", "brand_name:BrandName",
            "logo_url:LogoUrl", "primary_color:PrimaryColor", "secondary_color:SecondaryColor",
            "accent_color:AccentColor", "text_color:TextColor", "background_color:BackgroundColor",
            "button_radius:ButtonRadius", "font_family:FontFamily", "support_email:SupportEmail",
            "support_phone:SupportPhone", "website_url:WebsiteUrl",
            "email_header_html:EmailHeaderHtml", "email_footer_html:EmailFooterHtml",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_UsageMeterEvents"] = new[]
        {
            "tenant_id:TenantId", "notification_id:NotificationId",
            "notification_attempt_id:NotificationAttemptId",
            "provider_ownership_mode:ProviderOwnershipMode", "provider_config_id:ProviderConfigId",
            "usage_unit:UsageUnit", "is_billable:IsBillable",
            "provider_unit_cost:ProviderUnitCost", "provider_total_cost:ProviderTotalCost",
            "metadata_json:MetadataJson", "occurred_at:OccurredAt",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_TenantProviderConfigs"] = new[]
        {
            "tenant_id:TenantId", "provider_type:ProviderType", "display_name:DisplayName",
            "credentials_json:CredentialsJson", "settings_json:SettingsJson",
            "validation_status:ValidationStatus", "validation_message:ValidationMessage",
            "last_validated_at:LastValidatedAt", "health_status:HealthStatus",
            "last_health_check_at:LastHealthCheckAt", "health_check_latency_ms:HealthCheckLatencyMs",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_TenantChannelProviderSettings"] = new[]
        {
            "tenant_id:TenantId", "provider_mode:ProviderMode",
            "primary_tenant_provider_config_id:PrimaryTenantProviderConfigId",
            "fallback_tenant_provider_config_id:FallbackTenantProviderConfigId",
            "allow_platform_fallback:AllowPlatformFallback",
            "allow_automatic_failover:AllowAutomaticFailover",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_ProviderHealth"] = new[]
        {
            "provider_type:ProviderType", "ownership_mode:OwnershipMode",
            "tenant_provider_config_id:TenantProviderConfigId", "health_status:HealthStatus",
            "consecutive_failures:ConsecutiveFailures", "consecutive_successes:ConsecutiveSuccesses",
            "last_latency_ms:LastLatencyMs", "last_check_at:LastCheckAt",
            "last_failure_at:LastFailureAt", "last_recovery_at:LastRecoveryAt",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
        ["ntf_ProviderWebhookLogs"] = new[]
        {
            "request_headers_json:RequestHeadersJson", "payload_json:PayloadJson",
            "signature_verified:SignatureVerified", "processing_status:ProcessingStatus",
            "error_message:ErrorMessage", "received_at:ReceivedAt",
            "created_at:CreatedAt", "updated_at:UpdatedAt"
        },
    };

    private static readonly (string Table, string OldIndex, string NewIndex)[] IndexRenames =
    {
        ("ntf_Notifications", "uq_notifications_tenant_idempotency", "UX_Notifications_TenantId_IdempotencyKey"),
        ("ntf_NotificationAttempts", "idx_attempts_notification_id", "IX_NotificationAttempts_NotificationId"),
        ("ntf_NotificationAttempts", "idx_attempts_provider_message_id", "IX_NotificationAttempts_ProviderMessageId"),
        ("ntf_Templates", "idx_templates_key_channel_tenant", "IX_Templates_TemplateKey_Channel_TenantId"),
        ("ntf_TemplateVersions", "idx_template_versions_template_id", "IX_TemplateVersions_TemplateId"),
        ("ntf_NotificationEvents", "idx_events_dedup_key", "UX_NotificationEvents_DedupKey"),
        ("ntf_NotificationEvents", "idx_events_notification_id", "IX_NotificationEvents_NotificationId"),
        ("ntf_RecipientContactHealth", "uq_recipient_contact_health", "UX_RecipientContactHealth_TenantId_Channel_ContactValue"),
        ("ntf_DeliveryIssues", "idx_delivery_issues_tenant_notification", "IX_DeliveryIssues_TenantId_NotificationId"),
        ("ntf_ContactSuppressions", "idx_suppressions_tenant_channel_contact", "IX_ContactSuppressions_TenantId_Channel_ContactValue"),
        ("ntf_TenantProviderConfigs", "idx_tenant_provider_configs_tenant_channel", "IX_TenantProviderConfigs_TenantId_Channel"),
        ("ntf_TenantChannelProviderSettings", "uq_tenant_channel_settings", "UX_TenantChannelProviderSettings_TenantId_Channel"),
        ("ntf_TenantBrandings", "uq_tenant_branding", "UX_TenantBrandings_TenantId_ProductType"),
        ("ntf_UsageMeterEvents", "idx_usage_meter_events_tenant_unit_time", "IX_UsageMeterEvents_TenantId_UsageUnit_OccurredAt"),
    };

    private static readonly Dictionary<string, (string Column, string SqlType, string Default)[]> MissingColumns = new()
    {
        ["ntf_RecipientContactHealth"] = new[]
        {
            ("Channel",          "varchar(20) CHARACTER SET utf8mb4 NOT NULL",  "''"),
            ("HealthStatus",     "varchar(30) CHARACTER SET utf8mb4 NOT NULL",  "'valid'"),
            ("BounceCount",      "int NOT NULL",                                "0"),
            ("ComplaintCount",   "int NOT NULL",                                "0"),
            ("DeliveryCount",    "int NOT NULL",                                "0"),
            ("LastBounceAt",     "datetime(6) NULL",                            ""),
            ("LastComplaintAt",  "datetime(6) NULL",                            ""),
            ("LastDeliveryAt",   "datetime(6) NULL",                            ""),
            ("LastRawEventType", "varchar(100) CHARACTER SET utf8mb4 NULL",     ""),
        },
        ["ntf_ProviderHealth"] = new[]
        {
            ("ProviderType", "varchar(50) NOT NULL", "''"),
            ("Channel", "varchar(20) NOT NULL", "''"),
            ("OwnershipMode", "varchar(20) NOT NULL", "'platform'"),
            ("TenantProviderConfigId", "char(36) NULL", ""),
            ("HealthStatus", "varchar(20) NOT NULL", "'healthy'"),
            ("ConsecutiveFailures", "int NOT NULL", "0"),
            ("ConsecutiveSuccesses", "int NOT NULL", "0"),
            ("LastLatencyMs", "int NULL", ""),
            ("LastCheckAt", "datetime(6) NULL", ""),
            ("LastFailureAt", "datetime(6) NULL", ""),
            ("LastRecoveryAt", "datetime(6) NULL", ""),
            ("CreatedAt", "datetime(6) NOT NULL", "CURRENT_TIMESTAMP(6)"),
            ("UpdatedAt", "datetime(6) NOT NULL", "CURRENT_TIMESTAMP(6)"),
        },
        // ntf_Notifications — columns added by InitialCreate, AddRetryFields, AddCategoryAndSeverity
        ["ntf_Notifications"] = new[]
        {
            ("Channel",              "varchar(20) CHARACTER SET utf8mb4 NOT NULL",  "''"),
            ("Status",               "varchar(30) CHARACTER SET utf8mb4 NOT NULL",  "'accepted'"),
            ("RecipientJson",        "text CHARACTER SET utf8mb4 NOT NULL",          "''"),
            ("MessageJson",          "longtext CHARACTER SET utf8mb4 NOT NULL",      "''"),
            ("MetadataJson",         "longtext CHARACTER SET utf8mb4 NULL",          ""),
            ("IdempotencyKey",       "varchar(255) CHARACTER SET utf8mb4 NULL",      ""),
            ("ProviderUsed",         "varchar(100) CHARACTER SET utf8mb4 NULL",      ""),
            ("FailureCategory",      "varchar(100) CHARACTER SET utf8mb4 NULL",      ""),
            ("LastErrorMessage",     "text CHARACTER SET utf8mb4 NULL",              ""),
            ("TemplateId",           "char(36) NULL",                                ""),
            ("TemplateVersionId",    "char(36) NULL",                                ""),
            ("TemplateKey",          "varchar(200) CHARACTER SET utf8mb4 NULL",      ""),
            ("RenderedSubject",      "varchar(500) CHARACTER SET utf8mb4 NULL",      ""),
            ("RenderedBody",         "longtext CHARACTER SET utf8mb4 NULL",          ""),
            ("RenderedText",         "longtext CHARACTER SET utf8mb4 NULL",          ""),
            ("ProviderOwnershipMode","varchar(50) CHARACTER SET utf8mb4 NULL",       ""),
            ("ProviderConfigId",     "char(36) NULL",                                ""),
            ("PlatformFallbackUsed", "tinyint(1) NOT NULL",                          "0"),
            ("BlockedByPolicy",      "tinyint(1) NOT NULL",                          "0"),
            ("BlockedReasonCode",    "varchar(100) CHARACTER SET utf8mb4 NULL",      ""),
            ("OverrideUsed",         "tinyint(1) NOT NULL",                          "0"),
            ("RetryCount",           "int NOT NULL",                                 "0"),
            ("MaxRetries",           "int NOT NULL",                                 "3"),
            ("NextRetryAt",          "datetime(6) NULL",                             ""),
            ("Severity",             "varchar(50) CHARACTER SET utf8mb4 NULL",       ""),
            ("Category",             "varchar(100) CHARACTER SET utf8mb4 NULL",      ""),
            ("CreatedAt",            "datetime(6) NOT NULL",                         "CURRENT_TIMESTAMP(6)"),
            ("UpdatedAt",            "datetime(6) NOT NULL",                         "CURRENT_TIMESTAMP(6)"),
        },
        // ntf_NotificationAttempts — columns added by InitialCreate
        ["ntf_NotificationAttempts"] = new[]
        {
            ("Channel",              "varchar(20) CHARACTER SET utf8mb4 NOT NULL",  "''"),
            ("Provider",             "varchar(100) CHARACTER SET utf8mb4 NOT NULL", "''"),
            ("Status",               "varchar(20) CHARACTER SET utf8mb4 NOT NULL",  "'pending'"),
            ("AttemptNumber",        "int NOT NULL",                                 "1"),
            ("ProviderMessageId",    "varchar(500) CHARACTER SET utf8mb4 NULL",      ""),
            ("ProviderOwnershipMode","varchar(50) CHARACTER SET utf8mb4 NULL",       ""),
            ("ProviderConfigId",     "char(36) NULL",                                ""),
            ("FailureCategory",      "varchar(100) CHARACTER SET utf8mb4 NULL",      ""),
            ("ErrorMessage",         "text CHARACTER SET utf8mb4 NULL",              ""),
            ("IsFailover",           "tinyint(1) NOT NULL",                          "0"),
            ("CompletedAt",          "datetime(6) NULL",                             ""),
            ("CreatedAt",            "datetime(6) NOT NULL",                         "CURRENT_TIMESTAMP(6)"),
            ("UpdatedAt",            "datetime(6) NOT NULL",                         "CURRENT_TIMESTAMP(6)"),
        },
    };

    public static async Task RenameSchemaAsync(NotificationsDbContext db, ILogger logger)
    {
        var connection = db.Database.GetDbConnection();
        var dbName = connection.Database;
        await db.Database.OpenConnectionAsync();

        try
        {
            await RenameTablesAsync(db, dbName, logger);
            await RenameColumnsAsync(db, dbName, logger);
            await RenameIndexesAsync(db, dbName, logger);
            await EnsureMissingColumnsAsync(db, dbName, logger);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task RenameTablesAsync(NotificationsDbContext db, string dbName, ILogger logger)
    {
        foreach (var (oldName, newName) in TableRenames)
        {
            if (oldName == newName) continue;

            var oldExists = await TableExistsAsync(db, dbName, oldName);
            var newExists = await TableExistsAsync(db, dbName, newName);

            if (oldExists && !newExists)
            {
                await db.Database.ExecuteSqlRawAsync($"RENAME TABLE `{oldName}` TO `{newName}`");
                logger.LogInformation("Renamed table {Old} → {New}", oldName, newName);
            }
        }
    }

    private static async Task RenameColumnsAsync(NotificationsDbContext db, string dbName, ILogger logger)
    {
        foreach (var (tableName, columns) in ColumnRenames)
        {
            if (!await TableExistsAsync(db, dbName, tableName)) continue;

            var existingColumns = await GetColumnNamesAsync(db, dbName, tableName);

            foreach (var mapping in columns)
            {
                var parts = mapping.Split(':');
                var oldCol = parts[0];
                var newCol = parts[1];

                if (existingColumns.Contains(oldCol) && !existingColumns.Contains(newCol))
                {
                    try
                    {
                        await db.Database.ExecuteSqlRawAsync(
                            $"ALTER TABLE `{tableName}` RENAME COLUMN `{oldCol}` TO `{newCol}`");
                        logger.LogInformation("Renamed column {Table}.{Old} → {New}", tableName, oldCol, newCol);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to rename column {Table}.{Old} → {New}", tableName, oldCol, newCol);
                    }
                }
            }
        }
    }

    private static async Task RenameIndexesAsync(NotificationsDbContext db, string dbName, ILogger logger)
    {
        foreach (var (tableName, oldIndex, newIndex) in IndexRenames)
        {
            if (!await TableExistsAsync(db, dbName, tableName)) continue;

            var indexExists = await IndexExistsAsync(db, dbName, tableName, oldIndex);
            if (indexExists)
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync(
                        $"ALTER TABLE `{tableName}` RENAME INDEX `{oldIndex}` TO `{newIndex}`");
                    logger.LogInformation("Renamed index {Table}.{Old} → {New}", tableName, oldIndex, newIndex);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to rename index {Table}.{Old} → {New}", tableName, oldIndex, newIndex);
                }
            }
        }
    }

    private static async Task<bool> IndexExistsAsync(NotificationsDbContext db, string dbName, string tableName, string indexName)
    {
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $@"SELECT COUNT(*) FROM information_schema.statistics
            WHERE table_schema = '{dbName}' AND table_name = '{tableName}' AND index_name = '{indexName}'";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }

    private static async Task<bool> TableExistsAsync(NotificationsDbContext db, string dbName, string tableName)
    {
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $@"SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = '{dbName}' AND table_name = '{tableName}'";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(NotificationsDbContext db, string dbName, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $@"SELECT COLUMN_NAME FROM information_schema.columns
            WHERE table_schema = '{dbName}' AND table_name = '{tableName}'";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private static async Task EnsureMissingColumnsAsync(NotificationsDbContext db, string dbName, ILogger logger)
    {
        foreach (var (tableName, columns) in MissingColumns)
        {
            if (!await TableExistsAsync(db, dbName, tableName)) continue;

            var existingColumns = await GetColumnNamesAsync(db, dbName, tableName);

            foreach (var (colName, sqlType, defaultValue) in columns)
            {
                if (existingColumns.Contains(colName)) continue;

                try
                {
                    var defaultClause = string.IsNullOrEmpty(defaultValue) ? "" : $" DEFAULT {defaultValue}";
                    await db.Database.ExecuteSqlRawAsync(
                        $"ALTER TABLE `{tableName}` ADD COLUMN `{colName}` {sqlType}{defaultClause}");
                    logger.LogInformation("Added missing column {Table}.{Column}", tableName, colName);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to add column {Table}.{Column}", tableName, colName);
                }
            }
        }
    }
}
