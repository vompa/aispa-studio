using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Tagesplan und Warteliste als PDF (PDF-Ausgaben E, ADR 0092) ----------
public partial class CalendarViewModel
{
    /// <summary>
    /// „Tagesplan drucken …“ im Menü „+ Termin“: der Knopfblock „Drucken · PDF speichern“ mit der Wahl Team oder Räume; null, solange er zu ist. Der Pfad nennt den Tag und den Standort,
    /// deshalb schließt ein Wechsel von Tag oder Standort den Block.
    /// </summary>
    [ObservableProperty]
    public partial DayPlanPrintViewModel? DayPlanPrint { get; private set; }

    /// <summary>Öffnet den Block mit der Ansicht, die der Kalender gerade zeigt; ein zweiter Klick schließt ihn.</summary>
    [RelayCommand]
    private void OpenDayPlanPrint() =>
        DayPlanPrint = DayPlanPrint is null && Location is { } location
            ? new DayPlanPrintViewModel(location.Id, Date, ByRoom, _api, Dialogs, text => ShowToast(text), () => DayPlanPrint = null)
            : null;

    private WaitlistViewModel NewWaitlistPanel()
    {
        var date = Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var path = Location is { } location ? $"api/locations/{location.Id}/waitlist/pdf?date={Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" : null;
        var document = path is null ? null : new DocumentActionsViewModel(new DocumentSource($"Warteliste {date}", path), _api, Dialogs) { ShowToast = text => ShowToast(text) };
        return new WaitlistViewModel(LocationName, Waitlist, () => WaitlistPanel = null, document, $"Druckt die offenen Wünsche, die am {date} gelten.");
    }
}
