using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Rechnet einen Kalendertag (UTC aus der API) in Spalten und Pixel um, in der Zeitzone des Standorts.
/// Reine Funktion, damit sich die Darstellung ohne Oberfläche testen lässt.
/// </summary>
public static class DayLayout
{
    public const double PixelsPerMinute = 1.6;

    /// <summary>Breite einer Spalte und Abstand dazwischen; das Ziehen rechnet damit die Zielspalte aus.</summary>
    public const double ColumnWidth = 208;

    public const double ColumnGap = 8;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private const string BlockColor = "#8a9099";

    public sealed record Result(IReadOnlyList<CalendarColumn> Columns, IReadOnlyList<HourMark> Hours, DateTimeOffset DayStart, double Height);

    /// <summary>Farben für Paar und Spa-Tag; die Gruppe wählt ihre Farbe fest aus ihrer Id.</summary>
    private static readonly string[] GroupColors = ["#b4569a", "#3f7fbf", "#c27a2c", "#5b8c3a", "#8a5cc2", "#2d8a8a"];

    /// <summary>Ab so vielen Besuchen in zwölf Monaten gilt ein Gast als Stammgast.</summary>
    public const int RegularVisits = 3;

    public static Result Build(CalendarDay day, LocationResources resources, bool byRoom, TimeZoneInfo zone, Guid? selected = null, Guid? lifted = null,
        IReadOnlySet<Guid>? pending = null, CalendarScale? scale = null, DateTimeOffset? now = null, (int Start, int End)? sharedHours = null,
        IReadOnlyDictionary<Guid, string>? servings = null, bool allStaff = false, IReadOnlyList<AreaVisitDto>? areaVisits = null, bool onlyAvailableRooms = false)
    {
        var ppm = (scale ?? CalendarScale.Default).PixelsPerMinute;
        var (startHour, endHour) = sharedHours ?? VisibleHours(day, zone);
        var dayStart = LocalToUtc(day.Date, new TimeOnly(startHour, 0), zone);
        double Y(DateTimeOffset t) => (t - dayStart).TotalMinutes * ppm;
        CalendarSpan Span(DateTimeOffset from, DateTimeOffset until) => new(Y(from), Math.Max(18, Y(until) - Y(from)));

        var employees = resources.Employees.ToDictionary(e => e.Id);
        var appointments = day.Appointments.Where(a => a.Status != AppointmentStatus.Cancelled).ToList();
        var columns = new List<CalendarColumn>();
        var absences = (day.Absences ?? []).ToDictionary(a => a.EmployeeId);
        var breaks = (day.Breaks ?? []).ToDictionary(b => b.EmployeeId);
        var roomClosures = day.RoomClosures ?? [];
        var dayHeight = (endHour - startHour) * 60 * ppm;

        if (byRoom)
        {
            // Nach Kategorie sortiert (Stammdaten haben keine, sie folgt aus dem Typ); Sauna und Ruheraum haben Eintritte statt Termine.
            foreach (var room in resources.Rooms.OrderBy(r => r.IsArea).Where(r => !onlyAvailableRooms || !IsUnavailable(day, r)))
            {
                if (room.IsArea)
                {
                    columns.Add(AreaColumn(room, day, areaVisits ?? [], zone, Span));
                    continue;
                }

                var closures = roomClosures.Where(c => c.RoomId == room.Id).OrderBy(c => c.From).ToList();
                var column = RoomColumn(room, resources, closures, day, zone, appointments.Count(a => a.RoomId == room.Id && IsOpen(a) && closures.Any(c => InRoomClosure(a, c))));
                // Die gesperrte Zeit gehört nicht zur verfügbaren: das Raster schraffiert sie wie die Spalte einer Person ohne Dienst.
                column.Category = TreatmentRoomsCategory;
                column.Available.AddRange(day.Opening.SelectMany(o => Without(o.Start, o.End, closures)).Select(o => Span(o.Start, o.End)));
                foreach (var closure in closures)
                {
                    var top = Math.Max(0, Y(closure.From));
                    var bottom = Math.Min(dayHeight, Y(closure.Until));
                    if (bottom > top)
                    {
                        column.RoomClosures.Add(new RoomClosureSpanItem(closure.Id, top, bottom - top, closure.Kind, RoomClosureLabel(closure), RoomClosureTip(closure, zone)));
                    }
                }

                columns.Add(column);
            }
        }
        else
        {
            // Personen mit Dienst an diesem Tag, dazu alle, die trotzdem Termine haben (z. B. nach einer Planänderung).
            // Mit „Alle Mitarbeitenden“ zusätzlich alle Personen des Teams ohne Dienst hier, mit Grund in der Spalte (frei, Urlaub, krank, anderer Standort).
            var ids = StaffWithColumn(day);
            var elsewhere = (day.Elsewhere ?? []).GroupBy(e => e.EmployeeId).ToDictionary(g => g.Key, g => g.OrderBy(e => e.Start).First());
            foreach (var employee in resources.Employees.Where(e => allStaff || ids.Contains(e.Id)))
            {
                var shifts = day.Shifts.Where(s => s.EmployeeId == employee.Id).OrderBy(s => s.Start).ToList();
                var absence = absences.GetValueOrDefault(employee.Id);
                var away = elsewhere.GetValueOrDefault(employee.Id);
                var subtitle = absence is not null ? AbsenceText(absence, day.Date)
                    : shifts.Count > 0 ? string.Join(", ", shifts.Select(s => TimeText(s.Start, s.End, zone)))
                    : away is not null ? ElsewhereText(away, zone)
                    : ids.Contains(employee.Id) ? "ohne Dienst"
                    : "frei";
                var shownAway = absence is null && shifts.Count == 0 ? away : null;
                var shiftText = shownAway is null ? subtitle : TimeText(shownAway.Start, shownAway.End, zone);
                var breakInfo = breaks.GetValueOrDefault(employee.Id);
                var column = new CalendarColumn(employee.Id, employee.InHouseName(), employee.Color, subtitle)
                {
                    Place = shownAway?.LocationName,
                    ShiftText = shiftText,
                    TipLines = PersonTipLines(employee, shownAway?.LocationName, shiftText),
                    OffDuty = shifts.Count == 0,
                    Absence = absence?.Kind,
                    Affected = absence is null ? 0 : appointments.Count(a => a.EmployeeId == employee.Id && IsOpen(a)),
                    BreakMinutes = breakInfo?.RequiredMinutes ?? 0,
                };
                column.Available.AddRange(shifts.Select(s => Span(s.Start, s.End)));
                columns.Add(column);
            }
        }

        var byId = columns.ToDictionary(c => c.Id);
        var rooms = resources.Rooms.ToDictionary(r => r.Id);
        var guests = (day.Guests ?? []).ToDictionary(g => g.CustomerId);
        var preparations = (day.Preparations ?? []).ToDictionary(p => p.TreatmentId, p => p.Text);
        var series = (day.Series ?? []).ToDictionary(s => s.Id);
        var selectedGroup = appointments.FirstOrDefault(a => a.Id == selected)?.GroupId;
        var groupAreas = day.GroupAreas ?? [];
        foreach (var a in appointments.OrderBy(a => a.Start))
        {
            if (byId.TryGetValue(byRoom ? a.RoomId : a.EmployeeId, out var column))
            {
                var employee = employees.GetValueOrDefault(a.EmployeeId);
                // Gast ohne Namen: Kennzeichen und Besuche der buchenden Person gehören nicht an seinen Termin.
                var guest = a.PlaceholderGuest is null ? guests.GetValueOrDefault(a.CustomerId) : null;
                var span = Span(a.Start, a.End);
                // Einwirkzeit: nur in der Spalte der Person; im Raum bleibt er belegt, dort gibt es kein Band.
                var (handsOn, processing) = byRoom ? (0, 0) : ProcessingOf(a);
                var groupLabel = GroupLabel(a, appointments, groupAreas);
                var overdue = OverdueMinutes(a, now);
                var flags = Flags(guest, a.EmployeeId, employees);
                if (PackageFlag(guest, a.TreatmentId, DateOnly.FromDateTime(Local(a.Start, zone))) is { } packageFlag)
                {
                    // Gleich nach dem Hinweis, damit es unter den ersten drei Kennzeichen bleibt.
                    flags = [.. flags.Where(f => f.Kind == GuestFlagKind.Note), packageFlag, .. flags.Where(f => f.Kind != GuestFlagKind.Note)];
                }

                var preparation = preparations.GetValueOrDefault(a.TreatmentId);
                var room = rooms.GetValueOrDefault(a.RoomId);
                var closedRoom = IsOpen(a) ? roomClosures.FirstOrDefault(c => c.RoomId == a.RoomId && InRoomClosure(a, c)) : null;
                var info = a.SeriesId is { } seriesId ? series.GetValueOrDefault(seriesId) : null;
                AddStripes(column, a, preparation, byRoom ? room?.CleaningMinutes ?? 0 : 0, Y, byRoom ? CareTaskOf(day, a) : null, zone);
                column.Items.Add(new CalendarItem(a.Id, a.PlaceholderGuest is { } number ? $"Gast {number} · Name folgt" : a.CustomerName, a.TreatmentName, TimeText(a.Start, a.End, zone),
                    span.Top, span.Height, employee?.Color ?? "#2f6f5e", a.Status, a)
                {
                    IsSelected = a.Id == selected,
                    IsLifted = a.Id == lifted,
                    IsPending = pending?.Contains(a.Id) == true,
                    IsGroup = a.GroupId is not null,
                    IsGroupHighlighted = selectedGroup is not null && a.GroupId == selectedGroup && a.Id != selected,
                    GroupLabel = groupLabel,
                    SeriesLabel = info is null ? null : SeriesLabel(info, a.Start),
                    SourceLabel = SourceText(a.Source),
                    LateLabel = LateText(a, zone),
                    ConfirmedLabel = a is { Status: AppointmentStatus.Booked, ConfirmedAt: not null } ? "✓ zugesagt" : null,
                    GroupColor = a.GroupId is { } g ? GroupColors[(int)((uint)g.GetHashCode() % (uint)GroupColors.Length)] : null,
                    OverdueMinutes = overdue,
                    IsAffected = IsOpen(a) && (absences.ContainsKey(a.EmployeeId) || closedRoom is not null),
                    Who = byRoom ? employee?.InHouseName() : null,
                    WhoColor = byRoom ? employee?.Color : null,
                    Flags = [.. flags.Take(3)],
                    ProcessingTop = handsOn * ppm,
                    ProcessingHeight = processing * ppm,
                    ProcessingText = processing > 0 ? $"Einwirkzeit {processing} Min. · frei" : null,
                    Symbol = guest is null ? null : PersonNames.Symbol(guest.Salutation),
                    BirthdayToday = guest?.Birthday is { DaysAway: 0 },
                    Hover = HoverCardFor(a, employee?.InHouseName(), room, groupLabel, guest, flags, overdue, preparation, absences.GetValueOrDefault(a.EmployeeId), zone,
                        employees, info, byRoom, AreaText(a, groupAreas, rooms, zone), PackageText(guest, a.TreatmentId),
                        resources.Treatments.FirstOrDefault(t => t.Id == a.TreatmentId), closedRoom) with
                    { Serving = servings?.GetValueOrDefault(a.Id) },
                });
            }
        }

        foreach (var b in day.Blocks)
        {
            if ((b.Kind == ResourceKind.Room) == byRoom && byId.TryGetValue(b.ResourceId, out var column))
            {
                var span = Span(b.From, b.Until);
                var isBreak = b.Kind == ResourceKind.Employee && IsBreak(b.Reason);
                var legal = isBreak && column.BreakMinutes > 0;
                column.Items.Add(new CalendarItem(b.Id, isBreak ? (legal ? "Pause, gesetzlich" : "Pause") : b.Reason, isBreak ? "" : "Gesperrt",
                    TimeText(b.From, b.Until, zone), span.Top, span.Height, BlockColor, null, null)
                {
                    IsBreak = isBreak,
                    IsLegalBreak = legal,
                    Block = b,
                    Hover = isBreak
                        ? new HoverCard("Pause", TimeText(b.From, b.Until, zone), null, null)
                        {
                            Infos = Lines(legal ? $"Gesetzlich verlangt: {column.BreakMinutes} Min." : null),
                        }
                        : new HoverCard("Gesperrt", TimeText(b.From, b.Until, zone), b.Reason, null),
                });
            }
        }

        foreach (var note in day.Notes ?? [])
        {
            if (note is { At: { } at, ResourceId: { } resourceId } && (note.Kind == ResourceKind.Room) == byRoom
                && byId.TryGetValue(resourceId, out var column))
            {
                // Mit Ende wird der Zettel über die Dauer gezeichnet (mindestens 18 Pixel), sonst steht er nur zu seiner Uhrzeit.
                var (time, height) = note.Until is { } until ? (TimeText(at, until, zone), Math.Max(MinNoteHeight, Y(until) - Y(at))) : ($"{Local(at, zone):HH:mm}", NoteHeight);
                column.Notes.Add(new CalendarNoteItem(note.Id, Y(at), time, note.Text, note.Done, note) { Height = height });
            }
        }

        columns.ForEach(AssignLanes);

        var hours = Enumerable.Range(startHour, endHour - startHour + 1)
            .Select(h => new HourMark($"{h:00}:00", (h - startHour) * 60 * ppm))
            .ToList();
        return new(columns, hours, dayStart, dayHeight);
    }

