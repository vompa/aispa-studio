using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BefristeteZugaenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                schema: "aispa",
                table: "StaffAccess",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AccessExpiresAt",
                schema: "aispa",
                table: "Invitations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                schema: "aispa",
                table: "StaffAccess");

            migrationBuilder.DropColumn(
                name: "AccessExpiresAt",
                schema: "aispa",
                table: "Invitations");
        }
    }
}
