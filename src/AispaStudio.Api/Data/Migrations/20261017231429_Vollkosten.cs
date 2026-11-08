using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Vollkosten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeCosts",
                schema: "aispa",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    HourlyCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCosts", x => x.EmployeeId);
                    table.ForeignKey(
                        name: "FK_EmployeeCosts_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocationCostSettings",
                schema: "aispa",
                columns: table => new
                {
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CleaningHourlyCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DefaultStaffHourlyCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    OtherMonthlyCosts = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PlannedUtilizationPercent = table.Column<int>(type: "integer", nullable: false),
                    StaffProductivityPercent = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationCostSettings", x => x.LocationId);
                    table.ForeignKey(
                        name: "FK_LocationCostSettings_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomCosts",
                schema: "aispa",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CleaningMaterialCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomCosts", x => x.RoomId);
                    table.ForeignKey(
                        name: "FK_RoomCosts_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "aispa",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentMaterials",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreatmentMaterials_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentMaterials_TreatmentId_Position",
                schema: "aispa",
                table: "TreatmentMaterials",
                columns: new[] { "TreatmentId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeCosts",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "LocationCostSettings",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "RoomCosts",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "TreatmentMaterials",
                schema: "aispa");
        }
    }
}
