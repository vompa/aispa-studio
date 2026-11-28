using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FiscalCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FiscalCredential",
                schema: "aispa",
                columns: table => new
                {
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedValues = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalCredential", x => x.RegisterId);
                    table.ForeignKey(
                        name: "FK_FiscalCredential_CashRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalSchema: "aispa",
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FiscalCredential_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalCredential_TenantId",
                schema: "aispa",
                table: "FiscalCredential",
                column: "TenantId");

            // Nur zu einer noch vorhandenen Kassa desselben Mandanten zurückholen. Nach der Wiederherstellung
            // gibt es genau eine Kopie, damit später gelöschte Zugangsdaten auch nach einem weiteren Downgrade weg sind.
            migrationBuilder.Sql("""
                DO $restore$
                BEGIN
                    IF to_regclass('aispa_maintenance.fiscal_credentials_20261128') IS NOT NULL THEN
                        INSERT INTO aispa."FiscalCredential" ("RegisterId", "TenantId", "ProtectedValues", "UpdatedAt")
                            SELECT b."RegisterId", b."TenantId", b."ProtectedValues", b."UpdatedAt"
                            FROM aispa_maintenance.fiscal_credentials_20261128 b
                            JOIN aispa."CashRegisters" r ON r."Id" = b."RegisterId" AND r."TenantId" = b."TenantId";
                        DROP TABLE aispa_maintenance.fiscal_credentials_20261128;
                    END IF;
                END;
                $restore$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE aispa."FiscalCredential" IN ACCESS EXCLUSIVE MODE;
                CREATE SCHEMA IF NOT EXISTS aispa_maintenance;
                CREATE TABLE aispa_maintenance.fiscal_credentials_20261128 (
                    "RegisterId" uuid PRIMARY KEY,
                    "TenantId" uuid NOT NULL,
                    "ProtectedValues" text NOT NULL,
                    "UpdatedAt" timestamptz NOT NULL
                );
                INSERT INTO aispa_maintenance.fiscal_credentials_20261128 ("RegisterId", "TenantId", "ProtectedValues", "UpdatedAt")
                    SELECT "RegisterId", "TenantId", "ProtectedValues", "UpdatedAt" FROM aispa."FiscalCredential";
                DROP TABLE aispa."FiscalCredential";
                """);
        }
    }
}
