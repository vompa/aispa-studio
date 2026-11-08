using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaffRegulations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffNotes",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ShowAsSymbol = table.Column<bool>(type: "boolean", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffNotes_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffNotes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffPowers",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    GrantedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffPowers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffPowers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffPowers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffRules",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    LatestEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    MaxHours = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    DaysOff = table.Column<int[]>(type: "integer[]", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffRules_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffNotes_EmployeeId",
                schema: "hr",
                table: "StaffNotes",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffNotes_TenantId",
                schema: "hr",
                table: "StaffNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPowers_EmployeeId",
                schema: "hr",
                table: "StaffPowers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPowers_Kind_EmployeeId",
                schema: "hr",
                table: "StaffPowers",
                columns: new[] { "Kind", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffPowers_TenantId",
                schema: "hr",
                table: "StaffPowers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffRules_EmployeeId",
                schema: "hr",
                table: "StaffRules",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffRules_TenantId",
                schema: "hr",
                table: "StaffRules",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffNotes",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "StaffPowers",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "StaffRules",
                schema: "hr");
        }
    }
}
