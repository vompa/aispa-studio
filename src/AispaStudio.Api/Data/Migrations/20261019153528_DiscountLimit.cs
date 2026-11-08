using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DiscountLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountLimitPercent",
                schema: "aispa",
                table: "Tenants",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 20m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountLimitPercent",
                schema: "aispa",
                table: "Tenants");
        }
    }
}
