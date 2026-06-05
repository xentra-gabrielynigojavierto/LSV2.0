using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundEmailDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "comms_EmailDeliveryStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConversationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MessageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmailMessageReferenceId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DeliveryStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderMessageId = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NotificationsRequestId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastStatusAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastErrorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastErrorMessage = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comms_EmailDeliveryStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_comms_EmailDeliveryStates_comms_EmailMessageReferences_Email~",
                        column: x => x.EmailMessageReferenceId,
                        principalTable: "comms_EmailMessageReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_comms_EmailDeliveryStates_EmailMessageReferenceId",
                table: "comms_EmailDeliveryStates",
                column: "EmailMessageReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDelivery_TenantId_ConversationId",
                table: "comms_EmailDeliveryStates",
                columns: new[] { "TenantId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDelivery_TenantId_EmailMessageReferenceId",
                table: "comms_EmailDeliveryStates",
                columns: new[] { "TenantId", "EmailMessageReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDelivery_TenantId_NotificationsRequestId",
                table: "comms_EmailDeliveryStates",
                columns: new[] { "TenantId", "NotificationsRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDelivery_TenantId_ProviderMessageId",
                table: "comms_EmailDeliveryStates",
                columns: new[] { "TenantId", "ProviderMessageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comms_EmailDeliveryStates");
        }
    }
}
