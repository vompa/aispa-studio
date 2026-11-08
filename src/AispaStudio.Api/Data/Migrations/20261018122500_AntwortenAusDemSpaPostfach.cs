using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AntwortenAusDemSpaPostfach : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastSendError",
                schema: "aispa",
                table: "SpaMailboxes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SendReplies",
                schema: "aispa",
                table: "SpaMailboxes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SmtpHost",
                schema: "aispa",
                table: "SpaMailboxes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                schema: "aispa",
                table: "SpaMailboxes",
                type: "integer",
                nullable: false,
                defaultValue: 587);

            migrationBuilder.AddColumn<string>(
                name: "SmtpSecurity",
                schema: "aispa",
                table: "SpaMailboxes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "StartTls");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSendError",
                schema: "aispa",
                table: "SpaMailboxes");

            migrationBuilder.DropColumn(
                name: "SendReplies",
                schema: "aispa",
                table: "SpaMailboxes");

            migrationBuilder.DropColumn(
                name: "SmtpHost",
                schema: "aispa",
                table: "SpaMailboxes");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                schema: "aispa",
                table: "SpaMailboxes");

            migrationBuilder.DropColumn(
                name: "SmtpSecurity",
                schema: "aispa",
                table: "SpaMailboxes");
        }
    }
}
