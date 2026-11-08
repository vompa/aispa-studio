using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RollenManagerBackoffice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AmountReports",
                schema: "aispa",
                table: "StaffAccess",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AmountReports",
                schema: "aispa",
                table: "Invitations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountReports",
                schema: "aispa",
                table: "StaffAccess");

            migrationBuilder.DropColumn(
                name: "AmountReports",
                schema: "aispa",
                table: "Invitations");
        }
    }
}
