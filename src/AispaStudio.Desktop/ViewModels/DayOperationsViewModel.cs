using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Punkt der Tagesstart-Kontrolle eines Raums; ein Klick hakt ab oder nimmt den Haken zurück.</summary>
public sealed partial class StartItemRow(RoomStartItemStateDto item, bool canCheck, Func<StartItemRow, Task> toggle, TimeZoneInfo zone) : ViewModelBase
{
    public RoomStartItemStateDto Item { get; } = item;

    public string Text => Item.Text;

    public bool IsDone => Item.Done;

    public bool CanCheck => canCheck;

    /// <summary>„11:02 Anna“, wenn abgehakt.</summary>
    public string? Info => Item is { Done: true, DoneAt: { } at }
        ? $"{DayLayout.Local(at, zone):HH:mm}{(Item.DoneBy is null ? "" : $" {Item.DoneBy}")}"
        : null;

    [RelayCommand]
    private Task ToggleAsync() => canCheck ? toggle(this) : Task.CompletedTask;
}

/// <summary>Ein Raum mit seiner Checkliste für den Tagesstart und dem Stand der Abnahme.</summary>
public sealed partial class RoomStartRow(RoomStartRoomDto room, IReadOnlyList<StartItemRow> items, bool canApprove, Func<RoomStartRow, Task> approve, TimeZoneInfo zone)
    : ViewModelBase
{
    public RoomStartRoomDto Room { get; } = room;

    public string Name => Room.RoomName;

    public IReadOnlyList<StartItemRow> Items { get; } = items;

    public bool IsApproved => Room.State == RoomStartState.Approved;

    /// <summary>Alle Punkte abgehakt, aber noch nicht abgenommen.</summary>
    public bool CanApprove => canApprove && Room.State == RoomStartState.Checked;

    public bool IsChecked => Room.State is RoomStartState.Checked or RoomStartState.Approved;

    public string StateText => Room.State switch
    {
        RoomStartState.Open => "offen",
        RoomStartState.Partial => $"{Room.Items.Count(i => i.Done)} von {Room.Items.Count}",
        RoomStartState.Checked => "geprüft",
        RoomStartState.Approved => Room.ApprovedBy is null ? "abgenommen" : $"abgenommen von {Room.ApprovedBy}",
        _ => "",
    };

    public string? ApprovedAtText => Room.ApprovedAt is { } at ? $"um {DayLayout.Local(at, zone):HH:mm}" : null;

    [RelayCommand]
    private Task ApproveAsync() => approve(this);
}

/// <summary>Eintrag des Rezeptionshandbuchs, der am Tagesstart erinnert.</summary>
public sealed record ReminderRow(string Title, string Text, string? Area);

