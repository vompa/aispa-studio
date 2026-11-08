using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StockBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                schema: "aispa",
                table: "StockItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_TenantId_Barcode",
                schema: "aispa",
                table: "StockItems",
                columns: new[] { "TenantId", "Barcode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockItems_TenantId_Barcode",
                schema: "aispa",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "Barcode",
                schema: "aispa",
                table: "StockItems");
        }
    }
}
