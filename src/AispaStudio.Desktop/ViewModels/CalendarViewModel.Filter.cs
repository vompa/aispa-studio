using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// 7-Tage-Ansicht: Ein Klick auf eine Person (oder in der Raumansicht auf einen Raum) zeigt deren Termine der nächsten sieben Tage,
/// eine Spalte je Tag. Die Pfeile schalten wochenweise weiter; „Filter aufheben“ und Esc führen zum Tag mit allen Spalten zurück.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Die sieben geladenen Tage der 7-Tage-Ansicht; <c>null</c> im normalen Tageskalender.</summary>
    private IReadOnlyList<CalendarDay>? _week;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFiltered), nameof(FilterText), nameof(DayTitle), nameof(DateShort), nameof(LocationSuffix), nameof(KeyHints), nameof(CanResetView), nameof(DayFactsText), nameof(HasDayFactsText), nameof(DayFactsTip), nameof(OverviewDayLine), nameof(HasOverviewDayLine))]
    public partial CalendarFilter? Filter { get; private set; }

    public bool IsFiltered => Filter is not null;

    /// <summary>Etikett in der Kopfzeile: „Lena Huber · 7 Tage“.</summary>
    public string FilterText => Filter?.Label ?? "";

    /// <summary>Gesetzt, solange <see cref="GoToDay"/> Filter und Datum gemeinsam ändert, damit der Tag nur einmal geladen wird.</summary>
    private bool _changingDay;

    partial void OnFilterChanged(CalendarFilter? value)
    {
        Ghost = null;
        _hoverAt = null;
        if (!_changingDay)
        {
            _ = LoadAsync();
        }
    }

    /// <summary>Klick auf den Kopf einer Person bzw. eines Raums: deren Termine über sieben Tage.</summary>
    public void FilterBy(CalendarColumn column)
    {
        if (!column.CanFilter || Filter is not null)
        {
            return;
        }

        Filter = new CalendarFilter(column.IsRoom ? CalendarFilterKind.Room : CalendarFilterKind.Employee, column.Id, column.Title, column.Color);
    }

    /// <summary>Klick auf den Kopf eines Tages der 7-Tage-Ansicht: zurück zur normalen Ansicht, an diesem Tag.</summary>
    public void GoToDay(CalendarColumn column)
    {
        if (column.Date is not { } date)
        {
            return;
        }

        _changingDay = true;
        try
        {
            Filter = null;
            Date = date;
        }
        finally
        {
            _changingDay = false;
        }

        _ = LoadAsync();
    }

    /// <summary>
    /// „Alle Termine von Steffi Huber“ (Assistent): die 7-Tage-Ansicht mit den Terminen des Gastes bei allen Personen und in allen Räumen,
    /// ab dem Tag des nächsten Termins (sonst ab dem letzten, sonst ab heute). Die Pfeile blättern wochenweise.
    /// </summary>
    public async Task FilterByCustomerAsync(Guid customerId)
    {
        var customer = await _api.GetAsync<CustomerDto>($"api/customers/{customerId}").ConfigureAwait(true);
        if (customer.Value is not { } guest)
        {
            ShowProblem("Gast nicht gefunden", customer.Error, action: () => _ = FilterByCustomerAsync(customerId));
            return;
        }

        var appointments = await _api.GetAsync<List<AppointmentDto>>($"api/customers/{customerId}/appointments").ConfigureAwait(true);
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var days = (appointments.Value ?? []).Where(a => a.Status != AppointmentStatus.Cancelled && (Location is null || a.LocationId == Location.Id))
            .Select(a => DateOnly.FromDateTime(DayLayout.Local(a.Start, _zone))).Distinct().Order().ToList();
        var start = days.FirstOrDefault(d => d >= today, days.Count > 0 ? days[^1] : today);
        DropAllPanels();
        Ghost = null;
        ByRoom = false;
        Date = start;
        Filter = new CalendarFilter(CalendarFilterKind.Customer, customerId, guest.InHouseName, "#0d5c63");
    }

    /// <summary>„Filter aufheben“: zurück zum Tag mit allen Spalten.</summary>
    [RelayCommand]
    private void ClearFilter() => Filter = null;

    /// <summary>Die Ansicht beim ersten Öffnen: Teamansicht, heutiger Tag, kein Filter, nichts hervorgehoben.</summary>
    private bool _openedByRoom;

    /// <summary>Filter, Team oder Räume, Hervorhebung oder Tag weichen vom ersten Öffnen ab: dann bietet die Tastenleiste „Ansicht zurücksetzen“.</summary>
    public bool CanResetView => IsFiltered || ByRoom != _openedByRoom || HasFocus || !IsToday;

    private bool HasAppointments => Columns.Any(c => c.Items.Any(i => i.Appointment is not null));

    /// <summary>Strg+R: zurück zur Ansicht beim ersten Öffnen. Offene Leisten und Eingaben bleiben, Zoom und „Alle zeigen“ auch.</summary>
    private void ResetView()
    {
        _changingDay = true;
        try
        {
            Filter = null;
            ByRoom = _openedByRoom;
            Focus = CalendarFocus.None;
            Date = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        }
        finally
        {
            _changingDay = false;
        }

        _ = LoadAsync();
    }

    /// <summary>Tage, die der Kalender gerade zeigt: einer oder sieben.</summary>
    private int SpanDays => Filter is null ? 1 : CalendarFilter.Days;

    /// <summary>Liegt der Tag im angezeigten Zeitraum (ein Tag oder die sieben der Filteransicht)?</summary>
    private bool InRange(DateOnly date) => _week is null ? date == _day?.Date : _week.Any(d => d.Date == date);

    /// <summary>„19.–25. Oktober 2026“ bzw. „28. Oktober – 3. November 2026“.</summary>
    public string RangeText
    {
        get
        {
            var culture = CultureInfo.GetCultureInfo("de-AT");
            var last = Date.AddDays(CalendarFilter.Days - 1);
            if (Date.Year != last.Year)
            {
                return $"{Date.ToString("d. MMMM yyyy", culture)} – {last.ToString("d. MMMM yyyy", culture)}";
            }

            return Date.Month == last.Month
                ? $"{Date.Day}.–{last.ToString("d. MMMM yyyy", culture)}"
                : $"{Date.ToString("d. MMMM", culture)} – {last.ToString("d. MMMM yyyy", culture)}";
        }
    }

    /// <summary>
    /// Der Tag, aus dem Tagesüberblick und „Als Nächstes“ rechnen: in der 7-Tage-Ansicht nur die Termine und Dienste der gewählten Person
    /// (bei einem Raum ohne Dienste, die Auslastung zeigt dann „–“).
    /// </summary>
    private CalendarDay Scoped(CalendarDay day) => Filter is not { } filter ? day : day with
    {
        Appointments = [.. day.Appointments.Where(filter.Matches)],
        Shifts = [.. day.Shifts.Where(s => !filter.ByRoom && s.EmployeeId == filter.Id)],
    };

    /// <summary>Lädt einen oder mehrere aufeinanderfolgende Tage; der erste Fehler zählt, bei Offline-Daten der älteste Stand.</summary>
    private async Task<(IReadOnlyList<CalendarDay>? Days, string? Error, DateTimeOffset? CachedAt)> LoadDaysAsync(LocationDto location, DateOnly start, int count)
    {
        var results = await Task.WhenAll(Enumerable.Range(0, count)
            .Select(i => _api.GetAsync<CalendarDay>($"api/locations/{location.Id}/calendar?date={start.AddDays(i):yyyy-MM-dd}"))).ConfigureAwait(true);
        if (results.FirstOrDefault(r => r.Value is null) is { } failed)
        {
            return (null, failed.Error, null);
        }

        return ([.. results.Select(r => r.Value!)], null, results.Select(r => r.CachedAt).Where(c => c is not null).Min());
    }

    /// <summary>Alle sieben Tage in einem Tag zusammengefasst, damit Suche in Gästen, Gruppen und Serien über die ganze Spanne funktioniert.</summary>
    private static CalendarDay Merge(IReadOnlyList<CalendarDay> days) => days[0] with
    {
        Opening = [.. days.SelectMany(d => d.Opening)],
        Shifts = [.. days.SelectMany(d => d.Shifts)],
        Appointments = [.. days.SelectMany(d => d.Appointments)],
        Blocks = [.. days.SelectMany(d => d.Blocks)],
        Guests = [.. days.SelectMany(d => d.Guests ?? []).DistinctBy(g => g.CustomerId)],
        Absences = [.. days.SelectMany(d => d.Absences ?? [])],
        Breaks = [.. days.SelectMany(d => d.Breaks ?? [])],
        Notes = [.. days.SelectMany(d => d.Notes ?? [])],
        Preparations = [.. days.SelectMany(d => d.Preparations ?? []).DistinctBy(p => p.TreatmentId)],
        Series = [.. days.SelectMany(d => d.Series ?? []).DistinctBy(s => s.Id)],
        GroupAreas = [.. days.SelectMany(d => d.GroupAreas ?? [])],
    };

    /// <summary>Der geladene Stand je Tag, mit Terminen, Sperrzeiten und Notizen aus dem zusammengefassten Tag (dort laufen Live-Änderungen auf).</summary>
    private IReadOnlyList<CalendarDay> WeekDays() => [.. _week!.Select(d => d with
    {
        Appointments = [.. _day!.Appointments.Where(a => DateOnly.FromDateTime(DayLayout.Local(a.Start, _zone)) == d.Date)],
        Blocks = [.. _day.Blocks.Where(b => DateOnly.FromDateTime(DayLayout.Local(b.From, _zone)) == d.Date)],
        Notes = [.. (_day.Notes ?? []).Where(n => n.Date == d.Date)],
    })];
}
