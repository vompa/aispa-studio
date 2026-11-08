using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class KontenJeLand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "aispa",
                table: "AccountingSettings",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevenueAccountCodes",
                schema: "aispa",
                table: "AccountingSettings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingSettings_TenantId_Country",
                schema: "aispa",
                table: "AccountingSettings",
                columns: new[] { "TenantId", "Country" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountingSettings_TenantId_Country",
                schema: "aispa",
                table: "AccountingSettings");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "aispa",
                table: "AccountingSettings");

            migrationBuilder.DropColumn(
                name: "RevenueAccountCodes",
                schema: "aispa",
                table: "AccountingSettings");
        }
    }
}
