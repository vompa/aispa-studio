using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class GuestMessageDelay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GuestMessageDelayMinutes",
                schema: "aispa",
                table: "Locations",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledFor",
                schema: "aispa",
                table: "ClosureNotices",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuestMessageDelayMinutes",
                schema: "aispa",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ScheduledFor",
                schema: "aispa",
                table: "ClosureNotices");
        }
    }
}
