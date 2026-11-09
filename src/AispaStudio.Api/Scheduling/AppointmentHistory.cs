using System.Globalization;
using System.Text.Json;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Verlauf eines Termins für Menschen (ADR 0082): liest <c>sync.history</c> und macht aus jedem Eintrag einen Satz wie „Angelegt per SMS“,
/// „Verschoben von Lena: 14:00 → 15:30“ oder „Storniert von Lena: Gast krank“. Uhrzeiten in der Zeitzone des Standorts, Personen mit ihrem
/// Anzeigenamen zur Zeit des Lesens. Den Verlauf sieht, wer den Termin sieht: der Termin kommt über den Mandantenfilter. Zahlungen zum Termin aus der Kassa
/// (<see cref="PaymentHistory"/>) stehen zeitlich einsortiert dazwischen, für alle, die die Kassa sehen.
/// </summary>
public sealed class AppointmentHistory(AispaDbContext db, PaymentHistory payments, AccessService access)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Neueste oben; <c>null</c>, wenn es den Termin (für diesen Mandanten) nicht gibt.</summary>
    public async Task<List<HistoryEntry>?> ForAsync(Guid appointmentId, CancellationToken ct)
    {
        var appointment = await db.Appointments.AsNoTracking().Where(a => a.Id == appointmentId)
            .Select(a => new { a.Source, a.LocationId }).FirstOrDefaultAsync(ct);
        if (appointment is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(await db.Locations.AsNoTracking().Where(l => l.Id == appointment.LocationId).Select(l => l.TimeZone).SingleAsync(ct));
        var key = JsonSerializer.Serialize(new { Id = appointmentId });
#pragma warning disable EF1002
        var rows = await db.Database.SqlQueryRaw<HistoryRow>($$"""
            SELECT {{HistoryRow.Columns}}
              FROM sync.history h
             WHERE h.table_schema = 'aispa' AND h.table_name = 'Appointments' AND h.row_key = CAST({0} AS jsonb)
             ORDER BY h.changed_at DESC, h.seq DESC
            """, key).ToListAsync(ct);
#pragma warning restore EF1002

        var described = await DescribeAsync(rows, _ => appointment.Source, zone, ct);
        var entries = rows.Select((row, i) => new HistoryEntry(row.ChangedAt, described[i].Text));
        if (await access.CurrentAsync(ct) is { } current && current.Has(AccessRight.Book))
        {
            entries = entries.Concat((await payments.ForAppointmentAsync(appointmentId, ct)).Select(p => new HistoryEntry(p.At, p.Text)));
        }

        return [.. entries.OrderByDescending(e => e.At)];
    }

    /// <summary>Satz und Werte je Zeile, in der Reihenfolge der Zeilen; <paramref name="sourceOf"/> nennt die Quelle des Termins einer Zeile (für „Angelegt per SMS“).</summary>
    internal async Task<List<EntityHistory.Described>> DescribeAsync(
        IReadOnlyList<HistoryRow> rows, Func<HistoryRow, AppointmentSource> sourceOf, TimeZoneInfo zone, CancellationToken ct)
    {
        var changes = rows.Select(r => Changes(r.Changes)).ToList();
        var userIds = rows.Select(r => r.UserId).OfType<string>().Distinct().ToList();
        var people = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var resourceIds = changes.SelectMany(c => c.Where(p => p.Key is "EmployeeId" or "RoomId").SelectMany(p => new[] { p.Value.Old, p.Value.New }))
            .Select(GuidOf).OfType<Guid>().Distinct().ToList();
        var resources = await db.Employees.AsNoTracking().Where(e => resourceIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);
        foreach (var room in await db.Rooms.AsNoTracking().Where(r => resourceIds.Contains(r.Id)).Select(r => new { r.Id, r.Name }).ToListAsync(ct))
        {
            resources[room.Id] = room.Name;
        }

        var describer = new Describer(zone, people, resources);
        return [.. rows.Select((row, i) => new EntityHistory.Described(describer.Describe(row, changes[i], sourceOf(row)), describer.Values(changes[i])))];
    }

    private static Dictionary<string, (JsonElement Old, JsonElement New)> Changes(string? json)
    {
        if (json is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => (p.Value[0].Clone(), p.Value[1].Clone()), StringComparer.Ordinal);
    }

    private static Guid? GuidOf(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? id : null;

    private sealed class Describer(TimeZoneInfo zone, Dictionary<string, string> people, Dictionary<Guid, string> resources)
    {
        public string Describe(HistoryRow row, Dictionary<string, (JsonElement Old, JsonElement New)> changes, AppointmentSource source)
        {
            var channel = HistoryActor.Channel(row.Channel);
            var by = Actor(channel, row.UserId);
            return row.Operation switch
            {
                "I" => Created(source, channel, row.UserId),
                "D" => $"Gelöscht {by}",
                _ => Changed(changes, channel, by),
            };
        }

        /// <summary>„von Lena“, „per SMS“, „online vom Gast“, „automatisch“ …</summary>
        private string Actor(ChangeChannel channel, string? userId) => HistoryActor.Text(channel, userId, people);

        private string Person(string userId) => HistoryActor.Person(userId, people);

        /// <summary>Anlegen: über die Quelle des Termins, wenn kein Kanal mit Person dabei war (Terminannahme im Hintergrund, alte Termine).</summary>
        private string Created(AppointmentSource source, ChangeChannel channel, string? userId)
        {
            var person = channel == ChangeChannel.Team && userId is not null ? Person(userId) : null;
            if (source == AppointmentSource.Reception)
            {
                return person is null ? $"Angelegt {Actor(channel, userId)}" : $"Angelegt von {person}";
            }

            var text = source switch
            {
                AppointmentSource.Online => "Online gebucht vom Gast",
                AppointmentSource.Email => "Angelegt per Mail",
                AppointmentSource.Sms => "Angelegt per SMS",
                AppointmentSource.WhatsApp => "Angelegt per WhatsApp",
                AppointmentSource.Phone => "Angelegt per Telefon",
                AppointmentSource.Treatwell => "Angelegt über Treatwell",
                _ => "Angelegt",
            };
            return person is null ? text : $"{text}, übernommen von {person}";
        }

        private string Changed(Dictionary<string, (JsonElement Old, JsonElement New)> changes, ChangeChannel channel, string by)
        {
            if (changes.TryGetValue("Status", out var status))
            {
                return StatusText(changes, Text(status.Old), Text(status.New), by);
            }

            var moved = Moves(changes);
            if (moved.Count > 0)
            {
                return $"{(changes.ContainsKey("Start") ? "Verschoben" : "Umgebucht")} {by}: {string.Join(", ", moved)}";
            }

            if (changes.TryGetValue("Note", out var note))
            {
                return Text(note.New) is null ? $"Notiz gelöscht {by}" : $"Notiz geändert {by}";
            }

            if (changes.ContainsKey("PreparationMinutes") || changes.ContainsKey("FollowUpMinutes"))
            {
                return $"Vor- und Nachbereitung geändert {by}";
            }

            if (changes.TryGetValue("LateMinutes", out var late))
            {
                return late.New.ValueKind == JsonValueKind.Number && late.New.GetInt32() > 0
                    ? $"Verspätung gemeldet {by}: {late.New.GetInt32()} min"
                    : $"Verspätung zurückgenommen {by}";
            }

            if (changes.ContainsKey("CustomerId") || changes.ContainsKey("PlaceholderGuest"))
            {
                return $"Gast geändert {by}";
            }

            if (changes.TryGetValue("ConfirmedAt", out var confirmed) && confirmed.New.ValueKind == JsonValueKind.String)
            {
                return channel == ChangeChannel.Online ? "Vom Gast bestätigt (online)" : $"Als bestätigt vermerkt {by}";
            }

            if (changes.TryGetValue("AcceptInPlatformPending", out var pending) && pending.New.ValueKind == JsonValueKind.False)
            {
                return $"In Treatwell als angenommen vermerkt {by}";
            }

            return $"Geändert {by}";
        }

        private static string StatusText(Dictionary<string, (JsonElement Old, JsonElement New)> changes, string? from, string? to, string by)
        {
            switch (to)
            {
                case nameof(AppointmentStatus.Cancelled):
                    var business = changes.TryGetValue("CancelledByBusiness", out var flag) && flag.New.ValueKind == JsonValueKind.True;
                    var reason = changes.TryGetValue("CancelReason", out var why) && Text(why.New) is { Length: > 0 } text ? $": {text}" : "";
                    return $"{(business ? "Vom Betrieb abgesagt" : "Storniert")} {by}{reason}";
                case nameof(AppointmentStatus.NoShow):
                    return $"Als nicht erschienen markiert {by}";
                case nameof(AppointmentStatus.CheckedIn):
                    return $"Eingecheckt {by}";
                case nameof(AppointmentStatus.Completed):
                    return $"Abgeschlossen {by}";
                default:
                    return from switch
                    {
                        nameof(AppointmentStatus.Cancelled) or nameof(AppointmentStatus.NoShow) => $"Wiederhergestellt {by}",
                        nameof(AppointmentStatus.CheckedIn) => $"Check-in zurückgenommen {by}",
                        _ => $"Status geändert {by}",
                    };
            }
        }

        /// <summary>Geänderte Spalten als Werte für die Verlaufsseite: Beginn (Ortszeit), Person, Raum, Status, Grund und Verspätung; Notiz und Gast nur als Satz.</summary>
        public List<ActivityValue> Values(Dictionary<string, (JsonElement Old, JsonElement New)> changes)
        {
            var values = new List<ActivityValue>();
            if (changes.TryGetValue("Start", out var start))
            {
                values.Add(new ActivityValue("Beginn", Moment(start.Old), Moment(start.New)));
            }

            if (changes.TryGetValue("EmployeeId", out var employee))
            {
                values.Add(new ActivityValue("Person", Resource(employee.Old), Resource(employee.New)));
            }

            if (changes.TryGetValue("RoomId", out var room))
            {
                values.Add(new ActivityValue("Raum", Resource(room.Old), Resource(room.New)));
            }

            if (changes.TryGetValue("Status", out var status))
            {
                values.Add(new ActivityValue("Status", Status(status.Old), Status(status.New)));
            }

            if (changes.TryGetValue("CancelReason", out var reason) && Text(reason.New) is { Length: > 0 })
            {
                values.Add(new ActivityValue("Grund", Text(reason.Old), Text(reason.New)));
            }

            if (changes.TryGetValue("LateMinutes", out var late))
            {
                values.Add(new ActivityValue("Verspätung", Minutes(late.Old), Minutes(late.New)));
            }

            return values;
        }

        private string? Moment(JsonElement value) =>
            value.ValueKind == JsonValueKind.String ? TimeZoneInfo.ConvertTime(value.GetDateTimeOffset(), zone).ToString("d.M.yyyy HH:mm", German) : null;

        private static string? Minutes(JsonElement value) => value.ValueKind == JsonValueKind.Number ? $"{value.GetInt32()} min" : null;

        private static string? Status(JsonElement value) => Text(value) switch
        {
            nameof(AppointmentStatus.Booked) => "Gebucht",
            nameof(AppointmentStatus.CheckedIn) => "Eingecheckt",
            nameof(AppointmentStatus.Completed) => "Abgeschlossen",
            nameof(AppointmentStatus.NoShow) => "Nicht erschienen",
            nameof(AppointmentStatus.Cancelled) => "Storniert",
            var other => other,
        };

        /// <summary>„14:00 → 15:30“ (an einem anderen Tag mit Datum), „Anna → Ben“, „Raum A → Raum B“.</summary>
        private List<string> Moves(Dictionary<string, (JsonElement Old, JsonElement New)> changes)
        {
            var parts = new List<string>();
            if (changes.TryGetValue("Start", out var start) && start.Old.ValueKind == JsonValueKind.String && start.New.ValueKind == JsonValueKind.String)
            {
                var (from, to) = (TimeZoneInfo.ConvertTime(start.Old.GetDateTimeOffset(), zone), TimeZoneInfo.ConvertTime(start.New.GetDateTimeOffset(), zone));
                parts.Add(from.Date == to.Date
                    ? $"{from.ToString("HH:mm", German)} → {to.ToString("HH:mm", German)}"
                    : $"{from.ToString("d.M. HH:mm", German)} → {to.ToString("d.M. HH:mm", German)}");
            }

            foreach (var column in new[] { "EmployeeId", "RoomId" })
            {
                if (changes.TryGetValue(column, out var change))
                {
                    parts.Add($"{Resource(change.Old)} → {Resource(change.New)}");
                }
            }

            return parts;
        }

        private string Resource(JsonElement value) => GuidOf(value) is { } id && resources.TryGetValue(id, out var name) ? name : "–";

        private static string? Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
