using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- „+ Termin“ › „Terminliste exportieren …“ (Export/Import, Aufgabe C) ----------
public partial class CalendarViewModel
{
    public const string AppointmentListNotSaved = "Terminliste nicht exportiert";

    /// <summary>Fragt Zeitraum (vorbelegt mit der Woche des gezeigten Tages) und Format, holt die Datei vom Server und speichert sie über den Dialog des Fensters.</summary>
    [RelayCommand]
    private async Task ExportAppointmentListAsync()
    {
        if (Location is not { } location || Dialogs is not { } dialogs)
        {
            return;
        }

        var monday = Date.AddDays(-(((int)Date.DayOfWeek + 6) % 7));
        AppointmentListDialogViewModel? dialog = null;
        var answer = await dialogs.ShowAsync(done => dialog = new AppointmentListDialogViewModel(location.Name, monday, monday.AddDays(6), done)).ConfigureAwait(true);
        if (!answer.IsConfirmed || dialog is not { From: { } from, Until: { } until })
        {
            return;
        }

        var format = dialog.IsExcel ? ExportFormat.Xlsx : ExportFormat.Csv;
        var path = string.Create(CultureInfo.InvariantCulture,
            $"api/locations/{location.Id}/appointments/export?from={from:yyyy-MM-dd}&until={until:yyyy-MM-dd}&format={dialog.Format}");
        var option = ExportOption.For(format, () => path);
        var export = new ExportViewModel(_api, [option])
        {
            Files = FileSaver,
            Saved = text => ShowToast(text),
            Failed = text => ShowProblem(AppointmentListNotSaved, text),
        };
        await export.ExportCommand.ExecuteAsync(option).ConfigureAwait(true);
    }
}
