using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Lagerort in der Liste; ohne Kennung ist es der Eintrag „Neuer Lagerort“.</summary>
public sealed record StoragePlaceRow(Guid? Id, string Name, string Sub)
{
    public string Summary => Sub.Length == 0 ? Name : $"{Name}, {Sub}";

    public override string ToString() => Summary;
}

/// <summary>Art des Lagerorts in der Auswahl.</summary>
public sealed record StoragePlaceKindOption(StoragePlaceKind Kind, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Raum in der Auswahl; ohne Kennung steht er für „kein Raum“.</summary>
public sealed record StockRoomOption(Guid? Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Eine Mindestmenge im Formular des Lagerorts: Material und Menge (mit Komma oder Punkt).</summary>
public sealed partial class MinimumRow : ObservableObject
{
    private readonly Action _changed;

    internal MinimumRow(StockItemChoice? item, string amount, string maximum, IReadOnlyList<StockItemChoice> choices, Action changed)
    {
        _changed = changed;
        Choices = choices;
        Item = item;
        Amount = amount;
        Maximum = maximum;
    }

    /// <summary>Das Material, aus dem diese Zeile wählt (die aktiven Materialien des Lagers).</summary>
    public IReadOnlyList<StockItemChoice> Choices { get; }

    [ObservableProperty]
    public partial StockItemChoice? Item { get; set; }

    [ObservableProperty]
    public partial string Amount { get; set; }

    /// <summary>Höchstmenge an diesem Ort, freiwillig; die Summe der Lagerorte ist das Bestellziel des Standorts.</summary>
    [ObservableProperty]
    public partial string Maximum { get; set; }

    /// <summary>Entfernt die Mindestmenge aus dem Formular; gesetzt vom Formular des Lagerorts.</summary>
    public ICommand? RemoveCommand { get; internal set; }

    public string RemoveName => Item is null ? "Mindestmenge entfernen" : $"Mindestmenge {Item.Name} entfernen";

    public string AmountName => Item is null ? "Mindestmenge" : $"Mindestmenge {Item.Name} ({Item.Unit})";

    public string MaximumName => Item is null ? "Höchstmenge" : $"Höchstmenge {Item.Name} ({Item.Unit})";

    partial void OnItemChanged(StockItemChoice? value)
    {
        OnPropertyChanged(nameof(AmountName));
        OnPropertyChanged(nameof(MaximumName));
        OnPropertyChanged(nameof(RemoveName));
        _changed();
    }

    partial void OnAmountChanged(string value) => _changed();

    partial void OnMaximumChanged(string value) => _changed();
}

/// <summary>
/// Lagerorte eines Standorts (R05 Slice 101, ADR 0068) nach <c>StoragePlacesAdmin.tsx</c>: links die Liste, rechts das Formular mit Name, Art, Raum, Beschreibung, Aktiv und den Mindestmengen
/// je Material. Das Hauptlager gibt es immer; es lässt sich umbenennen, aber weder löschen noch deaktivieren. Wer einen anderen Lagerort wählt, wird bei Änderungen gefragt. Nur mit ManageLocation.
/// </summary>
public sealed partial class StoragePlacesViewModel : ViewModelBase, IFocusTarget
{
    public const string NameField = "PlaceNameField";

    private readonly StockViewModel _owner;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private List<RoomDto> _rooms = [];
    private StoragePlaceDto? _current;
    private string? _savedSignature;
    private Guid? _selectAfterLoad;
    private bool _applying;
    private bool _switching;
    private bool _reverting;

    internal StoragePlacesViewModel(StockViewModel owner) => _owner = owner;

    public ObservableCollection<StoragePlaceRow> Rows { get; } = [];

    public ObservableCollection<StoragePlaceKindOption> KindOptions { get; } = [];

    public ObservableCollection<StockRoomOption> RoomOptions { get; } = [];

    public ObservableCollection<StockItemChoice> ItemChoices { get; } = [];

    public ObservableCollection<MinimumRow> Minimums { get; } = [];

    public ObservableCollection<StockPlaceOption> CopySources { get; } = [];

    [ObservableProperty]
    public partial StoragePlaceRow? SelectedRow { get; set; }

    [ObservableProperty]
    public partial string PlaceName { get; set; } = "";

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRoom))]
    public partial StoragePlaceKindOption? SelectedKind { get; set; }

    [ObservableProperty]
    public partial StockRoomOption? SelectedRoom { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyMinimumsCommand))]
    public partial StockPlaceOption? CopySource { get; set; }

    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    public bool IsCreating => _current is null;

    public bool IsMain => _current?.Kind == StoragePlaceKind.Main;

    public bool CanEditKind => !IsMain;

    public bool ShowRoom => SelectedKind?.Kind == StoragePlaceKind.Room;

    public string HeadingText => _current?.Name ?? "Neuer Lagerort";

    public bool CanDelete => _current is not null && !IsMain;

    public bool HasCopySources => CopySources.Count > 0;

    public bool HasMinimums => Minimums.Count > 0;

    public bool HasUnsavedChanges => _savedSignature is not null && Signature() != _savedSignature;

    /// <summary>Ohne Änderung gibt es nichts zu speichern (außer beim neuen Lagerort); ohne Name auch nicht.</summary>
    public bool CanSave => !IsSaving && PlaceName.Trim().Length > 0 && (IsCreating || HasUnsavedChanges);

    /// <summary>„Von Raum … übernehmen“ geht erst, wenn nichts mehr ungespeichert ist (das Ziel würde sonst überschrieben).</summary>
    public bool CanCopy => CopySource is not null && !HasUnsavedChanges && !IsCreating;

    /// <summary>Baut Liste und Auswahlen aus dem Stand des Besitzers neu auf (nach dem Laden und nach jeder Buchung); die Auswahl bleibt, sonst gilt das Hauptlager.</summary>
    internal void Rebuild()
    {
        var select = _selectAfterLoad ?? _current?.Id;
        _selectAfterLoad = null;
        _switching = true;
        Rows.Clear();
        foreach (var place in _owner.Places)
        {
            Rows.Add(new StoragePlaceRow(place.Id, place.Name, place.IsActive ? StockFormat.PlaceKind(place.Kind) : $"{StockFormat.PlaceKind(place.Kind)}, deaktiviert"));
        }

        Rows.Add(new StoragePlaceRow(null, "Neuer Lagerort", ""));
        ItemChoices.Clear();
        foreach (var item in _owner.Items.Where(i => i.IsActive).OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            ItemChoices.Add(new StockItemChoice(item.Id, item.Name, item.Unit));
        }

        _switching = false;
        Select(select is { } id && _owner.Places.Any(p => p.Id == id) ? id : _owner.MainPlace?.Id);
        OnPropertyChanged(nameof(CanEditKind));
    }

    /// <summary>Die Räume des Standorts für die Art „Raum“ (Gemeinschaftsbereiche sind keine Räume zum Lagern).</summary>
    internal async Task LoadRoomsAsync()
    {
        _rooms = [];
        if (_owner.Location is { } location)
        {
            var result = await _owner.Api.GetAsync<LocationResources>($"api/locations/{location.Id}/resources").ConfigureAwait(true);
            _rooms = [.. result.Value?.Rooms.Where(r => !r.IsArea) ?? []];
        }

        RoomOptions.Clear();
        RoomOptions.Add(new StockRoomOption(null, "kein Raum"));
        foreach (var room in _rooms)
        {
            RoomOptions.Add(new StockRoomOption(room.Id, room.Name));
        }

        SelectedRoom = RoomOptions.FirstOrDefault(r => r.Id == _current?.RoomId) ?? RoomOptions[0];
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || _owner.Location is not { } location)
        {
            return false;
        }

        NameError = PlaceName.Trim().Length == 0 ? "Der Name fehlt. Trage ihn ein." : null;
        if (NameError is not null)
        {
            FocusTarget = NameField;
            return false;
        }

        var minimums = new List<StoragePlaceMinimumDto>();
        foreach (var row in Minimums.Where(m => m.Item is not null && (m.Amount.Trim().Length > 0 || m.Maximum.Trim().Length > 0)))
        {
            var amount = StockFormat.ParseOrZero(row.Amount);
            if (amount is null or < 0 || minimums.Any(m => m.ItemId == row.Item!.Id))
            {
                Error = $"Die Mindestmenge für {row.Item!.Name} ist keine Zahl ab 0 oder steht doppelt. Prüfe die Mindestmengen.";
                return false;
            }

            var maximum = StockFormat.Parse(row.Maximum);
            if (row.Maximum.Trim().Length > 0 && (maximum is null || maximum < amount))
            {
                Error = $"Die Höchstmenge für {row.Item!.Name} ist keine Zahl oder liegt unter der Mindestmenge. Gib sie z. B. als 1000 ein.";
                return false;
            }

            minimums.Add(new StoragePlaceMinimumDto(row.Item!.Id, amount.Value, maximum));
        }

        Error = null;
        IsSaving = true;
        var kind = IsMain ? StoragePlaceKind.Main : SelectedKind?.Kind ?? StoragePlaceKind.Other;
        var body = new StoragePlaceInput(PlaceName.Trim(), Description.Trim().Length == 0 ? null : Description.Trim(), kind,
            kind == StoragePlaceKind.Room ? SelectedRoom?.Id : null, IsMain || IsActive, minimums);
        var path = $"api/stock/locations/{location.Id}/storage-places";
        var result = _current is { } place
            ? await _owner.Api.PutAsync<StoragePlaceDto>($"{path}/{place.Id}", body).ConfigureAwait(true)
            : await _owner.Api.PostAsync<StoragePlaceDto>(path, body).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } saved)
        {
            Error = result.Error ?? "Der Lagerort wurde nicht gespeichert.";
            return false;
        }

        // Gespeichert ist gespeichert: auch wenn das Neuladen scheitert, gilt ab hier der Lagerort mit seiner Kennung (ein zweites Speichern ändert ihn, statt einen neuen anzulegen).
        _current = saved;
        _savedSignature = Signature();
        NotifyStateChanged();
        _selectAfterLoad = saved.Id;
        await _owner.ReloadAsync("Gespeichert.").ConfigureAwait(true);
        return true;
    }

    [RelayCommand]
    public void Discard()
    {
        Apply(_current);
        Error = null;
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        if (!await UnsavedChanges.ConfirmLeaveAsync(_owner.Dialogs, _owner).ConfigureAwait(true))
        {
            return;
        }

        Select(null);
    }

    [RelayCommand]
    private void AddMinimum()
    {
        AddMinimumRow(null, "", "");
        Touch();
    }

    private void AddMinimumRow(StockItemChoice? item, string amount, string maximum)
    {
        var row = new MinimumRow(item, amount, maximum, ItemChoices, Touch);
        row.RemoveCommand = new RelayCommand(() =>
        {
            Minimums.Remove(row);
            Touch();
        });
        Minimums.Add(row);
    }

    /// <summary>„Von Raum … übernehmen“: ersetzt die Mindestmengen (Raumausstattung) durch die eines anderen Lagerorts; gespeichert wird gleich.</summary>
    [RelayCommand(CanExecute = nameof(CanCopy))]
    private async Task CopyMinimumsAsync()
    {
        if (_owner.Location is not { } location || _current is not { } place || CopySource is not { } source)
        {
            return;
        }

        var result = await _owner.Api.PostAsync<StoragePlaceDto>($"api/stock/locations/{location.Id}/storage-places/{place.Id}/minimums/copy-from/{source.Id}", null).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error ?? "Die Mindestmengen wurden nicht übernommen.";
            return;
        }

        Error = null;
        _selectAfterLoad = place.Id;
        CopySource = null;
        await _owner.ReloadAsync("Mindestmengen übernommen.").ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (_owner.Location is not { } location || _current is not { } place || IsMain)
        {
            return;
        }

        var answer = await _owner.Ask(new ConfirmRequest("Lagerort löschen?",
            $"Der Lagerort „{place.Name}“ wird gelöscht. Das geht nur, solange er keine Bewegungen hat; sonst deaktiviere ihn.", "Löschen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _owner.Api.DeleteAsync<object>($"api/stock/locations/{location.Id}/storage-places/{place.Id}").ConfigureAwait(true);
        if (!result.Ok)
        {
            Error = result.Error ?? "Der Lagerort wurde nicht gelöscht.";
            return;
        }

        Error = null;
        _selectAfterLoad = _owner.MainPlace?.Id;
        _current = null;
        await _owner.ReloadAsync("Lagerort gelöscht.").ConfigureAwait(true);
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    partial void OnSelectedRowChanged(StoragePlaceRow? oldValue, StoragePlaceRow? newValue)
    {
        if (_reverting || _switching || newValue is null)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Liste bleibt beim alten Lagerort, bis die Antwort da ist.
            Revert(() => SelectedRow = oldValue);
            _ = LeaveToRowAsync(newValue.Id);
            return;
        }

        Select(newValue.Id);
    }

    private async Task LeaveToRowAsync(Guid? id)
    {
        if (await UnsavedChanges.ConfirmLeaveAsync(_owner.Dialogs, _owner).ConfigureAwait(true))
        {
            Select(id);
        }
    }

    private void Revert(Action restore)
    {
        void Restore()
        {
            _reverting = true;
            try
            {
                restore();
            }
            finally
            {
                _reverting = false;
            }
        }

        if (_ui is null)
        {
            Restore();
        }
        else
        {
            _ui.Post(_ => Restore(), null);
        }
    }

    /// <summary>Wählt einen Lagerort (oder mit <c>null</c> den neuen) in Liste und Formular.</summary>
    private void Select(Guid? id)
    {
        _switching = true;
        SelectedRow = Rows.FirstOrDefault(r => r.Id == id) ?? Rows.LastOrDefault();
        _switching = false;
        Apply(id is { } known ? _owner.Places.FirstOrDefault(p => p.Id == known) : null);
    }

    private void Apply(StoragePlaceDto? place)
    {
        _applying = true;
        _current = place;
        PlaceName = place?.Name ?? "";
        Description = place?.Description ?? "";
        KindOptions.Clear();
        foreach (var kind in place?.Kind == StoragePlaceKind.Main ? new[] { StoragePlaceKind.Main } : new[] { StoragePlaceKind.Room, StoragePlaceKind.Sales, StoragePlaceKind.Other })
        {
            KindOptions.Add(new StoragePlaceKindOption(kind, StockFormat.PlaceKind(kind)));
        }

        SelectedKind = KindOptions.FirstOrDefault(k => k.Kind == place?.Kind) ?? KindOptions.Last();
        SelectedRoom = RoomOptions.FirstOrDefault(r => r.Id == place?.RoomId) ?? RoomOptions.FirstOrDefault();
        IsActive = place?.IsActive ?? true;
        Minimums.Clear();
        foreach (var minimum in place?.Minimums ?? [])
        {
            AddMinimumRow(ItemChoices.FirstOrDefault(i => i.Id == minimum.ItemId), StockFormat.Plain(minimum.MinimumQuantity), minimum.MaximumQuantity is { } maximum ? StockFormat.Plain(maximum) : "");
        }

        CopySources.Clear();
        foreach (var source in _owner.Places.Where(p => p.Id != place?.Id && p.Minimums.Count > 0))
        {
            CopySources.Add(new StockPlaceOption(source.Id, source.Name));
        }

        CopySource = null;
        NameError = null;
        _applying = false;
        _savedSignature = Signature();
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        foreach (var name in new[] { nameof(IsCreating), nameof(IsMain), nameof(CanEditKind), nameof(HeadingText), nameof(CanDelete), nameof(HasCopySources), nameof(HasMinimums), nameof(HasUnsavedChanges), nameof(CanSave), nameof(CanCopy) })
        {
            OnPropertyChanged(name);
        }

        DeleteCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        CopyMinimumsCommand.NotifyCanExecuteChanged();
        _owner.NotifyChanged();
    }

    private void Touch()
    {
        if (_applying)
        {
            return;
        }

        foreach (var name in new[] { nameof(HasUnsavedChanges), nameof(CanSave), nameof(CanCopy), nameof(HasMinimums) })
        {
            OnPropertyChanged(name);
        }

        SaveCommand.NotifyCanExecuteChanged();
        CopyMinimumsCommand.NotifyCanExecuteChanged();
        _owner.NotifyChanged();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(PlaceName) or nameof(Description) or nameof(SelectedKind) or nameof(SelectedRoom) or nameof(IsActive))
        {
            if (e.PropertyName == nameof(PlaceName))
            {
                NameError = null;
            }

            Touch();
        }
    }

    private string Signature() => string.Join('\u001f', PlaceName, Description, SelectedKind?.Kind, SelectedRoom?.Id, IsActive,
        string.Join(',', Minimums.Select(m => $"{m.Item?.Id}={m.Amount}/{m.Maximum}")));
}
