using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UidPruefung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VatIdCheckId",
                schema: "aispa",
                table: "ReceiptRecipients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VatIdChecks",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VatId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequesterVatId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConsultationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Message = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatIdChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VatIdChecks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptRecipients_VatIdCheckId",
                schema: "aispa",
                table: "ReceiptRecipients",
                column: "VatIdCheckId");

            migrationBuilder.CreateIndex(
                name: "IX_VatIdChecks_TenantId",
                schema: "aispa",
                table: "VatIdChecks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_VatIdChecks_VatId_RequestedAt",
                schema: "aispa",
                table: "VatIdChecks",
                columns: new[] { "VatId", "RequestedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptRecipients_VatIdChecks_VatIdCheckId",
                schema: "aispa",
                table: "ReceiptRecipients",
                column: "VatIdCheckId",
                principalSchema: "aispa",
                principalTable: "VatIdChecks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptRecipients_VatIdChecks_VatIdCheckId",
                schema: "aispa",
                table: "ReceiptRecipients");

            migrationBuilder.DropTable(
                name: "VatIdChecks",
                schema: "aispa");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptRecipients_VatIdCheckId",
                schema: "aispa",
                table: "ReceiptRecipients");

            migrationBuilder.DropColumn(
                name: "VatIdCheckId",
                schema: "aispa",
                table: "ReceiptRecipients");
        }
    }
}
