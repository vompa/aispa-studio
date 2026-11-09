using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Rückfrage „Terminliste exportieren?“ (Export/Import, Aufgabe C): Zeitraum Von/Bis, vorbelegt mit der gezeigten Woche, und das Format Excel oder CSV. „Exportieren“ prüft den
/// Zeitraum (Ende nicht vor Beginn, höchstens ein Jahr) und schließt erst dann; der Kalender holt danach die Datei.
/// </summary>
public sealed partial class AppointmentListDialogViewModel : ConfirmDialogViewModel
{
    public const string DialogTitle = "Terminliste exportieren?";
    public const string MissingRange = "Von und Bis angeben.";
    public const string EndBeforeStart = "Bis liegt vor Von.";
    public const string TooLong = "Höchstens ein Jahr auf einmal.";

    /// <summary>Längster Zeitraum, wie ihn auch der Server annimmt.</summary>
    private const int MaxDays = 366;

    public AppointmentListDialogViewModel(string locationName, DateOnly from, DateOnly until, Action<ConfirmResult> answer)
        : base(new ConfirmRequest(DialogTitle, $"Alle Termine von {locationName} im Zeitraum als Datei, z. B. für die Planung oder die Buchhaltung.", "Exportieren", IsDestructive: false), answer)
    {
        From = from;
        Until = until;
    }

    /// <summary>Fehler am Feld „Bis“; Ändern eines Datums nimmt ihn weg.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    public partial DateOnly? From { get; set; }

    [ObservableProperty]
    public partial DateOnly? Until { get; set; }

    /// <summary>Excel (Standard) oder CSV.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCsv))]
    public partial bool IsExcel { get; set; } = true;

    public bool IsCsv
    {
        get => !IsExcel;
        set => IsExcel = !value;
    }

    public string Format => IsExcel ? "xlsx" : "csv";

    /// <summary>Hinweis zum Zeitraum oder null.</summary>
    public static string? RangeError(DateOnly? from, DateOnly? until) => (from, until) switch
    {
        ({ } f, { } u) when u < f => EndBeforeStart,
        ({ } f, { } u) when u.DayNumber - f.DayNumber >= MaxDays => TooLong,
        ({ }, { }) => null,
        _ => MissingRange,
    };

    partial void OnFromChanged(DateOnly? value) => Error = null;

    partial void OnUntilChanged(DateOnly? value) => Error = null;

    [RelayCommand]
    private void Export()
    {
        Error = RangeError(From, Until);
        if (Error is null)
        {
            Answer(new ConfirmResult(ConfirmChoice.Confirmed));
        }
    }
}
