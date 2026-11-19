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
        GapsPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GapsViewModel.SelectedRow))
            {
                ShowGapSelection();
            }
        };
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
        panel.Show(result.Value?.Select(s => new GapRow(s, _zone, MoveGapAsync, showDay: week.Length > 0, select: panel.Select)).ToList());
    }

    /// <summary>Die Termine des gewählten Vorschlags (der Termin selbst und die der Kette), die der Kalender hervorhebt.</summary>
    private HashSet<Guid> GapMarkedIds() =>
        GapsPanel?.SelectedRow is { } row ? [row.Suggestion.AppointmentId, .. (row.Suggestion.Chain ?? []).Select(c => c.AppointmentId)] : [];

    /// <summary>Hebt die Termine des gewählten Vorschlags hervor; ohne Wahl oder ohne Seite verschwindet die Hervorhebung.</summary>
    private void ApplyGapMarks()
    {
        var marked = GapMarkedIds();
        foreach (var item in Columns.SelectMany(c => c.Items))
        {
            item.IsGapHighlighted = marked.Contains(item.Id);
        }
    }

    /// <summary>Wahl geändert: Hervorhebung und Vorschau des Ziels nachführen.</summary>
    private void ShowGapSelection()
    {
        ApplyGapMarks();
        UpdateEntryPreview();
    }

    partial void OnGapsPanelChanged(GapsViewModel? value) => ShowGapSelection();

    /// <summary>
    /// Das Ziel des gewählten Vorschlags als gestrichelte Vorschau in der Spalte der Person (Raumansicht: des Raums), wie beim Ziehen; die Termine der Kette stehen als weitere Vorschauen daneben.
    /// </summary>
    private CalendarGhost? GapPreview(GapRow row)
    {
        var ghosts = new List<CalendarGhost>();
        foreach (var (id, start, text) in new[] { (row.Suggestion.AppointmentId, row.Suggestion.SuggestedStart, row.Suggestion.CustomerName) }
                     .Concat((row.Suggestion.Chain ?? []).Select(c => (c.AppointmentId, c.SuggestedStart, c.CustomerName))))
        {
            if (_day?.Appointments.FirstOrDefault(a => a.Id == id) is not { } appointment)
            {
                continue;
            }

            var columnId = ByRoom ? appointment.RoomId : appointment.EmployeeId;
            var date = DateOnly.FromDateTime(DayLayout.Local(start, _zone));
            if (PreviewColumn(columnId, date) is not { } column)
            {
                continue;
            }

            var end = start + (appointment.End - appointment.Start);
            ghosts.Add(PreviewAt(column, start, end, $"{DayLayout.Local(start, _zone):HH:mm} · {text}", 0, 0, warning: false) with { IsMove = true });
        }

        GroupGhosts = ghosts.Skip(1).ToList();
        return ghosts.FirstOrDefault();
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
