using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ManagementAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManagementAnalyses",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: false),
                    Compare = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amounts = table.Column<bool>(type: "boolean", nullable: false),
                    Focus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Question = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagementAnalyses_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ManagementAnalyses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManagementAnalyses_LocationId_From_Until",
                schema: "aispa",
                table: "ManagementAnalyses",
                columns: new[] { "LocationId", "From", "Until" });

            migrationBuilder.CreateIndex(
                name: "IX_ManagementAnalyses_TenantId",
                schema: "aispa",
                table: "ManagementAnalyses",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManagementAnalyses",
                schema: "aispa");
        }
    }
}
