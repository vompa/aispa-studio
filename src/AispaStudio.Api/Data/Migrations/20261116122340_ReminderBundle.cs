using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReminderBundle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BundledWithId",
                schema: "aispa",
                table: "AppointmentReminders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentReminders_BundledWithId",
                schema: "aispa",
                table: "AppointmentReminders",
                column: "BundledWithId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppointmentReminders_BundledWithId",
                schema: "aispa",
                table: "AppointmentReminders");

            migrationBuilder.DropColumn(
                name: "BundledWithId",
                schema: "aispa",
                table: "AppointmentReminders");
        }
    }
}
