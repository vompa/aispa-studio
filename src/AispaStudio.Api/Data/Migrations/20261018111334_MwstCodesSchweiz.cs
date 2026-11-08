using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MwstCodesSchweiz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SwissTaxCodes",
                schema: "aispa",
                table: "AccountingSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SwissTaxCodes",
                schema: "aispa",
                table: "AccountingSettings");
        }
    }
}
