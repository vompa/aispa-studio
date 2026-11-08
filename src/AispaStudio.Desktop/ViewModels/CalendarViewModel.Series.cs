using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Serien: Ergebnis von Anlegen und Stornieren übernehmen ----------
public partial class CalendarViewModel
{
    private SeriesInfo? SeriesFor(AppointmentDto appointment) =>
        appointment.SeriesId is { } id ? _day?.Series?.FirstOrDefault(s => s.Id == id) : null;

    /// <summary>Neuer Stand einer Serie (Anlegen, ab hier stornieren).</summary>
    private void ApplySeries(SeriesResult result)
    {
        if (_day is null)
        {
            return;
        }

        _day = _day with { Series = [.. (_day.Series ?? []).Where(s => s.Id != result.Series.Id), result.Series] };
        ApplyAll(result.Changed);
        Rebuild();
    }

    private async Task SeriesChangedAsync(SeriesResult result)
    {
        var created = result.Changed.Count > 0 && result.Changed.All(a => a.Status != AppointmentStatus.Cancelled);
        ApplySeries(result);
        var others = result.Changed.Count - 1;
        ShowToast(created
            ? others == 1 ? "Serie angelegt, 1 weiterer Termin gebucht" : $"Serie angelegt, {others} weitere Termine gebucht"
            : result.Changed.Count == 1 ? "1 Termin storniert, Serie beendet" : $"{result.Changed.Count} Termine storniert, Serie beendet");
        WarnAboutFailed(result);
        await Task.CompletedTask.ConfigureAwait(true);
    }

    /// <summary>Warnung „Nicht frei: Mo. 29.3. (Anna: Urlaub)“, wenn einzelne Termine der Serie nicht gebucht oder storniert werden konnten.</summary>
    private void WarnAboutFailed(SeriesResult result)
    {
        if (result.Failed.Count == 0)
        {
            return;
        }

        var days = string.Join(", ", result.Failed.Select(f => $"{DayLayout.ShortDay(DateOnly.FromDateTime(DayLayout.Local(f.Start, _zone)))} ({f.Reason})"));
        ShowProblem("Nicht alle Termine frei", $"Nicht frei: {days}", isError: false);
    }
}
