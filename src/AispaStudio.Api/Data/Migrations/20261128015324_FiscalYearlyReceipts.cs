using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FiscalYearlyReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClosingYear",
                schema: "aispa",
                table: "Receipts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RegisterConfirmation",
                schema: "aispa",
                columns: table => new
                {
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    YearKey = table.Column<int>(type: "integer", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedBy = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisterConfirmation", x => new { x.RegisterId, x.Kind, x.YearKey });
                    table.ForeignKey(
                        name: "FK_RegisterConfirmation_CashRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalSchema: "aispa",
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegisterConfirmation_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "aispa",
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegisterConfirmation_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegisterConfirmation_ReceiptId",
                schema: "aispa",
                table: "RegisterConfirmation",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_RegisterConfirmation_TenantId",
                schema: "aispa",
                table: "RegisterConfirmation",
                column: "TenantId");

            migrationBuilder.Sql("""
                DO $restore$
                BEGIN
                    IF to_regclass('aispa_maintenance.closing_year_20261128') IS NOT NULL THEN
                        UPDATE aispa."Receipts" r SET "ClosingYear" = b."ClosingYear"
                        FROM aispa_maintenance.closing_year_20261128 b
                        WHERE r."Id" = b."Id" AND r."TenantId" = b."TenantId";
                        INSERT INTO aispa."RegisterConfirmation"
                            ("RegisterId", "Kind", "YearKey", "ReceiptId", "ConfirmedAt", "ConfirmedBy", "TenantId")
                        SELECT b."RegisterId", b."Kind", b."YearKey", b."ReceiptId", b."ConfirmedAt", b."ConfirmedBy", b."TenantId"
                        FROM aispa_maintenance.register_confirmation_20261128 b
                        JOIN aispa."CashRegisters" r ON r."Id" = b."RegisterId" AND r."TenantId" = b."TenantId"
                        LEFT JOIN aispa."Receipts" s ON s."Id" = b."ReceiptId" AND s."RegisterId" = r."Id" AND s."TenantId" = r."TenantId"
                        WHERE b."ReceiptId" IS NULL OR s."Id" IS NOT NULL;
                        DROP TABLE aispa_maintenance.register_confirmation_20261128, aispa_maintenance.closing_year_20261128;
                    END IF;
                END $restore$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE aispa."Receipts", aispa."RegisterConfirmation" IN ACCESS EXCLUSIVE MODE;
                CREATE SCHEMA IF NOT EXISTS aispa_maintenance;
                CREATE TABLE aispa_maintenance.closing_year_20261128 AS
                    SELECT "Id", "TenantId", "ClosingYear" FROM aispa."Receipts" WHERE "ClosingYear" IS NOT NULL;
                CREATE TABLE aispa_maintenance.register_confirmation_20261128 AS
                    SELECT "RegisterId", "Kind", "YearKey", "ReceiptId", "ConfirmedAt", "ConfirmedBy", "TenantId"
                    FROM aispa."RegisterConfirmation";
                """);
            migrationBuilder.DropTable(
                name: "RegisterConfirmation",
                schema: "aispa");

            migrationBuilder.DropColumn(
                name: "ClosingYear",
                schema: "aispa",
                table: "Receipts");
        }
    }
}
