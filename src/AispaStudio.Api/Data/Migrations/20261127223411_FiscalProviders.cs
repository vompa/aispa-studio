using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FiscalProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ClientId",
                schema: "aispa",
                table: "TestTseLog",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "ClientId",
                schema: "aispa",
                table: "TestTseClientState",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "FiscalProvider",
                schema: "aispa",
                table: "CashRegisters",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPractice",
                schema: "aispa",
                table: "CashRegisters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""UPDATE aispa."CashRegisters" SET "IsPractice" = TRUE WHERE "Country" IN ('AT', 'DE')""");
            migrationBuilder.Sql("""
                DO $restore$
                BEGIN
                    IF to_regclass('aispa_maintenance.fiscal_registers_20261127') IS NOT NULL THEN
                        UPDATE aispa."CashRegisters" r
                        SET "FiscalProvider" = b."FiscalProvider", "IsPractice" = b."IsPractice"
                        FROM aispa_maintenance.fiscal_registers_20261127 b
                        WHERE r."Id" = b."RegisterId";
                    END IF;
                END;
                $restore$;
                """);

            // Ältere Einzelplätze schreiben per jsonb_populate_record alle Zielspalten einschließlich NULL.
            // BEFORE INSERT läuft auch vor dem Konfliktzweig ihres Upserts: eine vorhandene Übungskassa behält ihren Modus.
            migrationBuilder.Sql("""
                CREATE FUNCTION aispa.legacy_register_fiscal_fields() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    SELECT r."IsPractice", r."FiscalProvider" INTO NEW."IsPractice", NEW."FiscalProvider"
                    FROM aispa."CashRegisters" r WHERE r."Id" = NEW."Id";
                    IF NOT FOUND THEN
                        NEW."IsPractice" := NEW."Country" IN ('AT', 'DE');
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER legacy_register_fiscal_fields BEFORE INSERT ON aispa."CashRegisters"
                FOR EACH ROW WHEN (NEW."IsPractice" IS NULL AND nullif(current_setting('aispa.sync_origin', true), '') IS NOT NULL)
                EXECUTE FUNCTION aispa.legacy_register_fiscal_fields();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Die alte Anwendung kennt diese beiden Felder nicht. Die lokale Sicherung bleibt außerhalb des
            // Abgleichs und stellt beim nächsten Upgrade auch den Übungsmodus einer Schweizer Kassa wieder her.
            migrationBuilder.Sql("""
                LOCK TABLE aispa."CashRegisters" IN ACCESS EXCLUSIVE MODE;
                CREATE SCHEMA IF NOT EXISTS aispa_maintenance;
                CREATE TABLE IF NOT EXISTS aispa_maintenance.fiscal_registers_20261127 AS
                    SELECT "Id" AS "RegisterId", "FiscalProvider", "IsPractice" FROM aispa."CashRegisters" WITH NO DATA;
                CREATE UNIQUE INDEX IF NOT EXISTS fiscal_registers_20261127_id
                    ON aispa_maintenance.fiscal_registers_20261127 ("RegisterId");
                INSERT INTO aispa_maintenance.fiscal_registers_20261127 ("RegisterId", "FiscalProvider", "IsPractice")
                    SELECT "Id", "FiscalProvider", "IsPractice" FROM aispa."CashRegisters"
                    ON CONFLICT ("RegisterId") DO UPDATE
                    SET "FiscalProvider" = EXCLUDED."FiscalProvider", "IsPractice" = EXCLUDED."IsPractice";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER legacy_register_fiscal_fields ON aispa."CashRegisters";
                DROP FUNCTION aispa.legacy_register_fiscal_fields();
                """);

            migrationBuilder.DropColumn(
                name: "FiscalProvider",
                schema: "aispa",
                table: "CashRegisters");

            migrationBuilder.DropColumn(
                name: "IsPractice",
                schema: "aispa",
                table: "CashRegisters");

            // Auch ältere Versionen erlauben 50 Zeichen im Kassencode. Die TSE-Spalten behalten diese Breite,
            // damit bestehende Schlüssel und Protokolle nach einem Downgrade vollständig lesbar bleiben.
        }
    }
}
