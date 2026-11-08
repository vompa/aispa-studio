using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TherapistGender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gender",
                schema: "aispa",
                table: "Employees",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredGender",
                schema: "aispa",
                table: "Customers",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gender",
                schema: "aispa",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PreferredGender",
                schema: "aispa",
                table: "Customers");
        }
    }
}
