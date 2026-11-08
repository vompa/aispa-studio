using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                schema: "aispa",
                table: "Appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppointmentSeries",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntervalWeeks = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: true),
                    Until = table.Column<DateOnly>(type: "date", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Problem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentSeries_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "aispa",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentSeries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentSeries_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentSeries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentSeries_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_SeriesId",
                schema: "aispa",
                table: "Appointments",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSeries_CustomerId",
                schema: "aispa",
                table: "AppointmentSeries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSeries_EmployeeId",
                schema: "aispa",
                table: "AppointmentSeries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSeries_LocationId",
                schema: "aispa",
                table: "AppointmentSeries",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSeries_TenantId",
                schema: "aispa",
                table: "AppointmentSeries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSeries_TreatmentId",
                schema: "aispa",
                table: "AppointmentSeries",
                column: "TreatmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_AppointmentSeries_SeriesId",
                schema: "aispa",
                table: "Appointments",
                column: "SeriesId",
                principalSchema: "aispa",
                principalTable: "AppointmentSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_AppointmentSeries_SeriesId",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropTable(
                name: "AppointmentSeries",
                schema: "aispa");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_SeriesId",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                schema: "aispa",
                table: "Appointments");
        }
    }
}
