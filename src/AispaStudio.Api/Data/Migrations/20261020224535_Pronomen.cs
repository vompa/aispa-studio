using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Pronomen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Pronouns",
                schema: "aispa",
                table: "Customers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pronouns",
                schema: "aispa",
                table: "Customers");
        }
    }
}
