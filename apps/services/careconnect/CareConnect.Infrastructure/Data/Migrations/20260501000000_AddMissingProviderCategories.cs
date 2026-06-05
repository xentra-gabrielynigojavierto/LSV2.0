using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    public partial class AddMissingProviderCategories : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "cc_Categories",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000006"), "EXTREM",  new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Extremities"   },
                    { new Guid("40000000-0000-0000-0000-000000000007"), "SPINE",   new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Spine Surgeon" },
                    { new Guid("40000000-0000-0000-0000-000000000008"), "NEURO",   new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Neurology"     },
                    { new Guid("40000000-0000-0000-0000-000000000009"), "SURGERY", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Surgery Center" },
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "cc_Categories",
                keyColumn: "Id",
                keyValues: new object[]
                {
                    new Guid("40000000-0000-0000-0000-000000000006"),
                    new Guid("40000000-0000-0000-0000-000000000007"),
                    new Guid("40000000-0000-0000-0000-000000000008"),
                    new Guid("40000000-0000-0000-0000-000000000009"),
                });
        }
    }
}
