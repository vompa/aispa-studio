using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HeldStart",
                schema: "aispa",
                table: "InboundMails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HeldSince",
                schema: "aispa",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HoldAnsweredAt",
                schema: "aispa",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HoldMailId",
                schema: "aispa",
                table: "Appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HoldRemindAt",
                schema: "aispa",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeldStart",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "HeldSince",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "HoldAnsweredAt",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "HoldMailId",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "HoldRemindAt",
                schema: "aispa",
                table: "Appointments");
        }
    }
}
