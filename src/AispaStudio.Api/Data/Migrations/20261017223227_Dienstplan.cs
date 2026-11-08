using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Dienstplan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RosterDays",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RosterDays_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RosterDayEntries",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RosterDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Start = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    End = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterDayEntries", x => x.Id);
                    table.CheckConstraint("CK_RosterDayEntries_Work", "(\"Kind\" = 'Work') = (\"LocationId\" IS NOT NULL AND \"Start\" IS NOT NULL AND \"End\" IS NOT NULL AND \"Start\" < \"End\")");
                    table.ForeignKey(
                        name: "FK_RosterDayEntries_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterDayEntries_RosterDays_RosterDayId",
                        column: x => x.RosterDayId,
                        principalSchema: "aispa",
                        principalTable: "RosterDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RosterDayEntries_LocationId",
                schema: "aispa",
                table: "RosterDayEntries",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterDayEntries_RosterDayId",
                schema: "aispa",
                table: "RosterDayEntries",
                column: "RosterDayId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterDays_EmployeeId_Date",
                schema: "aispa",
                table: "RosterDays",
                columns: new[] { "EmployeeId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RosterDayEntries",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "RosterDays",
                schema: "aispa");
        }
    }
}
