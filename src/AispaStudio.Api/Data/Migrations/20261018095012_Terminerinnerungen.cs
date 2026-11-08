using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Terminerinnerungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentReminders",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    AppointmentStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentReminders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReminderSettings",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    FirstHoursBefore = table.Column<int>(type: "integer", nullable: false),
                    SecondHoursBefore = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WhatsAppTemplate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WhatsAppTemplateLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReminderSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentReminders_AppointmentId_Stage_AppointmentStart",
                schema: "aispa",
                table: "AppointmentReminders",
                columns: new[] { "AppointmentId", "Stage", "AppointmentStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentReminders_TenantId",
                schema: "aispa",
                table: "AppointmentReminders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderSettings_TenantId",
                schema: "aispa",
                table: "ReminderSettings",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentReminders",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "ReminderSettings",
                schema: "aispa");
        }
    }
}
