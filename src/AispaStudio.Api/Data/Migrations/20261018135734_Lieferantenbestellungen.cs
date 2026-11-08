using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Lieferantenbestellungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderId",
                schema: "aispa",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Supplier",
                schema: "aispa",
                table: "StockItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierArticleNumber",
                schema: "aispa",
                table: "StockItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Supplier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrderedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLines",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    Received = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_PurchaseOrders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "aispa",
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_StockItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "aispa",
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PurchaseOrderId",
                schema: "aispa",
                table: "StockMovements",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_ItemId",
                schema: "aispa",
                table: "PurchaseOrderLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_OrderId",
                schema: "aispa",
                table: "PurchaseOrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_TenantId",
                schema: "aispa",
                table: "PurchaseOrderLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_LocationId_Status",
                schema: "aispa",
                table: "PurchaseOrders",
                columns: new[] { "LocationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId",
                schema: "aispa",
                table: "PurchaseOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_Number",
                schema: "aispa",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_PurchaseOrders_PurchaseOrderId",
                schema: "aispa",
                table: "StockMovements",
                column: "PurchaseOrderId",
                principalSchema: "aispa",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_PurchaseOrders_PurchaseOrderId",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLines",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "PurchaseOrders",
                schema: "aispa");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_PurchaseOrderId",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "Supplier",
                schema: "aispa",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "SupplierArticleNumber",
                schema: "aispa",
                table: "StockItems");
        }
    }
}
