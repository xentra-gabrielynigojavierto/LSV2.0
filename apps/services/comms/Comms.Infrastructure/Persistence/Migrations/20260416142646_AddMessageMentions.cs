using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageMentions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sc_MessageMentions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConversationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MessageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MentionedUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MentionedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IsMentionedUserParticipant = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sc_MessageMentions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MessageMentions_TenantId_ConversationId",
                table: "sc_MessageMentions",
                columns: new[] { "TenantId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageMentions_TenantId_MentionedUserId",
                table: "sc_MessageMentions",
                columns: new[] { "TenantId", "MentionedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageMentions_TenantId_MessageId",
                table: "sc_MessageMentions",
                columns: new[] { "TenantId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageMentions_UniquePerMessageUser",
                table: "sc_MessageMentions",
                columns: new[] { "TenantId", "MessageId", "MentionedUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sc_MessageMentions");
        }
    }
}
