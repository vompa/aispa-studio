using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StockSuppliersPackagingExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaseUnit",
                schema: "aispa",
                table: "StockItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpiryWarningDays",
                schema: "aispa",
                table: "StockItems",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<decimal>(
                name: "PackSize",
                schema: "aispa",
                table: "StockItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackUnit",
                schema: "aispa",
                table: "StockItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PacksPerCase",
                schema: "aispa",
                table: "StockItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "aispa",
                table: "StockItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TreatmentDose",
                schema: "aispa",
                table: "StockItems",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockSuppliers",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    MinimumOrderValue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    OrderChannel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockSuppliers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_SupplierId",
                schema: "aispa",
                table: "StockItems",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_StockSuppliers_TenantId",
                schema: "aispa",
                table: "StockSuppliers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockSuppliers_TenantId_Name",
                schema: "aispa",
                table: "StockSuppliers",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StockItems_StockSuppliers_SupplierId",
                schema: "aispa",
                table: "StockItems",
                column: "SupplierId",
                principalSchema: "aispa",
                principalTable: "StockSuppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Der Text wird zum Stamm: je Mandant ein Lieferant je Name, Schreibweisen zusammengeführt; leere Texte bleiben ohne Lieferant.
            migrationBuilder.Sql("""
                INSERT INTO aispa."StockSuppliers" ("Id", "Name", "OrderChannel", "IsActive", "TenantId")
                SELECT gen_random_uuid(), MIN(trim("Supplier") COLLATE "C"), 'Email', TRUE, "TenantId"
                FROM aispa."StockItems"
                WHERE trim(coalesce("Supplier", '')) <> ''
                GROUP BY "TenantId", lower(trim("Supplier"));

                UPDATE aispa."StockItems" i
                SET "SupplierId" = s."Id"
                FROM aispa."StockSuppliers" s
                WHERE s."TenantId" = i."TenantId" AND lower(s."Name") = lower(trim(i."Supplier"));
                """);

            migrationBuilder.DropColumn(
                name: "Supplier",
                schema: "aispa",
                table: "StockItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Supplier",
                schema: "aispa",
                table: "StockItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE aispa."StockItems" i
                SET "Supplier" = s."Name"
                FROM aispa."StockSuppliers" s
                WHERE s."Id" = i."SupplierId";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_StockItems_StockSuppliers_SupplierId",
                schema: "aispa",
                table: "StockItems");

            migrationBuilder.DropTable(
                name: "StockSuppliers",
                schema: "aispa");

            migrationBuilder.DropIndex(
                name: "IX_StockItems_SupplierId",
                schema: "aispa",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "CaseUnit",
                schema: "aispa",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "ExpiryWarningDays",
                schema: "aispa",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "PackSize",
                schema: "aispa",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "PackUnit",
                schema: "aispa",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "PacksPerCase",
                schema: "aispa",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "aispa",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "TreatmentDose",
                schema: "aispa",
                table: "StockItems");
        }
    }
}
