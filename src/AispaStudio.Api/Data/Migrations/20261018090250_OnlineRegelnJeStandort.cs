using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OnlineRegelnJeStandort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocationOnlineSettings",
                schema: "aispa",
                columns: table => new
                {
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MinLeadMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxDaysAhead = table.Column<int>(type: "integer", nullable: false),
                    CancellationDeadlineHours = table.Column<int>(type: "integer", nullable: false),
                    SlotStepMinutes = table.Column<int>(type: "integer", nullable: false),
                    GuestNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationOnlineSettings", x => x.LocationId);
                    table.ForeignKey(
                        name: "FK_LocationOnlineSettings_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocationOnlineSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocationOnlineSettings_TenantId",
                schema: "aispa",
                table: "LocationOnlineSettings",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocationOnlineSettings",
                schema: "aispa");
        }
    }
}