    /// <summary>Ein Zeitraum ohne die Raumsperren: übrig bleiben die Stücke davor, dazwischen und danach.</summary>
    private static IEnumerable<TimeRange> Without(DateTimeOffset from, DateTimeOffset until, IReadOnlyList<RoomClosureBand> closures)
    {
        var start = from;
        foreach (var closure in closures.OrderBy(c => c.From))
        {
            if (closure.Until <= start || until <= closure.From)
            {
                continue;
            }

            if (start < closure.From)
            {
                yield return new TimeRange(start, closure.From);
            }

            start = closure.Until > start ? closure.Until : start;
        }

        if (start < until)
        {
            yield return new TimeRange(start, until);
        }
    }

    /// <summary>Liegt der Termin ganz oder teilweise in der Raumsperre? Dieselbe Rechnung wie am Server, der die offenen Termine einer Sperre zählt.</summary>
    public static bool InRoomClosure(AppointmentDto appointment, RoomClosureBand closure) => appointment.Start < closure.Until && closure.From < appointment.End;

    /// <summary>Text in der Fläche der Sperre: Art und Grund („Wartung · Lüftung wird gewartet“).</summary>
    public static string RoomClosureLabel(RoomClosureBand closure) => string.IsNullOrWhiteSpace(closure.Reason) ? closure.Title : $"{closure.Title} · {closure.Reason}";

