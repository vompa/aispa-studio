using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TagesbetriebBereitstellenUndHandbuch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StockItemId",
                schema: "aispa",
                table: "TreatmentPreparationSteps",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HandbookEntries",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Area = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RemindAtDayStart = table.Column<bool>(type: "boolean", nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandbookEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandbookEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentExtras",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Timing = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "OnArrival"),
                    MinutesBeforeEnd = table.Column<int>(type: "integer", nullable: true),
                    StockItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentExtras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreatmentExtras_StockItems_StockItemId",
                        column: x => x.StockItemId,
                        principalSchema: "aispa",
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentExtras_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentExtras_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentExtraChecks",
                schema: "aispa",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentExtraChecks", x => new { x.AppointmentId, x.ExtraId });
                    table.ForeignKey(
                        name: "FK_AppointmentExtraChecks_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "aispa",
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppointmentExtraChecks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentExtraChecks_TreatmentExtras_ExtraId",
                        column: x => x.ExtraId,
                        principalSchema: "aispa",
                        principalTable: "TreatmentExtras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPreparationSteps_StockItemId",
                schema: "aispa",
                table: "TreatmentPreparationSteps",
                column: "StockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentExtraChecks_ExtraId",
                schema: "aispa",
                table: "AppointmentExtraChecks",
                column: "ExtraId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentExtraChecks_TenantId",
                schema: "aispa",
                table: "AppointmentExtraChecks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HandbookEntries_TenantId",
                schema: "aispa",
                table: "HandbookEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentExtras_StockItemId",
                schema: "aispa",
                table: "TreatmentExtras",
                column: "StockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentExtras_TenantId",
                schema: "aispa",
                table: "TreatmentExtras",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentExtras_TreatmentId_Position",
                schema: "aispa",
                table: "TreatmentExtras",
                columns: new[] { "TreatmentId", "Position" });

            migrationBuilder.AddForeignKey(
                name: "FK_TreatmentPreparationSteps_StockItems_StockItemId",
                schema: "aispa",
                table: "TreatmentPreparationSteps",
                column: "StockItemId",
                principalSchema: "aispa",
                principalTable: "StockItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TreatmentPreparationSteps_StockItems_StockItemId",
                schema: "aispa",
                table: "TreatmentPreparationSteps");

            migrationBuilder.DropTable(
                name: "AppointmentExtraChecks",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "HandbookEntries",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "TreatmentExtras",
                schema: "aispa");

            migrationBuilder.DropIndex(
                name: "IX_TreatmentPreparationSteps_StockItemId",
                schema: "aispa",
                table: "TreatmentPreparationSteps");

            migrationBuilder.DropColumn(
                name: "StockItemId",
                schema: "aispa",
                table: "TreatmentPreparationSteps");
        }
    }
}
