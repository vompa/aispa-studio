using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StockCountSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockCountSessions",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoragePlaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCountSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockCountSessions_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCountSessions_StoragePlaces_StoragePlaceId",
                        column: x => x.StoragePlaceId,
                        principalSchema: "aispa",
                        principalTable: "StoragePlaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCountSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockCountLines",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Counted = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCountLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockCountLines_StockCountSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "aispa",
                        principalTable: "StockCountSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockCountLines_StockItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "aispa",
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCountLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockCountLines_ItemId",
                schema: "aispa",
                table: "StockCountLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCountLines_SessionId_ItemId",
                schema: "aispa",
                table: "StockCountLines",
                columns: new[] { "SessionId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockCountLines_TenantId",
                schema: "aispa",
                table: "StockCountLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCountSessions_LocationId_Status",
                schema: "aispa",
                table: "StockCountSessions",
                columns: new[] { "LocationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockCountSessions_StoragePlaceId_Running",
                schema: "aispa",
                table: "StockCountSessions",
                column: "StoragePlaceId",
                unique: true,
                filter: "\"Status\" IN ('Open', 'Submitted')");

            migrationBuilder.CreateIndex(
                name: "IX_StockCountSessions_TenantId",
                schema: "aispa",
                table: "StockCountSessions",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockCountLines",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "StockCountSessions",
                schema: "aispa");
        }
    }
}
