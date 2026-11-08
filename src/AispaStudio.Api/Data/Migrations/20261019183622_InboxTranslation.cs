using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InboxTranslation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReplyHistory",
                schema: "aispa",
                table: "InboundMails",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ReplyNotice",
                schema: "aispa",
                table: "InboundMails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReplyRevising",
                schema: "aispa",
                table: "InboundMails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReplyTranslation",
                schema: "aispa",
                table: "InboundMails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyTranslationOf",
                schema: "aispa",
                table: "InboundMails",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslatedBody",
                schema: "aispa",
                table: "InboundMails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslatedSubject",
                schema: "aispa",
                table: "InboundMails",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslationState",
                schema: "aispa",
                table: "InboundMails",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplyHistory",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "ReplyNotice",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "ReplyRevising",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "ReplyTranslation",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "ReplyTranslationOf",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "TranslatedBody",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "TranslatedSubject",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "TranslationState",
                schema: "aispa",
                table: "InboundMails");
        }
    }
}
