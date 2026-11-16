using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Online.Migrations
{
    /// <inheritdoc />
    public partial class FristErinnerung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderSentAt",
                schema: "online",
                table: "Deposits",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderSentAt",
                schema: "online",
                table: "CardGuarantees",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                schema: "online",
                table: "Deposits");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                schema: "online",
                table: "CardGuarantees");
        }
    }
}
