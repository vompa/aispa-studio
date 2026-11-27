using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Eine Zelle der Besetzungsmatrix (Brief 5.32, T-102): die Zahl der Personen im Dienst in einer Stunde. Außerhalb der Öffnungszeit steht keine Zahl (<c>IsOff</c>),
/// in einer geöffneten Stunde ohne Person trägt die Zelle die Lücke (<c>IsGap</c>: Fläche, Warndreieck und „0“, nie nur Farbe).
/// </summary>
public sealed record StaffingCellViewModel(string Text, bool IsOff, bool IsGap, string AutomationName)
{
    public bool ShowsNumber => !IsOff;
}

/// <summary>Eine Stunde der Matrix: Beschriftung „10–11“ und eine Zelle je Tag der Woche.</summary>
public sealed record StaffingRowViewModel(string Label, string AutomationName, IReadOnlyList<StaffingCellViewModel> Cells)
{
    /// <summary>Die Matrix steht nur im Wochenplan des Teams, ihre Namensspalte ist so breit wie die des Plans darüber.</summary>
    public static double NameColumnWidth => RosterPlanViewModel.NameColumnWidthFor(true);
}

public sealed partial class RosterPlanViewModel
{
    /// <summary>Satz statt Matrix, wenn Dienste da sind, der Standort laut Öffnungszeiten aber in der ganzen Woche nie geöffnet ist (auch ohne eingetragene Öffnungszeiten).</summary>
    public const string StaffingHintText = "In dieser Woche ist der Standort laut Öffnungszeiten nie geöffnet. Die Öffnungszeiten stehen unter Stammdaten › Standorte & Räume.";

    /// <summary>Besetzung je Stunde (Zahlen vom Server, <c>RosterWeek.HourlyStaffing</c>): eine Zeile je Stunde, eine Zelle je Tag.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<StaffingRowViewModel> StaffingRows { get; private set; } = [];

    /// <summary>Die Matrix steht nur im Wochenplan des Teams (nie auf der Personenkarte), mit Personen im Plan und mindestens einer geöffneten Stunde.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStaffingSection))]
    public partial bool ShowsStaffing { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStaffingSection))]
    public partial bool ShowsStaffingHint { get; private set; }

    /// <summary>Ohne Personen entfällt der ganze Abschnitt wie die Zeile „Besetzung“.</summary>
    public bool HasStaffingSection => ShowsStaffing || ShowsStaffingHint;

    private void ClearStaffing()
    {
        StaffingRows = [];
        ShowsStaffing = false;
        ShowsStaffingHint = false;
    }

    private void BuildStaffing(RosterWeek week, Guid locationId)
    {
        var hourly = week.HourlyStaffing ?? [];
        StaffingRows =
        [
            .. hourly.GroupBy(h => h.Hour).OrderBy(g => g.Key).Select(group =>
            {
                var hour = group.Key;
                var cells = week.Dates.Select(date => StaffingCellOf(date, hour, group.FirstOrDefault(h => h.Date == date))).ToList();
                return new StaffingRowViewModel($"{hour}–{hour + 1}", $"{hour} bis {hour + 1} Uhr", cells);
            }),
        ];
        var hasDutyHere = Rows.Any(r => r.Cells.Any(c => c.Cell.Entries.Any(e => e.Kind == RosterKind.Work && e.LocationId == locationId)));
        ShowsStaffing = ShowsNames && Rows.Count > 0 && StaffingRows.Count > 0;
        ShowsStaffingHint = ShowsNames && StaffingRows.Count == 0 && hasDutyHere;
    }

    private static StaffingCellViewModel StaffingCellOf(DateOnly date, int hour, HourlyStaffing? slot)
    {
        var start = $"{DayLayout.ShortDay(date)}, {hour} bis {hour + 1} Uhr: ";
        if (slot is not { Open: true })
        {
            return new StaffingCellViewModel(string.Empty, true, false, start + "geschlossen");
        }

        var state = slot.Count switch
        {
            0 => "niemand im Dienst",
            1 => "1 Person im Dienst",
            _ => $"{slot.Count} Personen im Dienst",
        };
        return new StaffingCellViewModel(slot.Count.ToString(CultureInfo.InvariantCulture), false, slot.Count == 0, start + state);
    }
}
