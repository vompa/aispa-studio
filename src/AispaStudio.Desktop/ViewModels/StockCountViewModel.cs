using System.Collections.ObjectModel;
using System.Windows.Input;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zeile der Zählliste: Soll, das gezählte Feld und daraus Differenz und Wert (live, wie im Web).</summary>
public sealed partial class CountRow : ObservableObject
{
    private readonly Action _changed;

    internal CountRow(StockItemDto item, decimal target, bool showTarget, Action changed, bool isReadOnly = false)
    {
        IsReadOnly = isReadOnly;
        ShowTarget = showTarget;
        Item = item;
        Target = target;
        _changed = changed;
    }

    public StockItemDto Item { get; }

    public string Name => Item.Name;

    public string Unit => Item.Unit;

    /// <summary>Eine übernommene Zählung vom Handy: das Gezählte steht fest, die Leitung schließt sie nur ab.</summary>
    public bool IsReadOnly { get; }

    public decimal Target { get; private set; }

    public string TargetText => StockFormat.Quantity(Target);

    /// <summary>Soll, Differenz und Wert sind zu sehen (nicht ausgeblendet).</summary>
    [ObservableProperty]
    public partial bool ShowTarget { get; set; }

    /// <summary>Der gezählte Bestand mit Komma oder Punkt; leer heißt „nicht gezählt“.</summary>
    [ObservableProperty]
    public partial string Entered { get; set; } = "";

    public bool IsEntered => Entered.Trim().Length > 0;

    /// <summary>Gezählt und keine Zahl ab 0.</summary>
    public bool IsInvalid => IsEntered && Value is null or < 0;

    public decimal? Value => StockFormat.Parse(Entered);

    /// <summary>Gezählt minus Soll; ohne gültige Eingabe <c>null</c>.</summary>
    public decimal? Difference => IsEntered && !IsInvalid ? Value - Target : null;

    public decimal Worth => Difference is { } difference ? difference * Item.UnitCost : 0;

    public string DifferenceText => Difference is { } difference ? StockFormat.Quantity(difference) : "";

    public bool DifferenceIsNegative => Difference < 0;

    public string WorthText => Difference is { } difference && difference != 0 ? Money.Format(Worth) : "";

    /// <summary>Gebucht wird, was gezählt wurde und vom Soll abweicht.</summary>
    public bool IsChange => Difference is { } difference && difference != 0;

    /// <summary>Die Zeile ist gebucht: das Gezählte ist jetzt das Soll, das Zählfeld wieder leer.</summary>
    internal void MarkBooked(decimal counted)
    {
        Target = counted;
        Entered = "";
        OnPropertyChanged(nameof(Target));
        OnPropertyChanged(nameof(TargetText));
    }

    partial void OnEnteredChanged(string value)
    {
        foreach (var name in new[] { nameof(IsEntered), nameof(IsInvalid), nameof(Value), nameof(Difference), nameof(Worth), nameof(DifferenceText), nameof(DifferenceIsNegative),
            nameof(WorthText), nameof(IsChange) })
        {
            OnPropertyChanged(name);
        }

        _changed();
    }
}

/// <summary>Eine abgeschickte Zählung vom Handy in der Liste über der Auswahl: wo, wer und wann, mit „Übernehmen“ und „Verwerfen“.</summary>
public sealed record HandySessionRow(string PlaceName, string Detail, string AdoptName, string DiscardName)
{
    public ICommand? AdoptCommand { get; init; }

    public ICommand? DiscardCommand { get; init; }

    public override string ToString() => $"{PlaceName}, {Detail}";
}

