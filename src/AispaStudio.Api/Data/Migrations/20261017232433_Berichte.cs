using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Berichte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentFollowUps",
                schema: "aispa",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentFollowUps", x => x.AppointmentId);
                    table.ForeignKey(
                        name: "FK_AppointmentFollowUps_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "aispa",
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPreferences",
                schema: "aispa",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pressure = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Scent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RoomTemperature = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Music = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PrefersQuiet = table.Column<bool>(type: "boolean", nullable: true),
                    FocusAreas = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AvoidAreas = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Other = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPreferences", x => x.CustomerId);
                    table.ForeignKey(
                        name: "FK_CustomerPreferences_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "aispa",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentPreparationSteps",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MinutesBefore = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentPreparationSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreatmentPreparationSteps_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentPreparationChecks",
                schema: "aispa",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentPreparationChecks", x => new { x.AppointmentId, x.StepId });
                    table.ForeignKey(
                        name: "FK_AppointmentPreparationChecks_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "aispa",
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppointmentPreparationChecks_TreatmentPreparationSteps_Step~",
                        column: x => x.StepId,
                        principalSchema: "aispa",
                        principalTable: "TreatmentPreparationSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentPreparationChecks_StepId",
                schema: "aispa",
                table: "AppointmentPreparationChecks",
                column: "StepId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPreparationSteps_TreatmentId_Position",
                schema: "aispa",
                table: "TreatmentPreparationSteps",
                columns: new[] { "TreatmentId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentFollowUps",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "AppointmentPreparationChecks",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "CustomerPreferences",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "TreatmentPreparationSteps",
                schema: "aispa");
        }
    }
}
