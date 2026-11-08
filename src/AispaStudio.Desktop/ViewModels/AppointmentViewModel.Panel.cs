using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Teil einer gemeinsamen Buchung im Seitenpanel: Zeit, Gast, Behandlung, Person, Raum und Status.</summary>
public sealed record GroupPartRow(AppointmentDto Appointment, string Time, string Guest, string? Symbol, string Treatment, string Person, string Room, bool IsCurrent)
{
    public string? SymbolIcon => Controls.Icons.Salutation(Symbol);

    /// <summary>Zweite Zeile: „Hot-Stone-Massage · Marco Berger · Raum Zeder“.</summary>
    public string Detail => string.Join(" · ", new[] { Treatment, Person, Room }.Where(p => !string.IsNullOrEmpty(p)));

    public string Status => CalendarItem.StatusName(Appointment.Status);

    /// <summary>Name für den Screenreader wie bei der Terminkarte: „14:00–14:50 Paul Muster, Massage · Anna · Raum Zeder, Gebucht“.</summary>
    public string AccessibleName => $"{Time} {Guest}, {Detail}, {Status}";

    public bool IsCancelled => Appointment.Status == AppointmentStatus.Cancelled;

    public bool IsCheckedIn => Appointment.Status == AppointmentStatus.CheckedIn;

    public bool IsProblem => Appointment.Status is AppointmentStatus.NoShow or AppointmentStatus.Cancelled;
}

/// <summary>Eine Behandlung des Raums; die Dauern der Varianten stehen in einer Zeile („60 und 90 Min.“), ohne Varianten ist <paramref name="Durations"/> leer.</summary>
public sealed record RoomTreatmentRow(string Name, string Durations);

/// <summary>Ein Termin des Raums an dem Tag in festen Spalten: Zeit, Gast, Behandlung; <paramref name="IsCurrent"/> ist der Termin im Panel.</summary>
public sealed record RoomDayRow(string Time, string Guest, string Treatment, bool IsCurrent)
{
    /// <summary>Name für den Screenreader: „10:00–10:50 Eva Muster, Massage, gewählter Termin“.</summary>
    public string AccessibleName => $"{Time} {Guest}, {Treatment}{(IsCurrent ? ", gewählter Termin" : "")}";
}

/// <summary>Karte „Raum-Infos“ neben dem Seitenpanel: Raumart, Raumpflege, Behandlungen (aufklappbar) und die Termine des Raums an dem Tag; Link zur Verwaltung.</summary>
public sealed partial class RoomInfoViewModel(RoomDto room, DayLayout.RoomFacts facts, IReadOnlyList<RoomDayRow> day, string dayTitle, Action? editInAdmin) : ViewModelBase
{
    /// <summary>Ab so vielen Zeilen klappt die Liste der Behandlungen auf und zu.</summary>
    private const int CollapsedTreatmentRows = 6;

    /// <summary>So viele Terminzeilen zeigt die Karte höchstens, der Rest steht als „und n weitere“.</summary>
    private const int MaxDayRows = 6;

    private static readonly Regex Variant = new(@"^(?<name>.+?)\s*\((?<minutes>\d+)\s*Min\.?\)$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public string Name => room.Name;

    public string Kind => facts.Kind;

    /// <summary>„Raumpflege 10 Min. nach jedem Termin“.</summary>
    public string Cleaning => facts.CleaningLong;

    /// <summary>Name für den Screenreader: „Raum Lavendel, Einzelraum“.</summary>
    public string AccessibleName => $"{room.Name}, {facts.Kind}";

    public IReadOnlyList<RoomTreatmentRow> TreatmentRows { get; } = MergeVariants(facts.Treatments);

    /// <summary>„11 Behandlungen“, „1 Behandlung“ bzw. „Keine Behandlung zugeordnet“.</summary>
    public string TreatmentsTitle => TreatmentRows.Count switch
    {
        0 => "Keine Behandlung zugeordnet",
        1 => "1 Behandlung",
        var n => $"{n} Behandlungen",
    };

    public bool HasTreatments => TreatmentRows.Count > 0;

    public bool CanExpandTreatments => TreatmentRows.Count > CollapsedTreatmentRows;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleTreatments), nameof(ToggleTreatmentsText))]
    private bool _isTreatmentsExpanded;

    public IReadOnlyList<RoomTreatmentRow> VisibleTreatments => IsTreatmentsExpanded ? TreatmentRows : [.. TreatmentRows.Take(CollapsedTreatmentRows)];

    public string ToggleTreatmentsText => IsTreatmentsExpanded ? "Weniger anzeigen" : "Alle anzeigen";

    [RelayCommand]
    private void ToggleTreatments() => IsTreatmentsExpanded = !IsTreatmentsExpanded;

    /// <summary>„Heute“ bzw. „Am 28.10.“: der Tag des Termins im Panel.</summary>
    public string DayTitle => dayTitle;

    /// <summary>Alle Termine des Raums an dem Tag, auch der im Panel (hervorgehoben).</summary>
    public IReadOnlyList<RoomDayRow> DayRows => day;

    /// <summary>Gibt es außer dem Termin im Panel weitere Termine, steht der Abschnitt da.</summary>
    public bool HasOthers => day.Any(r => !r.IsCurrent);

    /// <summary>Die ersten Zeilen; liegt der Termin im Panel dahinter, ersetzt er die letzte sichtbare Zeile.</summary>
    public IReadOnlyList<RoomDayRow> VisibleDayRows
    {
        get
        {
            if (day.Count <= MaxDayRows)
            {
                return day;
            }

            var first = day.Take(MaxDayRows).ToList();
            if (day.FirstOrDefault(r => r.IsCurrent) is { } current && !first.Contains(current))
            {
                first[^1] = current;
            }

            return first;
        }
    }

    public bool HasMoreDayRows => day.Count > MaxDayRows;

    /// <summary>„und 3 weitere“.</summary>
    public string MoreDayRowsText => $"und {day.Count - MaxDayRows} weitere";

    public bool CanEdit => editInAdmin is not null;

    [RelayCommand]
    private void EditInAdmin() => editInAdmin?.Invoke();

    /// <summary>
    /// Fasst Varianten desselben Namens zusammen: „Massage (60 Min.)“ und „Massage (90 Min.)“ werden „Massage“ mit „60 und 90 Min.“.
    /// Die Reihenfolge richtet sich nach dem ersten Vorkommen; Namen ohne Dauer in Klammern bleiben, wie sie sind.
    /// </summary>
    public static IReadOnlyList<RoomTreatmentRow> MergeVariants(IEnumerable<string> names)
    {
        var order = new List<string>();
        var minutes = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var full in names)
        {
            var match = Variant.Match(full);
            var name = match.Success ? match.Groups["name"].Value : full;
            if (!minutes.TryGetValue(name, out var list))
            {
                minutes[name] = list = [];
                order.Add(name);
            }

            if (match.Success)
            {
                list.Add(int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture));
            }
        }

        return [.. order.Select(name => new RoomTreatmentRow(name, DurationText(minutes[name])))];
    }

    private static string DurationText(List<int> minutes)
    {
        var sorted = minutes.Distinct().Order().Select(m => m.ToString(CultureInfo.InvariantCulture)).ToList();
        return sorted.Count switch
        {
            0 => "",
            1 => $"{sorted[0]} Min.",
            _ => $"{string.Join(", ", sorted[..^1])} und {sorted[^1]} Min.",
        };
    }
}

