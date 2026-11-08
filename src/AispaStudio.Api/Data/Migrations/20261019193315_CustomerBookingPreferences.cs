using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerBookingPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PreferredEmployeeFixed",
                schema: "aispa",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PreferredEmployeeId",
                schema: "aispa",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "PreferredFrom",
                schema: "aispa",
                table: "Customers",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "PreferredUntil",
                schema: "aispa",
                table: "Customers",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreferredWeekdays",
                schema: "aispa",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredEmployeeFixed",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PreferredEmployeeId",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PreferredFrom",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PreferredUntil",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PreferredWeekdays",
                schema: "aispa",
                table: "Customers");
        }
    }
}
