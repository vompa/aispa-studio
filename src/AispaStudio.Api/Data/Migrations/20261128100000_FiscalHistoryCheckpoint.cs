using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AispaStudio.Api.Data.Migrations;

[DbContext(typeof(AispaDbContext))]
[Migration("20261128100000_FiscalHistoryCheckpoint")]
public sealed class FiscalHistoryCheckpoint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE SCHEMA IF NOT EXISTS aispa_maintenance;
        CREATE TABLE aispa_maintenance.fiscal_history_checkpoint (
            tenant_id uuid NOT NULL, register_id uuid NOT NULL REFERENCES aispa."CashRegisters"("Id") ON DELETE CASCADE,
            register_code text, through_nr bigint NOT NULL DEFAULT 0, fiscal_date date, turnover bigint NOT NULL DEFAULT 0,
            pending numeric[] NOT NULL DEFAULT ARRAY[]::numeric[], scheduled_year integer, scheduled_nr bigint, scheduled_counter bigint,
            PRIMARY KEY (tenant_id, register_id)
        );
        CREATE FUNCTION aispa_maintenance.invalidate_fiscal_prefix() RETURNS trigger LANGUAGE plpgsql AS $body$
        BEGIN
            -- Auch der erste Aufbau (noch ohne Prüfzeile) und rückwirkende Sync-Schreibvorgänge
            -- verwenden die Kassensperre. Bei Registerwechseln gilt dieselbe sortierte Reihenfolge.
            PERFORM 1 FROM aispa."CashRegisters" WHERE "Id" IN (OLD."RegisterId", NEW."RegisterId")
                ORDER BY "Id" FOR UPDATE;
            IF TG_OP <> 'INSERT' THEN
                UPDATE aispa_maintenance.fiscal_history_checkpoint SET through_nr = 0, scheduled_nr = NULL
                WHERE tenant_id = OLD."TenantId" AND register_id = OLD."RegisterId"
                  AND (through_nr >= OLD."Number" OR scheduled_nr >= OLD."Number");
            END IF;
            IF TG_OP <> 'DELETE' THEN
                UPDATE aispa_maintenance.fiscal_history_checkpoint SET through_nr = 0, scheduled_nr = NULL
                WHERE tenant_id = NEW."TenantId" AND register_id = NEW."RegisterId" AND through_nr >= NEW."Number";
            END IF;
            RETURN NULL;
        END $body$;
        CREATE TRIGGER fiscal_prefix_changed AFTER INSERT OR UPDATE OR DELETE ON aispa."Receipts"
            FOR EACH ROW EXECUTE FUNCTION aispa_maintenance.invalidate_fiscal_prefix();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER fiscal_prefix_changed ON aispa."Receipts";
        DROP FUNCTION aispa_maintenance.invalidate_fiscal_prefix();
        DROP TABLE aispa_maintenance.fiscal_history_checkpoint;
        """);
}
