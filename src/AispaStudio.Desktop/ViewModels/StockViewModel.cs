using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Reiter des Lagers (R07 Slice 143): Übersicht, Bestand, Bestellen (<see cref="Orders"/>, mit der Bedarfsprüfung), Chargen (<see cref="Tasks"/>) und Bewegungen für den Alltag; Inventur, Lagerorte,
/// Lieferanten und Verbrauch dahinter als Verwaltung.
/// </summary>
public enum StockTab
{
    Overview,
    Stock,
    Movements,
    Count,
    Places,
    Tasks,
    Suppliers,
    Orders,
    Usage,
}

/// <summary>Standort in der Auswahl.</summary>
public sealed record StockLocationChoice(Guid Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Lagerort-Filter von Bestand und Bewegungen; ohne Kennung steht der Eintrag für „Alle Lagerorte“.</summary>
public sealed record PlaceChoice(Guid? Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// Lager (R05 Slice 101, ADR 0084), native Maske nach <c>web/src/stock/StockPage.tsx</c>: Reiter Bestand (mit „Material anlegen“), Bewegungen (mit der Leiste „Bewegung buchen“), Aufgaben, Inventur,
/// Lagerorte, Lieferanten und Bestellungen, darüber Standort und Lagerort. Verbrauch und Verkauf bucht das System selbst; von Hand kommen Wareneingang, Nachfüllen, Umbuchen, Korrektur und die Inventur. Die Maske zeigt nur an
/// und sendet; gerechnet wird in der API. Aufgaben, Inventur, Lagerorte und Bestellungen gibt es nur mit ManageLocation, Material und Lieferanten nur mit ManageCatalog; die API prüft ohnehin.
/// Die Bedarfsprüfung steht im Reiter Bestand (mit Book), die Chargen-Rückverfolgung im Reiter Bewegungen (mit ManageLocation), die Lieferung in den Bestellungen.
/// </summary>
public sealed partial class StockViewModel : ViewModelBase, INativeArea
{
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private List<LocationDto> _locations = [];
    private Guid? _pendingItem;
    private bool _switching;
    private bool _reverting;
    private int _locationVersion;
    private int _levelsVersion;
    private readonly bool _isLeadership;

    public StockViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time, ILiveUpdates? live = null)
    {
        Api = api;
        Time = time;
        CanViewUsage = user.Rights.Contains(AccessRight.ViewFinance);
        CanBook = user.Rights.Contains(AccessRight.Book);
        CanManage = user.Rights.Contains(AccessRight.ManageLocation);
        CanCatalog = user.Rights.Contains(AccessRight.ManageCatalog);
        _isLeadership = user.Access?.Role is StaffRole.Owner or StaffRole.GeneralManager;
        Todo = new StockTodoViewModel(this);
        Overview = new StockOverviewViewModel(this);
        ItemForm = new StockItemFormViewModel(this, api);
        Booking = new MovementBookingViewModel(this, api);
        Movements = new StockMovementsViewModel(this, time);
        Count = new StockCountViewModel(this);
        PlacesAdmin = new StoragePlacesViewModel(this);
        Tasks = new StockTasksViewModel(this);
        SuppliersAdmin = new StockSuppliersViewModel(this);
        Orders = new StockOrdersViewModel(this);
        Demand = new StockDemandViewModel(this, time);
        BatchTrace = new StockBatchTraceViewModel(this);
        Usage = new StockUsageViewModel(this, time);
        if (live is not null)
        {
            live.OperationsChanged += OnOperationsLive;
        }
    }

    public string Title => "Lager";

    internal IWorkstationApi Api { get; }

    internal TimeProvider Time { get; }

    /// <summary>Der Reiter „Verbrauch“ (Soll und Ist) zeigt Kennzahlen der Leitung (ViewFinance).</summary>
    public bool CanViewUsage { get; }

    public bool CanBook { get; }

    /// <summary>Inventur und Lagerorte (ManageLocation).</summary>
    public bool CanManage { get; }

    /// <summary>„Material anlegen“ (ManageCatalog).</summary>
    public bool CanCatalog { get; }

    /// <summary>Reiter „Bestellen“: mit Lagerrecht ganz, mit Buchungsrecht nur die Bedarfsprüfung (T-025).</summary>
    public bool CanSeeOrdersTab => CanManage || CanBook;

    public StockTodoViewModel Todo { get; }

    /// <summary>Der Reiter „Bestand“ mit der Liste, ihrer Suche und dem Seitenpanel einer Zeile.</summary>
    public StockOverviewViewModel Overview { get; }

    public StockItemFormViewModel ItemForm { get; }

    public MovementBookingViewModel Booking { get; }

    public StockMovementsViewModel Movements { get; }

    public StockCountViewModel Count { get; }

    public StoragePlacesViewModel PlacesAdmin { get; }

    public StockTasksViewModel Tasks { get; }

    public StockSuppliersViewModel SuppliersAdmin { get; }

    public StockOrdersViewModel Orders { get; }

    public StockDemandViewModel Demand { get; }

    public StockBatchTraceViewModel BatchTrace { get; }

    public StockUsageViewModel Usage { get; }

    public ObservableCollection<StockLocationChoice> Locations { get; } = [];

    public ObservableCollection<PlaceChoice> PlaceFilter { get; } = [];

    internal IReadOnlyList<StockItemDto> Items { get; private set; } = [];

    internal IReadOnlyList<StoragePlaceDto> Places { get; private set; } = [];

    internal IReadOnlyList<StockSupplierDto> Suppliers { get; private set; } = [];

    internal StockOverviewDto? OverviewData { get; private set; }

    internal StockBatchesDto? BatchData { get; private set; }

    internal StoragePlaceLevelsDto? LevelData { get; private set; }

    internal StoragePlaceDto? MainPlace => Places.FirstOrDefault(p => p.Kind == StoragePlaceKind.Main);

    internal LocationDto? Location => _locations.FirstOrDefault(l => l.Id == SelectedLocation?.Id);

    internal Guid? PlaceFilterId => SelectedPlace?.Id;

    [ObservableProperty]
    public partial StockLocationChoice? SelectedLocation { get; set; }

    [ObservableProperty]
    public partial PlaceChoice? SelectedPlace { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverviewTab), nameof(IsStockTab), nameof(IsMovementsTab), nameof(IsCountTab), nameof(IsPlacesTab), nameof(IsTasksTab), nameof(IsSuppliersTab), nameof(IsOrdersTab), nameof(IsUsageTab),
        nameof(ShowPlaceFilter), nameof(ShowBookButton), nameof(ShowSearch))]
    public partial StockTab Tab { get; private set; } = StockTab.Overview;

    public bool IsOverviewTab => Tab == StockTab.Overview;

    /// <summary>Die Verwaltung (Inventur, Lagerorte, Lieferanten, Verbrauch) steht hinter einem Trenner und fehlt ganz ohne die Rechte dafür.</summary>
    public bool HasAdminTabs => CanManage || CanCatalog || CanViewUsage;

    /// <summary>Das Feld „Material suchen oder scannen“ steht, wo man scannen darf, sonst nur dort, wo es etwas zu suchen gibt.</summary>
    public bool ShowSearch => CanScan || Tab is StockTab.Overview or StockTab.Stock;

    public bool IsStockTab => Tab == StockTab.Stock;

    public bool IsMovementsTab => Tab == StockTab.Movements;

    public bool IsCountTab => Tab == StockTab.Count;

    public bool IsPlacesTab => Tab == StockTab.Places;

    public bool IsTasksTab => Tab == StockTab.Tasks;

    public bool IsSuppliersTab => Tab == StockTab.Suppliers;

    public bool IsOrdersTab => Tab == StockTab.Orders;

    public bool IsUsageTab => Tab == StockTab.Usage;

    /// <summary>Der Reiter „Chargen“ nennt, wie viele Chargen zu erledigen sind.</summary>
    public string BatchesTabLabel => Tasks.OpenCount > 0 ? $"Chargen ({Tasks.OpenCount})" : "Chargen";

    public bool HasSeveralLocations => Locations.Count > 1;

    /// <summary>Der Lagerort-Filter gilt für Bestand und Bewegungen und erscheint erst bei mehreren Lagerorten; die Inventur wählt ihren Lagerort selbst.</summary>
    public bool ShowPlaceFilter => Places.Count > 1 && Tab is StockTab.Stock or StockTab.Movements;

    /// <summary>„Bewegung buchen“ steht in der Übersicht und im Bestand.</summary>
    public bool ShowBookButton => CanBook && Tab is StockTab.Overview or StockTab.Stock;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowContent))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(ShowContent))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>Es gibt keinen Standort, an dem ein Lager geführt werden könnte.</summary>
    public bool ShowEmpty => IsLoaded && Locations.Count == 0 && !HasLoadError;

    public bool ShowContent => IsLoaded && Location is not null && !HasLoadError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    public bool HasMessage => Message is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    public bool HasUnsavedChanges => ItemForm.IsDirty || PlacesAdmin.HasUnsavedChanges || Count.HasUnsavedChanges || SuppliersAdmin.HasUnsavedChanges || Orders.HasUnsavedChanges;

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var locations = await Api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
        if (locations.Value is not { } known)
        {
            LoadError = locations.Error ?? "Das Lager wurde nicht geladen.";
            return;
        }

        _locations = known;
        _switching = true;
        Locations.Clear();
        foreach (var location in known)
        {
            Locations.Add(new StockLocationChoice(location.Id, location.Name));
        }

        SelectedLocation = Locations.FirstOrDefault();
        _switching = false;
        OnPropertyChanged(nameof(HasSeveralLocations));
        if (Location is null)
        {
            IsLoaded = true;
            return;
        }

        if (!await LoadItemsAsync().ConfigureAwait(true) || !await LoadLocationAsync(withRooms: true).ConfigureAwait(true))
        {
            return;
        }

        IsLoaded = true;
        OnPropertyChanged(nameof(ShowContent));
        ApplyPendingItem();
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    /// <summary>
    /// Lädt Material und Bestand des Standorts neu (nach jeder Buchung); <paramref name="message"/> erscheint danach als Rückmeldung. Eingaben in der Zählliste und in
    /// den Formularen bleiben stehen.
    /// </summary>
    public async Task ReloadAsync(string? message = null)
    {
        if (message is not null)
        {
            Message = message;
            Error = null;
        }

        if (!await LoadItemsAsync().ConfigureAwait(true))
        {
            return;
        }

        await LoadLocationAsync(withRooms: false).ConfigureAwait(true);
    }

    private async Task<bool> LoadItemsAsync()
    {
        var items = await Api.GetAsync<List<StockItemDto>>("api/stock/items").ConfigureAwait(true);
        if (items.Value is not { } known)
        {
            LoadError = items.Error ?? "Das Material wurde nicht geladen.";
            return false;
        }

        Items = known;
        BatchTrace.RebuildItems(Items);
        if (CanCatalog)
        {
            var articles = await Api.GetAsync<List<ArticleDto>>("api/pos/articles").ConfigureAwait(true);
            ItemForm.SetArticles(articles.Value ?? []);
            var suppliers = await Api.GetAsync<List<StockSupplierDto>>("api/stock/suppliers").ConfigureAwait(true);
            if (suppliers.Value is { } loaded)
            {
                Suppliers = loaded;
                ItemForm.SetSuppliers(Suppliers);
                SuppliersAdmin.Rebuild();
            }
            else
            {
                // Der alte Stand bleibt: eine leere Liste sähe aus, als gäbe es keine Lieferanten mehr.
                Error = suppliers.Error ?? "Die Lieferanten wurden nicht geladen.";
            }
        }

        return true;
    }

    private async Task<bool> LoadLocationAsync(bool withRooms)
    {
        if (Location is not { } location)
        {
            return false;
        }

        // Eine spätere Ladung (anderer Standort, neuer Stand) macht die Antworten dieser ungültig; sie werden verworfen.
        var version = ++_locationVersion;
        var path = $"api/stock/locations/{location.Id}";
        var overview = await Api.GetAsync<StockOverviewDto>(path).ConfigureAwait(true);
        var batches = await Api.GetAsync<StockBatchesDto>($"{path}/batches").ConfigureAwait(true);
        var places = await Api.GetAsync<List<StoragePlaceDto>>($"{path}/storage-places").ConfigureAwait(true);
        var tasks = CanManage ? await Api.GetAsync<StockExpiryTasksDto>($"{path}/expiry-tasks").ConfigureAwait(true) : default;
        var orders = CanManage ? await Api.GetAsync<List<PurchaseOrderDto>>($"{path}/orders").ConfigureAwait(true) : default;
        var reorder = CanManage ? await Api.GetAsync<List<ReorderSuggestionDto>>($"{path}/reorder").ConfigureAwait(true) : default;
        if (version != _locationVersion)
        {
            return false;
        }

        if (overview.Value is null || places.Value is null)
        {
            LoadError = "Der Bestand konnte nicht geladen werden.";
            return false;
        }

        LoadError = null;
        OverviewData = overview.Value;
        BatchData = batches.Value;
        Places = places.Value;
        RebuildPlaceFilter();
        if (PlaceFilterId is { } placeId)
        {
            var levelsVersion = ++_levelsVersion;
            var levels = await Api.GetAsync<StoragePlaceLevelsDto>($"{path}/storage-places/{placeId}/levels").ConfigureAwait(true);
            if (version != _locationVersion)
            {
                return false;
            }

            if (levelsVersion == _levelsVersion)
            {
                LevelData = levels.Value;
            }
        }
        else
        {
            _levelsVersion++;
            LevelData = null;
        }

        if (withRooms && CanManage)
        {
            await PlacesAdmin.LoadRoomsAsync().ConfigureAwait(true);
            if (version != _locationVersion)
            {
                return false;
            }
        }

        Overview.Refresh();
        Booking.Rebuild();
        Count.Rebuild();
        if (CanManage)
        {
            PlacesAdmin.Rebuild();
            Tasks.Apply(tasks?.Value);
            OnPropertyChanged(nameof(BatchesTabLabel));
            Orders.Apply(orders?.Value, reorder?.Value);
        }

        Todo.Refresh();

        if (Tab == StockTab.Movements || Movements.IsLoaded)
        {
            await Movements.LoadAsync().ConfigureAwait(true);
        }

        if (CanBook)
        {
            await Demand.LoadAsync().ConfigureAwait(true);
        }

        if (CanManage)
        {
            await Count.LoadHandySessionsAsync().ConfigureAwait(true);
        }

        if (Tab == StockTab.Usage)
        {
            await Usage.LoadAsync().ConfigureAwait(true);
        }

        OnPropertyChanged(nameof(ShowContent));
        return true;
    }

    /// <summary>Eine Zählung vom Handy ist abgeschickt oder geschlossen worden: die Liste in der Inventur folgt dem Stand.</summary>
    private void OnOperationsLive(Guid locationId, OperationsChangeKind kind)
    {
        if (kind == OperationsChangeKind.Stock && CanManage && IsLoaded && Location?.Id == locationId)
        {
            _ = Count.LoadHandySessionsAsync();
        }
    }

    private void RebuildPlaceFilter()
    {
        var keep = SelectedPlace?.Id;
        _switching = true;
        PlaceFilter.Clear();
        PlaceFilter.Add(new PlaceChoice(null, "Alle Lagerorte"));
        foreach (var place in Places)
        {
            PlaceFilter.Add(new PlaceChoice(place.Id, place.IsActive ? place.Name : $"{place.Name} (deaktiviert)"));
        }

        SelectedPlace = PlaceFilter.FirstOrDefault(p => p.Id == keep) ?? PlaceFilter[0];
        _switching = false;
        OnPropertyChanged(nameof(ShowPlaceFilter));
    }

    [RelayCommand]
    private void ShowOverview() => SetTab(StockTab.Overview);

    [RelayCommand]
    private void ShowStock() => SetTab(StockTab.Stock);

    [RelayCommand]
    private void ShowMovements() => SetTab(StockTab.Movements);

    [RelayCommand]
    private void ShowCount() => SetTab(StockTab.Count);

    [RelayCommand]
    private void ShowPlaces() => SetTab(StockTab.Places);

    [RelayCommand]
    private void ShowTasks() => SetTab(StockTab.Tasks);

    [RelayCommand]
    private void ShowSuppliers() => SetTab(StockTab.Suppliers);

    [RelayCommand]
    private void ShowOrders() => SetTab(StockTab.Orders);

    [RelayCommand]
    private void ShowUsage() => SetTab(StockTab.Usage);

    /// <summary>
    /// Wechselt den Reiter. Aufgaben, Inventur und Lagerorte gibt es nur mit ManageLocation, Lieferanten nur mit ManageCatalog.
    /// „Bestellen“ öffnet schon, wer buchen darf (Bedarfsprüfung, T-025); Bestellvorschlag und Bestellungen darin bleiben bei ManageLocation.
    /// </summary>
    public void SetTab(StockTab tab)
    {
        if ((tab is StockTab.Count or StockTab.Places or StockTab.Tasks && !CanManage) || (tab == StockTab.Orders && !CanManage && !CanBook)
            || (tab == StockTab.Suppliers && !CanCatalog) || (tab == StockTab.Usage && !CanViewUsage))
        {
            return;
        }

        if (tab != StockTab.Stock)
        {
            // „Einem Material zuordnen“ gilt nur im Bestand, wo „Ändern“ steht.
            _barcodeToAssign = null;
        }

        Tab = tab;
        if (tab == StockTab.Movements && Location is not null)
        {
            _ = Movements.LoadAsync();
        }

        if (tab == StockTab.Usage && Location is not null)
        {
            _ = Usage.LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanBook))]
    private void OpenBookingPanel() => OpenBooking(new MovementPreset(PlaceId: PlaceFilterId));

    internal void OpenBooking(MovementPreset preset)
    {
        if (CanBook)
        {
            Booking.Open(preset);
        }
    }

    internal Task<ConfirmResult> Ask(ConfirmRequest request) => AskAsync(request);

    internal void ShowError(string text)
    {
        Error = text;
        Message = null;
    }

    internal void NotifyChanged() => OnPropertyChanged(nameof(HasUnsavedChanges));

    [RelayCommand]
    private void CloseMessage() => Message = null;

    [RelayCommand]
    private void CloseError() => Error = null;

    /// <summary>Ein Sprung aus Kalender oder Assistent: das Material im Bestand wählen. Steht die Maske noch nicht, gilt das Ziel nach dem Laden.</summary>
    public void Open(Guid? id, string? section, string? field)
    {
        SetTab(id is null ? StockTab.Overview : StockTab.Stock);
        _pendingItem = id;
        if (IsLoaded)
        {
            ApplyPendingItem();
        }
    }

    private void ApplyPendingItem()
    {
        if (_pendingItem is { } id)
        {
            _pendingItem = null;
            Overview.Select(id);
        }
    }

    public async Task<bool> SaveChangesAsync()
    {
        if (ItemForm.IsDirty && !await ItemForm.CreateItemAsync().ConfigureAwait(true))
        {
            return false;
        }

        if (PlacesAdmin.HasUnsavedChanges && !await PlacesAdmin.SaveChangesAsync().ConfigureAwait(true))
        {
            return false;
        }

        if (SuppliersAdmin.HasUnsavedChanges && !await SuppliersAdmin.SaveChangesAsync().ConfigureAwait(true))
        {
            return false;
        }

        if (Orders.HasUnsavedChanges && !await Orders.SaveChangesAsync().ConfigureAwait(true))
        {
            return false;
        }

        return !Count.HasUnsavedChanges || await Count.BookCountAsync().ConfigureAwait(true);
    }

    public void Discard()
    {
        ItemForm.Clear();
        PlacesAdmin.Discard();
        SuppliersAdmin.Discard();
        Orders.Discard();
        Count.Discard();
    }

    partial void OnSelectedLocationChanged(StockLocationChoice? oldValue, StockLocationChoice? newValue)
    {
        if (_reverting || _switching || newValue is null || oldValue is null)
        {
            return;
        }

        if (Count.HasUnsavedChanges || PlacesAdmin.HasUnsavedChanges || Orders.HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Auswahl bleibt beim alten Standort, bis die Antwort da ist.
            Revert(() => SelectedLocation = oldValue);
            _ = LeaveToLocationAsync(newValue);
            return;
        }

        _ = SwitchLocationAsync();
    }

    private async Task LeaveToLocationAsync(StockLocationChoice target)
    {
        if (await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            _switching = true;
            SelectedLocation = target;
            _switching = false;
            await SwitchLocationAsync().ConfigureAwait(true);
        }
    }

    private async Task SwitchLocationAsync()
    {
        Booking.Close();
        Count.Reset();
        Orders.Reset();
        Demand.Reset();
        Usage.Reset();
        SelectedPlace = null;
        OverviewData = null;
        Places = [];
        await LoadLocationAsync(withRooms: true).ConfigureAwait(true);
    }

    partial void OnSelectedPlaceChanged(PlaceChoice? value)
    {
        if (_switching || value is null)
        {
            return;
        }

        _ = ApplyPlaceFilterAsync(value);
    }

    private async Task ApplyPlaceFilterAsync(PlaceChoice value)
    {
        if (Location is not { } location)
        {
            return;
        }

        var version = ++_levelsVersion;
        LevelData = null;
        if (value.Id is { } placeId)
        {
            var levels = await Api.GetAsync<StoragePlaceLevelsDto>($"api/stock/locations/{location.Id}/storage-places/{placeId}/levels").ConfigureAwait(true);
            if (version != _levelsVersion)
            {
                return;
            }

            LevelData = levels.Value;
        }

        Overview.Refresh();
        Movements.Refresh();
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
}
