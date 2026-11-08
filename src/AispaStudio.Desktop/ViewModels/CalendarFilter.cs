using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

public enum CalendarFilterKind
{
    /// <summary>Alle Termine einer Person.</summary>
    Employee,

    /// <summary>Alle Termine eines Raums.</summary>
    Room,

    /// <summary>Alle Termine eines Gastes, bei wem und in welchem Raum auch immer.</summary>
    Customer,
}

/// <summary>
/// Schränkt den Kalender auf eine Person oder einen Raum ein und zeigt sie über sieben Tage: eine Spalte je Tag.
/// Der Filter auf einen Gast zeigt dessen Termine bei allen Personen und in allen Räumen in einer Spalte je Tag.
/// </summary>
public sealed record CalendarFilter(CalendarFilterKind Kind, Guid Id, string Title, string Color)
{
    /// <summary>Wie viele Tage der Filter zeigt.</summary>
    public const int Days = 7;

    public bool ByRoom => Kind == CalendarFilterKind.Room;

    /// <summary>Etikett in der Kopfzeile: „Lena Huber · 7 Tage“.</summary>
    public string Label => $"{Title} · {Days} Tage";

    /// <summary>Gehört der Termin zum Filter?</summary>
    public bool Matches(AppointmentDto appointment) => Kind switch
    {
        CalendarFilterKind.Room => appointment.RoomId == Id,
        CalendarFilterKind.Customer => appointment.CustomerId == Id,
        _ => appointment.EmployeeId == Id,
    };

    /// <summary>Die Spalte eines Tages, die zum Filter gehört (null, wenn die Person bzw. der Raum an dem Tag keine Spalte hat).</summary>
    public CalendarColumn? Pick(IReadOnlyList<CalendarColumn> columns)
    {
        if (Kind != CalendarFilterKind.Customer)
        {
            return columns.FirstOrDefault(c => c.Id == Id);
        }

        // Ein Gast hat keine eigene Spalte: seine Termine aus allen Spalten des Tages in eine legen (zeitlich nacheinander, nie gleichzeitig).
        var items = columns.SelectMany(c => c.Items).Where(i => i.Appointment is { } a && a.CustomerId == Id).OrderBy(i => i.Top)
            .Select(i => i with { Place = LanePlace.Alone }).ToList();
        var merged = new CalendarColumn(Id, Title, Color, items.Count switch { 0 => "kein Termin", 1 => "1 Termin", var n => $"{n} Termine" });
        merged.Items.AddRange(items);
        merged.Stripes.AddRange(columns.SelectMany(c => c.Stripes).Where(s => s.Appointment?.CustomerId == Id));
        return merged;
    }
}

/// <summary>Sieben Tage einer Person oder eines Raums nebeneinander, mit denselben Terminkarten wie der Tageskalender.</summary>
public static class WeekLayout
{
    /// <summary>
    /// Jeder Tag wird wie im Tageskalender aufgebaut (mit gemeinsamer erster und letzter Stunde, damit die Tage auf einer Höhe liegen);
    /// aus jedem Tag zählt nur die Spalte des Filters, mit dem Tag als Titel.
    /// </summary>
    public static DayLayout.Result Build(IReadOnlyList<CalendarDay> days, LocationResources resources, CalendarFilter filter, TimeZoneInfo zone,
        Guid? selected = null, Guid? lifted = null, IReadOnlySet<Guid>? pending = null, CalendarScale? scale = null, DateTimeOffset? now = null)
    {
        var ranges = days.Select(d => DayLayout.VisibleHours(d, zone)).ToList();
        var shared = (Start: ranges.Min(r => r.Start), End: ranges.Max(r => r.End));
        var columns = new List<CalendarColumn>();
        DayLayout.Result? first = null;
        foreach (var day in days)
        {
            var built = DayLayout.Build(day, resources, filter.ByRoom, zone, selected, lifted, pending, scale, now, shared);
            first ??= built;
            columns.Add(DayColumn(day, filter, filter.Pick(built.Columns), zone));
        }

        return first! with { Columns = columns };
    }

    private static CalendarColumn DayColumn(CalendarDay day, CalendarFilter filter, CalendarColumn? picked, TimeZoneInfo zone)
    {
        var title = DayLayout.ShortDay(day.Date);
        // Unter dem Tag steht die Dienstzeit der Person bzw. die Öffnungszeit des Standorts, nicht noch einmal die Angaben zum Raum.
        var subtitle = filter.ByRoom
            ? day.Opening.Count == 0 ? "geschlossen" : string.Join(", ", day.Opening.Select(o => DayLayout.TimeText(o.Start, o.End, zone)))
            : picked?.Subtitle ?? "ohne Dienst";
        var column = new CalendarColumn(filter.Id, title, filter.Color, subtitle)
        {
            Initials = day.Date.ToString("dd", System.Globalization.CultureInfo.InvariantCulture),
            IsDay = true,
            Date = day.Date,
            OffDuty = picked?.OffDuty ?? false,
            Absence = picked?.Absence,
            Affected = picked?.Affected ?? 0,
            BreakMinutes = picked?.BreakMinutes ?? 0,
        };
        if (picked is not null)
        {
            column.Available.AddRange(picked.Available);
            column.Items.AddRange(picked.Items);
            column.Stripes.AddRange(picked.Stripes);
            column.Notes.AddRange(picked.Notes);
        }

        return column;
    }
}
