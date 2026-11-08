using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InboxRepropose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PreviousMailId",
                schema: "aispa",
                table: "InboundMails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProposalRound",
                schema: "aispa",
                table: "InboundMails",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RejectedStarts",
                schema: "aispa",
                table: "InboundMails",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "Reproposing",
                schema: "aispa",
                table: "InboundMails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WishNote",
                schema: "aispa",
                table: "InboundMails",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Bestehende Vorschläge zählen als erster Vorschlag.
            migrationBuilder.Sql("""UPDATE aispa."InboundMails" SET "ProposalRound" = 1 WHERE "Proposals" <> '[]'::jsonb;""");

            migrationBuilder.CreateIndex(
                name: "IX_InboundMails_From_RepliedAt",
                schema: "aispa",
                table: "InboundMails",
                columns: new[] { "From", "RepliedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InboundMails_From_RepliedAt",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "PreviousMailId",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "ProposalRound",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "RejectedStarts",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "Reproposing",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "WishNote",
                schema: "aispa",
                table: "InboundMails");
        }
    }
}
