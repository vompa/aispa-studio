using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class GuestLanguagesAndSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Languages",
                schema: "aispa",
                table: "Employees",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "aispa",
                table: "Customers",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "aispa",
                table: "Appointments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Reception");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Languages",
                schema: "aispa",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "aispa",
                table: "Appointments");
        }
    }
}
