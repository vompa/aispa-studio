using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LeerlaufAmEmpfangsgeraet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdleMinutes",
                schema: "aispa",
                table: "SharedDevices",
                type: "integer",
                nullable: false,
                defaultValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdleMinutes",
                schema: "aispa",
                table: "SharedDevices");
        }
    }
}
