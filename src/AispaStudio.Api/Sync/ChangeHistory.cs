namespace AispaStudio.Api.Sync;

/// <summary>
/// Änderungsverlauf (ADR 0082): ein Trigger neben dem Abgleich-Trigger (<see cref="ChangeLog"/>) schreibt je Änderung Tabelle, Schlüssel, Mandant, Gast,
/// Art, Zeitpunkt, Person, Kanal und bei Änderungen die alten und neuen Werte der geänderten Spalten nach <c>sync.history</c>. Person und Kanal setzt
/// die API je Verbindung als Sitzungseinstellung (<see cref="ChangeActorInterceptor"/>); ohne sie gilt die Änderung als Hintergrundjob, mit
/// <see cref="ChangeLog.OriginSetting"/> als Abgleich. Tabellen in den Schemas <c>health</c> und <c>hr</c> tragen nie Werte, nur Art, Person und Zeit.
/// Der Verlauf bleibt <see cref="RetentionYears"/> Jahre (<see cref="HistoryCleanup"/>) und fällt mit dem Gast, dem seine Zeilen gehören.
/// </summary>
public static class ChangeHistory
{
    public const string UserSetting = "aispa.user_id";

    public const string ChannelSetting = "aispa.channel";

    /// <summary>Verjährung nach § 1489 ABGB; die Kassa bleibt bei RKSV/TSE und den 7 Jahren der BAO.</summary>
    public const int RetentionYears = 3;

    /// <summary>
    /// Tabellen mit Verlauf (<c>schema.Tabelle</c>); dazu kommt jede Tabelle in den Schemas <c>health</c> und <c>hr</c>. Termine (Slice 90), Kundendaten, Behandlungen mit Preisen,
    /// Dienstplan mit Abwesenheiten sowie Zugänge und Rollen (Slice 91); Firmendaten des Spas (R05 Slice 93); Anfragen nur mit Status, Antwortstatus, Termin und Gast
    /// (Verlauf, Anfragen), nie mit Inhalt, Notiz, Antworttext oder Gastdaten der Mail.
    /// </summary>
    public static readonly string[] Tables =
    [
        "aispa.Appointments",
        "aispa.Customers", "aispa.CustomerEmails",
        "aispa.Treatments", "aispa.TreatmentLocationPrices", "aispa.TreatmentPriceRules",
        "aispa.RosterDays", "aispa.RosterDayEntries",
        "aispa.StaffAccess",
        "aispa.Tenants",
        "aispa.InboundMails",
    ];

    private static string TableList => string.Join(", ", Tables.Select(t => $"'{t}'"));

