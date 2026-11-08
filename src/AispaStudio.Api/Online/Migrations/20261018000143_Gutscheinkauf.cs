using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Online.Migrations
{
    /// <inheritdoc />
    public partial class Gutscheinkauf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VoucherOrders",
                schema: "online",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    BuyerEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Message = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PrivacyAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PrivacyNoticeVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BillId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherOrders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherOrders_LocationId_Status",
                schema: "online",
                table: "VoucherOrders",
                columns: new[] { "LocationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherOrders_TokenHash",
                schema: "online",
                table: "VoucherOrders",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoucherOrders",
                schema: "online");
        }
    }
}
