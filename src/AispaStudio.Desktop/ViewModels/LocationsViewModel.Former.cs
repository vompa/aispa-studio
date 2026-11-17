using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// „Nicht mehr verwendete zeigen“ unter den Listen der Räume und Geräte (ADR 0099): Die Zeilen kommen von <c>…/rooms/former</c> und <c>…/equipment/former</c>,
/// stehen gedämpft unter den aktiven und haben im Menü „⋯“ nur „Wieder verwenden“. Nach dem Zurückholen steht der Eintrag wieder in der normalen Liste.
/// </summary>
public sealed partial class LocationsViewModel
{
    /// <summary>Die nicht mehr verwendeten Räume des gewählten Standorts, solange <see cref="ShowFormerRooms"/> gesetzt ist.</summary>
    public ObservableCollection<FormerRowViewModel> FormerRooms { get; } = [];

    /// <summary>Die nicht mehr verwendeten Geräte des gewählten Standorts, solange <see cref="ShowFormerEquipment"/> gesetzt ist.</summary>
    public ObservableCollection<FormerRowViewModel> FormerEquipment { get; } = [];

    /// <summary>Das Laden der letzten Umschaltung, damit Tests darauf warten können.</summary>
    public Task FormerLoading { get; private set; } = Task.CompletedTask;

    /// <summary>Die Listen der nicht mehr verwendeten gibt es wie ihre Schnittstellen nur mit dem Recht, Standorte zu verwalten.</summary>
    public bool CanShowFormer => _user.Rights.Contains(AccessRight.ManageLocation);

    [ObservableProperty]
    public partial bool ShowFormerRooms { get; set; }

    [ObservableProperty]
    public partial bool ShowFormerEquipment { get; set; }

    /// <summary>Bestätigung nach „Wieder verwenden“ am Raum; die Leiste lässt sich schließen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoomNotice))]
    public partial string? RoomNotice { get; private set; }

