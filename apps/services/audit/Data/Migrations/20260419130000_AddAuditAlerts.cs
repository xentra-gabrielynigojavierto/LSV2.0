using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformAuditEventService.Data.Migrations;

/// <summary>
/// Adds the aud_AuditAlerts table for the Audit Alerting Engine (LS-ID-TNT-017-008-02).
///
/// The table stores durable alert records created from anomaly detection results.
/// It supports:
///   - Alert deduplication via the Fingerprint column (SHA-256 hex, indexed)
///   - Lifecycle management (Open → Acknowledged → Resolved)
///   - Tenant isolation via TenantId
///   - Platform-wide alerts via ScopeType = "Platform" (TenantId null)
///   - Detection history (FirstDetectedAtUtc, LastDetectedAtUtc, DetectionCount)
///
/// Applies to MySQL production databases. Development SQLite uses EnsureCreated()
/// which derives the schema from the EF Core model directly.
/// </summary>
[Migration("20260419130000_AddAuditAlerts")]
public partial class AddAuditAlerts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "aud_AuditAlerts",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                AlertId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                RuleKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                Fingerprint = table.Column<string>(type: "char(64)", nullable: false, collation: "ascii_general_ci"),
                ScopeType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                TenantId = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                Severity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false, defaultValue: (byte)0),
                Title = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                ContextJson = table.Column<string>(type: "text", nullable: true),
                DrillDownPath = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true),
                FirstDetectedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                LastDetectedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                DetectionCount = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                AcknowledgedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                ResolvedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_aud_AuditAlerts", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_AuditAlerts_AlertId_Unique",
            table: "aud_AuditAlerts",
            column: "AlertId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AuditAlerts_Fingerprint",
            table: "aud_AuditAlerts",
            column: "Fingerprint");

        migrationBuilder.CreateIndex(
            name: "IX_AuditAlerts_Status",
            table: "aud_AuditAlerts",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_AuditAlerts_TenantId_Status",
            table: "aud_AuditAlerts",
            columns: ["TenantId", "Status"]);

        migrationBuilder.CreateIndex(
            name: "IX_AuditAlerts_FirstDetectedAtUtc",
            table: "aud_AuditAlerts",
            column: "FirstDetectedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "aud_AuditAlerts");
    }
}
