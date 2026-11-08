using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreiseNachZeit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TreatmentPriceRules",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    From = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Until = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentPriceRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreatmentPriceRules_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TreatmentPriceRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentPriceRules_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPriceRules_LocationId",
                schema: "aispa",
                table: "TreatmentPriceRules",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPriceRules_TenantId",
                schema: "aispa",
                table: "TreatmentPriceRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPriceRules_TreatmentId",
                schema: "aispa",
                table: "TreatmentPriceRules",
                column: "TreatmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TreatmentPriceRules",
                schema: "aispa");
        }
    }
}
