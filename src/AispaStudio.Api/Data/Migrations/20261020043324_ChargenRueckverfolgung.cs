using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChargenRueckverfolgung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ItemId",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ReceiptId_ItemId",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemId_Batch",
                schema: "aispa",
                table: "StockMovements",
                columns: new[] { "ItemId", "Batch" },
                filter: "\"Batch\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReceiptId_ItemId_Batch_ExpiresOn",
                schema: "aispa",
                table: "StockMovements",
                columns: new[] { "ReceiptId", "ItemId", "Batch", "ExpiresOn" },
                unique: true,
                filter: "\"ReceiptId\" IS NOT NULL")
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ItemId_Batch",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ReceiptId_ItemId_Batch_ExpiresOn",
                schema: "aispa",
                table: "StockMovements");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemId",
                schema: "aispa",
                table: "StockMovements",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReceiptId_ItemId",
                schema: "aispa",
                table: "StockMovements",
                columns: new[] { "ReceiptId", "ItemId" },
                unique: true,
                filter: "\"ReceiptId\" IS NOT NULL");
        }
    }
}
