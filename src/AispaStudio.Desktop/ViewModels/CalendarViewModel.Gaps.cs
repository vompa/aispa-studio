using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Lücken schließen (Ä291): Vorschläge des Servers für den angezeigten Tag, in der 7-Tage-Ansicht für alle sieben Tage ----------
public partial class CalendarViewModel
{
    /// <summary>Lückenvorschläge des Tages in der Seitenleiste; leer, solange die Seite zu ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanel))]
    public partial GapsViewModel? GapsPanel { get; private set; }

    /// <summary>„Lücken schließen“: öffnet die Vorschläge des Servers; verschoben wird erst auf Knopfdruck. Nur mit dem Recht zum Buchen.</summary>
    [RelayCommand]
    private void OpenGaps()
    {
        if (!CanBook || Location is null)
        {
            return;
        }

        ClosePanels();
        GapsPanel = new GapsViewModel(LocationName, () => GapsPanel = null);
        Rebuild();
        _ = LoadGapsAsync();
    }

    private async Task LoadGapsAsync()
    {
        if (GapsPanel is not { } panel || Location is not { } location)
        {
            return;
        }

        var date = Date;
        var filter = Filter;
        // 7-Tage-Ansicht: Vorschläge für alle gezeigten Tage; mit dem Filter auf eine Person nur für sie.
        var week = _week is { Count: > 1 } days ? $"&days={days.Count}" : "";
        var from = _week is { Count: > 1 } shown ? shown[0].Date : date;
        var person = week.Length > 0 && filter is { Kind: CalendarFilterKind.Employee } ? $"&employeeId={filter.Id}" : "";
        var result = await _api.GetAsync<List<GapSuggestion>>($"api/locations/{location.Id}/gap-suggestions?date={from:yyyy-MM-dd}{week}{person}").ConfigureAwait(true);
        if (!ReferenceEquals(GapsPanel, panel) || date != Date || Location?.Id != location.Id || filter != Filter)
        {
            return;
        }

        panel.Scope = week.Length > 0 ? $"{CalendarFilter.Days} Tage ab {DayLayout.ShortDay(from)}{(person.Length > 0 ? $", nur {filter!.Title}" : "")}" : null;
        panel.Show(result.Value?.Select(s => new GapRow(s, _zone, MoveGapAsync, showDay: week.Length > 0)).ToList());
    }

    /// <summary>Verschiebt den Termin auf die Zeit des Vorschlags (zu der Person aus dem Vorschlag); der Server prüft wie bei jedem Verschieben.</summary>
    private async Task MoveGapAsync(GapRow row)
    {
        if (GapsPanel is not { } panel)
        {
            return;
        }

        var suggestion = row.Suggestion;
        var before = _day?.Appointments.FirstOrDefault(a => a.Id == suggestion.AppointmentId);
        if (before is null)
        {
            panel.Message = "Termin nicht mehr im Kalender.";
            return;
        }

        panel.Message = null;
        if (row.IsChain)
        {
            await MoveGapChainAsync(panel, row).ConfigureAwait(true);
            return;
        }

        var result = await _api.PostAsync<AppointmentDto>($"api/appointments/{before.Id}/move",
            new MoveAppointmentRequest(suggestion.SuggestedStart, suggestion.EmployeeId, null, null)).ConfigureAwait(true);
        if (result.Value is not { } moved)
        {
            panel.Message = ConflictNotice.ToastText(result, _zone, Date);
            return;
        }

        Apply(moved);
        ShowToast($"{suggestion.CustomerName} auf {row.NewTime} verschoben", new MoveUndo(before));
        await LoadGapsAsync().ConfigureAwait(true);
    }

    /// <summary>Kette: alle Termine in einem Aufruf; scheitert einer, nimmt der Server die schon verschobenen zurück und die Seitenleiste nennt den Grund.</summary>
    private async Task MoveGapChainAsync(GapsViewModel panel, GapRow row)
    {
        var suggestion = row.Suggestion;
        List<GapChainStep> steps = [new(suggestion.AppointmentId, suggestion.SuggestedStart), .. suggestion.Chain!.Select(c => new GapChainStep(c.AppointmentId, c.SuggestedStart))];
        var result = await _api.PostAsync<List<AppointmentDto>>("api/gap-chains", new GapChainRequest(steps)).ConfigureAwait(true);
        if (result.Value is not { } moved)
        {
            panel.Message = ConflictNotice.ToastText(result, _zone, Date);
            return;
        }

        foreach (var appointment in moved)
        {
            Apply(appointment);
        }

        ShowToast($"{steps.Count} Termine bei {suggestion.EmployeeName} verschoben");
        await LoadGapsAsync().ConfigureAwait(true);
    }
}
