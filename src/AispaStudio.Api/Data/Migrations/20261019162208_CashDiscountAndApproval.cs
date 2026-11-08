using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CashDiscountAndApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CashDiscountPercent",
                schema: "aispa",
                table: "Tenants",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountApprovedByUserId",
                schema: "aispa",
                table: "Customers",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountApprovedByUserId",
                schema: "aispa",
                table: "BillLines",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashDiscountPercent",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DiscountApprovedByUserId",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DiscountApprovedByUserId",
                schema: "aispa",
                table: "BillLines");
        }
    }
}
