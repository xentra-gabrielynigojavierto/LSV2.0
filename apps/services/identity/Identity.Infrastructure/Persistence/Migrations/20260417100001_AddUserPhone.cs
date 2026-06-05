using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the optional Phone column to idt_Users so the membership-lookup
    /// endpoint can surface a primary phone number to the notifications
    /// service. Without this column SMS role/org fan-outs always skip with
    /// reason "no_phone_on_file" because there is no phone to dispatch to.
    /// </summary>
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20260417100001_AddUserPhone")]
    public partial class AddUserPhone : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "idt_Users",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Phone",
                table: "idt_Users");
        }
    }
}
