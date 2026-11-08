using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Raum in der Auswahl der Tagesstart-Kontrolle.</summary>
public sealed record ChecklistRoom(Guid Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// Abschnitt „Tagesstart-Kontrolle“ (Standorte &amp; Räume, R05 Slice 95) wie <c>RoomChecklist.tsx</c>: die Punkte, die das Team vor dem ersten Termin im Raum abhakt, einer je Zeile.
/// Gespeichert wird für den gewählten Raum (<c>PUT api/rooms/{id}/start-checklist</c>); leere Zeilen fallen weg, ohne Punkte gibt es keine Kontrolle.
/// </summary>
public sealed partial class RoomChecklistViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private int _loadId;

    public RoomChecklistViewModel(IWorkstationApi api) => _api = api;

    /// <summary>Die Checkliste eines Raums wurde gespeichert.</summary>
    public event Action? Saved;

    /// <summary>Alle Räume des Standorts; die Auswahl bleibt, solange der Raum noch da ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRooms))]
    public partial IReadOnlyList<ChecklistRoom> Rooms { get; private set; } = [];

    public bool HasRooms => Rooms.Count > 0;

    [ObservableProperty]
    public partial ChecklistRoom? SelectedRoom { get; set; }

    /// <summary>Die Punkte, einer je Zeile.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = "";

    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>Setzt die Räume des Standorts; ohne Raum fällt der Abschnitt weg.</summary>
    public void SetRooms(IEnumerable<RoomDto> rooms)
    {
        var choices = rooms.Select(r => new ChecklistRoom(r.Id, r.Name)).ToList();
        var keep = choices.FirstOrDefault(c => c.Id == SelectedRoom?.Id);
        Rooms = choices;
        SelectedRoom = keep ?? choices.FirstOrDefault();
    }

    partial void OnSelectedRoomChanged(ChecklistRoom? value) => _ = LoadAsync(value);

    /// <summary>Lädt die Punkte des gewählten Raums; eine spätere Auswahl überholt eine frühere Antwort.</summary>
    public async Task LoadAsync(ChecklistRoom? room)
    {
        var load = ++_loadId;
        StatusText = null;
        if (room is null)
        {
            Text = "";
            return;
        }

        var result = await _api.GetAsync<List<RoomStartItemDto>>($"api/rooms/{room.Id}/start-checklist").ConfigureAwait(true);
        if (load != _loadId)
        {
            return;
        }

        Error = result.Value is null ? result.Error ?? "Die Checkliste wurde nicht geladen." : null;
        Text = string.Join('\n', (result.Value ?? []).Select(i => i.Text));
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (SelectedRoom is not { } room)
        {
            return;
        }

        var items = Text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).Select(line => new RoomStartItemInput(line)).ToList();
        var result = await _api.PutAsync<List<RoomStartItemDto>>($"api/rooms/{room.Id}/start-checklist", items).ConfigureAwait(true);
        if (result.Value is not { } saved)
        {
            Error = result.Error ?? "Die Checkliste wurde nicht gespeichert.";
            StatusText = null;
            return;
        }

        Error = null;
        Text = string.Join('\n', saved.Select(i => i.Text));
        StatusText = "Checkliste gespeichert.";
        Saved?.Invoke();
    }
}
