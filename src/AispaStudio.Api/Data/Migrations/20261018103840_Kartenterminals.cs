using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Kartenterminals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentTerminals",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTerminals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTerminals_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTerminals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CardPayments",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillId = table.Column<Guid>(type: "uuid", nullable: false),
                    TerminalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CardBrand = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    MaskedPan = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AuthorizationCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardPayments_Bills_BillId",
                        column: x => x.BillId,
                        principalSchema: "aispa",
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardPayments_PaymentTerminals_TerminalId",
                        column: x => x.TerminalId,
                        principalSchema: "aispa",
                        principalTable: "PaymentTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardPayments_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "aispa",
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardPayments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardPayments_BillId",
                schema: "aispa",
                table: "CardPayments",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_CardPayments_ReceiptId",
                schema: "aispa",
                table: "CardPayments",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_CardPayments_TenantId",
                schema: "aispa",
                table: "CardPayments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CardPayments_TerminalId_Status",
                schema: "aispa",
                table: "CardPayments",
                columns: new[] { "TerminalId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerminals_LocationId",
                schema: "aispa",
                table: "PaymentTerminals",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerminals_TenantId",
                schema: "aispa",
                table: "PaymentTerminals",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardPayments",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "PaymentTerminals",
                schema: "aispa");
        }
    }
}
