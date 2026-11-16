using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeLeaveEntitlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeLeaveEntitlements",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Days = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    CarriedOver = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveEntitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveEntitlements_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveEntitlements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveEntitlements_EmployeeId_Year",
                schema: "aispa",
                table: "EmployeeLeaveEntitlements",
                columns: new[] { "EmployeeId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveEntitlements_TenantId",
                schema: "aispa",
                table: "EmployeeLeaveEntitlements",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeLeaveEntitlements",
                schema: "aispa");
        }
    }
}
