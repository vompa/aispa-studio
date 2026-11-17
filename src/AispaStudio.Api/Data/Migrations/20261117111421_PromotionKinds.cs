using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PromotionKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "aispa",
                table: "Promotions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // Bestehende Aktionen sind befristet (Nachtrag zu ADR 0071).
                defaultValue: "Limited");

            migrationBuilder.AddColumn<int>(
                name: "LeadHours",
                schema: "aispa",
                table: "Promotions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OffPeakSlots",
                schema: "aispa",
                table: "Promotions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "aispa",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "LeadHours",
                schema: "aispa",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "OffPeakSlots",
                schema: "aispa",
                table: "Promotions");
        }
    }
}
