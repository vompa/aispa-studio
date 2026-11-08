using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                schema: "aispa",
                table: "ReceiptLine",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountNote",
                schema: "aispa",
                table: "ReceiptLine",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                schema: "aispa",
                table: "Customers",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountScope",
                schema: "aispa",
                table: "Customers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "All");

            migrationBuilder.AddColumn<string>(
                name: "DiscountNote",
                schema: "aispa",
                table: "BillLines",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discount",
                schema: "aispa",
                table: "ReceiptLine");

            migrationBuilder.DropColumn(
                name: "DiscountNote",
                schema: "aispa",
                table: "ReceiptLine");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DiscountScope",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DiscountNote",
                schema: "aispa",
                table: "BillLines");
        }
    }
}
