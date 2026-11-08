using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaffAppAbsenceRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                schema: "aispa",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AbsenceRequests",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbsenceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbsenceRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceRequests_TenantId",
                schema: "aispa",
                table: "AbsenceRequests",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbsenceRequests",
                schema: "aispa");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                schema: "aispa",
                table: "Appointments");
        }
    }
}
