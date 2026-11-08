using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Verspätung: der Gast kommt später; der Termin bleibt an seiner Zeit, die Karte zeigt nur die Ankunft (Runde 6 Ä71) ----------
public partial class CalendarViewModel
{
    /// <summary>Auswahl im Rechtsklick „Kommt später“: drei Einträge, bei längerer Verspätung verschiebt die Rezeption den Termin (Runde 5 Ä32).</summary>
    public static readonly int[] LateChoices = [5, 10, 15];

    private List<CalendarAction> LateActions(AppointmentDto appointment)
    {
        var choices = LateChoices.Select(m => new CalendarAction($"{m} Min. später", null, () => _ = ReportLateAsync(appointment, m))).ToList();
        if (appointment.LateMinutes is not null)
        {
            choices.Add(new("Doch pünktlich", null, () => _ = ReportLateAsync(appointment, 0)));
        }

        return choices;
    }

    /// <summary>Meldet die Verspätung an den Server (0 nimmt sie zurück); der Termin bleibt stehen.</summary>
    public async Task ReportLateAsync(AppointmentDto appointment, int minutes)
    {
        var result = await _api.PutAsync<AppointmentDto>($"api/appointments/{appointment.Id}/late", new ReportLateRequest(minutes)).ConfigureAwait(true);
        if (result.Value is not { } late)
        {
            ShowProblem("Verspätung nicht gespeichert", ConflictNotice.ToastText(result, _zone, Date), action: () => _ = ReportLateAsync(appointment, minutes));
            return;
        }

        Apply(late);
        if (minutes == 0)
        {
            ShowToast($"{late.CustomerName} kommt doch pünktlich.");
            return;
        }

        var arrival = $"{DayLayout.Local(late.Start.AddMinutes(minutes), _zone):HH:mm}";
        ShowToast($"{late.CustomerName} kommt {minutes} Min. später ({arrival})");
    }
}
