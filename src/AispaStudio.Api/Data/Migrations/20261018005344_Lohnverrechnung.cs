using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Lohnverrechnung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CashShortageAccount",
                schema: "aispa",
                table: "AccountingSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "4900");

            migrationBuilder.AddColumn<string>(
                name: "CashSurplusAccount",
                schema: "aispa",
                table: "AccountingSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "2700");

            migrationBuilder.CreateTable(
                name: "EmployeePayrolls",
                schema: "aispa",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonnelNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayrolls", x => x.EmployeeId);
                    table.ForeignKey(
                        name: "FK_EmployeePayrolls_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeePayrolls_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollSettings",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    DatevConsultantNumber = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    DatevClientNumber = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    NightStart = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    NightEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    HoursWageType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SundayWageType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    HolidayWageType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    NightWageType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    VacationWageType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SickWageType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TrainingWageType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrolls_TenantId",
                schema: "aispa",
                table: "EmployeePayrolls",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSettings_TenantId",
                schema: "aispa",
                table: "PayrollSettings",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePayrolls",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "PayrollSettings",
                schema: "aispa");

            migrationBuilder.DropColumn(
                name: "CashShortageAccount",
                schema: "aispa",
                table: "AccountingSettings");

            migrationBuilder.DropColumn(
                name: "CashSurplusAccount",
                schema: "aispa",
                table: "AccountingSettings");
        }
    }
}
