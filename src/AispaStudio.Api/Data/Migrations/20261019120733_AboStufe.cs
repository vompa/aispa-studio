using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AboStufe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bestehende Spas behalten alles, was sie hatten (ADR 0015: Funktionen nicht wegnehmen).
            migrationBuilder.AddColumn<string>(
                name: "Plan",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "SpaPlus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Plan",
                schema: "aispa",
                table: "Tenants");
        }
    }
}
