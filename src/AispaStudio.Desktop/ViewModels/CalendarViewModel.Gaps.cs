using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Lücken schließen (Ä291): Vorschläge des Servers für den angezeigten Tag ----------
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
        var result = await _api.GetAsync<List<GapSuggestion>>($"api/locations/{location.Id}/gap-suggestions?date={date:yyyy-MM-dd}").ConfigureAwait(true);
        if (!ReferenceEquals(GapsPanel, panel) || date != Date || Location?.Id != location.Id)
        {
            return;
        }

        panel.Show(result.Value?.Select(s => new GapRow(s, _zone, MoveGapAsync)).ToList());
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
}
