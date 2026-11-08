using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AusfallgebuehrJeBehandlung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NoShowFeePercent",
                schema: "aispa",
                table: "Treatments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NoShowFeeNewCustomersOnly",
                schema: "aispa",
                table: "LocationOnlineSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoShowFeePercent",
                schema: "aispa",
                table: "Treatments");

            migrationBuilder.DropColumn(
                name: "NoShowFeeNewCustomersOnly",
                schema: "aispa",
                table: "LocationOnlineSettings");
        }
    }
}
