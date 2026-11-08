using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EintritteKassa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AdmissionArticleId",
                schema: "aispa",
                table: "Rooms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AreaVisitId",
                schema: "aispa",
                table: "BillLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_AdmissionArticleId",
                schema: "aispa",
                table: "Rooms",
                column: "AdmissionArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_AreaVisitId",
                schema: "aispa",
                table: "BillLines",
                column: "AreaVisitId",
                unique: true,
                filter: "\"AreaVisitId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_BillLines_AreaVisits_AreaVisitId",
                schema: "aispa",
                table: "BillLines",
                column: "AreaVisitId",
                principalSchema: "aispa",
                principalTable: "AreaVisits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_Articles_AdmissionArticleId",
                schema: "aispa",
                table: "Rooms",
                column: "AdmissionArticleId",
                principalSchema: "aispa",
                principalTable: "Articles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BillLines_AreaVisits_AreaVisitId",
                schema: "aispa",
                table: "BillLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_Articles_AdmissionArticleId",
                schema: "aispa",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_AdmissionArticleId",
                schema: "aispa",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_BillLines_AreaVisitId",
                schema: "aispa",
                table: "BillLines");

            migrationBuilder.DropColumn(
                name: "AdmissionArticleId",
                schema: "aispa",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "AreaVisitId",
                schema: "aispa",
                table: "BillLines");
        }
    }
}
