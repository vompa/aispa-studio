using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ZugangAuswertungenOhneBetraege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PlainReports",
                schema: "aispa",
                table: "StaffAccess",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PlainReports",
                schema: "aispa",
                table: "Invitations",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlainReports",
                schema: "aispa",
                table: "StaffAccess");

            migrationBuilder.DropColumn(
                name: "PlainReports",
                schema: "aispa",
                table: "Invitations");
        }
    }
}
