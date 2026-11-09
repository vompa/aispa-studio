using System.Globalization;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Verlauf eines Termins für Menschen (ADR 0082): liest <c>sync.history</c> und macht aus jedem Eintrag einen Satz wie „Angelegt per SMS“,
/// „Verschoben von Lena: 14:00 → 15:30“ oder „Storniert von Lena: Gast krank“; dazu „Bestätigung per Mail an … gesendet von Lena“ (PDF-Ausgaben C). Uhrzeiten in der Zeitzone des Standorts, Personen mit ihrem
/// Anzeigenamen zur Zeit des Lesens. Den Verlauf sieht, wer den Termin sieht: der Termin kommt über den Mandantenfilter.
/// </summary>
public sealed class AppointmentHistory(AispaDbContext db)
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
        var rows = await db.Database.SqlQuery<HistoryRow>($"""
            SELECT operation::text AS "Operation", changed_at AS "ChangedAt", user_id AS "UserId", channel AS "Channel", changes::text AS "Changes"
              FROM sync.history
             WHERE table_schema = 'aispa' AND table_name = 'Appointments' AND row_key = CAST({key} AS jsonb)
             ORDER BY changed_at DESC, seq DESC
            """).ToListAsync(ct);

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

        var describer = new Describer(appointment.Source, zone, people, resources);
        var sent = (await SentDocuments.ListAsync(db, SentDocument.AppointmentConfirmationKind, [appointmentId], ct)).GetValueOrDefault(appointmentId) ?? [];
        return [.. rows.Select((row, i) => new HistoryEntry(row.ChangedAt, describer.Describe(row, changes[i])))
            .Concat(sent.Select(d => new HistoryEntry(d.SentAt, $"Bestätigung per Mail an {d.To} gesendet{(d.SentBy is null ? "" : $" von {d.SentBy}")}")))
            .OrderByDescending(e => e.At)];
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

    /// <summary>Eine Zeile aus <c>sync.history</c>.</summary>
    private sealed class HistoryRow
    {
        public required string Operation { get; init; }
        public DateTimeOffset ChangedAt { get; init; }
        public string? UserId { get; init; }
        public required string Channel { get; init; }
        public string? Changes { get; init; }
    }

    private sealed class Describer(AppointmentSource source, TimeZoneInfo zone, Dictionary<string, string> people, Dictionary<Guid, string> resources)
    {
        public string Describe(HistoryRow row, Dictionary<string, (JsonElement Old, JsonElement New)> changes)
        {
            var channel = HistoryActor.Channel(row.Channel);
            var by = Actor(channel, row.UserId);
            return row.Operation switch
            {
                "I" => Created(channel, row.UserId),
                "D" => $"Gelöscht {by}",
                _ => Changed(changes, channel, by),
            };
        }

        /// <summary>„von Lena“, „per SMS“, „online vom Gast“, „automatisch“ …</summary>
        private string Actor(ChangeChannel channel, string? userId) => HistoryActor.Text(channel, userId, people);

        private string Person(string userId) => HistoryActor.Person(userId, people);

        /// <summary>Anlegen: über die Quelle des Termins, wenn kein Kanal mit Person dabei war (Terminannahme im Hintergrund, alte Termine).</summary>
        private string Created(ChangeChannel channel, string? userId)
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
