using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// „Tagesplan drucken …“ (PDF-Ausgaben E, ADR 0092): wählt, ob der Plan je Person (Team) oder je Raum gedruckt wird, und trägt den Knopfblock „Drucken · PDF speichern“ für diese Wahl.
/// Den Plan rechnet die API (<c>GET /api/locations/{id}/day-plan/pdf</c>); die App kennt nur den Pfad. Ohne Senden: der Plan hängt aus oder liegt am Empfang.
/// </summary>
public sealed partial class DayPlanPrintViewModel : ObservableObject
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private readonly Guid _locationId;
    private readonly DateOnly _date;
    private readonly IAispaApi _api;
    private readonly IConfirmDialogs? _dialogs;
    private readonly Action<string> _showToast;
    private readonly Action _close;

    public DayPlanPrintViewModel(Guid locationId, DateOnly date, bool byRooms, IAispaApi api, IConfirmDialogs? dialogs, Action<string> showToast, Action close)
    {
        _locationId = locationId;
        _date = date;
        _api = api;
        _dialogs = dialogs;
        _showToast = showToast;
        _close = close;
        ByRooms = byRooms;
        Document = DocumentFor(byRooms);
    }

    public string Title => $"Tagesplan {_date.ToString("dddd, dd.MM.yyyy", German)}";

    /// <summary>Eine Spalte je Raum statt je Person.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTeam))]
    public partial bool ByRooms { get; set; }

    public bool IsTeam => !ByRooms;

    /// <summary>Der Knopfblock für die gewählte Ansicht; ein Wechsel von Team auf Räume ersetzt ihn samt Pfad.</summary>
    [ObservableProperty]
    public partial DocumentActionsViewModel Document { get; private set; }

    partial void OnByRoomsChanged(bool value) => Document = DocumentFor(value);

    [RelayCommand]
    private void Close() => _close();

    private DocumentActionsViewModel DocumentFor(bool byRooms)
    {
        var path = $"api/locations/{_locationId}/day-plan/pdf?date={_date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&by={(byRooms ? "rooms" : "team")}";
        return new DocumentActionsViewModel(new DocumentSource($"Tagesplan {_date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}", path), _api, _dialogs) { ShowToast = _showToast };
    }
}
