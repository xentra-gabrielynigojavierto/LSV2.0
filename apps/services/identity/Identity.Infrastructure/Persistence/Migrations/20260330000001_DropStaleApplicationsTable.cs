using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropStaleApplicationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Applications table was created in identity_db by an accidental
            // Fund service migration run. All application data lives in fund_db.
            // This table is safe to drop — it holds no authoritative data.
            migrationBuilder.Sql("DROP TABLE IF EXISTS `Applications`;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cannot restore data that was never legitimate in this database.
            // Down is intentionally a no-op.
        }
    }
}
