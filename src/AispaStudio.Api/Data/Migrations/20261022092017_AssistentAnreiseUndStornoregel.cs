using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssistentAnreiseUndStornoregel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Directions",
                schema: "aispa",
                table: "Locations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationPolicy",
                schema: "aispa",
                table: "LocationOnlineSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Directions",
                schema: "aispa",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CancellationPolicy",
                schema: "aispa",
                table: "LocationOnlineSettings");
        }
    }
}