/// <summary>
/// Betriebsdaten des Tages für die Meldungen (ADR 0065): Tagesstart-Kontrolle je Raum mit Abhaken und Abnahme, „Bereitstellen“ je Termin, wichtige Einträge des
/// Rezeptionshandbuchs und Warnungen der Lager-Bedarfsprüfung. Die Seitenleiste „Meldungen“ macht daraus Karten; die Checkliste eines Raums und die Abnahme
/// stehen im Detail einer Tagesstart-Meldung. Was das Spa tun muss, rechnet der Server; hier wird nur gezeigt und abgehakt.
/// </summary>
public sealed partial class DayOperationsViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly Guid _locationId;
    private readonly DateOnly _date;
    private readonly TimeZoneInfo _zone;
    private readonly bool _canCheck;
    private readonly bool _mayApprove;
    private readonly bool _canSeeStock;
    private readonly bool _isFuture;
    private int _startVersion;
    private int _servingsVersion;
    private int _remindersVersion;
    private int _stockVersion;

    public DayOperationsViewModel(IWorkstationApi api, Guid locationId, DateOnly date, TimeZoneInfo zone, bool isFuture, bool canCheck, bool mayApprove, bool canSeeStock)
    {
        _api = api;
        _locationId = locationId;
        _date = date;
        _zone = zone;
        _isFuture = isFuture;
        _canCheck = canCheck;
        _mayApprove = mayApprove;
        _canSeeStock = canSeeStock;
    }

    /// <summary>Tag und Standort, für die die Daten gelten; der Kalender hält sie nur, solange beide noch angezeigt werden.</summary>
    public (Guid LocationId, DateOnly Date) Scope => (_locationId, _date);

    /// <summary>Etwas hat sich geändert (geladen, abgehakt, abgenommen); der Kalender baut die Meldungen neu.</summary>
    public Action? Changed { get; init; }

    public IReadOnlyList<RoomStartRow> Rooms { get; private set; } = [];

    public IReadOnlyList<ReminderRow> Reminders { get; private set; } = [];

    /// <summary>Meldungen der Bedarfsprüfung, z. B. „Massageöl: Bestand reicht nicht ab 14:00“.</summary>
    public IReadOnlyList<StockDemandLine> StockWarnings { get; private set; } = [];

    /// <summary>Räume, deren Raumschrank bei mindestens einem Material unter der Mindestmenge liegt, mit diesen Materialien.</summary>
    public IReadOnlyList<RoomStartRoomDto> RoomsBelowMinimum { get; private set; } = [];

    /// <summary>Alle „Bereitstellen“-Einträge der Termine des Tages, für Terminfenster und Hover-Karte.</summary>
    public IReadOnlyList<ServingItemDto> ServingItems { get; private set; } = [];

    /// <summary>„Tee 11:20, Früchteteller 11:20“ je Termin für die Hover-Karte.</summary>
    public IReadOnlyDictionary<Guid, string> ServingTexts { get; private set; } = new Dictionary<Guid, string>();

    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>Raum, dessen Abnahme gerade mit Name und PIN der Leitung bestätigt wird; sonst leer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAskingApproval), nameof(ApprovalTitle))]
    public partial RoomStartRow? ApprovingRoom { get; private set; }

    public bool IsAskingApproval => ApprovingRoom is not null;

    public string ApprovalTitle => ApprovingRoom is null ? "" : $"Abnahme {ApprovingRoom.Name}: Leitung mit Name und PIN bestätigen";

    public ObservableCollection<QuickSwitchPerson> Approvers { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmApprovalCommand))]
    public partial QuickSwitchPerson? Approver { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmApprovalCommand))]
    public partial string ApprovalPin { get; set; } = "";

    /// <summary>Lädt Tagesstart, Bereitstellen, Handbuch-Erinnerungen und (mit Buchungsrecht) die Bedarfsprüfung; fehlt eine Antwort, fehlt nur dieser Teil.</summary>
    public Task LoadAsync() => Task.WhenAll(LoadStartAsync(), LoadServingsAsync(), LoadRemindersAsync(), LoadStockAsync());

    /// <summary>Lädt nur die Quelle, die sich laut Server geändert hat; Pausen und Raumpflege kommen mit dem Kalendertag.</summary>
    public Task LoadAsync(OperationsChangeKind kind) => kind switch
    {
        OperationsChangeKind.RoomStart => LoadStartAsync(),
        OperationsChangeKind.Stock => LoadStockAsync(),
        OperationsChangeKind.Reminders => LoadRemindersAsync(),
        _ => Task.CompletedTask,
    };

    private string Day => $"date={_date:yyyy-MM-dd}";

    private async Task LoadStartAsync()
    {
        var version = ++_startVersion;
        var start = await _api.GetAsync<RoomStartDayDto>($"api/locations/{_locationId}/room-start?{Day}").ConfigureAwait(true);
        if (version != _startVersion)
        {
            return;
        }

        // Räume nur mit Raumausstattung (ohne Checkliste) haben keine Tagesstart-Kontrolle; sie zählen nur unter „Räume unter Mindestbestand“.
        var rooms = start.Value?.Rooms ?? [];
        Rooms = [.. rooms.Where(r => r.Items.Count > 0).Select(RowOf)];
        RoomsBelowMinimum = [.. rooms.Where(r => r.Equipment is { Complete: false })];
        ApprovingRoom = null;
        OnPropertyChanged(nameof(Rooms));
        Changed?.Invoke();
    }

    private async Task LoadServingsAsync()
    {
        var version = ++_servingsVersion;
        var servings = await _api.GetAsync<ServingDayDto>($"api/locations/{_locationId}/servings?{Day}").ConfigureAwait(true);
        if (version != _servingsVersion)
        {
            return;
        }

        ServingItems = servings.Value?.Items ?? [];
        ServingTexts = ServingItems.GroupBy(i => i.AppointmentId).ToDictionary(g => g.Key,
            g => string.Join(", ", g.OrderBy(i => i.DueAt).Select(i => $"{i.Text} {DayLayout.Local(i.DueAt, _zone):HH:mm}")));
        Changed?.Invoke();
    }

    private async Task LoadRemindersAsync()
    {
        var version = ++_remindersVersion;
        var reminders = await _api.GetAsync<List<HandbookEntryDto>>($"api/handbook/reminders?{Day}").ConfigureAwait(true);
        if (version != _remindersVersion)
        {
            return;
        }

        Reminders = [.. (reminders.Value ?? []).Select(r => new ReminderRow(r.Title, r.Text, r.Area))];
        Changed?.Invoke();
    }

    private async Task LoadStockAsync()
    {
        if (!_canSeeStock)
        {
            return;
        }

        var version = ++_stockVersion;
        var stock = await _api.GetAsync<StockDemandDto>($"api/stock/locations/{_locationId}/demand?from={_date:yyyy-MM-dd}&until={_date:yyyy-MM-dd}").ConfigureAwait(true);
        if (version != _stockVersion)
        {
            return;
        }

        StockWarnings = stock.Value?.Warnings ?? [];
        Changed?.Invoke();
    }

    private RoomStartRow RowOf(RoomStartRoomDto room) =>
        new(room, [.. room.Items.Select(i => new StartItemRow(i, _canCheck && !_isFuture && room.State != RoomStartState.Approved, ToggleItemAsync, _zone))],
            canApprove: !_isFuture, ApproveAsync, _zone);

    private async Task ToggleItemAsync(StartItemRow row)
    {
        if (Rooms.FirstOrDefault(r => r.Items.Contains(row)) is not { } room)
        {
            return;
        }

        Error = null;
        var result = await _api.PutAsync<object>($"api/rooms/{room.Room.RoomId}/start-checks/{row.Item.ItemId}", new RoomStartCheckInput(_date, !row.IsDone)).ConfigureAwait(true);
        if (!result.Ok)
        {
            Error = $"Nicht abgehakt: {result.Error}";
            return;
        }

        await LoadStartAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// „Abnehmen“: wer das Recht „Standort verwalten“ hat, nimmt selbst ab; sonst fragt die Leiste nach Name und PIN der Standortleitung
    /// (wie beim Rabatt über der Grenze) und nimmt erst nach „Abnehmen“ ab.
    /// </summary>
    private async Task ApproveAsync(RoomStartRow room)
    {
        Error = null;
        if (_mayApprove)
        {
            await SendApprovalAsync(room, null).ConfigureAwait(true);
            return;
        }

        var result = await _api.GetAsync<List<QuickSwitchPerson>>($"api/locations/{_locationId}/room-approvers").ConfigureAwait(true);
        Approvers.Clear();
        foreach (var person in result.Value ?? [])
        {
            Approvers.Add(person);
        }

        if (Approvers.Count == 0)
        {
            Error = "Für die Abnahme ist am Standort keine Leitung mit PIN eingerichtet.";
            return;
        }

        Approver = Approvers[0];
        ApprovalPin = "";
        ApprovingRoom = room;
    }

    private bool CanConfirmApproval() => ApprovingRoom is not null && Approver is not null && ApprovalPin.Trim().Length >= 4;

    [RelayCommand(CanExecute = nameof(CanConfirmApproval))]
    private async Task ConfirmApprovalAsync()
    {
        if (ApprovingRoom is { } room && Approver is { } approver)
        {
            await SendApprovalAsync(room, new SupervisorApproval(approver.UserId, ApprovalPin.Trim())).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void CancelApproval()
    {
        ApprovingRoom = null;
        ApprovalPin = "";
    }

    private async Task SendApprovalAsync(RoomStartRow room, SupervisorApproval? approval)
    {
        var result = await _api.PostAsync<RoomApprovalDto>($"api/rooms/{room.Room.RoomId}/approvals", new RoomApprovalInput(_date, RoomApprovalKind.DayStart, approval))
            .ConfigureAwait(true);
        ApprovalPin = "";
        if (!result.Ok)
        {
            Error = $"Nicht abgenommen: {result.Error}";
            return;
        }

        await LoadStartAsync().ConfigureAwait(true);
    }

    /// <summary>„Bereitstellen“ eines Termins: Einträge mit Uhrzeit und Haken, dazu Wünsche des Gastes und der Hinweis auf die Anamnese.</summary>
    public ServingPanelViewModel? ServingsFor(Guid appointmentId, bool canCheck, Func<Task> changed)
    {
        var items = ServingItems.Where(i => i.AppointmentId == appointmentId).OrderBy(i => i.DueAt).ToList();
        return items.Count == 0 ? null : new ServingPanelViewModel(_api, items, _zone, canCheck, changed);
    }
}

/// <summary>Ein „Bereitstellen“-Eintrag eines Termins mit Haken.</summary>
public sealed partial class ServingRow(ServingItemDto item, string time, bool canCheck, Func<ServingRow, Task> toggle) : ViewModelBase
{
    public ServingItemDto Item { get; } = item;

    public string Text => Item.Text;

    public string Time => time;

    public bool IsDone => Item.Done;

    public bool CanCheck => canCheck;

    [RelayCommand]
    private Task ToggleAsync() => canCheck ? toggle(this) : Task.CompletedTask;
}

/// <summary>„Bereitstellen“ im Terminfenster: „Tee 11:20“, „Früchteteller 11:20“ mit Haken; „Anamnese beachten“ nur als Hinweis (nie der Inhalt).</summary>
public sealed partial class ServingPanelViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly Func<Task> _changed;

    public ServingPanelViewModel(IWorkstationApi api, IReadOnlyList<ServingItemDto> items, TimeZoneInfo zone, bool canCheck, Func<Task> changed)
    {
        _api = api;
        _changed = changed;
        Rows = [.. items.Select(i => new ServingRow(i, DayLayout.Local(i.DueAt, zone).ToString("HH:mm", CultureInfo.InvariantCulture), canCheck, ToggleAsync))];
        Wishes = items.Select(i => i.Wishes).FirstOrDefault(w => w is not null);
        AnamnesisOnFile = items.Any(i => i.AnamnesisOnFile);
    }

    public IReadOnlyList<ServingRow> Rows { get; }

    /// <summary>Wünsche des Gastes von der Kundenkarte, z. B. die Teesorte.</summary>
    public string? Wishes { get; }

    public string? WishesText => Wishes is null ? null : $"Wunsch des Gastes: {Wishes}";

    public bool HasWishes => Wishes is not null;

    /// <summary>Nur der Hinweis, dass ein Gesundheitsfragebogen vorliegt.</summary>
    public bool AnamnesisOnFile { get; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>Kurzfassung für die Hover-Karte: „Tee 11:20, Früchteteller 11:20“.</summary>
    public string Summary => string.Join(", ", Rows.Select(r => $"{r.Text} {r.Time}"));

    private async Task ToggleAsync(ServingRow row)
    {
        Error = null;
        var result = await _api.PutAsync<object>($"api/appointments/{row.Item.AppointmentId}/extras/{row.Item.ExtraId}/check", new ServingCheck(!row.IsDone)).ConfigureAwait(true);
        if (!result.Ok)
        {
            Error = $"Nicht abgehakt: {result.Error}";
            return;
        }

        await _changed().ConfigureAwait(true);
    }
}
