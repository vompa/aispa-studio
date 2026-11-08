using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Sync;

/// <summary>
/// Änderungsprotokoll für den Abgleich (ADR 0041, Schritt 4). Ein Trigger je Fachtabelle schreibt in derselben Transaktion
/// Tabelle, Schlüssel, Mandant und Art der Änderung nach <c>sync.changes</c>. So ist jede Änderung erfasst, auch aus
/// <c>ExecuteUpdate</c>, rohem SQL oder dem zweiten Kontext, ohne dass die Fachmodule etwas davon wissen müssen.
/// <c>aispa.sync_origin</c> (Sitzungs- oder Transaktionseinstellung) markiert Änderungen, die der Abgleich selbst einspielt,
/// damit er sie nicht zurückschickt.
/// </summary>
public static class ChangeLog
{
    /// <summary>Schemas mit Fachdaten; Wolverine und die Migrationstabellen gehören nicht dazu.</summary>
    public static readonly string[] Schemas = ["aispa", "online", "health", "hr"];

    public const string OriginSetting = "aispa.sync_origin";

    private static string SchemaList => string.Join(", ", Schemas.Select(s => $"'{s}'"));

    /// <summary>Tabellen und Funktionen; mehrfach ausführbar.</summary>
    public static string InstallSql => $$"""
        CREATE SCHEMA IF NOT EXISTS sync;

        CREATE TABLE IF NOT EXISTS sync.changes (
            seq bigserial PRIMARY KEY,
            table_schema text NOT NULL,
            table_name text NOT NULL,
            row_key jsonb NOT NULL,
            tenant_id uuid NULL,
            operation char(1) NOT NULL,
            changed_at timestamptz NOT NULL DEFAULT now(),
            origin text NULL
        );
        CREATE INDEX IF NOT EXISTS ix_changes_tenant_seq ON sync.changes (tenant_id, seq);
        -- Transaktion der Änderung: Der Abgleich liest nur abgeschlossene Transaktionen unterhalb von pg_snapshot_xmin, damit keine
        -- Änderung verloren geht, die eine kleinere seq hat, aber später festgeschrieben wird.
        ALTER TABLE sync.changes ADD COLUMN IF NOT EXISTS xid xid8 NOT NULL DEFAULT pg_current_xact_id();
        CREATE INDEX IF NOT EXISTS ix_changes_xid ON sync.changes (xid);

        -- Stand des Abgleichs je Gegenstelle: bis zu welcher Transaktion gesendet und geholt wurde.
        CREATE TABLE IF NOT EXISTS sync.peers (
            peer text PRIMARY KEY,
            pushed_below xid8 NOT NULL DEFAULT '0',
            pulled_below xid8 NOT NULL DEFAULT '0',
            last_run timestamptz NULL,
            last_error text NULL
        );

        -- Aufgeräumtes Protokoll (zentrale Seite): Änderungen bis einschließlich dieser Transaktion sind gelöscht. Wer noch nicht so
        -- weit geholt hat, holt statt des Protokolls den ganzen Bestand seines Spas.
        CREATE TABLE IF NOT EXISTS sync.pruned (
            single boolean PRIMARY KEY DEFAULT true CHECK (single),
            up_to xid8 NOT NULL,
            pruned_at timestamptz NOT NULL DEFAULT now()
        );

        -- Kennung dieser Datenbank (Stelle); wird nie abgeglichen. Jede Kasse gehört der Stelle, die sie anlegt oder zuerst benutzt.
        CREATE TABLE IF NOT EXISTS sync.site (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            created_at timestamptz NOT NULL DEFAULT now(),
            single boolean NOT NULL DEFAULT true UNIQUE CHECK (single)
        );
        INSERT INTO sync.site DEFAULT VALUES ON CONFLICT DO NOTHING;

        -- Schlüssel für Gesundheitsdaten je Stelle, verschlüsselt mit dem Schlüsselcode des Spas (ADR 0048). Der Code liegt nie hier.
        CREATE TABLE IF NOT EXISTS sync.health_keys (
            key_id text PRIMARY KEY,
            tenant_id uuid NOT NULL,
            sealed text NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        );

        -- Was die zentrale Seite abgelehnt hat (z. B. ein Termin, dessen Raum dort schon belegt ist). Die Zeile bleibt lokal erhalten.
        CREATE TABLE IF NOT EXISTS sync.conflicts (
            id bigserial PRIMARY KEY,
            table_schema text NOT NULL,
            table_name text NOT NULL,
            row_key jsonb NOT NULL,
            tenant_id uuid NULL,
            reason text NOT NULL,
            detected_at timestamptz NOT NULL DEFAULT now(),
            resolved_at timestamptz NULL
        );

        -- Mandant einer Zeile: aus TenantId oder, bei Tabellen ohne eigene Spalte (Zugänge, Anamnesen, Zuordnungen, Online-Daten), über
        -- die Zeile, auf die sie verweist. NULL, wenn sich keiner finden lässt (z. B. Anmeldecodes); solche Zeilen bleiben, wo sie sind.
        CREATE OR REPLACE FUNCTION sync.row_tenant(s text, t text, r jsonb) RETURNS uuid LANGUAGE plpgsql STABLE AS $fn$
        DECLARE
            result uuid;
            p record;
        BEGIN
            IF r ? 'TenantId' THEN
                RETURN (r ->> 'TenantId')::uuid;
            END IF;
            IF s = 'aispa' AND t = 'AspNetUsers' THEN
                SELECT a."TenantId" INTO result FROM aispa."StaffAccess" a WHERE a."UserId" = r ->> 'Id' LIMIT 1;
                RETURN result;
            END IF;
            IF s = 'aispa' AND r ? 'UserId' THEN
                SELECT a."TenantId" INTO result FROM aispa."StaffAccess" a WHERE a."UserId" = r ->> 'UserId' LIMIT 1;
                RETURN result;
            END IF;
            IF s = 'online' AND r ? 'UserId' THEN
                SELECT c."TenantId" INTO result FROM online."CustomerAccounts" a JOIN aispa."Customers" c ON c."Id" = a."CustomerId"
                 WHERE a."Id" = (r ->> 'UserId')::uuid;
                RETURN result;
            END IF;
            FOR p IN SELECT * FROM (VALUES ('LocationId', 'Locations'), ('CustomerId', 'Customers'), ('AppointmentId', 'Appointments'),
                                           ('TreatmentId', 'Treatments'), ('EmployeeId', 'Employees'), ('RoomsId', 'Rooms'),
                                           ('QualificationsId', 'Qualifications'), ('RequiredQualificationsId', 'Qualifications'),
                                           ('EquipmentId', 'Equipment'), ('ReceiptId', 'Receipts')) v(col, parent)
            LOOP
                -- Nur Guid-Werte (36 Zeichen aus Hex und Bindestrichen); keine geschweiften Klammern, ExecuteSqlRaw liest sie als Platzhalter.
                CONTINUE WHEN length(coalesce(r ->> p.col, '')) <> 36 OR (r ->> p.col) !~ '^[0-9a-fA-F-]+$';
                EXECUTE format('SELECT "TenantId" FROM aispa.%I WHERE "Id" = $1', p.parent) INTO result USING (r ->> p.col)::uuid;
                IF result IS NOT NULL THEN
                    RETURN result;
                END IF;
            END LOOP;
            RETURN NULL;
        END
        $fn$;

        -- Beim Lesen des Protokolls: Mandant einer Änderung ohne Mandant aus dem jetzigen Stand der Zeile. Ein Zugang wird z. B. vor
        -- seiner Berechtigung (StaffAccess) angelegt; beim Schreiben war der Mandant noch nicht bekannt.
        CREATE OR REPLACE FUNCTION sync.key_tenant(s text, t text, k jsonb) RETURNS uuid LANGUAGE plpgsql STABLE AS $fn$
        DECLARE
            r jsonb;
            cond text;
        BEGIN
            IF to_regclass(format('%I.%I', s, t)) IS NOT NULL THEN
                SELECT string_agg(format('t.%I = k.%I', key, key), ' AND ') INTO cond FROM jsonb_object_keys(k) key;
                EXECUTE format('SELECT to_jsonb(t) FROM %I.%I t, jsonb_populate_record(NULL::%I.%I, $1) k WHERE %s', s, t, s, t, cond)
                   INTO r USING k;
            END IF;
            RETURN sync.row_tenant(s, t, coalesce(r, k));
        END
        $fn$;

        CREATE OR REPLACE FUNCTION sync.log_change() RETURNS trigger LANGUAGE plpgsql AS $fn$
        DECLARE
            r jsonb;
            k jsonb := jsonb_build_object();
            c text;
        BEGIN
            IF TG_OP = 'DELETE' THEN r := to_jsonb(OLD); ELSE r := to_jsonb(NEW); END IF;
            FOREACH c IN ARRAY TG_ARGV LOOP
                k := k || jsonb_build_object(c, r -> c);
            END LOOP;
            INSERT INTO sync.changes (table_schema, table_name, row_key, tenant_id, operation, origin)
            VALUES (TG_TABLE_SCHEMA, TG_TABLE_NAME, k, sync.row_tenant(TG_TABLE_SCHEMA, TG_TABLE_NAME, r), left(TG_OP, 1),
                    nullif(current_setting('{{OriginSetting}}', true), ''));
            RETURN NULL;
        END
        $fn$;

        -- Hängt den Trigger an jede Fachtabelle mit Primärschlüssel und macht Fremdschlüssel aufschiebbar, damit der Abgleich
        -- Zeilen in beliebiger Reihenfolge einspielen kann (SET CONSTRAINTS ALL DEFERRED). Gibt die Zahl der Tabellen zurück.
        -- Ein passender Trigger bleibt unberührt: Ihn zu ersetzen sperrt die Tabelle bis zum Ende der Funktion und verklemmt sich
        -- mit Transaktionen, die gerade in mehrere Tabellen schreiben.
        CREATE OR REPLACE FUNCTION sync.attach_all() RETURNS integer LANGUAGE plpgsql AS $fn$
        DECLARE
            t record;
            n integer := 0;
        BEGIN
            FOR t IN
                SELECT ns.nspname AS schema_name, c.relname AS table_name, c.oid AS table_oid,
                       (SELECT string_agg(quote_literal(a.attname), ', ' ORDER BY array_position(i.indkey::int2[], a.attnum))
                          FROM pg_index i
                          JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY (i.indkey::int2[])
                         WHERE i.indrelid = c.oid AND i.indisprimary) AS pk
                  FROM pg_class c
                  JOIN pg_namespace ns ON ns.oid = c.relnamespace
                 WHERE c.relkind = 'r' AND ns.nspname IN ({{SchemaList}}) AND c.relname <> '__EFMigrationsHistory'
            LOOP
                CONTINUE WHEN t.pk IS NULL;
                n := n + 1;
                CONTINUE WHEN EXISTS (SELECT 1 FROM pg_trigger tr
                                       WHERE tr.tgrelid = t.table_oid AND tr.tgname = 'aispa_sync_log'
                                         AND pg_get_triggerdef(tr.oid) LIKE '%log_change(' || t.pk || ')');
                EXECUTE format('CREATE OR REPLACE TRIGGER aispa_sync_log AFTER INSERT OR UPDATE OR DELETE ON %I.%I '
                               'FOR EACH ROW EXECUTE FUNCTION sync.log_change(%s)', t.schema_name, t.table_name, t.pk);
            END LOOP;

            FOR t IN
                SELECT ns.nspname AS schema_name, c.relname AS table_name, con.conname
                  FROM pg_constraint con
                  JOIN pg_class c ON c.oid = con.conrelid
                  JOIN pg_namespace ns ON ns.oid = c.relnamespace
                 WHERE con.contype = 'f' AND NOT con.condeferrable AND ns.nspname IN ({{SchemaList}})
            LOOP
                EXECUTE format('ALTER TABLE %I.%I ALTER CONSTRAINT %I DEFERRABLE INITIALLY IMMEDIATE',
                               t.schema_name, t.table_name, t.conname);
            END LOOP;
            RETURN n;
        END
        $fn$;
        """;

    /// <summary>
    /// Nach jeder Migration: neue Tabellen bekommen den Trigger, neue Fremdschlüssel werden aufschiebbar. Beide Kontexte müssen
    /// vorher migriert sein, sonst fehlen ihre Tabellen.
    /// </summary>
    public static async Task<int> AttachAsync(DbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(InstallSql, ct);
        var attached = await db.Database.SqlQueryRaw<int>("SELECT sync.attach_all() AS \"Value\"").SingleAsync(ct);
        // Änderungsverlauf (ADR 0082) am selben Ort: braucht row_tenant aus InstallSql und die Tabellen beider Kontexte.
        await db.Database.ExecuteSqlRawAsync(ChangeHistory.InstallSql, ct);
        _ = await db.Database.SqlQueryRaw<int>(ChangeHistory.AttachSql).SingleAsync(ct);
        return attached;
    }
}
