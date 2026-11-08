using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Einwirkzeit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HandsOnMinutes",
                schema: "aispa",
                table: "Treatments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingMinutes",
                schema: "aispa",
                table: "Treatments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HandsOnMinutes",
                schema: "aispa",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingMinutes",
                schema: "aispa",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HandsOnMinutes",
                schema: "aispa",
                table: "Treatments");

            migrationBuilder.DropColumn(
                name: "ProcessingMinutes",
                schema: "aispa",
                table: "Treatments");

            migrationBuilder.DropColumn(
                name: "HandsOnMinutes",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ProcessingMinutes",
                schema: "aispa",
                table: "Appointments");
        }
    }
}