/// <summary>
/// Inventur eines Lagerorts (R05 Slice 101) nach <c>StockCount.tsx</c>: Soll (ausblendbar), Zählfeld je Zeile, Differenz und Wert live. Gebucht wird nur, was gezählt wurde und vom Soll
/// abweicht, jeweils als Bewegung der Art „Inventur“ mit der gezählten Menge am gewählten Lagerort, und nie ohne Rückfrage. Die Inventur bucht die Leitung (ManageLocation).
/// </summary>
public sealed partial class StockCountViewModel : ViewModelBase
{
    private readonly StockViewModel _owner;
    private StockCountSessionDto? _adopted;
    private int _handyVersion;

    internal StockCountViewModel(StockViewModel owner) => _owner = owner;

    public ObservableCollection<StockPlaceOption> PlaceOptions { get; } = [];

    public ObservableCollection<CountRow> Rows { get; } = [];

    /// <summary>Zählungen vom Handy, die auf die Leitung warten (abgeschickt, noch nicht gebucht).</summary>
    public ObservableCollection<HandySessionRow> HandySessions { get; } = [];

    public bool HasHandySessions => HandySessions.Count > 0;

    public string HandyHeading => HandySessions.Count == 1 ? "Eine Zählung vom Handy wartet" : $"{HandySessions.Count} Zählungen vom Handy warten";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHandyError))]
    public partial string? HandyError { get; private set; }

    public bool HasHandyError => HandyError is not null;

    /// <summary>Die Zählliste stammt aus einer Zählung vom Handy; „Inventur buchen“ schließt sie über die API.</summary>
    public bool IsAdopted => _adopted is not null;

    /// <summary>Der Lagerort der Zählung; vorbelegt ist das Hauptlager.</summary>
    [ObservableProperty]
    public partial StockPlaceOption? SelectedPlace { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsCounting { get; private set; }

    public bool IsIdle => !IsCounting;

    /// <summary>Die Ansicht setzt den Fokus in das Zählfeld dieser Zeile (Handscanner, Slice 114).</summary>
    public event Action<CountRow>? RowFocusRequested;

    /// <summary>Springt in der laufenden Inventur auf die Zeile des Materials; false, wenn keine läuft oder die Zeile fehlt.</summary>
    internal bool FocusRow(Guid itemId)
    {
        if (!IsCounting || Rows.FirstOrDefault(r => r.Item.Id == itemId) is not { } row)
        {
            return false;
        }

        RowFocusRequested?.Invoke(row);
        return true;
    }

    /// <summary>Die Zählfelder sind bedienbar; während der Buchung nicht, damit der Stand vom Klick gebucht wird.</summary>
    public bool CanEditRows => !IsBooking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTarget))]
    public partial bool HideTarget { get; set; }

    public bool ShowTarget => !HideTarget;

    partial void OnHideTargetChanged(bool value)
    {
        foreach (var row in Rows)
        {
            row.ShowTarget = !value;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    [NotifyPropertyChangedFor(nameof(CanEditRows))]
    public partial bool IsBooking { get; private set; }

    public int ChangeCount => Rows.Count(r => r.IsChange);

    public bool HasInvalid => Rows.Any(r => r.IsInvalid);

    public decimal WorthSum => Rows.Where(r => r.IsChange).Sum(r => r.Worth);

    public string SumText => $"Summe der Differenzen ({(ChangeCount == 1 ? "1 Position" : $"{ChangeCount} Positionen")})";

    public string WorthSumText => Money.Format(WorthSum);

    public string Heading => $"Inventur: {SelectedPlace?.Name}{(_adopted?.StartedBy is { } by ? $", gezählt von {by}" : "")}";

    public bool CanBook => !IsBooking && IsCounting && !HasInvalid && (ChangeCount > 0 || IsAdopted);

    public bool CanStart => SelectedPlace is not null;

    /// <summary>Nur buchbare Abweichungen zählen; Gezähltes ohne Abweichung oder mit ungültigem Wert hält niemanden in der Maske fest.</summary>
    public bool HasUnsavedChanges => !IsAdopted && Rows.Any(r => r.IsChange);

    public bool ShowEmpty => IsCounting && Rows.Count == 0;

    /// <summary>Neues Material, ein anderer Standort oder neu geladener Bestand: Lagerorte und Zählliste folgen dem Stand.</summary>
    internal void Rebuild()
    {
        var keep = SelectedPlace?.Id;
        PlaceOptions.Clear();
        foreach (var place in _owner.Places)
        {
            PlaceOptions.Add(new StockPlaceOption(place.Id, place.IsActive ? place.Name : $"{place.Name} (deaktiviert)"));
        }

        SelectedPlace = PlaceOptions.FirstOrDefault(p => p.Id == keep) ?? PlaceOptions.FirstOrDefault(p => p.Id == _owner.MainPlace?.Id) ?? PlaceOptions.FirstOrDefault();
        OnPropertyChanged(nameof(CanStart));
    }

    /// <summary>Bricht die laufende Zählung ab (anderer Standort, anderer Lagerort, gebucht).</summary>
    internal void Reset()
    {
        IsCounting = false;
        _adopted = null;
        Rows.Clear();
        HandySessions.Clear();
        _handyVersion++;
        HandyError = null;
        Error = null;
        OnPropertyChanged(nameof(IsAdopted));
        NotifyHandyChanged();
        Changed();
        RefreshDocument();
    }

    /// <summary>Lädt die abgeschickten Zählungen vom Handy des Standorts; bei einem Fehler bleibt die alte Liste stehen und die Maske nennt ihn.</summary>
    internal async Task LoadHandySessionsAsync()
    {
        if (!_owner.CanManage || _owner.Location is not { } location)
        {
            return;
        }

        var version = ++_handyVersion;
        var result = await _owner.Api.GetAsync<List<StockCountSessionDto>>($"api/stock/locations/{location.Id}/count-sessions?status=Submitted").ConfigureAwait(true);
        if (version != _handyVersion)
        {
            return; // Standort gewechselt oder neu geladen: die neuere Antwort gilt.
        }

        if (result.Value is not { } sessions)
        {
            HandyError = result.Error ?? "Die Zählungen vom Handy wurden nicht geladen.";
            return;
        }

        HandyError = null;
        HandySessions.Clear();
        foreach (var session in sessions)
        {
            HandySessions.Add(Row(session));
        }

        NotifyHandyChanged();
    }

    private HandySessionRow Row(StockCountSessionDto session)
    {
        var by = session.StartedBy ?? "Unbekannt";
        var at = TimeZoneInfo.ConvertTime(session.SubmittedAt ?? session.StartedAt, _owner.Time.LocalTimeZone);
        return new HandySessionRow(session.StoragePlaceName, $"gezählt von {by}, abgeschickt am {StockFormat.Date(DateOnly.FromDateTime(at.DateTime))} um {at:HH:mm} Uhr",
            $"Zählung {session.StoragePlaceName} von {by} übernehmen", $"Zählung {session.StoragePlaceName} von {by} verwerfen")
        {
            AdoptCommand = new AsyncRelayCommand(() => AdoptAsync(session)),
            DiscardCommand = new AsyncRelayCommand(() => DiscardSessionAsync(session)),
        };
    }

    private void NotifyHandyChanged()
    {
        OnPropertyChanged(nameof(HasHandySessions));
        OnPropertyChanged(nameof(HandyHeading));
    }

    /// <summary>„Übernehmen“: lädt die Zählung mit dem Soll und zeigt sie als Zählliste (gezählt vorbelegt, nicht änderbar).</summary>
    private async Task AdoptAsync(StockCountSessionDto summary)
    {
        if (_owner.Location is not { } location)
        {
            return;
        }

        var result = await _owner.Api.GetAsync<StockCountSessionDto>($"api/stock/locations/{location.Id}/count-sessions/{summary.Id}").ConfigureAwait(true);
        if (result.Value is not { Status: StockCountSessionStatus.Submitted } session)
        {
            Error = result.Error ?? "Die Zählung wartet nicht mehr auf die Leitung.";
            await LoadHandySessionsAsync().ConfigureAwait(true);
            return;
        }

        if (PlaceOptions.FirstOrDefault(p => p.Id == session.StoragePlaceId) is not { } place)
        {
            Error = $"Den Lagerort „{session.StoragePlaceName}“ gibt es nicht mehr.";
            return;
        }

        Error = null;
        SelectedPlace = place;
        Rows.Clear();
        foreach (var line in session.Lines)
        {
            var item = _owner.Items.FirstOrDefault(i => i.Id == line.ItemId) ?? new StockItemDto(line.ItemId, line.Name, line.Unit, 0m, 0m, null, true);
            Rows.Add(new CountRow(item, line.Expected ?? 0m, !HideTarget, Changed, isReadOnly: true) { Entered = StockFormat.Plain(line.Counted) });
        }

        _adopted = session;
        IsCounting = true;
        OnPropertyChanged(nameof(IsAdopted));
        Changed();
        RefreshDocument();
    }

    private async Task DiscardSessionAsync(StockCountSessionDto session)
    {
        if (_owner.Location is not { } location)
        {
            return;
        }

        var answer = await AskAsyncFor(new ConfirmRequest("Zählung verwerfen?",
            $"Die Zählung „{session.StoragePlaceName}“ von {session.StartedBy ?? "Unbekannt"} wird verworfen. Es wird nichts gebucht, und die Zählung lässt sich nicht wiederherstellen.", "Zählung verwerfen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _owner.Api.PostAsync<StockCountSessionDto>($"api/stock/locations/{location.Id}/count-sessions/{session.Id}/discard", null).ConfigureAwait(true);
        if (result.Value is null)
        {
            _owner.ShowError($"Die Zählung wurde nicht verworfen: {result.Error ?? "unbekannter Fehler"}");
            return;
        }

        await _owner.ReloadAsync("Zählung verworfen.").ConfigureAwait(true);
    }

    /// <summary>„Zurück“ aus einer übernommenen Zählung: die Zählung wartet weiter auf die Leitung.</summary>
    [RelayCommand]
    private async Task LeaveAdoptedAsync()
    {
        Reset();
        await LoadHandySessionsAsync().ConfigureAwait(true);
    }

    /// <summary>Schließt die übernommene Zählung: die API bucht jede Abweichung als Inventur, wie die Windows-Inventur selbst.</summary>
    private async Task<bool> CloseSessionAsync(LocationDto location, StockCountSessionDto session, int changes)
    {
        var result = await _owner.Api.PostAsync<StockCountSessionDto>($"api/stock/locations/{location.Id}/count-sessions/{session.Id}/close", null).ConfigureAwait(true);
        IsBooking = false;
        if (result.Value is null)
        {
            Error = result.Error ?? "Die Zählung wurde nicht abgeschlossen.";
            return false;
        }

        Reset();
        await _owner.ReloadAsync($"Inventur gebucht: {(changes == 1 ? "1 Position" : $"{changes} Positionen")}.").ConfigureAwait(true);
        return true;
    }

    /// <summary>„Zählung starten“: lädt den Soll-Bestand des Lagerorts und zeigt die Zählliste mit allen aktiven Materialien.</summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (_owner.Location is not { } location || SelectedPlace is not { } place)
        {
            return;
        }

        var result = await _owner.Api.GetAsync<StoragePlaceLevelsDto>($"api/stock/locations/{location.Id}/storage-places/{place.Id}/levels").ConfigureAwait(true);
        if (result.Value is not { } levels)
        {
            Error = result.Error ?? "Der Bestand des Lagerorts konnte nicht geladen werden.";
            return;
        }

        Error = null;
        var target = levels.Items.ToDictionary(l => l.ItemId, l => l.Quantity);
        Rows.Clear();
        foreach (var item in _owner.Items.Where(i => i.IsActive))
        {
            Rows.Add(new CountRow(item, target.GetValueOrDefault(item.Id), !HideTarget, Changed));
        }

        IsCounting = true;
        Changed();
    }

    [RelayCommand(CanExecute = nameof(CanBook))]
    private async Task BookAsync() => await BookCountAsync().ConfigureAwait(true);

    /// <summary>Fragt nach und bucht die abweichenden Zeilen; false bei „Abbrechen“, bei einer ungültigen Eingabe (nichts geht an die API) oder bei einem Fehler.</summary>
    public async Task<bool> BookCountAsync()
    {
        if (!CanBook || _owner.Location is not { } location || SelectedPlace is not { } place)
        {
            return false;
        }

        // Der Stand vom Klick: die Zeilen und ihre Werte werden vor der Rückfrage festgehalten.
        var changes = Rows.Where(r => r.IsChange).Select(r => (Row: r, Quantity: r.Value!.Value)).ToList();
        var worth = changes.Sum(c => c.Row.Worth);
        var answer = await AskAsyncFor(new ConfirmRequest("Inventur buchen?",
            changes.Count == 0
                ? $"Die Zählung am Lagerort „{place.Name}“ stimmt mit dem Bestand überein. Sie wird abgeschlossen, gebucht wird nichts."
                : $"{(changes.Count == 1 ? "Eine Position wird" : $"{changes.Count} Positionen werden")} am Lagerort „{place.Name}“ auf den gezählten Bestand gebucht"
                    + $"{(worth != 0 ? $" (Wert der Differenzen {Money.Format(worth)})" : "")}. Zurücknehmen lässt sich das nur mit einer Korrektur.", "Inventur buchen", IsDestructive: false)).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return false;
        }

        IsBooking = true;
        if (_adopted is { } session)
        {
            return await CloseSessionAsync(location, session, changes.Count).ConfigureAwait(true);
        }

        var done = 0;
        foreach (var (row, quantity) in changes)
        {
            var body = new StockMovementInput(row.Item.Id, StockMovementKind.Count, quantity, StoragePlaceId: place.Id);
            var result = await _owner.Api.PostAsync<List<StockMovementDto>>($"api/stock/locations/{location.Id}/movements", body).ConfigureAwait(true);
            if (result.Value is null)
            {
                IsBooking = false;
                Error = $"{row.Name}: {result.Error ?? "nicht gebucht"}";
                if (done > 0)
                {
                    await _owner.ReloadAsync().ConfigureAwait(true);
                }

                return false;
            }

            row.MarkBooked(quantity);
            done++;
        }

        IsBooking = false;
        Reset();
        await _owner.ReloadAsync($"Inventur gebucht: {(done == 1 ? "1 Position" : $"{done} Positionen")}.").ConfigureAwait(true);
        return true;
    }

    /// <summary>Alle Eingaben verwerfen (die Zählliste bleibt, mit leeren Feldern).</summary>
    public void Discard()
    {
        if (IsAdopted)
        {
            Reset();
            return;
        }

        foreach (var row in Rows)
        {
            row.Entered = "";
        }

        Error = null;
    }

    private Task<ConfirmResult> AskAsyncFor(ConfirmRequest request) => _owner.Ask(request);

    private void Changed()
    {
        foreach (var name in new[] { nameof(ChangeCount), nameof(HasInvalid), nameof(WorthSum), nameof(SumText), nameof(WorthSumText), nameof(CanBook), nameof(HasUnsavedChanges),
            nameof(ShowEmpty), nameof(Heading) })
        {
            OnPropertyChanged(name);
        }

        BookCommand.NotifyCanExecuteChanged();
        _owner.NotifyChanged();
    }

    partial void OnSelectedPlaceChanged(StockPlaceOption? value)
    {
        RefreshDocument();
        StartCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(Heading));
        if (IsCounting)
        {
            Reset();
        }
    }
}
