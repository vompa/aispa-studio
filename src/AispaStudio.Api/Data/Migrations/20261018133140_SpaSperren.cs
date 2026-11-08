using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SpaSperren : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LockReason",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedAt",
                schema: "aispa",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockReason",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                schema: "aispa",
                table: "Tenants");
        }
    }
}
