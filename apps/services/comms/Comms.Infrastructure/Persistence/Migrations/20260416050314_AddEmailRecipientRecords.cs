using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailRecipientRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "comms_EmailRecipientRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConversationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmailMessageReferenceId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ParticipantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    NormalizedEmail = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecipientType = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecipientVisibility = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsResolvedToParticipant = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RecipientSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comms_EmailRecipientRecords", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_TenantId_ConversationId",
                table: "comms_EmailRecipientRecords",
                columns: new[] { "TenantId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_TenantId_EmailMessageReferenceId",
                table: "comms_EmailRecipientRecords",
                columns: new[] { "TenantId", "EmailMessageReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_TenantId_EmailMessageReferenceId_Visibility",
                table: "comms_EmailRecipientRecords",
                columns: new[] { "TenantId", "EmailMessageReferenceId", "RecipientVisibility" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_TenantId_NormalizedEmail",
                table: "comms_EmailRecipientRecords",
                columns: new[] { "TenantId", "NormalizedEmail" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comms_EmailRecipientRecords");
        }
    }
}
