using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant;

/// <summary>
/// KI-Anfrage im Kalender (Strg+K): Die KI liest aus dem Text Behandlung, Person und Wunschzeit (<see cref="IBookingAssistant"/>),
/// Ausschlüsse („nicht bei Marco“), Raum („im Raum Lotus“) und die Zahl der Gäste („zu zweit“) erkennen feste Regeln. Die freien Zeiten und
/// ihre Reihenfolge rechnet der <see cref="ResourcePlanner"/> bzw. dieser Dienst: nah an der Wunschzeit, gewünschte Person zuerst,
/// lückenlos anschließend vor Lücken. Es gibt immer mindestens drei Vorschläge, solange in den nächsten 14 Tagen so viel frei ist;
/// fehlt die Behandlung, liefert der Dienst die passendsten als Auswahl, und bei Behandlungen mit Paarraum fragt er nach der Zahl der Gäste.
/// </summary>
internal sealed partial class CalendarRequestService(AispaDbContext db, BookingService booking, IBookingAssistant reader, TimeProvider time)
{
    private const int MaxProposals = 3;

    /// <summary>So viele Tage ab dem Wunschtag sucht der Dienst nach Vorschlägen.</summary>
    private const int SearchDays = 14;

    /// <summary>So viele Behandlungen bietet der Dienst zur Auswahl an, wenn der Text keine nennt.</summary>
    private const int MaxChoices = 5;

    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Wörter in Raumnamen, die nichts unterscheiden („Raum Lotus“, „Kosmetikkabine“).</summary>
    private static readonly HashSet<string> GenericRoomWords = new(StringComparer.OrdinalIgnoreCase) { "raum", "room", "zimmer", "kabine", "suite", "der", "die", "das" };

    public async Task<CalendarRequestResult?> ProposeAsync(Guid locationId, CalendarRequestInput input, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var now = time.GetUtcNow();
        var today = LocalTime.LocalDate(now, zone);
        var treatments = await OnlineBookingEndpoints.OfferedTreatmentsAsync(db, location.Id, ct);
        var employees = await db.Employees.AsNoTracking().Include(e => e.Qualifications)
            .Where(e => e.IsActive && e.WorkingHours.Any(w => w.LocationId == location.Id)).OrderBy(e => e.PreferredName ?? e.DisplayName).ToListAsync(ct);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == location.Id && r.IsActive && !r.IsArea).OrderBy(r => r.Name).ToListAsync(ct);

        var wish = input.Wish ?? new CalendarWish(input.Date is { } open && open >= today ? open : today, null, null, null, null, []);
        if (!string.IsNullOrWhiteSpace(input.Text))
        {
            wish = await ReadAsync(input.Text.Trim(), wish, location, treatments, employees, rooms, ct);
        }

        if (wish.Date < today)
        {
            wish = wish with { Date = today };
        }

        var treatment = treatments.FirstOrDefault(t => t.Id == wish.TreatmentId);
        var names = employees.ToDictionary(e => e.Id, e => e.InHouseName);
        var chips = Chips(wish, treatment, names, rooms);
        if (treatment is null)
        {
            var choices = await ChoicesAsync(location, treatments, employees, wish, input.Text, ct);
            return new CalendarRequestResult(wish with { TreatmentId = null }, chips, [],
                "Welche Behandlung? Die passendsten stehen unten, alle Behandlungen darunter.", RoleAssistant.Disclosure, TreatmentChoices: choices);
        }

