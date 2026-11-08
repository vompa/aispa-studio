using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Lager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StockItemId",
                schema: "aispa",
                table: "TreatmentMaterials",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockItems",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    MinimumQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockItems_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalSchema: "aispa",
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "aispa",
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentMaterials_StockItemId",
                schema: "aispa",
                table: "TreatmentMaterials",
                column: "StockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_ArticleId",
                schema: "aispa",
                table: "StockItems",
                column: "ArticleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_TenantId",
                schema: "aispa",
                table: "StockItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_AppointmentId",
                schema: "aispa",
                table: "StockMovements",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemId",
                schema: "aispa",
                table: "StockMovements",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_LocationId_At",
                schema: "aispa",
                table: "StockMovements",
                columns: new[] { "LocationId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_LocationId_ItemId",
                schema: "aispa",
                table: "StockMovements",
                columns: new[] { "LocationId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReceiptId_ItemId",
                schema: "aispa",
                table: "StockMovements",
                columns: new[] { "ReceiptId", "ItemId" },
                unique: true,
                filter: "\"ReceiptId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId",
                schema: "aispa",
                table: "StockMovements",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_TreatmentMaterials_StockItems_StockItemId",
                schema: "aispa",
                table: "TreatmentMaterials",
                column: "StockItemId",
                principalSchema: "aispa",
                principalTable: "StockItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TreatmentMaterials_StockItems_StockItemId",
                schema: "aispa",
                table: "TreatmentMaterials");

            migrationBuilder.DropTable(
                name: "StockMovements",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "StockItems",
                schema: "aispa");

            migrationBuilder.DropIndex(
                name: "IX_TreatmentMaterials_StockItemId",
                schema: "aispa",
                table: "TreatmentMaterials");

            migrationBuilder.DropColumn(
                name: "StockItemId",
                schema: "aispa",
                table: "TreatmentMaterials");
        }
    }
}