    /// <summary>Hinweis an der Fläche: Zeitraum, Grund und Zahl der offenen Termine.</summary>
    public static string RoomClosureTip(RoomClosureBand closure, TimeZoneInfo zone)
    {
        var lines = new List<string> { $"{closure.RoomName} gesperrt ({closure.Title}): {ClosurePeriod(closure.From, closure.Until, zone)}" };
        if (!string.IsNullOrWhiteSpace(closure.Reason))
        {
            lines.Add($"Grund: {closure.Reason}");
        }

        if (closure.OpenAppointments > 0)
        {
            lines.Add(closure.OpenAppointments == 1 ? "1 Termin noch offen" : $"{closure.OpenAppointments} Termine noch offen");
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Zeile 2 im Kopf des gesperrten Raums: „gesperrt: Wartung“, bei halbem Tag mit Uhrzeit („gesperrt: Wartung ab 13:00“), bei mehreren Tagen mit dem letzten Tag
    /// („gesperrt: Wartung bis 25.10.“).
    /// </summary>
    public static string RoomClosureHeadText(RoomClosureBand closure, DateOnly date, TimeZoneInfo zone)
    {
        var from = Local(closure.From, zone);
        var until = Local(closure.Until, zone);
        var last = DateOnly.FromDateTime(until.AddTicks(-1));
        var startsToday = DateOnly.FromDateTime(from) == date && from.TimeOfDay != TimeSpan.Zero;
        var endsToday = last == date && until.TimeOfDay != TimeSpan.Zero;
        var when = (startsToday, endsToday) switch
        {
            (true, true) => $" {from:HH:mm}–{until:HH:mm}",
            (true, false) => $" ab {from:HH:mm}",
            (false, true) => $" bis {until:HH:mm}",
            _ => last > date ? $" bis {last.Day}.{last.Month}." : "",
        };
        return $"gesperrt: {closure.Title}{when}";
    }

    /// <summary>Höhe eines Zettels ohne Ende und kleinste Höhe eines Zettels mit Ende (Pixel).</summary>
    public const double NoteHeight = 22;

    public const double MinNoteHeight = 18;

    /// <summary>Schmalste Bahn am rechten Rand, in der Zettel und Sperrzeiten neben einem Termin stehen (Pixel).</summary>
    public const double MinSideLaneWidth = 72;

    /// <summary>Was in einer Spalte Platz braucht: ein Termin, eine Sperrzeit bzw. Pause oder ein Zettel, mit Lage und Rang (0 Termin, 1 Sperrzeit, 2 Zettel).</summary>
    private readonly record struct LaneBox(bool IsNote, int Index, double Top, double Bottom, int Tier);

    /// <summary>
    /// Nichts steht übereinander: was sich zeitlich überschneidet, steht in Bahnen nebeneinander. Gruppen überlappender Elemente (Termine,
    /// Sperrzeiten, Pausen, Zettel) teilen sich die Spalte; Termine stehen immer links. Gibt es in der Gruppe Termine und anderes, bekommen Sperrzeiten
    /// und Zettel eine schmale Bahn rechts (ein Drittel der Spaltenbreite, mindestens <see cref="MinSideLaneWidth"/>) und die Termine den Rest, je Termin
    /// in einer eigenen Bahn (der frühere links). Ohne Überlappung bleibt es bei der vollen Breite.
    /// </summary>
    private static void AssignLanes(CalendarColumn column)
    {
        var boxes = column.Items.Select((item, i) => new LaneBox(false, i, item.Top, item.Top + item.Height, item.Appointment is not null ? 0 : 1))
            .Concat(column.Notes.Select((note, i) => new LaneBox(true, i, note.Top, note.Top + note.Height, 2)))
            .OrderBy(b => b.Top).ToList();
        var cluster = new List<LaneBox>();
        var clusterEnd = double.MinValue;
        foreach (var box in boxes)
        {
            // Kleiner Spielraum: Elemente, die genau aneinander anschließen, überlappen nicht (Rundung der Pixel).
            if (cluster.Count > 0 && box.Top >= clusterEnd - 0.01)
            {
                PlaceCluster(column, cluster);
                cluster.Clear();
            }

            cluster.Add(box);
            clusterEnd = cluster.Count == 1 ? box.Bottom : Math.Max(clusterEnd, box.Bottom);
        }

        PlaceCluster(column, cluster);
    }

    private static void PlaceCluster(CalendarColumn column, List<LaneBox> cluster)
    {
        if (cluster.Count < 2)
        {
            return;
        }

        var mainTier = cluster.Min(b => b.Tier);
        var hasSide = cluster.Any(b => b.Tier != mainTier);
        foreach (var inSide in hasSide ? new[] { false, true } : [false])
        {
            var part = cluster.Where(b => (b.Tier != mainTier) == inSide).ToList();
            var ends = new List<double>();
            var lanes = new List<int>();
            foreach (var box in part)
            {
                var lane = ends.FindIndex(end => end <= box.Top + 0.01);
                if (lane < 0)
                {
                    lane = ends.Count;
                    ends.Add(0);
                }

                ends[lane] = box.Bottom;
                lanes.Add(lane);
            }

            for (var i = 0; i < part.Count; i++)
            {
                var place = new LanePlace(lanes[i], ends.Count, inSide, hasSide);
                if (part[i].IsNote)
                {
                    column.Notes[part[i].Index] = column.Notes[part[i].Index] with { Place = place };
                }
                else
                {
                    column.Items[part[i].Index] = column.Items[part[i].Index] with { Place = place };
                }
            }
        }
    }

    /// <summary>Breite der schmalen Bahn rechts: ein Drittel der Termin-Breite, mindestens <see cref="MinSideLaneWidth"/>, höchstens die Hälfte.</summary>
    public static double SideLaneWidth(double itemWidth) => Math.Min(Math.Max(itemWidth / 3, MinSideLaneWidth), itemWidth / 2);

    private static double RegionWidth(double itemWidth, LanePlace place) =>
        !place.HasSide ? itemWidth : place.InSide ? SideLaneWidth(itemWidth) : itemWidth - SideLaneWidth(itemWidth);

    /// <summary>Breite eines Elements in seiner Bahn: bei einer einzigen Bahn die volle Breite, sonst ein Teil davon mit 2 Pixel Abstand zur Nachbarbahn.</summary>
    public static double LaneWidth(double itemWidth, LanePlace place) =>
        place is { LaneCount: <= 1, HasSide: false } ? itemWidth : RegionWidth(itemWidth, place) / place.LaneCount - 2;

    /// <summary>Abstand vom linken Rand der Spalte bis zum Anfang der Bahn.</summary>
    public static double LaneLeft(double itemWidth, LanePlace place) =>
        place is { LaneCount: <= 1, HasSide: false }
            ? 0
            : (place.InSide ? itemWidth - SideLaneWidth(itemWidth) : 0) + place.Lane * RegionWidth(itemWidth, place) / place.LaneCount;

    /// <summary>Kategorie eines Raums in der Raumansicht: die Stammdaten kennen keine, sie folgt aus dem Typ.</summary>
    public const string TreatmentRoomsCategory = "Behandlungsräume";

    public const string AreasCategory = "Sauna und Ruhe";

    /// <summary>Die Sperren des Raums decken die ganze Öffnungszeit des Tages ab; eine Reinigung am Vormittag zählt nicht.</summary>
    public static bool IsUnavailable(CalendarDay day, RoomDto room)
    {
        var closures = (day.RoomClosures ?? []).Where(c => c.RoomId == room.Id).ToList();
        return closures.Count > 0 && day.Opening.Count > 0 && day.Opening.All(o => !Without(o.Start, o.End, closures).Any());
    }

    /// <summary>Räume, die „Nur verfügbare Räume“ ausblendet.</summary>
    public static int UnavailableRoomCount(CalendarDay day, LocationResources resources) => resources.Rooms.Count(r => IsUnavailable(day, r));

    /// <summary>Spalte von Sauna oder Ruheraum: die Plätze im Kopf, die nicht stornierten Eintritte als Blöcke mit Name und Personenzahl.</summary>
    private static CalendarColumn AreaColumn(RoomDto room, CalendarDay day, IReadOnlyList<AreaVisitDto> visits, TimeZoneInfo zone, Func<DateTimeOffset, DateTimeOffset, CalendarSpan> span)
    {
        var column = new CalendarColumn(room.Id, room.Name, RoomColor, $"{room.Places} Plätze", isRoom: true)
        {
            IsArea = true,
            Category = AreasCategory,
            TipLines = [new HeaderTipLine(Icons.Users, $"{room.Places} Plätze")],
        };
        column.Available.AddRange(day.Opening.Select(o => span(o.Start, o.End)));
        foreach (var visit in visits.Where(v => v.RoomId == room.Id && !v.IsCancelled).OrderBy(v => v.From))
        {
            var place = span(visit.From, visit.Until);
            column.Items.Add(new CalendarItem(visit.Id, visit.CustomerName, visit.Persons == 1 ? "1 Person" : $"{visit.Persons} Personen", TimeText(visit.From, visit.Until, zone),
                place.Top, place.Height, RoomColor, null, null));
        }

        return column;
    }

    /// <summary>
    /// Kopf einer Raumspalte wie der einer Person: Zeile 1 der Name, Zeile 2 Raumpflege und Zahl der Behandlungen; Paarräume tragen das Symbol Personen-Paar.
    /// Raumart, Raumpflege und Auslastung stehen im Hinweis, die Behandlungen im Raum-Panel. Geräte hängen im Datenmodell an Behandlungen, nicht an Räumen.
    /// </summary>
    public static CalendarColumn RoomColumn(RoomDto room, LocationResources resources, IReadOnlyList<RoomClosureBand>? closures = null, CalendarDay? day = null,
        TimeZoneInfo? zone = null, int affected = 0)
    {
        var facts = RoomFactsOf(room, resources);
        var treatmentCount = facts.Treatments.Count switch { 0 => null, 1 => "1 Behandlung", var n => $"{n} Behandlungen" };
        var isCouple = room.Places > 1;
        var kind = new HeaderTipLine(isCouple ? Icons.Users : Icons.Door, facts.Kind);
        if (closures is not { Count: > 0 } || day is null || zone is null)
        {
            return new CalendarColumn(room.Id, room.Name, RoomColor, string.Join(" · ", new[] { facts.Cleaning, treatmentCount }.OfType<string>()), isRoom: true)
            {
                IsCouple = isCouple,
                TipLines = [kind, new HeaderTipLine(Icons.Broom, facts.CleaningLong)],
            };
        }

        // Gesperrt: Zeile 2 nennt Art und Zeitraum statt der Raumpflege, der Hinweis den Grund; die Raumpflege ist in der Zeit ohne Belang.
        var tip = new List<HeaderTipLine> { kind };
        foreach (var closure in closures)
        {
            var head = RoomClosureHeadText(closure, day.Date, zone);
            tip.Add(new HeaderTipLine(Icons.Lock, string.IsNullOrWhiteSpace(closure.Reason) ? head : $"{head}: {closure.Reason}"));
        }

        return new CalendarColumn(room.Id, room.Name, RoomColor, RoomClosureHeadText(closures[0], day.Date, zone), isRoom: true)
        {
            IsCouple = isCouple,
            TipLines = tip,
            Affected = affected,
            RoomClosureId = affected > 0 ? closures.FirstOrDefault(c => c.OpenAppointments > 0)?.Id ?? closures[0].Id : null,
        };
    }

    /// <summary>Hinweis am Kopf einer Person: Dienstzeit, Standort bei Dienst an einem anderen Standort, Sprachen (Deutsch ist die Grundsprache und steht dabei).</summary>
    private static List<HeaderTipLine> PersonTipLines(EmployeeDto employee, string? place, string shiftText)
    {
        var lines = new List<HeaderTipLine> { new(Icons.Clock, shiftText) };
        if (place is not null)
        {
            lines.Add(new(Icons.Pin, $"Dienst in {place}"));
        }

        if (employee.Languages is { Count: > 0 } languages)
        {
            lines.Add(new(Icons.Chat, $"Sprachen: {string.Join(", ", ["Deutsch", .. languages.Select(c => GuestLanguages.Name(c) ?? c)])}"));
        }

        return lines;
    }

    /// <summary>Die Raumpflege-Aufgabe, die an diesem Termin hängt (bei Paarsuiten gilt dieselbe Aufgabe für beide Termine).</summary>
    public static RoomCareTaskDto? CareTaskOf(CalendarDay day, AppointmentDto appointment) =>
        day.RoomCare?.Tasks.FirstOrDefault(t => t.AppointmentId == appointment.Id || t.AppointmentIds.Contains(appointment.Id));

    /// <summary>„Raumpflege bis 11:15: offen“, „… erledigt von Anna um 11:12“, „… überfällig“.</summary>
    public static string CareTip(RoomCareTaskDto task, TimeZoneInfo zone)
    {
        var text = task.State switch
        {
            RoomCareState.Done => $"{task.Text}: erledigt{(task.DoneBy is null ? "" : $" von {task.DoneBy}")}{(task.DoneAt is { } at ? $" um {Local(at, zone):HH:mm}" : "")}",
            RoomCareState.Overdue => $"{task.Text}: überfällig",
            _ => $"{task.Text}: offen",
        };
        var restock = task.Restock?.Count(l => l.State == RestockState.Open) ?? 0;
        return restock == 0 ? text : $"{text}\n{(restock == 1 ? "1 Material" : $"{restock} Materialien")} nachfüllen";
    }

    /// <summary>Was ein Raum ist und kann: Raumart, Raumpflege und Behandlungen, in Worten für Spaltenkopf, Hover-Karte und das Raum-Panel.</summary>
    public sealed record RoomFacts(string Kind, string? Cleaning, IReadOnlyList<string> Treatments)
    {
        /// <summary>„Raumpflege 10 Min. nach jedem Termin“ bzw. „Keine Raumpflege nach dem Termin“.</summary>
        public string CleaningLong => Cleaning is null ? "Keine Raumpflege nach dem Termin" : $"{Cleaning} nach jedem Termin";
    }

    public static RoomFacts RoomFactsOf(RoomDto room, LocationResources resources) => new(
        room.Places switch
        {
            <= 1 => "Einzelraum",
            2 => "Paarraum · 2 Plätze",
            var n => $"Gruppenraum · {n} Plätze",
        },
        room.CleaningMinutes > 0 ? $"Raumpflege {room.CleaningMinutes} Min." : null,
        [.. resources.Treatments.Where(t => t.RoomIds.Contains(room.Id)).Select(t => t.Name).Order(StringComparer.CurrentCulture)]);

    internal const string RoomColor = "#5c6660";

    /// <summary>
    /// Etikett einer gemeinsamen Buchung: ein Gast mit mehreren Schritten ist ein Spa-Tag („Spa-Tag 2/3“),
    /// zwei Gäste ein Paar („Paar mit Stefan Koller“, bei mehreren Schritten mit Schritt), mehr Gäste eine Gruppe.
    /// Sauna oder Ruheraum aus dem Baukasten zählen als Schritt mit.
    /// </summary>
    public static string? GroupLabel(AppointmentDto appointment, IReadOnlyList<AppointmentDto> day, IReadOnlyList<AreaVisitDto>? areas = null)
    {
        if (appointment.GroupId is not { } group)
        {
            return null;
        }

        var parts = day.Where(a => a.GroupId == group && a.Status != AppointmentStatus.Cancelled).OrderBy(a => a.Start).ThenBy(a => a.Id).ToList();
        var guests = parts.Select(a => a.CustomerId).Distinct().ToList();
        var own = parts.Where(a => a.CustomerId == appointment.CustomerId).Select(a => (a.Id, a.Start))
            .Concat((areas ?? []).Where(v => v.GroupId == group && v.CustomerId == appointment.CustomerId && !v.IsCancelled).Select(v => (v.Id, Start: v.From)))
            .OrderBy(p => p.Start).ThenBy(p => p.Id).ToList();
        var step = own.Count > 1 ? $"{own.FindIndex(p => p.Id == appointment.Id) + 1}/{own.Count}" : null;
        return guests.Count switch
        {
            <= 1 => step is null ? "gemeinsam" : $"Spa-Tag {step}",
            2 => $"Paar mit {parts.First(a => a.CustomerId != appointment.CustomerId).CustomerName}{(step is null ? "" : $" · {step}")}",
            _ => $"Gruppe · {guests.Count} Gäste",
        };
    }

    /// <summary>„Dazu: Sauna 10:00–11:00“ für die Bereiche, die der Gast in derselben Buchung hat.</summary>
    public static string? AreaText(AppointmentDto appointment, IReadOnlyList<AreaVisitDto> areas, IReadOnlyDictionary<Guid, RoomDto> rooms, TimeZoneInfo zone)
    {
        if (appointment.GroupId is not { } group)
        {
            return null;
        }

        var own = areas.Where(v => v.GroupId == group && v.CustomerId == appointment.CustomerId && !v.IsCancelled).OrderBy(v => v.From)
            .Select(v => $"{rooms.GetValueOrDefault(v.RoomId)?.Name ?? "Bereich"} {TimeText(v.From, v.Until, zone)}").ToList();
        return own.Count == 0 ? null : $"Dazu: {string.Join(", ", own)}";
    }

    /// <summary>
    /// Kennzeichen nach Wichtigkeit: Hinweis aus der Kundenkarte oder wiederholtes Nicht-Erscheinen, Kennzeichen, FEST, Sprache,
    /// dann Stammgast oder Erstbesuch.
    /// Weitere Zeichen (Anamnese, Wunsch Therapeutin, Gutschein, Anzahlung) kommen mit ihren Daten.
    /// </summary>
    public static IReadOnlyList<GuestFlag> Flags(CalendarGuest? guest, Guid? employeeId = null, IReadOnlyDictionary<Guid, EmployeeDto>? employees = null)
    {
        if (guest is null)
        {
            return [];
        }

        var flags = new List<GuestFlag>();
        if (guest.Note is not null || guest.NoShows >= 2)
        {
            var reasons = new[] { guest.Note, guest.NoShows >= 2 ? $"{guest.NoShows}× nicht erschienen" : null }.Where(r => r is not null);
            flags.Add(new GuestFlag("!", $"Hinweis: {string.Join(" · ", reasons)}", GuestFlagKind.Note));
        }

        // Kennzeichen aus der Kundenkarte; „Nicht bei mir“ wirkt nur im Planer und erscheint hier nicht.
        foreach (var kind in guest.Flags ?? [])
        {
            flags.Add(kind switch
            {
                CustomerFlagKind.DoNotBook => new GuestFlag("STOP", "Nicht mehr buchen: Online und KI buchen nicht, Grund in der Kundenkarte", GuestFlagKind.DoNotBook),
                CustomerFlagKind.InformManagement => new GuestFlag("L", "Leitung informieren, wenn der Gast kommt", GuestFlagKind.InformManagement),
                _ => new GuestFlag("+10", "Braucht mehr Zeit: 10 Minuten Puffer nach der Behandlung", GuestFlagKind.MoreTime),
            });
        }

        if (guest.Booking is { EmployeeFixed: true, EmployeeId: { } fixedId })
        {
            var name = employees?.GetValueOrDefault(fixedId)?.InHouseName() ?? "eine andere Person";
            flags.Add(fixedId == employeeId
                ? new GuestFlag("FEST", $"Feste Therapeut:in: {name}", GuestFlagKind.Fixed)
                : new GuestFlag("FEST", $"Feste Therapeut:in ist {name}, dieser Termin liegt bei jemand anderem", GuestFlagKind.FixedElsewhere));
        }

        if (guest.Booking?.Gender is { } gender)
        {
            var person = employeeId is { } id ? employees?.GetValueOrDefault(id) : null;
            var wish = GenderWish(gender);
            flags.Add(person?.Gender is { } actual && actual != gender
                ? new GuestFlag(GenderSymbol(gender), $"Wünscht {wish}; {person.InHouseName()} ist als {GenderNoun(actual)} eingetragen", GuestFlagKind.GenderMismatch)
                : new GuestFlag(GenderSymbol(gender), $"Wünscht {wish}", GuestFlagKind.Gender));
        }

        if (GuestLanguages.Name(guest.Language) is { } language)
        {
            var person = employeeId is { } id ? employees?.GetValueOrDefault(id) : null;
            var tip = person is null ? $"Spricht {language}"
                : person.Languages?.Contains(guest.Language!) == true ? $"Spricht {language}, {person.InHouseName()} auch"
                : $"Spricht {language}; {person.InHouseName()} hat {language} nicht als Sprache eingetragen";
            flags.Add(new GuestFlag(guest.Language!.ToUpperInvariant(), tip, GuestFlagKind.Language));
        }

        if (guest.Visits == 0)
        {
            flags.Add(new GuestFlag("N", "Erstbesuch: begrüßen und Anamnese prüfen", GuestFlagKind.New));
        }
        else if (guest.VisitsLastYear >= RegularVisits)
        {
            flags.Add(new GuestFlag("★", $"Stammgast: {guest.VisitsLastYear} Besuche in zwölf Monaten", GuestFlagKind.Regular));
        }

        return flags;
    }

    /// <summary>Gekauftes Paket des Gastes für diese Behandlung; bei mehreren das, das zuerst abläuft.</summary>
    private static GuestPackage? Package(CalendarGuest? guest, Guid treatmentId) =>
        guest?.Packages?.Where(p => p.TreatmentId == treatmentId && p.UnitsLeft > 0).OrderBy(p => p.ValidUntil ?? DateOnly.MaxValue).FirstOrDefault();

    /// <summary>„Paket Massage: 7 von 10 offen, gültig bis 30.06.2027“ für die Hover-Karte; die Kassa zieht den Termin dann ab.</summary>
    public static string? PackageText(CalendarGuest? guest, Guid treatmentId) =>
        Package(guest, treatmentId) is { } p
            ? $"Paket {p.TreatmentName}: {p.UnitsLeft} von {p.Units} offen{(p.ValidUntil is { } until ? $", gültig bis {until:dd.MM.yyyy}" : "")}"
            : null;

    /// <summary>Kennzeichen nur, wenn das Paket mit diesem Termin aufgebraucht ist oder innerhalb von 30 Tagen abläuft.</summary>
    public static GuestFlag? PackageFlag(CalendarGuest? guest, Guid treatmentId, DateOnly date)
    {
        if (Package(guest, treatmentId) is not { } p)
        {
            return null;
        }

        if (p.UnitsLeft == 1)
        {
            return new GuestFlag("P", $"Paket {p.TreatmentName}: das ist die letzte Behandlung, Verlängerung anbieten", GuestFlagKind.Package);
        }

        return p.ValidUntil is { } until && until <= date.AddDays(30)
            ? new GuestFlag("P", $"Paket {p.TreatmentName} läuft am {until:dd.MM.yyyy} ab, noch {p.UnitsLeft} offen", GuestFlagKind.Package)
            : null;
    }

    /// <summary>Kennzeichen, die Handeln verlangen: in der Hover-Karte im orangen Band statt unter „Kennzeichen“.</summary>
    private static bool NeedsAction(GuestFlag flag) => flag.Kind is GuestFlagKind.Note or GuestFlagKind.DoNotBook or GuestFlagKind.InformManagement
        or GuestFlagKind.FixedElsewhere or GuestFlagKind.GenderMismatch;

    /// <summary>Übrige Zeichen mit Erklärung; FEST bei derselben Person und ein passender ♀/♂-Wunsch stehen schon unter „Wünsche“.</summary>
    private static bool IsOtherFlag(GuestFlag flag) => flag.Kind is GuestFlagKind.MoreTime or GuestFlagKind.Language or GuestFlagKind.Package;

    /// <summary>Alles Wichtige zum Termin ohne Klick, für die Hover-Karte: Kopf in drei Zeilen, oranges Band, dann nur, was vorhanden ist.</summary>
    private static HoverCard HoverCardFor(AppointmentDto a, string? employee, RoomDto? roomInfo, string? groupLabel, CalendarGuest? guest,
        IReadOnlyList<GuestFlag> flags, int overdue, string? preparation, CalendarAbsence? absence, TimeZoneInfo zone,
        IReadOnlyDictionary<Guid, EmployeeDto> employees, SeriesInfo? series, bool byRoom, string? areaText = null, string? packageText = null,
        TreatmentDto? treatment = null, RoomClosureBand? roomClosure = null)
    {
        var before = TimesOf(a).Preparation;
        var minutes = (int)(a.End - a.Start).TotalMinutes;
        var cleaning = roomInfo?.CleaningMinutes ?? 0;
        var title = a.PlaceholderGuest is { } number ? $"Gast {number}, Name folgt (gebucht von {a.CustomerName})" : a.CustomerName;
        var dayTime = $"{ShortDay(DateOnly.FromDateTime(Local(a.Start, zone)))} · {TimeText(a.Start, a.End, zone)}";
        var when = string.Join(" · ", new[] { dayTime, $"{minutes} Min.", employee, roomInfo?.Name }.Where(p => !string.IsNullOrEmpty(p)));
        var flow = FlowParts(a).Concat(cleaning > 0 ? [$"Raumpflege {cleaning}"] : []).ToList();
        var tag = flags.Any(f => f.Kind == GuestFlagKind.New) ? HoverCard.NewTag : flags.Any(f => f.Kind == GuestFlagKind.Regular) ? HoverCard.RegularTag : null;
        return new HoverCard(title, when, a.TreatmentName, a.Status == AppointmentStatus.Booked ? (a.HeldSince is null ? null : CalendarItem.HeldName) : CalendarItem.StatusName(a.Status))
        {
            DayTime = dayTime,
            Duration = $"{minutes} Min.",
            Person = string.IsNullOrEmpty(employee) ? null : employee,
            Room = string.IsNullOrEmpty(roomInfo?.Name) ? null : roomInfo.Name,
            PersonFirst = byRoom,
            ShowFlow = IsTimesChanged(a, treatment) || ProcessingOf(a).Processing > 0,
            Symbol = guest is null ? null : PersonNames.Symbol(guest.Salutation),
            Tag = tag,
            Alerts = Lines([
                overdue > 0 ? $"{overdue} Min. überfällig, noch nicht eingecheckt" : null,
                LateText(a, zone) is { } late ? $"Gast {late}" : null,
                absence is not null && IsOpen(a) ? $"{employee ?? "Die Person"} ist {AbsenceWord(absence.Kind)}: bitte umbuchen" : null,
                roomClosure is not null ? $"{roomInfo?.Name ?? roomClosure.RoomName} ist gesperrt ({RoomClosureLabel(roomClosure)}): bitte umbuchen" : null,
                .. flags.Where(NeedsAction).Select(f => f.Tip),
                // Ab dem ersten Ausfall eine Warnung; ab zwei steht es schon im Hinweis (Flags).
                guest is { NoShows: 1 } ? "1× nicht erschienen" : null,
                a is { CustomerAssignmentOpen: true, Status: not AppointmentStatus.Cancelled } ? PlatformText.AssignmentOpen : null,
                a is { AcceptInPlatformPending: true, Status: not AppointmentStatus.Cancelled } ? PlatformText.AcceptInPlatform : null]),
            Infos = Lines(a.ConfirmedAt is { } confirmed ? $"Zugesagt über den Link am {Local(confirmed, zone):dd.MM. 'um' HH:mm}" : null),
            Birthday = BirthdayText.Line(guest?.Birthday),
            Appointment = Facts(
                ("Buchung", groupLabel is null or "gemeinsam" ? null : groupLabel),
                ("Bereiche", areaText),
                ("Serie", series is null ? null : SeriesText(series, a.Start, zone)),
                ("Quelle", SourceText(a.Source)),
                ("Paket", packageText),
                ("Zahlung", PlatformText.Payment(a.PlatformPayment, a.PrepaidAmount))),
            Flags = [.. flags.Where(IsOtherFlag)],
            Guest = guest is null ? [] : Facts(
                // Stammgast und Erstbesuch sagen es schon im Etikett.
                ("Besuche", tag is null ? VisitsText(guest) : null),
                ("Wünsche", BookingPreferenceText(guest.Booking, id => employees.GetValueOrDefault(id)?.InHouseName())),
                ("Vorlieben", guest.Likes)),
            Flow = flow.Count == 0 ? null : $"{string.Join(" · ", flow)} Min.{(IsTimesChanged(a, treatment) ? " (geändert)" : "")}",
            Preparation = before > 0 ? preparation : null,
            Note = string.IsNullOrWhiteSpace(a.Note) ? null : a.Note,
        };
    }

    /// <summary>„136 in 12 Monaten · zuletzt 19.10.2026“; ohne Besuch in zwölf Monaten die Zahl insgesamt; beim Erstbesuch nichts (steht im Etikett).</summary>
    private static string? VisitsText(CalendarGuest guest)
    {
        if (guest.Visits == 0)
        {
            return null;
        }

        var count = guest.VisitsLastYear > 0
            ? $"{guest.VisitsLastYear} in 12 Monaten"
            : $"{guest.Visits} insgesamt";
        return guest.LastVisit is { } last ? $"{count} · zuletzt {last:dd.MM.yyyy}" : count;
    }

    private static List<string> Lines(params IEnumerable<string?> lines) => [.. lines.Where(l => !string.IsNullOrEmpty(l)).Cast<string>()];

    private static List<HoverFact> Facts(params (string Label, string? Value)[] facts) =>
        [.. facts.Where(f => !string.IsNullOrEmpty(f.Value)).Select(f => new HoverFact(f.Label, f.Value!))];

    /// <summary>
    /// Vorbereitung, Nachbereitung und Gast-Puffer des Termins aus seinen eigenen Werten. Alte Stände (Offline-Cache) kennen sie noch nicht
    /// (alle 0); dann zählt die Bindung, ohne Puffer.
    /// </summary>
    public static (int Preparation, int FollowUp, int Extra) TimesOf(AppointmentDto a) =>
        a is { PreparationMinutes: 0, FollowUpMinutes: 0, ExtraFollowUpMinutes: 0 }
            ? ((int)(a.Start - a.BlockedFrom).TotalMinutes, (int)(a.BlockedUntil - a.End).TotalMinutes, 0)
            : (a.PreparationMinutes, a.FollowUpMinutes, a.ExtraFollowUpMinutes);

    /// <summary>Die Zeiten dieses Termins weichen von denen der Behandlung ab (geändert im Termin).</summary>
    public static bool IsTimesChanged(AppointmentDto a, TreatmentDto? treatment) =>
        treatment is not null && TimesOf(a) is var (preparation, followUp, _)
        && (preparation != treatment.PreparationMinutes || followUp != treatment.FollowUpMinutes);

    /// <summary>„Vorb. 5 · Nachb. 10 · Puffer 10“ ohne Einheit; Teile mit 0 Minuten fehlen.</summary>
    public static IEnumerable<string> TimeParts(AppointmentDto a)
    {
        var (preparation, followUp, extra) = TimesOf(a);
        return new[] { preparation > 0 ? $"Vorb. {preparation}" : null, followUp > 0 ? $"Nachb. {followUp}" : null, extra > 0 ? $"Puffer {extra}" : null }
            .OfType<string>();
    }

    /// <summary>Minuten am Gast bis zur Einwirkzeit und Einwirkzeit des Termins; ohne Einwirkzeit (auch in alten Ständen) beide 0.</summary>
    public static (int HandsOn, int Processing) ProcessingOf(AppointmentDto a) =>
        a.ProcessingMinutes > 0 ? (a.HandsOnMinutes, a.ProcessingMinutes) : (0, 0);

    /// <summary>
    /// Ablauf des Termins von der Vorbereitung bis zum Puffer ohne Einheit: „Vorb. 5 · Hände 20 · Einwirken 20 · Ausarbeiten 20 · Nachb. 10“.
    /// Ohne Einwirkzeit dasselbe wie <see cref="TimeParts"/>; Teile mit 0 Minuten fehlen.
    /// </summary>
    public static IEnumerable<string> FlowParts(AppointmentDto a)
    {
        var (handsOn, processing) = ProcessingOf(a);
        if (processing == 0)
        {
            return TimeParts(a);
        }

        var (preparation, followUp, extra) = TimesOf(a);
        var finishing = (int)(a.End - a.Start).TotalMinutes - handsOn - processing;
        return new[]
        {
            preparation > 0 ? $"Vorb. {preparation}" : null,
            $"Hände {handsOn}",
            $"Einwirken {processing}",
            finishing > 0 ? $"Ausarbeiten {finishing}" : null,
            followUp > 0 ? $"Nachb. {followUp}" : null,
            extra > 0 ? $"Puffer {extra}" : null,
        }.OfType<string>();
    }

    /// <summary>„kommt 10:15“, solange der Gast eine Verspätung gemeldet hat und noch nicht eingecheckt ist.</summary>
    public static string? LateText(AppointmentDto a, TimeZoneInfo zone) =>
        a is { Status: AppointmentStatus.Booked, LateMinutes: { } minutes }
            ? $"kommt {Local(a.Start.AddMinutes(minutes), zone):HH:mm}"
            : null;

    public static string GenderSymbol(TherapistGender gender) => gender == TherapistGender.Female ? "♀" : "♂";

    public static string GenderNoun(TherapistGender gender) => gender == TherapistGender.Female ? "Therapeutin" : "Therapeut";

    private static string GenderWish(TherapistGender gender) => gender == TherapistGender.Female ? "eine Therapeutin" : "einen Therapeuten";

    /// <summary>Quelle für die dritte Zeile; vom Team gebuchte Termine brauchen keine.</summary>
    public static string? SourceText(AppointmentSource source) => source switch
    {
        AppointmentSource.Online => "Online",
        AppointmentSource.Email => "per Mail",
        AppointmentSource.Sms => "per SMS",
        AppointmentSource.WhatsApp => "per WhatsApp",
        AppointmentSource.Phone => "per Anruf",
        AppointmentSource.Treatwell => "Treatwell",
        _ => null,
    };

    /// <summary>Kurz für die dritte Zeile: „↻ 3/6“ (feste Abstände) oder „↻ 3“ (nach dem letzten Besuch).</summary>
    public static string SeriesLabel(SeriesInfo series, DateTimeOffset start)
    {
        var position = series.Starts.Count(s => s <= start);
        return series.Mode == SeriesMode.Weekly ? $"↻ {position}/{series.Starts.Count}" : $"↻ {position}";
    }

    /// <summary>„↻ alle 4 Wochen · Termin 3 von 6 · nächster Mo. 16.11.“, dazu ein Problem beim nächsten Termin oder „beendet“.</summary>
    public static string SeriesText(SeriesInfo series, DateTimeOffset start, TimeZoneInfo zone)
    {
        var rule = series.Mode == SeriesMode.Weekly
            ? series.IntervalWeeks == 1 ? "↻ jede Woche" : $"↻ alle {series.IntervalWeeks} Wochen"
            : $"↻ {series.IntervalWeeks} {(series.IntervalWeeks == 1 ? "Woche" : "Wochen")} nach dem letzten Besuch";
        var position = series.Starts.Count(s => s <= start);
        var next = series.Starts.Where(s => s > start).Order().Cast<DateTimeOffset?>().FirstOrDefault();
        var parts = new[]
        {
            rule,
            series.Mode == SeriesMode.Weekly ? $"Termin {position} von {series.Starts.Count}" : $"Termin {position}",
            next is { } n ? $"nächster {ShortDay(DateOnly.FromDateTime(Local(n, zone)))}" : series.Ended ? "Serie beendet" : null,
            series.Problem,
        };
        return string.Join(" · ", parts.Where(p => p is not null));
    }

    /// <summary>„Feste Therapeut:in: Lena Huber · lieber Di., Do. · 14:00–18:00“, oder null ohne Angaben.</summary>
    public static string? BookingPreferenceText(CustomerBookingPreferences? p, Func<Guid, string?> employeeName)
    {
        if (p is null)
        {
            return null;
        }

        var days = p.Weekdays is { Count: > 0 } weekdays
            ? string.Join(", ", weekdays.OrderBy(d => ((int)d + 6) % 7).Select(d => $"{German.DateTimeFormat.GetAbbreviatedDayName(d).TrimEnd('.')}."))
            : null;
        var times = (p.From, p.Until) switch
        {
            ({ } from, { } until) => $"{from:HH\\:mm}–{until:HH\\:mm}",
            ({ } from, null) => $"ab {from:HH\\:mm}",
            (null, { } until) => $"bis {until:HH\\:mm}",
            _ => null,
        };
        var parts = new[]
        {
            p.EmployeeId is { } id ? $"{(p.EmployeeFixed ? "Feste Therapeut:in" : "Bevorzugt")}: {employeeName(id) ?? "unbekannt"}" : null,
            p.Gender is { } gender ? $"wünscht {GenderWish(gender)}" : null,
            days is null && times is null ? null : $"lieber {string.Join(" · ", new[] { days, times }.OfType<string>())}",
        }.OfType<string>().ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    internal static bool IsOpen(AppointmentDto a) => a.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn;

    /// <summary>Eine Sperrzeit der Person, deren Grund mit „Pause“ beginnt, ist eine Pause (so rechnet auch der Server).</summary>
    public static bool IsBreak(string? reason) => reason?.TrimStart().StartsWith("Pause", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Ist die Spalte im Zeitraum belegt? Ein Termin zählt mit Vor- und Nachbereitung, dazu Sperrzeiten und Pausen der Person bzw. des Raums.
    /// </summary>
    public static bool IsOccupied(CalendarDay day, bool byRoom, Guid columnId, DateTimeOffset from, DateTimeOffset until) =>
        day.Appointments.Any(a => a.Status != AppointmentStatus.Cancelled && (byRoom ? a.RoomId : a.EmployeeId) == columnId
            && a.BlockedFrom < until && from < a.BlockedUntil)
        || day.Blocks.Any(b => (b.Kind == ResourceKind.Room) == byRoom && b.ResourceId == columnId && b.From < until && from < b.Until)
        || byRoom && (day.RoomClosures ?? []).Any(c => c.RoomId == columnId && c.From < until && from < c.Until);

    /// <summary>
    /// Die freie Lücke einer Spalte um <paramref name="at"/>: vom Ende des vorigen belegten Stücks (oder Dienstbeginn bzw. Öffnung)
    /// bis zum Beginn des nächsten (oder Dienstende bzw. Schließung). Null, wenn <paramref name="at"/> belegt oder außerhalb der Dienst- bzw.
    /// Öffnungszeit liegt, oder wenn nach hinten nichts begrenzt (Person ohne Dienstzeit und nichts danach).
    /// </summary>
    public static FreeGap? GapAt(CalendarDay day, bool byRoom, Guid columnId, DateTimeOffset at)
    {
        var from = DateTimeOffset.MinValue;
        var until = DateTimeOffset.MaxValue;
        var windows = byRoom
            ? day.Opening.Select(o => (From: o.Start, Until: o.End)).ToList()
            : day.Shifts.Where(s => s.EmployeeId == columnId).Select(s => (From: s.Start, Until: s.End)).ToList();
        if (windows.Count > 0)
        {
            var inside = windows.Where(w => w.From <= at && at < w.Until).ToList();
            if (inside.Count == 0)
            {
                return null;
            }

            from = inside[0].From;
            until = inside[0].Until;
        }

        var busy = day.Appointments
            .Where(a => a.Status != AppointmentStatus.Cancelled && (byRoom ? a.RoomId : a.EmployeeId) == columnId)
            .Select(a => (From: a.BlockedFrom, Until: a.BlockedUntil))
            .Concat(day.Blocks.Where(b => (b.Kind == ResourceKind.Room) == byRoom && b.ResourceId == columnId).Select(b => (b.From, b.Until)))
            .Concat(byRoom ? (day.RoomClosures ?? []).Where(c => c.RoomId == columnId).Select(c => (From: c.From, Until: c.Until)) : []);
        foreach (var (busyFrom, busyUntil) in busy)
        {
            if (busyFrom <= at && at < busyUntil)
            {
                return null;
            }

            if (busyUntil <= at && busyUntil > from)
            {
                from = busyUntil;
            }
            else if (busyFrom > at && busyFrom < until)
            {
                until = busyFrom;
            }
        }

        return until == DateTimeOffset.MaxValue ? null : new FreeGap(from, until);
    }

    /// <summary>Mindesthöhe eines Streifens in Pixeln, damit Vor- und Nachbereitung auch im kleinsten Maßstab als Band zu sehen sind.</summary>
    private const double MinStripeHeight = 6;

    /// <summary>
    /// Vorbereitung vor dem Termin und danach ein einziger Streifen für die Nachbereitung, in der Raumansicht zusammen mit der Raumpflege.
    /// Der Streifen trägt seinen Text auch als Hinweis, falls der Platz im Kalender nicht reicht.
    /// </summary>
    private static void AddStripes(CalendarColumn column, AppointmentDto a, string? preparation, int cleaningMinutes, Func<DateTimeOffset, double> y, RoomCareTaskDto? care = null, TimeZoneInfo? zone = null)
    {
        var (minutes, followUp, extra) = TimesOf(a);
        if (minutes > 0)
        {
            // Auch bei kleinem Maßstab ein erkennbares Band; es wächst nach oben und endet am Termin.
            var height = Math.Max(MinStripeHeight, y(a.Start) - y(a.Start.AddMinutes(-minutes)));
            column.Stripes.Add(new(y(a.Start) - height, height, $"Vorb. {minutes} Min.", StripeKind.Preparation)
            {
                LongText = preparation is null ? null : $"Vorb. {minutes} · {preparation}",
                Tip = preparation is null ? $"Vorbereitung {minutes} Min." : $"Vorbereitung {minutes} Min.: {preparation}",
                Appointment = a,
            });
        }

        if (followUp > 0 || extra > 0 || cleaningMinutes > 0)
        {
            var until = a.End.AddMinutes(followUp + extra + cleaningMinutes);
            var tip = string.Join(" · ", new[]
            {
                followUp > 0 ? $"Nachbereitung {followUp} Min." : null,
                extra > 0 ? $"Puffer {extra} Min." : null,
                cleaningMinutes > 0 ? $"Raumpflege {cleaningMinutes} Min." : null,
            }.OfType<string>());
            // Alles in einem schmalen Streifen: gekürzt, damit es in die Spalte passt; der volle Text steht im Hinweis.
            var text = followUp > 0 || extra > 0
                ? $"{string.Join(" · ", TimeParts(a).Where(p => !p.StartsWith("Vorb.", StringComparison.Ordinal)).Concat(cleaningMinutes > 0 ? [$"Raumpflege {cleaningMinutes}"] : []))} Min."
                : tip;
            column.Stripes.Add(new(y(a.End), Math.Max(MinStripeHeight, y(until) - y(a.End)), text, followUp > 0 || extra > 0 ? StripeKind.FollowUp : StripeKind.Cleaning)
            {
                Tip = care is not null && zone is not null ? $"{tip}\n{CareTip(care, zone)}\nKlick: Raumpflege {(care.State == RoomCareState.Done ? "zurücknehmen" : "abhaken")}" : tip,
                Appointment = a,
                Care = care,
            });
        }
    }

    /// <summary>Personen des Standorts, die an diesem Tag ohne „Alle zeigen“ keine Spalte hätten (kein Dienst hier und kein Termin).</summary>
    public static int HiddenStaffCount(CalendarDay day, LocationResources resources)
    {
        var ids = StaffWithColumn(day);
        return resources.Employees.Count(e => !ids.Contains(e.Id));
    }

    /// <summary>Zahl der Personenspalten der Teamansicht an diesem Tag (ohne „Alle zeigen“); nach ihr richtet sich auch die Spaltenbreite der Raumansicht.</summary>
    public static int TeamColumnCount(CalendarDay day, LocationResources resources)
    {
        var ids = StaffWithColumn(day);
        return resources.Employees.Count(e => ids.Contains(e.Id));
    }

    /// <summary>Personen mit Dienst an diesem Tag, dazu alle, die trotzdem Termine haben (z. B. nach einer Planänderung).</summary>
    private static HashSet<Guid> StaffWithColumn(CalendarDay day) =>
        day.Shifts.Select(s => s.EmployeeId).Concat(day.Appointments.Where(a => a.Status != AppointmentStatus.Cancelled).Select(a => a.EmployeeId)).ToHashSet();

    /// <summary>Dienst an einem anderen Standort: „Graz 10:00–18:00“.</summary>
    public static string ElsewhereText(CalendarElsewhere shift, TimeZoneInfo zone) => $"{shift.LocationName} {TimeText(shift.Start, shift.End, zone)}";

    public static string AbsenceWord(RosterKind kind) => kind switch
    {
        RosterKind.Sick => "krank gemeldet",
        RosterKind.Vacation => "im Urlaub",
        RosterKind.Training => "auf Schulung",
        _ => "frei",
    };

    /// <summary>Spaltenkopf und Meldung „Abwesenheit“: „krank gemeldet“, „Urlaub bis 23.10.“.</summary>
    public static string AbsenceText(CalendarAbsence absence, DateOnly date)
    {
        var what = absence.Kind switch
        {
            RosterKind.Sick => "krank gemeldet",
            RosterKind.Vacation => "Urlaub",
            RosterKind.Training => "Schulung",
            _ => "frei",
        };
        return absence.Until > date ? $"{what} bis {absence.Until:d.M.}" : what;
    }

    /// <summary>
    /// Die Tagesnotizen ohne Uhrzeit für die Gruppe „Notizen heute“ der Seitenleiste „Meldungen“ in der Reihenfolge des Servers. Zettel in einer Spalte (mit Uhrzeit)
    /// gehören in die Spalte; wer heute nicht da ist, zeigen die Spalten und, mit offenen Terminen, die Meldungen.
    /// </summary>
    public static IReadOnlyList<CalendarNoteDto> DayNotes(CalendarDay day) =>
        [.. (day.Notes ?? []).Where(n => n.ResourceId is null)];

    /// <summary>Minuten vom ersten bis zum letzten sichtbaren Stundenstrich; daraus rechnet „Tag einpassen“ den Maßstab.</summary>
    public static int VisibleMinutes(CalendarDay day, TimeZoneInfo zone)
    {
        var (start, end) = VisibleHours(day, zone);
        return (end - start) * 60;
    }

    /// <summary>Spalte unter einer waagrechten Position über allen Spalten, begrenzt auf die vorhandenen.</summary>
    public static int ColumnAt(double x, int count, CalendarScale? scale = null) =>
        count == 0 ? -1 : Math.Clamp((int)Math.Floor(x / ((scale ?? CalendarScale.Default).ColumnWidth + ColumnGap)), 0, count - 1);

    public static double ColumnLeft(int index, CalendarScale? scale = null) => index * ((scale ?? CalendarScale.Default).ColumnWidth + ColumnGap);

    /// <summary>Raster beim Ziehen eines Termins in Minuten; die Anzeige im Kalender bleibt bei Viertelstunden.</summary>
    private const int DragStepMinutes = 5;

    /// <summary>Beginn auf die nächsten fünf Minuten gerundet, für das Ziehen eines Termins (oberer Rand bei <paramref name="top"/>).</summary>
    public static DateTimeOffset SnapStart(DateTimeOffset dayStart, double top, CalendarScale? scale = null) =>
        dayStart.AddMinutes(Math.Max(0, Math.Round(top / (scale ?? CalendarScale.Default).PixelsPerMinute / DragStepMinutes) * DragStepMinutes));

    public static double Top(DateTimeOffset dayStart, DateTimeOffset t, CalendarScale? scale = null) =>
        (t - dayStart).TotalMinutes * (scale ?? CalendarScale.Default).PixelsPerMinute;

    /// <summary>Zwei Buchstaben für den Kreis vor dem Namen: „Anna Kern“ → „AK“, „Lukas“ → „LU“; Titel wie „Dr.“ zählen nicht.</summary>
    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => char.IsLetter(p[0]) && !p.EndsWith('.')).ToList();
        return parts.Count switch
        {
            0 => "?",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpper(German),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpper(German),
        };
    }

    /// <summary>
    /// Begonnen, aber noch nicht eingecheckt: so viele Minuten, sonst 0. Mit gemeldeter Verspätung zählt „überfällig“ erst ab der angekündigten Ankunft.
    /// </summary>
    public static int OverdueMinutes(AppointmentDto a, DateTimeOffset? now)
    {
        var expected = a.Start.AddMinutes(a.LateMinutes ?? 0);
        return now is { } at && a.Status == AppointmentStatus.Booked && expected < at && at < a.End
            ? (int)Math.Floor((at - expected).TotalMinutes)
            : 0;
    }

    /// <summary>
    /// Tagesüberblick für die Seitenleiste. Auslastung: Behandlungsminuten aller Personen geteilt durch ihre Dienstminuten.
    /// Der geplante Umsatz ist nur eine Orientierung; verbindlich sind die Beträge der Kassa.
    /// </summary>
    public static DayOverview Overview(CalendarDay day, string? currency = null, DateTimeOffset? now = null)
    {
        var active = day.Appointments.Where(a => a.Status != AppointmentStatus.Cancelled).ToList();
        var shiftMinutes = day.Shifts.Sum(s => (s.End - s.Start).TotalMinutes);
        var bookedMinutes = active.Where(a => a.Status != AppointmentStatus.NoShow).Sum(a => (a.End - a.Start).TotalMinutes);
        var utilization = shiftMinutes <= 0 ? "–" : $"{Math.Min(100, Math.Round(bookedMinutes / shiftMinutes * 100)):0} %";
        return new(
            active.Count,
            active.Count(a => a.Status == AppointmentStatus.CheckedIn),
            active.Count(a => a.Status == AppointmentStatus.Booked),
            active.Count(a => a.Status == AppointmentStatus.Completed),
            utilization,
            Money.Whole(active.Where(a => a.Status != AppointmentStatus.NoShow).Sum(a => a.Price), currency),
            active.Count(a => a is { Status: AppointmentStatus.Booked, ConfirmedAt: null }),
            active.Any(a => a.ConfirmedAt is not null),
            active.Count(a => OverdueMinutes(a, now) > 0));
    }

    /// <summary>
    /// Zeile im Tagesüberblick, über „Als Nächstes“ (Ä45): „Geburtstag heute: Brigitte Müller (10:30)“, mehrere mit „ · “ getrennt, je Gast der erste Termin des Tages.
    /// Ohne Gast mit Geburtstag am Tag und Termin <c>null</c>; abgesagte Termine und Gäste ohne Namen zählen nicht.
    /// </summary>
    public static string? BirthdayLine(CalendarDay day, TimeZoneInfo zone)
    {
        var celebrating = (day.Guests ?? []).Where(g => g.Birthday is { DaysAway: 0 }).Select(g => g.CustomerId).ToHashSet();
        var entries = day.Appointments
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.PlaceholderGuest is null && celebrating.Contains(a.CustomerId))
            .GroupBy(a => a.CustomerId).Select(g => g.OrderBy(a => a.Start).First()).OrderBy(a => a.Start).ThenBy(a => a.CustomerName, StringComparer.Ordinal)
            .Select(a => $"{a.CustomerName} ({Local(a.Start, zone):HH:mm})")
            .ToList();
        return entries.Count == 0 ? null : $"Geburtstag heute: {string.Join(" · ", entries)}";
    }

    /// <summary>Die nächsten noch nicht eingecheckten Termine ab jetzt (bzw. ab Tagesbeginn an anderen Tagen).</summary>
    public static IReadOnlyList<UpcomingItem> Upcoming(CalendarDay day, LocationResources resources, DateTimeOffset now, TimeZoneInfo zone, int count = 6)
    {
        return UpcomingItems(day.Appointments.Where(a => a.Status == AppointmentStatus.Booked && a.End > now), resources, zone, count);
    }

    /// <summary>
    /// Überfällige Termine (begonnen, nicht eingecheckt) im Tagesüberblick, die ältesten zuerst: bis zu <paramref name="count"/> Einträge
    /// und die Zahl der weiteren (Ä100). Gezählt wird wie bei <see cref="Overview"/>.
    /// </summary>
    public static (IReadOnlyList<UpcomingItem> Items, int More) Overdue(CalendarDay day, LocationResources resources, DateTimeOffset now, TimeZoneInfo zone, int count = 5)
    {
        var overdue = day.Appointments.Where(a => OverdueMinutes(a, now) > 0).ToList();
        return (UpcomingItems(overdue, resources, zone, count), Math.Max(0, overdue.Count - count));
    }

    private static List<UpcomingItem> UpcomingItems(IEnumerable<AppointmentDto> appointments, LocationResources resources, TimeZoneInfo zone, int count)
    {
        var employees = resources.Employees.ToDictionary(e => e.Id);
        return appointments
            .OrderBy(a => a.Start)
            .Take(count)
            .Select(a =>
            {
                var employee = employees.GetValueOrDefault(a.EmployeeId);
                return new UpcomingItem($"{Local(a.Start, zone):HH:mm}", a.CustomerName,
                    employee is null ? a.TreatmentName : $"{a.TreatmentName} · {employee.InHouseName()}", employee?.Color ?? "#2f6f5e", a);
            })
            .ToList();
    }

    /// <summary>Uhrzeit an einer Stelle der Spalte, auf 15 Minuten abgerundet.</summary>
    public static DateTimeOffset TimeAt(DateTimeOffset dayStart, double y, CalendarScale? scale = null)
    {
        var minutes = Math.Max(0, (int)(y / (scale ?? CalendarScale.Default).PixelsPerMinute) / 15 * 15);
        return dayStart.AddMinutes(minutes);
    }

    public static DateTimeOffset LocalToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone) =>
        new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), zone), TimeSpan.Zero);

    public static string TimeText(DateTimeOffset from, DateTimeOffset until, TimeZoneInfo zone) =>
        $"{Local(from, zone):HH:mm}–{Local(until, zone):HH:mm}";

    public static DateTime Local(DateTimeOffset t, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(t, zone).DateTime;

    /// <summary>Die Art einer Schließzeit in Worten, wie in Kalender und Meldungen: „Betriebsurlaub“, „Sonderschließung“, „Ausfall“.</summary>
    public static string ClosureTitle(ClosureKind kind) => kind switch
    {
        ClosureKind.Vacation => "Betriebsurlaub",
        ClosureKind.SpecialClosure => "Sonderschließung",
        _ => "Ausfall",
    };

    /// <summary>
    /// Der Zeitraum einer Schließzeit in Ortszeit, „Mo. 8.3. bis Mi. 10.3.“; ein Tag „Mo. 8.3.“, ein halber Tag „Mo. 8.3. ab 13:00 Uhr“. <paramref name="until"/> ist ausschließlich.
    /// </summary>
    public static string ClosurePeriod(DateTimeOffset from, DateTimeOffset until, TimeZoneInfo zone)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo("de-AT");
        var start = Local(from, zone);
        var end = Local(until, zone);
        var endsAtMidnight = end.TimeOfDay == TimeSpan.Zero;
        var last = endsAtMidnight ? end.AddDays(-1) : end;
        string Day(DateTime d) => ShortDay(DateOnly.FromDateTime(d));
        string Clock(DateTime d) => d.ToString("HH:mm", culture);
        var startsAtMidnight = start.TimeOfDay == TimeSpan.Zero;
        if (start.Date == last.Date)
        {
            return (startsAtMidnight, endsAtMidnight) switch
            {
                (true, true) => Day(start),
                (false, true) => $"{Day(start)} ab {Clock(start)} Uhr",
                (true, false) => $"{Day(start)} bis {Clock(end)} Uhr",
                _ => $"{Day(start)} von {Clock(start)} bis {Clock(end)} Uhr",
            };
        }

        return $"{Day(start)}{(startsAtMidnight ? "" : $" {Clock(start)} Uhr")} bis {Day(last)}{(endsAtMidnight ? "" : $" {Clock(end)} Uhr")}";
    }

    /// <summary>
    /// Die Schließbänder, die den Tag schneiden, auf die gezeigten Stunden begrenzt (ADR 0073). Der Text im Band nennt Art und internen Grund (sonst den Text für Gäste), der Hinweis
    /// Zeitraum, Grund, Text für Gäste und die Zahl der offenen Termine.
    /// </summary>
    public static IReadOnlyList<ClosureBandItem> ClosureBands(IEnumerable<ClosureBand>? bands, DateTimeOffset dayStart, double dayHeight, CalendarScale scale, TimeZoneInfo zone)
    {
        var items = new List<ClosureBandItem>();
        foreach (var band in (bands ?? []).OrderBy(b => b.From))
        {
            var top = Math.Max(0, Top(dayStart, band.From, scale));
            var bottom = Math.Min(dayHeight, Top(dayStart, band.Until, scale));
            if (bottom <= top)
            {
                continue;
            }

            var period = ClosurePeriod(band.From, band.Until, zone);
            var lines = new List<string> { $"{band.Title}: {period}" };
            if (!string.IsNullOrWhiteSpace(band.InternalReason))
            {
                lines.Add($"Grund: {band.InternalReason}");
            }

            lines.Add($"Text für Gäste: {band.Text}");
            if (band.OpenAppointments > 0)
            {
                lines.Add(band.OpenAppointments == 1 ? "1 Termin noch offen" : $"{band.OpenAppointments} Termine noch offen");
            }

            items.Add(new ClosureBandItem(band.Id, top, bottom - top, band.Kind, band.Title,
                string.IsNullOrWhiteSpace(band.InternalReason) ? $"{band.Title} · {band.Text}" : $"{band.Title} · {band.InternalReason}", string.Join('\n', lines), band.OpenAppointments));
        }

        return items;
    }

    /// <summary>Der Chip der Kopfzeile für den Tag: die erste Schließzeit des Tages; ohne Schließzeit keiner.</summary>
    public static ClosureChipInfo? ClosureChip(IEnumerable<ClosureBand>? bands, TimeZoneInfo zone)
    {
        if ((bands ?? []).OrderBy(b => b.From).FirstOrDefault() is not { } band)
        {
            return null;
        }

        var period = ClosurePeriod(band.From, band.Until, zone);
        var tip = $"Geschlossen: {band.Title}, {(period.EndsWith('.') ? period : period + ".")} {band.Text}{(band.OpenAppointments > 0 ? $" {(band.OpenAppointments == 1 ? "1 Termin" : $"{band.OpenAppointments} Termine")} noch offen." : "")}";
        return new ClosureChipInfo(band.Kind, $"Geschlossen · {band.Title}", "Geschlossen", tip);
    }

    /// <summary>„Mo. 1.3.“, unter Windows und Linux gleich (die Kurznamen der Wochentage haben dort mal einen Punkt, mal nicht).</summary>
    public static string ShortDay(DateOnly date) =>
        $"{German.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek).TrimEnd('.')}. {date.Day}.{date.Month}.";

    /// <summary>„Mo. 1.3. 16:00“ in der Zeitzone des Standorts.</summary>
    public static string ShortDayTime(DateTimeOffset at, TimeZoneInfo zone)
    {
        var local = Local(at, zone);
        return $"{ShortDay(DateOnly.FromDateTime(local))} {local:HH:mm}";
    }

    public static string DateText(DateOnly date) => date.ToString("dddd, d. MMMM yyyy", German);

    /// <summary>Erste und letzte sichtbare Stunde eines Tages (Öffnung, Dienste und Termine mit Vor- und Nachbereitung).</summary>
    public static (int Start, int End) VisibleHours(CalendarDay day, TimeZoneInfo zone)
    {
        var ranges = day.Opening.Select(o => (o.Start, o.End))
            .Concat(day.Shifts.Select(s => (s.Start, s.End)))
            .Concat(day.Appointments.Select(a => (Start: a.BlockedFrom, End: a.BlockedUntil)))
            .ToList();
        if (ranges.Count == 0)
        {
            return (8, 20);
        }

        var start = ranges.Min(r => Local(r.Start, zone)).Hour;
        var end = ranges.Max(r => Local(r.End, zone));
        return (Math.Min(start, 23), Math.Clamp(end.Minute > 0 ? end.Hour + 1 : end.Hour, start + 1, 24));
    }
}

/// <summary>
/// Maßstab des Kalenders: Höhe einer Minute (Zoom) und Breite einer Spalte (füllt die Fensterbreite).
/// Ohne Angabe gilt <see cref="Default"/>, wie vor dem Zoom.
/// </summary>
public sealed record CalendarScale(double PixelsPerMinute, double ColumnWidth)
{
    public static CalendarScale Default { get; } = new(DayLayout.PixelsPerMinute, DayLayout.ColumnWidth);

    /// <summary>Breite eines Termins in der Spalte (3 Pixel Rand links und rechts).</summary>
    public double ItemWidth => ColumnWidth - 6;
}
