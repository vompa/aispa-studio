using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ErinnerungNaechsteBehandlung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketingConsents",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketingConsents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RebookReminders",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AfterAppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AiWritten = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RebookReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RebookReminders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RebookSettings",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RebookSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RebookSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentRebookIntervals",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentRebookIntervals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreatmentRebookIntervals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingConsents_CustomerId",
                schema: "aispa",
                table: "MarketingConsents",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketingConsents_TenantId",
                schema: "aispa",
                table: "MarketingConsents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RebookReminders_AfterAppointmentId",
                schema: "aispa",
                table: "RebookReminders",
                column: "AfterAppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RebookReminders_CustomerId_CreatedAt",
                schema: "aispa",
                table: "RebookReminders",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RebookReminders_TenantId",
                schema: "aispa",
                table: "RebookReminders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RebookSettings_TenantId",
                schema: "aispa",
                table: "RebookSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRebookIntervals_TenantId",
                schema: "aispa",
                table: "TreatmentRebookIntervals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRebookIntervals_TreatmentId",
                schema: "aispa",
                table: "TreatmentRebookIntervals",
                column: "TreatmentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketingConsents",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "RebookReminders",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "RebookSettings",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "TreatmentRebookIntervals",
                schema: "aispa");
        }
    }
}
