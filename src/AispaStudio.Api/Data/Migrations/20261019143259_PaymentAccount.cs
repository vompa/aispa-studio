using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaymentAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentAccounts",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ConnectAccountId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConnectChargesEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConnectDetailsSubmitted = table.Column<bool>(type: "boolean", nullable: false),
                    ConnectPreparing = table.Column<bool>(type: "boolean", nullable: false),
                    ConnectOnboardingUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConnectOnboardingExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProtectedSecretKey = table.Column<string>(type: "text", nullable: true),
                    ProtectedWebhookSecret = table.Column<string>(type: "text", nullable: true),
                    SecretKeyHint = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAccounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAccounts_ConnectAccountId",
                schema: "aispa",
                table: "PaymentAccounts",
                column: "ConnectAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAccounts_TenantId",
                schema: "aispa",
                table: "PaymentAccounts",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentAccounts",
                schema: "aispa");
        }
    }
}
