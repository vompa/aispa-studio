using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Packages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "aispa",
                table: "Vouchers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Units",
                schema: "aispa",
                table: "Vouchers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitsLeft",
                schema: "aispa",
                table: "Vouchers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VoucherUnits",
                schema: "aispa",
                table: "BillLines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "VoucherValidUntil",
                schema: "aispa",
                table: "BillLines",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_CustomerId",
                schema: "aispa",
                table: "Vouchers",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vouchers_CustomerId",
                schema: "aispa",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "aispa",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "Units",
                schema: "aispa",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "UnitsLeft",
                schema: "aispa",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "VoucherUnits",
                schema: "aispa",
                table: "BillLines");

            migrationBuilder.DropColumn(
                name: "VoucherValidUntil",
                schema: "aispa",
                table: "BillLines");
        }
    }
}