    public bool HasRoomNotice => RoomNotice is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEquipmentNotice))]
    public partial string? EquipmentNotice { get; private set; }

    public bool HasEquipmentNotice => EquipmentNotice is not null;

    [RelayCommand]
    private void CloseRoomNotice() => RoomNotice = null;

    [RelayCommand]
    private void CloseEquipmentNotice() => EquipmentNotice = null;

    partial void OnShowFormerRoomsChanged(bool value) => FormerLoading = ApplyShowFormerRoomsAsync(value);

    partial void OnShowFormerEquipmentChanged(bool value) => FormerLoading = ApplyShowFormerEquipmentAsync(value);

    private async Task ApplyShowFormerRoomsAsync(bool value)
    {
        FormerRooms.Clear();
        if (!value || SelectedChoice?.Id is not { } locationId)
        {
            return;
        }

        RoomMessage = null;
        var former = await _api.GetAsync<List<RoomDto>>($"api/locations/{locationId}/rooms/former").ConfigureAwait(true);
        if (former.Value is null)
        {
            RoomMessage = former.Error ?? "Die nicht mehr verwendeten Räume wurden nicht geladen.";
            ShowFormerRooms = false;
            return;
        }

        foreach (var room in former.Value.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            FormerRooms.Add(new FormerRowViewModel(room.Id, room.Name, "Raum", _ => ReactivateRoomAsync(room)));
        }
    }

    private async Task ApplyShowFormerEquipmentAsync(bool value)
    {
        FormerEquipment.Clear();
        if (!value || SelectedChoice?.Id is not { } locationId)
        {
            return;
        }

        EquipmentMessage = null;
        var former = await _api.GetAsync<List<EquipmentDto>>($"api/locations/{locationId}/equipment/former").ConfigureAwait(true);
        if (former.Value is null)
        {
            EquipmentMessage = former.Error ?? "Die nicht mehr verwendeten Geräte wurden nicht geladen.";
            ShowFormerEquipment = false;
            return;
        }

        foreach (var item in former.Value.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            FormerEquipment.Add(new FormerRowViewModel(item.Id, item.Name, "Gerät", _ => ReactivateEquipmentAsync(item)));
        }
    }

    private async Task ReactivateRoomAsync(RoomDto room)
    {
        if (SelectedChoice?.Id is not { } locationId)
        {
            return;
        }

        RoomMessage = null;
        RoomNotice = null;
        var result = await _api.PostAsync<RoomDto>($"api/rooms/{room.Id}/reactivate", null).ConfigureAwait(true);
        if (result.Value is not { } back)
        {
            RoomMessage = result.Error ?? "Der Raum wird weiter nicht verwendet; das Zurückholen hat nicht geklappt.";
            return;
        }

        _resources[locationId] = _resources[locationId] with
        {
            Rooms = [.. _resources[locationId].Rooms.Where(r => r.Id != back.Id).Append(back).OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)],
        };
        RemoveFormer(FormerRooms, room.Id);
        FillSections(locationId);
        RoomNotice = $"Raum {back.Name} wird wieder verwendet";
    }

    private async Task ReactivateEquipmentAsync(EquipmentDto item)
    {
        if (SelectedChoice?.Id is not { } locationId)
        {
            return;
        }

        EquipmentMessage = null;
        EquipmentNotice = null;
        var result = await _api.PostAsync<EquipmentDto>($"api/equipment/{item.Id}/reactivate", null).ConfigureAwait(true);
        if (result.Value is not { } back)
        {
            EquipmentMessage = result.Error ?? "Das Gerät wird weiter nicht verwendet; das Zurückholen hat nicht geklappt.";
            return;
        }

        _resources[locationId] = _resources[locationId] with
        {
            Equipment = [.. _resources[locationId].Equipment.Where(e => e.Id != back.Id).Append(back).OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)],
        };
        RemoveFormer(FormerEquipment, item.Id);
        FillSections(locationId);
        EquipmentNotice = $"Gerät {back.Name} wird wieder verwendet";
    }

    /// <summary>Ist der Haken gesetzt, wandert ein soeben entfernter Raum sofort in die Liste der nicht mehr verwendeten.</summary>
    private void AddFormerRoom(RoomDto room)
    {
        if (ShowFormerRooms)
        {
            InsertFormer(FormerRooms, new FormerRowViewModel(room.Id, room.Name, "Raum", _ => ReactivateRoomAsync(room)));
        }
    }

    private void AddFormerEquipment(EquipmentDto item)
    {
        if (ShowFormerEquipment)
        {
            InsertFormer(FormerEquipment, new FormerRowViewModel(item.Id, item.Name, "Gerät", _ => ReactivateEquipmentAsync(item)));
        }
    }

    /// <summary>Beim Wechsel des Standorts gelten Haken, Listen und Bestätigungen nicht mehr.</summary>
    private void ResetFormer()
    {
        ShowFormerRooms = false;
        ShowFormerEquipment = false;
        FormerRooms.Clear();
        FormerEquipment.Clear();
        RoomNotice = null;
        EquipmentNotice = null;
    }

    private static void RemoveFormer(ObservableCollection<FormerRowViewModel> rows, Guid id)
    {
        if (rows.FirstOrDefault(r => r.Id == id) is { } row)
        {
            rows.Remove(row);
        }
    }

    private static void InsertFormer(ObservableCollection<FormerRowViewModel> rows, FormerRowViewModel row)
    {
        var index = 0;
        while (index < rows.Count && StringComparer.CurrentCultureIgnoreCase.Compare(rows[index].Name, row.Name) <= 0)
        {
            index++;
        }

        rows.Insert(index, row);
    }
}

/// <summary>Ein nicht mehr verwendeter Raum oder ein nicht mehr verwendetes Gerät in der Liste: gedämpft, mit dem Kennzeichen und im Menü „⋯“ nur „Wieder verwenden“.</summary>
public sealed partial class FormerRowViewModel(Guid id, string name, string kind, Func<FormerRowViewModel, Task> reactivate) : ObservableObject
{
    public const string ReactivateLabel = "Wieder verwenden";

    public const string Tag = "Nicht mehr verwendet";

    public Guid Id { get; } = id;

    public string Name { get; } = name;

    /// <summary>Für den Screenreader: „Raum Sauna, nicht mehr verwendet“.</summary>
    public string RowName => $"{kind} {Name}, nicht mehr verwendet";

    [RelayCommand]
    private Task ReactivateAsync() => reactivate(this);
}
