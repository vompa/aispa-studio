using System.Globalization;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Monat der Auswahl im Lohn („Oktober 2026“).</summary>
public sealed record PayrollMonth(int Year, int Month)
{
    private static readonly string[] Names =
        ["Jänner", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"];

    public string Label => $"{Names[Month - 1]} {Year}";

    /// <summary>Schreibweise in Dateinamen: „2026-10“.</summary>
    public string Key => $"{Year:0000}-{Month:00}";

    public override string ToString() => Label;

    /// <summary>Die letzten <paramref name="count"/> Monate bis einschließlich des Monats von <paramref name="today"/>, der neueste zuerst.</summary>
    public static IReadOnlyList<PayrollMonth> Last(DateOnly today, int count = 24) =>
        [.. Enumerable.Range(0, count).Select(i => today.AddMonths(-i)).Select(d => new PayrollMonth(d.Year, d.Month))];

    /// <summary>Der Vormonat: der Monat, den die Lohnverrechnung meist abrechnet.</summary>
    public static PayrollMonth Previous(DateOnly today) => Last(today, 2)[1];
}

/// <summary>Eine Zeile der Vorschau: Person, Personalnummer, Position, Menge und Lohnart, fertig als Text (die Zahlen rechnet die API).</summary>
public sealed record PayrollRow(string Person, string PersonnelNumber, string Item, string Quantity, string WageType)
{
    private static string ItemLabel(PayrollItem item) => item switch
    {
        PayrollItem.Hours => "Stunden gesamt",
        PayrollItem.SundayHours => "davon Sonntag",
        PayrollItem.HolidayHours => "davon Feiertag",
        PayrollItem.NightHours => "davon Nacht",
        PayrollItem.VacationDays => "Urlaub",
        PayrollItem.SickDays => "Krankenstand",
        PayrollItem.TrainingDays => "Schulung",
        _ => item.ToString(),
    };

    public static PayrollRow From(PayrollLine line) => new(
        line.DisplayName,
        line.PersonnelNumber ?? "",
        ItemLabel(line.Item),
        $"{line.Quantity.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',')} {line.Unit}",
        line.WageType ?? "");

    /// <summary>Für den Screenreader: eine Zeile als Satz.</summary>
    public string AutomationName => $"{Person}, {Item}: {Quantity}" + (WageType.Length > 0 ? $", Lohnart {WageType}" : ", ohne Lohnart");
}

/// <summary>Prüfung vor dem Export, wie <c>web/src/payroll/exportCheck.ts</c>: ohne übergebbare Zeile gäbe es eine leere Datei, dann steht ein Text mit dem Weg statt der Datei.</summary>
public static class PayrollExportCheck
{
    public const string NoLines = "Der Export wurde nicht erstellt: Im Monat gibt es keine Dienste oder Abwesenheiten zum Übergeben. Wähle einen anderen Monat.";

    /// <summary>Der Text, warum nichts exportiert wird, oder <c>null</c>, wenn mindestens eine Zeile Personalnummer und Lohnart hat.</summary>
    public static string? Blocker(PayrollPreviewDto preview)
    {
        if (preview.Lines.Count == 0)
        {
            return NoLines;
        }

        if (preview.Lines.Any(l => !string.IsNullOrEmpty(l.PersonnelNumber) && !string.IsNullOrEmpty(l.WageType)))
        {
            return null;
        }

        var withoutNumber = string.Join(", ", preview.Lines.Where(l => string.IsNullOrEmpty(l.PersonnelNumber)).Select(l => l.DisplayName).Distinct());
        var reasons = new List<string>();
        if (withoutNumber.Length > 0)
        {
            reasons.Add($"Es fehlt die Personalnummer von {withoutNumber}. Sie steht auf der Karte der Person im Reiter „Lohn“.");
        }

        if (preview.Lines.Any(l => string.IsNullOrEmpty(l.WageType)))
        {
            reasons.Add("Es fehlen Lohnarten. Sie stehen in den Einstellungen der Lohnverrechnung.");
        }

        return $"Der Export wurde nicht erstellt, weil keine Zeile übergeben werden könnte. {string.Join(' ', reasons)} Danach den Export erneut starten.";
    }
}
