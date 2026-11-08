using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StammdatenJeStandort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "aispa",
                table: "Treatments",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "aispa",
                table: "Locations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "aispa",
                table: "Locations",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "aispa",
                table: "Locations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                schema: "aispa",
                table: "Locations",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                schema: "aispa",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                schema: "aispa",
                table: "Treatments");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "aispa",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "aispa",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "aispa",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                schema: "aispa",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Street",
                schema: "aispa",
                table: "Locations");
        }
    }
}
