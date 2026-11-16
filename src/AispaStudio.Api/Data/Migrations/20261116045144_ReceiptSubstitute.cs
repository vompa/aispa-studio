using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptSubstitute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubstituteId",
                schema: "aispa",
                table: "Receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubstituteIssuedAt",
                schema: "aispa",
                table: "Receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubstituteNumber",
                schema: "aispa",
                table: "Receipts",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_SubstituteId",
                schema: "aispa",
                table: "Receipts",
                column: "SubstituteId",
                unique: true,
                filter: "\"SubstituteId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Receipts_SubstituteId",
                schema: "aispa",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "SubstituteId",
                schema: "aispa",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "SubstituteIssuedAt",
                schema: "aispa",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "SubstituteNumber",
                schema: "aispa",
                table: "Receipts");
        }
    }
}