// ---------- Panel: ♀/♂, Kundendaten ergänzen, Raum-Info ----------
public partial class AppointmentViewModel
{
    /// <summary>♀ oder ♂ aus der Anrede des Gastes, neben dem Namen.</summary>
    public string? Symbol { get; init; }

    public string? SymbolIcon => Controls.Icons.Salutation(Symbol);

    /// <summary>Länder der Standorte, für die Vorschau der Anrede im Formular.</summary>
    public IReadOnlyList<FiscalCountry> Countries { get; init; } = [FiscalCountry.AT];

    /// <summary>Die Kundendaten wurden geändert: der Kalender lädt neu und zeigt den Termin mit den neuen Daten.</summary>
    public Func<Task>? CustomerChanged { get; init; }

    /// <summary>Raum-Infos hinter dem Raum-Symbol.</summary>
    public RoomInfoViewModel? RoomInfo { get; init; }

    /// <summary>Uhr für „nächste Termine“ in der Kundenübersicht.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;

    private CustomerOverviewViewModel? _overview;

    /// <summary>Kundenübersicht (Flyout am Gastnamen); ein Gast ohne Namen hat keine.</summary>
    public bool CanShowOverview => !IsPlaceholder;

    public CustomerOverviewViewModel Overview => _overview ??= new CustomerOverviewViewModel(Api, Appointment.CustomerId, Time,
        CanShowCustomer ? ShowCustomerAsync : null, CanEditCustomer ? () => EditCustomerCommand.Execute(null) : null);

    [RelayCommand]
    private void ClearError() => Error = null;

    /// <summary>Lädt die Kundenübersicht neu; beim Öffnen des Flyouts, damit der Stand nie veraltet ist.</summary>
    public void ReloadOverview() => _ = Overview.LoadAsync();

    /// <summary>„Bereitstellen“ zu diesem Termin (Tee, Früchteteller …) mit Haken; leer, wenn die Behandlung nichts vorsieht.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasServings))]
    public partial ServingPanelViewModel? Servings { get; set; }

    public bool HasServings => Servings is not null;

    /// <summary>Gast ohne Namen trägt man über „Name nachtragen“ ein; sonst darf bearbeiten, wer buchen darf.</summary>
    public bool CanEditCustomer => MayBook && !IsPlaceholder && CustomerChanged is not null;

    /// <summary>Das Formular für die Kundendaten (dasselbe wie in der Kundenkarte), solange es offen ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingCustomer))]
    public partial CustomerContactFormViewModel? CustomerForm { get; private set; }

    public bool IsEditingCustomer => CustomerForm is not null;

    /// <summary>Stift neben dem Gastnamen: Anrede, Rufname, Pronomen, Titel, Telefon, E-Mail, Anschrift … ergänzen, ohne die Kundenkarte zu öffnen.</summary>
    [RelayCommand]
    private async Task EditCustomerAsync()
    {
        if (!CanEditCustomer)
        {
            return;
        }

        var result = await Api.GetAsync<CustomerDto>($"api/customers/{Appointment.CustomerId}").ConfigureAwait(true);
        if (result.Value is not { } customer)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        CustomerForm = new CustomerContactFormViewModel(Api, customer, Countries,
            async _ =>
            {
                CustomerForm = null;
                await CustomerChanged!().ConfigureAwait(true);
            },
            () => CustomerForm = null);
    }
}
