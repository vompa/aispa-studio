using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Rechnungsangaben : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyRegisterCourt",
                schema: "aispa",
                table: "Locations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyRegisterNumber",
                schema: "aispa",
                table: "Locations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                schema: "aispa",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatId",
                schema: "aispa",
                table: "Locations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReceiptRecipients",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    VatId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptRecipients_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "aispa",
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptRecipients_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptRecipients_ReceiptId",
                schema: "aispa",
                table: "ReceiptRecipients",
                column: "ReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptRecipients_TenantId",
                schema: "aispa",
                table: "ReceiptRecipients",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptRecipients",
                schema: "aispa");

            migrationBuilder.DropColumn(
                name: "CompanyRegisterCourt",
                schema: "aispa",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CompanyRegisterNumber",
                schema: "aispa",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "LegalName",
                schema: "aispa",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "VatId",
                schema: "aispa",
                table: "Locations");
        }
    }
}
