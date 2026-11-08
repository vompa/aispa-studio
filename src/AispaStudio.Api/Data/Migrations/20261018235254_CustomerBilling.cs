using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingAddress",
                schema: "aispa",
                table: "Customers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingName",
                schema: "aispa",
                table: "Customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingVatId",
                schema: "aispa",
                table: "Customers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingAddress",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BillingName",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BillingVatId",
                schema: "aispa",
                table: "Customers");
        }
    }
}
