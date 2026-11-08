using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class KopieImOrdnerGesendet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SaveSentCopy",
                schema: "aispa",
                table: "SpaMailboxes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "SentFolder",
                schema: "aispa",
                table: "SpaMailboxes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SaveSentCopy",
                schema: "aispa",
                table: "SpaMailboxes");

            migrationBuilder.DropColumn(
                name: "SentFolder",
                schema: "aispa",
                table: "SpaMailboxes");
        }
    }
}
