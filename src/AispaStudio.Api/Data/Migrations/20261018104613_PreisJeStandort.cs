using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreisJeStandort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TreatmentLocationPrices",
                schema: "aispa",
                columns: table => new
                {
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentLocationPrices", x => new { x.TreatmentId, x.LocationId });
                    table.ForeignKey(
                        name: "FK_TreatmentLocationPrices_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TreatmentLocationPrices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentLocationPrices_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentLocationPrices_LocationId",
                schema: "aispa",
                table: "TreatmentLocationPrices",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentLocationPrices_TenantId",
                schema: "aispa",
                table: "TreatmentLocationPrices",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TreatmentLocationPrices",
                schema: "aispa");
        }
    }
}
