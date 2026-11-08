using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Chargen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Batch",
                schema: "aispa",
                table: "StockMovements",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpiresOn",
                schema: "aispa",
                table: "StockMovements",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Batch",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ExpiresOn",
                schema: "aispa",
                table: "StockMovements");
        }
    }
}
