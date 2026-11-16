using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptTip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Tip",
                schema: "aispa",
                table: "Receipts",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TipsAccount",
                schema: "aispa",
                table: "AccountingSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "3790");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tip",
                schema: "aispa",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "TipsAccount",
                schema: "aispa",
                table: "AccountingSettings");
        }
    }
}
