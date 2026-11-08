using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HrFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hr");

            migrationBuilder.CreateTable(
                name: "HrEntries",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    FollowUpOn = table.Column<DateOnly>(type: "date", nullable: true),
                    FollowUpDone = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Payload = table.Column<byte[]>(type: "bytea", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrEntries_EmployeeId",
                schema: "hr",
                table: "HrEntries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrEntries_TenantId",
                schema: "hr",
                table: "HrEntries",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HrEntries",
                schema: "hr");
        }
    }
}
