using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Aktionen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Promotions",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    GuestText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    AllLocations = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                    table.CheckConstraint("CK_Promotions_Discount", "(\"DiscountPercent\" IS NOT NULL AND \"DiscountAmount\" IS NULL) OR (\"DiscountPercent\" IS NULL AND \"DiscountAmount\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Promotions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromotionLocations",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionLocations_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromotionLocations_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalSchema: "aispa",
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PromotionLocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromotionTreatments",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionTreatments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionTreatments_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalSchema: "aispa",
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PromotionTreatments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromotionTreatments_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionLocations_LocationId",
                schema: "aispa",
                table: "PromotionLocations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionLocations_PromotionId_LocationId",
                schema: "aispa",
                table: "PromotionLocations",
                columns: new[] { "PromotionId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionLocations_TenantId",
                schema: "aispa",
                table: "PromotionLocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_TenantId",
                schema: "aispa",
                table: "Promotions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_ValidFrom_ValidUntil",
                schema: "aispa",
                table: "Promotions",
                columns: new[] { "ValidFrom", "ValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionTreatments_PromotionId_TreatmentId",
                schema: "aispa",
                table: "PromotionTreatments",
                columns: new[] { "PromotionId", "TreatmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionTreatments_TenantId",
                schema: "aispa",
                table: "PromotionTreatments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionTreatments_TreatmentId",
                schema: "aispa",
                table: "PromotionTreatments",
                column: "TreatmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromotionLocations",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "PromotionTreatments",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Promotions",
                schema: "aispa");
        }
    }
}
