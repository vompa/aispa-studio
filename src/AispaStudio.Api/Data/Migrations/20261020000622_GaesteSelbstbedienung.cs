using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class GaesteSelbstbedienung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestHint",
                schema: "aispa",
                table: "Treatments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAt",
                schema: "aispa",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GuestMoves",
                schema: "aispa",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuestHint",
                schema: "aispa",
                table: "Treatments");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "GuestMoves",
                schema: "aispa",
                table: "Appointments");
        }
    }
}