    /// <summary>Tabelle und Funktionen; mehrfach ausführbar. Ohne geschweifte Klammern, <c>ExecuteSqlRaw</c> liest sie als Platzhalter.</summary>
    public static string InstallSql => $$"""
        CREATE TABLE IF NOT EXISTS sync.history (
            seq bigserial PRIMARY KEY,
            table_schema text NOT NULL,
            table_name text NOT NULL,
            row_key jsonb NOT NULL,
            tenant_id uuid NULL,
            customer_id uuid NULL,
            operation char(1) NOT NULL,
            changed_at timestamptz NOT NULL DEFAULT now(),
            user_id text NULL,
            channel text NOT NULL,
            origin text NULL,
            changes jsonb NULL
        );
        -- Slice 91: subject = Datensatz, dem eine Zeile gehört (Behandlung, Person im Dienstplan, Benutzer); context = Angaben zur Zeile für den Satz im Verlauf.
        ALTER TABLE sync.history ADD COLUMN IF NOT EXISTS subject text NULL;
        ALTER TABLE sync.history ADD COLUMN IF NOT EXISTS context jsonb NULL;
        CREATE INDEX IF NOT EXISTS ix_history_subject ON sync.history (subject);
        CREATE INDEX IF NOT EXISTS ix_history_row ON sync.history (table_schema, table_name, row_key);
        CREATE INDEX IF NOT EXISTS ix_history_customer ON sync.history (customer_id);
        CREATE INDEX IF NOT EXISTS ix_history_changed_at ON sync.history (changed_at);

        CREATE OR REPLACE FUNCTION sync.log_history() RETURNS trigger LANGUAGE plpgsql AS $fn$
        DECLARE
            r jsonb;
            o jsonb;
            k jsonb := jsonb_build_object();
            c text;
            diff jsonb;
            customer uuid;
            subject text;
            ctx jsonb;
            parent record;
            origin text := nullif(current_setting('{{ChangeLog.OriginSetting}}', true), '');
            channel text := coalesce(nullif(current_setting('{{ChannelSetting}}', true), ''), 'Job');
        BEGIN
            -- Der Verlauf eines gelöschten Gastes fällt mit ihm (aispa_history_forget); das Löschen selbst hinterlässt keine Spur.
            IF TG_OP = 'DELETE' AND TG_TABLE_SCHEMA = 'aispa' AND TG_TABLE_NAME = 'Customers' THEN
                RETURN NULL;
            END IF;
            -- Eine Anfrage, die mit der Aufbewahrung fällt, hinterlässt keine Spur.
            IF TG_OP = 'DELETE' AND TG_TABLE_SCHEMA = 'aispa' AND TG_TABLE_NAME = 'InboundMails' THEN
                RETURN NULL;
            END IF;
            IF TG_OP = 'DELETE' THEN r := to_jsonb(OLD); ELSE r := to_jsonb(NEW); END IF;
            FOREACH c IN ARRAY TG_ARGV LOOP
                k := k || jsonb_build_object(c, r -> c);
            END LOOP;

            IF TG_OP = 'UPDATE' THEN
                o := to_jsonb(OLD);
                -- Nur die geänderten Spalten, alt und neu; UpdatedAt ändert sich bei jedem Speichern und sagt nichts.
                SELECT jsonb_object_agg(n.key, jsonb_build_array(o -> n.key, n.value)) INTO diff
                  FROM jsonb_each(r) n
                 WHERE n.key <> 'UpdatedAt' AND n.value IS DISTINCT FROM o -> n.key;
                -- Anfragen: nur Status, Antwortstatus, Termin und Gast; Text, Notiz, Antwort und Gastdaten der Mail bleiben draußen.
                IF TG_TABLE_SCHEMA = 'aispa' AND TG_TABLE_NAME = 'InboundMails' THEN
                    SELECT jsonb_object_agg(d.key, d.value) INTO diff FROM jsonb_each(diff) d
                     WHERE d.key IN ('Status', 'ReplyStatus', 'AppointmentId', 'CustomerId');
                END IF;
                IF diff IS NULL THEN
                    RETURN NULL;
                END IF;
            END IF;

            -- Gesundheitsdaten und Personalakte: nur Art, Person und Zeit, keine Inhalte (auch nicht verschlüsselt).
            IF TG_TABLE_SCHEMA IN ('health', 'hr') THEN
                diff := NULL;
            END IF;
            IF TG_TABLE_SCHEMA = 'hr' THEN
                subject := r ->> 'EmployeeId';
                ctx := jsonb_build_object('Kind', r -> 'Kind');
            END IF;

            IF origin IS NOT NULL THEN
                channel := 'Sync';
            END IF;

            -- Gast, dem die Zeile gehört: wird er gelöscht, fällt ihr Verlauf mit. Nur Guid-Werte (36 Zeichen aus Hex und Bindestrichen).
            IF TG_TABLE_SCHEMA = 'aispa' AND TG_TABLE_NAME = 'Customers' THEN
                customer := (r ->> 'Id')::uuid;
            ELSIF length(coalesce(r ->> 'CustomerId', '')) = 36 AND (r ->> 'CustomerId') ~ '^[0-9a-fA-F-]+$' THEN
                customer := (r ->> 'CustomerId')::uuid;
            END IF;
            IF TG_TABLE_SCHEMA = 'aispa' THEN
                CASE TG_TABLE_NAME
                    WHEN 'Appointments' THEN
                        -- Für den Verlauf je Standort (Seite „Verlauf“): Standort, Behandlung, Gast, Person, Beginn und Quelle zur Zeit der Änderung.
                        ctx := jsonb_build_object('LocationId', r -> 'LocationId', 'TreatmentId', r -> 'TreatmentId', 'CustomerId', r -> 'CustomerId',
                                                  'EmployeeId', r -> 'EmployeeId', 'Start', r -> 'Start', 'Source', r -> 'Source');
                    WHEN 'Treatments' THEN
                        subject := r ->> 'Id';
                    WHEN 'TreatmentLocationPrices' THEN
                        subject := r ->> 'TreatmentId';
                        ctx := jsonb_build_object('LocationId', r -> 'LocationId', 'Price', r -> 'Price');
                    WHEN 'TreatmentPriceRules' THEN
                        subject := r ->> 'TreatmentId';
                        ctx := jsonb_build_object('Name', r -> 'Name', 'Price', r -> 'Price');
                    WHEN 'RosterDays' THEN
                        subject := r ->> 'EmployeeId';
                        ctx := jsonb_build_object('Date', r -> 'Date');
                    WHEN 'RosterDayEntries' THEN
                        SELECT "EmployeeId", "Date" INTO parent FROM aispa."RosterDays" WHERE "Id" = (r ->> 'RosterDayId')::uuid;
                        IF NOT FOUND THEN
                            -- Der Tag ist samt Einträgen gelöscht; das Löschen des Tages steht schon im Verlauf.
                            RETURN NULL;
                        END IF;
                        subject := parent."EmployeeId"::text;
                        ctx := jsonb_build_object('Date', parent."Date", 'Kind', r -> 'Kind', 'Start', r -> 'Start', 'End', r -> 'End', 'LocationId', r -> 'LocationId');
                    WHEN 'Tenants' THEN
                        subject := r ->> 'Id';
                    WHEN 'StaffAccess' THEN
                        subject := r ->> 'UserId';
                        ctx := jsonb_build_object('Role', r -> 'Role');
                    WHEN 'CustomerEmails' THEN
                        ctx := jsonb_build_object('Address', r -> 'Address');
                    WHEN 'InboundMails' THEN
                        ctx := jsonb_build_object('LocationId', r -> 'LocationId', 'Channel', r -> 'Channel', 'Kind', r -> 'Kind', 'AppointmentId', r -> 'AppointmentId');
                    ELSE NULL;
                END CASE;
            END IF;

            IF TG_OP = 'UPDATE' AND (o ->> 'CustomerId') IS DISTINCT FROM (r ->> 'CustomerId') THEN
                UPDATE sync.history SET customer_id = customer
                 WHERE table_schema = TG_TABLE_SCHEMA AND table_name = TG_TABLE_NAME AND row_key = k;
            END IF;

            INSERT INTO sync.history (table_schema, table_name, row_key, tenant_id, customer_id, subject, context, operation, user_id, channel, origin, changes)
            VALUES (TG_TABLE_SCHEMA, TG_TABLE_NAME, k,
                    CASE WHEN TG_TABLE_SCHEMA = 'aispa' AND TG_TABLE_NAME = 'Tenants' THEN (r ->> 'Id')::uuid ELSE sync.row_tenant(TG_TABLE_SCHEMA, TG_TABLE_NAME, r) END, customer, subject, ctx, left(TG_OP, 1),
                    nullif(current_setting('{{UserSetting}}', true), ''), channel, origin, diff);
            RETURN NULL;
        END
        $fn$;

        -- Für das Löschen und, sobald es sie gibt, das Anonymisieren eines Gastes: der Verlauf seiner Zeilen fällt mit.
        CREATE OR REPLACE FUNCTION sync.forget_customer_history(customer uuid) RETURNS void LANGUAGE sql AS $fn$
            DELETE FROM sync.history WHERE customer_id = customer;
        $fn$;

        CREATE OR REPLACE FUNCTION sync.forget_deleted_customer() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            PERFORM sync.forget_customer_history(OLD."Id");
            RETURN NULL;
        END
        $fn$;

        -- Hängt den Verlaufs-Trigger an die genannten Tabellen und alle Tabellen in den Schemas health und hr; gibt deren Zahl zurück.
        -- Ein passender Trigger bleibt unberührt, siehe sync.attach_all.
        CREATE OR REPLACE FUNCTION sync.attach_history(tables text[]) RETURNS integer LANGUAGE plpgsql AS $fn$
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
                 WHERE c.relkind = 'r' AND c.relname <> '__EFMigrationsHistory'
                   AND (ns.nspname IN ('health', 'hr') OR ns.nspname || '.' || c.relname = ANY (tables))
            LOOP
                CONTINUE WHEN t.pk IS NULL;
                n := n + 1;
                CONTINUE WHEN EXISTS (SELECT 1 FROM pg_trigger tr
                                       WHERE tr.tgrelid = t.table_oid AND tr.tgname = 'aispa_history_log'
                                         AND pg_get_triggerdef(tr.oid) LIKE '%log_history(' || t.pk || ')');
                EXECUTE format('CREATE OR REPLACE TRIGGER aispa_history_log AFTER INSERT OR UPDATE OR DELETE ON %I.%I '
                               'FOR EACH ROW EXECUTE FUNCTION sync.log_history(%s)', t.schema_name, t.table_name, t.pk);
            END LOOP;

            IF to_regclass('aispa."Customers"') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM pg_trigger tr
                                WHERE tr.tgrelid = 'aispa."Customers"'::regclass AND tr.tgname = 'aispa_history_forget') THEN
                CREATE OR REPLACE TRIGGER aispa_history_forget AFTER DELETE ON aispa."Customers"
                    FOR EACH ROW EXECUTE FUNCTION sync.forget_deleted_customer();
            END IF;
            RETURN n;
        END
        $fn$;
        """;

    /// <summary>Hängt die Trigger an; nach <see cref="InstallSql"/>.</summary>
    public static string AttachSql => $"SELECT sync.attach_history(ARRAY[{TableList}]::text[]) AS \"Value\"";
}