        var (proposals, hasCoupleRoom) = await ProposalsAsync(location, zone, treatment, wish, input.Rejected ?? [], names, now, ct);
        var preferred = wish.EmployeeId is { } wanted ? names.GetValueOrDefault(wanted) : null;
        var couple = wish.Guests == 2 ? " für zwei Personen im Paarraum" : "";
        var text = proposals.Count == 0
            ? $"Für {treatment.Name}{couple} ist am {Day(wish.Date)} und in den {SearchDays - 1} Tagen danach nichts frei."
            : $"{(proposals.Count == 1 ? "1 Vorschlag" : $"{proposals.Count} Vorschläge")} für {treatment.Name}{couple}"
                + (DaysOf(proposals, zone) is var days && days[0].Day == wish.Date && days.Count == 1 ? $" am {Day(wish.Date)}"
                    : days[0].Day == wish.Date ? $": {string.Join(" und ", days.Select(d => $"{d.Count} am {Day(d.Day)}"))} (am {Day(wish.Date)} ist nicht mehr frei)"
                    : $": am {Day(wish.Date)} ist nichts frei, daher ab {Day(days[0].Day)}")
                + (preferred is not null && proposals.All(p => p.EmployeeId != wish.EmployeeId) ? $"; bei {preferred} ist nichts frei" : "")
                + ". Gebucht wird erst nach deiner Bestätigung.";
        return new CalendarRequestResult(wish, chips, proposals, text, RoleAssistant.Disclosure,
            Questions: Questions(wish, hasCoupleRoom));
    }

    /// <summary>Die Tage der Vorschläge in Reihenfolge mit der Zahl der Vorschläge je Tag.</summary>
    private static List<(DateOnly Day, int Count)> DaysOf(IReadOnlyList<CalendarProposal> proposals, TimeZoneInfo zone) =>
        [.. proposals.GroupBy(p => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(p.Start, zone).DateTime)).OrderBy(g => g.Key).Select(g => (g.Key, g.Count()))];

    /// <summary>Rückfrage zur Zahl der Gäste, wenn die Behandlung einen Paarraum hat und der Wunsch dazu nichts sagt; jede Antwort ist der geänderte Wunsch.</summary>
    private static List<CalendarQuestion> Questions(CalendarWish wish, bool hasCoupleRoom) =>
        hasCoupleRoom && wish.Guests is null
            ?
            [
                new("Für eine oder zwei Personen?",
                [
                    new("Eine Person", wish with { Guests = 1 }),
                    new("Zwei Personen (Paarraum)", wish with { Guests = 2 }),
                ]),
            ]
            : [];

    /// <summary>
    /// Die passendsten Behandlungen, wenn der Wunsch keine nennt: Wörter des Textes in Name und Gruppe, bei Wunschperson nur, was sie kann,
    /// bei zwei Gästen Behandlungen mit Paarraum, sonst was in den letzten 90 Tagen am häufigsten gebucht wurde.
    /// </summary>
    private async Task<List<Guid>> ChoicesAsync(Location location, List<Treatment> treatments, List<Employee> employees, CalendarWish wish, string? text,
        CancellationToken ct)
    {
        var since = time.GetUtcNow().AddDays(-90);
        var popularity = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == location.Id && a.Start >= since && a.Status != AppointmentStatus.Cancelled)
            .GroupBy(a => a.TreatmentId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var ids = treatments.Select(t => t.Id).ToList();
        var coupleIds = (await db.Treatments.AsNoTracking().Where(t => ids.Contains(t.Id))
                .Select(t => new { t.Id, Couple = t.Rooms.Any(r => r.LocationId == location.Id && r.IsActive && r.Places >= 2) }).ToListAsync(ct))
            .Where(x => x.Couple).Select(x => x.Id).ToHashSet();
        var wanted = wish.EmployeeId is { } id ? employees.FirstOrDefault(e => e.Id == id) : null;
        var words = Words(text ?? "").Where(w => w.Length >= 3).Select(w => w.ToLowerInvariant()).ToHashSet();
        var max = Math.Max(1, popularity.Values.DefaultIfEmpty(1).Max());

        bool CanDo(Treatment t) => wanted is null || t.RequiredQualifications.All(q => wanted.Qualifications.Any(x => x.Id == q.Id));
        double Score(Treatment t)
        {
            var fit = Words(t.Name + " " + t.Category).Count(w => words.Any(x => w.StartsWith(x, StringComparison.OrdinalIgnoreCase) || x.StartsWith(w, StringComparison.OrdinalIgnoreCase)));
            return fit * 3 + (wish.Guests == 2 && coupleIds.Contains(t.Id) ? 2 : 0) + (double)popularity.GetValueOrDefault(t.Id) / max;
        }

        return [.. treatments.Where(CanDo).Where(t => wish.Guests != 2 || coupleIds.Contains(t.Id))
            .OrderByDescending(Score).ThenBy(t => t.Name, StringComparer.CurrentCulture).Take(MaxChoices).Select(t => t.Id)];
    }

    /// <summary>Text lesen und in den bisherigen Wunsch einarbeiten: was der Text nennt, ersetzt den Baustein; der Rest bleibt.</summary>
    private async Task<CalendarWish> ReadAsync(string text, CalendarWish wish, Location location, List<Treatment> treatments,
        List<Employee> employees, List<Room> rooms, CancellationToken ct)
    {
        var analysis = await reader.AnalyzeAsync(new MailAnalysisRequest(
            "kalender@aispa.local", null, "Anfrage am Telefon", text, time.GetUtcNow(), location.TimeZone,
            [.. treatments.Select(t => new NamedOption(t.Id, t.Name))],
            [new NamedOption(location.Id, location.Name)],
            [.. employees.Select(e => new NamedOption(e.Id, EmployeeNames.Recognizable(e.DisplayName, e.PreferredName)))]), ct);

        var (excluded, rest) = Exclusions(text, employees);
        var excludedIds = wish.ExcludedEmployeeIds.Concat(excluded).Distinct().ToList();

        // Die KI liest auch einen ausgeschlossenen Namen als Wunschperson; dann zählt ein anderer genannter Name oder keiner.
        var employeeId = analysis.EmployeeId is { } read && !excluded.Contains(read) ? read : NamedIn(rest, employees, excludedIds);
        if (employeeId is { } chosen)
        {
            excludedIds.Remove(chosen);
        }
        else if (wish.EmployeeId is { } previous && !excludedIds.Contains(previous))
        {
            employeeId = previous;
        }

        var (date, from, until) = (wish.Date, wish.From, wish.Until);
        if (analysis.Wishes.OrderBy(w => w.Date).FirstOrDefault() is { } requested)
        {
            date = requested.Date;
            if (requested.From is not null)
            {
                (from, until) = (requested.From, requested.Until);
            }
        }

        // „um 17 Uhr“ ohne Tag: die Terminannahme kennt dann keinen Wunsch; die Uhrzeit gilt für den bisherigen Tag.
        if (analysis.Wishes.Count == 0 && RuleBasedBookingAssistant.FindTimes(text.ToLowerInvariant()) is { From: not null } times)
        {
            (from, until) = times;
        }

        var roomId = RoomNamed(text, rooms) ?? wish.RoomId;
        var guests = GuestsIn(text) ?? wish.Guests;
        return new CalendarWish(date, from, until, analysis.TreatmentId ?? wish.TreatmentId, employeeId, excludedIds, roomId, guests);
    }

    /// <summary>„im Raum Lotus“, „in der Kosmetikkabine“: der gemeinte Raum, wenn der Name nach „im“, „in“ oder „Raum“ eindeutig vorkommt.</summary>
    internal static Guid? RoomNamed(string text, IReadOnlyList<Room> rooms)
    {
        var hits = new HashSet<Guid>();
        foreach (Match match in RoomPhrase().Matches(text))
        {
            var word = match.Groups["n"].Value;
            foreach (var room in rooms.Where(r => Words(r.Name).Any(w => !GenericRoomWords.Contains(w) && w.Equals(word, StringComparison.OrdinalIgnoreCase))))
            {
                hits.Add(room.Id);
            }
        }

        return hits.Count == 1 ? hits.Single() : null;
    }

    /// <summary>„zu zweit“, „Paar“, „für zwei Personen“ ergeben 2; „allein“, „eine Person“ ergeben 1; sonst <c>null</c>.</summary>
    internal static int? GuestsIn(string text) =>
        TwoGuests().IsMatch(text) ? 2 : OneGuest().IsMatch(text) ? 1 : null;

    /// <summary>„nicht bei Marco“, „ohne Anna“, „kein Marco Berger“: die gemeinten Personen und der Text ohne diese Stellen.</summary>
    internal static (List<Guid> Excluded, string Remaining) Exclusions(string text, IReadOnlyList<Employee> employees)
    {
        var excluded = new List<Guid>();
        var rest = Exclusion().Replace(text, match =>
        {
            var hit = employees.FirstOrDefault(e => NameWords(e).Contains(match.Groups["a"].Value, StringComparer.OrdinalIgnoreCase));
            if (hit is null)
            {
                return match.Value;
            }

            excluded.Add(hit.Id);
            var next = match.Groups["b"].Value;
            return next.Length == 0 || NameWords(hit).Contains(next, StringComparer.OrdinalIgnoreCase) ? " " : $" {next}";
        });
        return (excluded.Distinct().ToList(), rest);
    }

    private static Guid? NamedIn(string text, IReadOnlyList<Employee> employees, IReadOnlyCollection<Guid> excluded)
    {
        var words = Words(text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hits = employees.Where(e => !excluded.Contains(e.Id) && NameWords(e).Any(words.Contains)).ToList();
        return hits.Count == 1 ? hits[0].Id : null;
    }

    private static IEnumerable<string> Words(string text) => Word().Matches(text).Select(m => m.Value);

    /// <summary>Wörter des vollständigen Namens und des Rufnamens: „bei Siri“ und „bei Siriporn“ meinen dieselbe Person.</summary>
    private static IEnumerable<string> NameWords(Employee employee) => Words(employee.DisplayName).Concat(Words(employee.PreferredName ?? ""));

    /// <summary>
    /// Die besten Vorschläge: Tag für Tag ab dem Wunschtag, bis es drei gibt (höchstens 14 Tage). Mit Raumwunsch nur dieser Raum, bei zwei
    /// Gästen nur Räume mit mindestens zwei Plätzen. Zweiter Wert: die Behandlung hat einen Paarraum (für die Rückfrage).
    /// </summary>
    private async Task<(List<CalendarProposal> Proposals, bool HasCoupleRoom)> ProposalsAsync(Location location, TimeZoneInfo zone, Treatment treatment,
        CalendarWish wish, IReadOnlyList<DateTimeOffset> rejected, Dictionary<Guid, string> names, DateTimeOffset now, CancellationToken ct)
    {
        var all = new List<CalendarProposal>();
        var hasCoupleRoom = false;
        for (var offset = 0; offset < SearchDays && all.Count < MaxProposals; offset++)
        {
            var date = wish.Date.AddDays(offset);
            var context = await booking.LoadContextAsync(location.Id, treatment.Id, date, null, ct);
            if (context is null)
            {
                return (all, hasCoupleRoom);
            }

            hasCoupleRoom |= context.Rooms.Any(r => r.Places >= 2);
            var allowed = context.Rooms.Where(r => (wish.RoomId is null || r.Id == wish.RoomId) && (wish.Guests != 2 || r.Places >= 2)).ToList();
            context = context with { Rooms = allowed };

            var target = wish.From is { } from ? LocalTime.ToUtc(date, from, zone) : (DateTimeOffset?)null;
            var windowStart = wish.Until is not null && wish.From is { } start ? LocalTime.ToUtc(date, start, zone) : DateTimeOffset.MinValue;
            var windowEnd = wish.Until is { } end ? LocalTime.ToUtc(date, end, zone) : DateTimeOffset.MaxValue;
            var candidates = new ResourcePlanner(context).FreeSlots(date, Step, null, snug: true)
                .Where(s => s.Start >= now && s.Start >= windowStart && s.Start <= windowEnd)
                .Where(s => !wish.ExcludedEmployeeIds.Contains(s.EmployeeId))
                .Where(s => rejected.All(r => Math.Abs((r - s.Start).TotalMinutes) >= 15))
                .DistinctBy(s => (s.Start, s.EmployeeId))
                .OrderBy(s => Score(s, target, wish.EmployeeId))
                .ThenBy(s => s.Start)
                .ToList();

            var picked = new List<AvailableSlot>();
            foreach (var slot in candidates)
            {
                // Abwechslung: eine weitere Person zur selben Zeit oder mindestens 30 Minuten Abstand.
                if (picked.All(p => p.EmployeeId != slot.EmployeeId || Math.Abs((p.Start - slot.Start).TotalMinutes) >= 30))
                {
                    picked.Add(slot);
                }

                if (all.Count + picked.Count == MaxProposals)
                {
                    break;
                }
            }

            var roomNames = context.Rooms.ToDictionary(r => r.Id, r => r.Name);
            all.AddRange(picked.Select(s => new CalendarProposal(s.Start, s.End, treatment.Id, treatment.Name, s.EmployeeId,
                names.GetValueOrDefault(s.EmployeeId) ?? "", s.RoomId, roomNames.GetValueOrDefault(s.RoomId) ?? "", s.EquipmentId,
                treatment.PriceAt(location.Id, s.Start, zone),
                target is { } t ? (int)Math.Round((s.Start - t).TotalMinutes) : 0,
                Reason(s, target, wish.EmployeeId, offset > 0 ? date : null))));
        }

        return (all, hasCoupleRoom);
    }

    /// <summary>
    /// Kleiner ist besser: Minuten Abstand zur Wunschzeit, Aufschlag ohne Wunschperson. Lücken zählen nur wenig,
    /// damit die Wunschzeit des Gastes vor einem ruhigeren Kalender steht.
    /// </summary>
    internal static double Score(AvailableSlot slot, DateTimeOffset? target, Guid? preferred)
    {
        var distance = target is { } t ? Math.Abs((slot.Start - t).TotalMinutes) : 0;
        var person = preferred is { } p && slot.EmployeeId != p ? 180 : 0;
        var fit = slot.Fit switch { SlotFit.Snug => -5, SlotFit.LeavesGap => 10, _ => 0 };
        return distance + person + fit;
    }

    private static string Reason(AvailableSlot slot, DateTimeOffset? target, Guid? preferred, DateOnly? otherDay)
    {
        var minutes = target is { } t ? (int)Math.Round((slot.Start - t).TotalMinutes) : 0;
        return string.Join(" · ", new[]
        {
            otherDay is { } day ? Day(day) : null,
            target is null ? null : minutes == 0 ? "zur Wunschzeit" : minutes > 0 ? $"{minutes} Min. später" : $"{-minutes} Min. früher",
            preferred is { } p && slot.EmployeeId == p ? "Wunschperson" : null,
            slot.Fit switch { SlotFit.Snug => "schließt lückenlos an", SlotFit.LeavesGap => slot.FitReason ?? "lässt eine Lücke", _ => null },
        }.Where(s => s is not null));
    }

    private static List<CalendarWishChip> Chips(CalendarWish wish, Treatment? treatment, Dictionary<Guid, string> names, IReadOnlyList<Room> rooms)
    {
        var chips = new List<CalendarWishChip> { new(CalendarWishPart.Day, Day(wish.Date)) };
        if (wish.From is { } from)
        {
            chips.Add(new(CalendarWishPart.Time, wish.Until is { } until ? $"{from:HH:mm} bis {until:HH:mm}" : $"ab ca. {from:HH:mm}"));
        }

        if (treatment is not null)
        {
            chips.Add(new(CalendarWishPart.Treatment, treatment.Name, treatment.Id));
        }

        if (wish.EmployeeId is { } employee && names.TryGetValue(employee, out var name))
        {
            chips.Add(new(CalendarWishPart.Employee, $"bei {name}", employee));
        }

        chips.AddRange(wish.ExcludedEmployeeIds.Where(names.ContainsKey).Select(id => new CalendarWishChip(CalendarWishPart.Excluded, $"nicht {names[id]}", id)));
        if (wish.RoomId is { } roomId && rooms.FirstOrDefault(r => r.Id == roomId) is { } room)
        {
            chips.Add(new(CalendarWishPart.Room, room.Name.Contains("raum", StringComparison.OrdinalIgnoreCase) ? room.Name : $"Raum {room.Name}", roomId));
        }

        if (wish.Guests is { } guests)
        {
            chips.Add(new(CalendarWishPart.Guests, guests == 2 ? "2 Personen · Paarraum" : "1 Person"));
        }

        return chips;
    }

    private static string Day(DateOnly date) => date.ToString("ddd d.M.", German);

    [GeneratedRegex(@"\b(?i:nicht|ohne|kein|keine|keinen|not|without)\s+(?:(?i:bei|mit|von|zu|with|by)\s+)?(?<a>\p{L}+)(?:\s+(?<b>\p{L}+))?")]
    private static partial Regex Exclusion();

    [GeneratedRegex(@"\p{L}{2,}")]
    private static partial Regex Word();

    [GeneratedRegex(@"\b(?i:im|in|raum|room)\s+(?:(?i:dem|der|raum|room)\s+)?(?<n>\p{L}{3,})")]
    private static partial Regex RoomPhrase();

    [GeneratedRegex(@"\b(?i:zu zweit|paar|pärchen|partnermassage|für zwei|zwei personen|2 personen|couple|for two|two people)\b")]
    private static partial Regex TwoGuests();

    [GeneratedRegex(@"\b(?i:allein|alleine|einzeln|eine person|1 person|einzelperson|only me)\b")]
    private static partial Regex OneGuest();
}
