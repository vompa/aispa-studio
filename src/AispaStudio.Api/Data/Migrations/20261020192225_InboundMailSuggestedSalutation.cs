using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InboundMailSuggestedSalutation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SuggestedSalutation",
                schema: "aispa",
                table: "InboundMails",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedTitlesBefore",
                schema: "aispa",
                table: "InboundMails",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuggestedSalutation",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "SuggestedTitlesBefore",
                schema: "aispa",
                table: "InboundMails");
        }
    }
}
