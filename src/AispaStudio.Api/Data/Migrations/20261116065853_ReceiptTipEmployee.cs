using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptTipEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TipEmployeeId",
                schema: "aispa",
                table: "Receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_TipEmployeeId",
                schema: "aispa",
                table: "Receipts",
                column: "TipEmployeeId",
                filter: "\"TipEmployeeId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Receipts_TipEmployeeId",
                schema: "aispa",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "TipEmployeeId",
                schema: "aispa",
                table: "Receipts");
        }
    }
}
