using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DigitalerEuro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DigitalEuroAccount",
                schema: "aispa",
                table: "AccountingSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "1362");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DigitalEuroAccount",
                schema: "aispa",
                table: "AccountingSettings");
        }
    }
}
