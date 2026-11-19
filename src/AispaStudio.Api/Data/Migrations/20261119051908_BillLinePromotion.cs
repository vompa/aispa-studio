using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BillLinePromotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PromotionId",
                schema: "aispa",
                table: "BillLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_PromotionId",
                schema: "aispa",
                table: "BillLines",
                column: "PromotionId");

            migrationBuilder.AddForeignKey(
                name: "FK_BillLines_Promotions_PromotionId",
                schema: "aispa",
                table: "BillLines",
                column: "PromotionId",
                principalSchema: "aispa",
                principalTable: "Promotions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BillLines_Promotions_PromotionId",
                schema: "aispa",
                table: "BillLines");

            migrationBuilder.DropIndex(
                name: "IX_BillLines_PromotionId",
                schema: "aispa",
                table: "BillLines");

            migrationBuilder.DropColumn(
                name: "PromotionId",
                schema: "aispa",
                table: "BillLines");
        }
    }
}
