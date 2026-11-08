using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TerminalProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Protocol",
                schema: "aispa",
                table: "PaymentTerminals",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "Zvt"); // Bestehende Geräte im Netz sprechen ZVT.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Protocol",
                schema: "aispa",
                table: "PaymentTerminals");
        }
    }
}
