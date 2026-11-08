using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Lagerorte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Erst nullbar: jeder Standort bekommt sein Hauptlager, alle bisherigen Bewegungen wandern dorthin, dann wird die Spalte Pflicht.
            migrationBuilder.AddColumn<Guid>(
                name: "StoragePlaceId",
                schema: "aispa",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetStoragePlaceId",
                schema: "aispa",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoragePlaces",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoragePlaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoragePlaces_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoragePlaces_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "aispa",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoragePlaces_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoragePlaceMinimums",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoragePlaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinimumQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoragePlaceMinimums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoragePlaceMinimums_StockItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "aispa",
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoragePlaceMinimums_StoragePlaces_StoragePlaceId",
                        column: x => x.StoragePlaceId,
                        principalSchema: "aispa",
                        principalTable: "StoragePlaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoragePlaceMinimums_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO aispa."StoragePlaces" ("Id", "LocationId", "Name", "Kind", "IsActive", "Position", "TenantId")
                SELECT gen_random_uuid(), l."Id", 'Hauptlager', 'Main', TRUE, 0, l."TenantId"
                FROM aispa."Locations" l;
                """);

            migrationBuilder.Sql("""
                UPDATE aispa."StockMovements" m
                SET "StoragePlaceId" = p."Id"
                FROM aispa."StoragePlaces" p
                WHERE p."LocationId" = m."LocationId" AND p."Kind" = 'Main';
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "StoragePlaceId",
                schema: "aispa",
                table: "StockMovements",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StoragePlaceId_ItemId",
                schema: "aispa",
                table: "StockMovements",
                columns: new[] { "StoragePlaceId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TargetStoragePlaceId",
                schema: "aispa",
                table: "StockMovements",
                column: "TargetStoragePlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_StoragePlaceMinimums_ItemId",
                schema: "aispa",
                table: "StoragePlaceMinimums",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StoragePlaceMinimums_StoragePlaceId_ItemId",
                schema: "aispa",
                table: "StoragePlaceMinimums",
                columns: new[] { "StoragePlaceId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoragePlaceMinimums_TenantId",
                schema: "aispa",
                table: "StoragePlaceMinimums",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StoragePlaces_LocationId_Main",
                schema: "aispa",
                table: "StoragePlaces",
                column: "LocationId",
                unique: true,
                filter: "\"Kind\" = 'Main'");

            migrationBuilder.CreateIndex(
                name: "IX_StoragePlaces_LocationId_Position",
                schema: "aispa",
                table: "StoragePlaces",
                columns: new[] { "LocationId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_StoragePlaces_RoomId",
                schema: "aispa",
                table: "StoragePlaces",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_StoragePlaces_TenantId",
                schema: "aispa",
                table: "StoragePlaces",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_StoragePlaces_StoragePlaceId",
                schema: "aispa",
                table: "StockMovements",
                column: "StoragePlaceId",
                principalSchema: "aispa",
                principalTable: "StoragePlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_StoragePlaces_TargetStoragePlaceId",
                schema: "aispa",
                table: "StockMovements",
                column: "TargetStoragePlaceId",
                principalSchema: "aispa",
                principalTable: "StoragePlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_StoragePlaces_StoragePlaceId",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_StoragePlaces_TargetStoragePlaceId",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "StoragePlaceMinimums",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "StoragePlaces",
                schema: "aispa");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_StoragePlaceId_ItemId",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_TargetStoragePlaceId",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "StoragePlaceId",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "TargetStoragePlaceId",
                schema: "aispa",
                table: "StockMovements");
        }
    }
}
