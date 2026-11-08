using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Online.Migrations
{
    /// <inheritdoc />
    public partial class Rueckerstattung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Refunds",
                schema: "online",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoidReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RefundReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RefundedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherOrders_ReceiptId",
                schema: "online",
                table: "VoucherOrders",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_CardGuarantees_ReceiptId",
                schema: "online",
                table: "CardGuarantees",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ReceiptId",
                schema: "online",
                table: "Refunds",
                column: "ReceiptId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Refunds",
                schema: "online");

            migrationBuilder.DropIndex(
                name: "IX_VoucherOrders_ReceiptId",
                schema: "online",
                table: "VoucherOrders");

            migrationBuilder.DropIndex(
                name: "IX_CardGuarantees_ReceiptId",
                schema: "online",
                table: "CardGuarantees");
        }
    }
}
