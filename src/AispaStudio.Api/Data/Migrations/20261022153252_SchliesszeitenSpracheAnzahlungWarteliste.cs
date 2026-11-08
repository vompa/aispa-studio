using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchliesszeitenSpracheAnzahlungWarteliste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "AppointmentId",
                schema: "aispa",
                table: "ClosureNotices",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "aispa",
                table: "ClosureNotices",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "de");

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                schema: "aispa",
                table: "ClosureNotices",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundError",
                schema: "aispa",
                table: "ClosureNotices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefundReceiptId",
                schema: "aispa",
                table: "ClosureNotices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WaitlistEntryId",
                schema: "aispa",
                table: "ClosureNotices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClosureNotices_ClosureId_WaitlistEntryId",
                schema: "aispa",
                table: "ClosureNotices",
                columns: new[] { "ClosureId", "WaitlistEntryId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClosureNotices_ClosureId_WaitlistEntryId",
                schema: "aispa",
                table: "ClosureNotices");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "aispa",
                table: "ClosureNotices");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                schema: "aispa",
                table: "ClosureNotices");

            migrationBuilder.DropColumn(
                name: "RefundError",
                schema: "aispa",
                table: "ClosureNotices");

            migrationBuilder.DropColumn(
                name: "RefundReceiptId",
                schema: "aispa",
                table: "ClosureNotices");

            migrationBuilder.DropColumn(
                name: "WaitlistEntryId",
                schema: "aispa",
                table: "ClosureNotices");

            migrationBuilder.AlterColumn<Guid>(
                name: "AppointmentId",
                schema: "aispa",
                table: "ClosureNotices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
