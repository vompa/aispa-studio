using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zeile, die eine Seite beim Abgleich abgelehnt hat, mit Zeitpunkt in Ortszeit.</summary>
public sealed record SyncConflictItem(SyncConflict Conflict, string When)
{
    public string Area => Conflict.Area;

    public string Reason => Conflict.Reason;

    /// <summary>Name des Knopfs „Erledigt“ für den Screenreader: „Erledigt: Termine 19.10.2026 10:12“, damit jede Zeile ihren eigenen Namen hat.</summary>
    public string ResolveName => $"Erledigt: {Area} {When}";
}

/// <summary>
/// Abgleich des Einzelplatzes mit einer zentralen PostgreSQL (ADR 0041, Schritt 4): Stand, wartende Änderungen, abgelehnte Zeilen
/// und, mit Recht „Standort verwalten“, die zentrale Datenbank eintragen oder austragen. Nur im Einzelplatz-Betrieb.
/// </summary>
public partial class SyncViewModel(IWorkstationApi api, ILocalServer local, TimeProvider time) : ViewModelBase
{
    /// <summary>Zentrale Datenbank eintragen oder austragen.</summary>
    public bool CanConfigure { get; init; }

    /// <summary>Abgelehnte Zeilen als erledigt markieren (Buchungsrecht).</summary>
    public bool CanResolve { get; init; }

    /// <summary>Nach jedem Laden: Anzahl offener Konflikte für die Zahl in der Navigation.</summary>
    public Action<int>? ConflictsChanged { get; init; }

    public ObservableCollection<SyncConflictItem> Conflicts { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(ErrorText), nameof(HasError), nameof(PendingText), nameof(IsEnabled))]
    public partial SyncStatus? Status { get; private set; }

    [ObservableProperty]
    public partial bool HasConflicts { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CentralText), nameof(HasCentral), nameof(HasShareCode))]
    public partial int CentralVersion { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial string ConnectionInput { get; set; } = "";

    /// <summary>Schlüsselcode des Spas; bei der ersten Stelle leer lassen, dann entsteht einer (ADR 0048).</summary>
    [ObservableProperty]
    public partial string ShareCodeInput { get; set; } = "";

    /// <summary>Schlüsselcode zum Abschreiben für die nächste Stelle; sichtbar bis „Verstanden“.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShownCode))]
    public partial string? ShownCode { get; private set; }

    public bool HasShownCode => ShownCode is not null;

    public bool HasShareCode => local.ShareCode is not null;

    [ObservableProperty]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand), nameof(DisconnectCommand))]
    public partial bool IsBusy { get; private set; }

    public bool IsEnabled => Status?.Enabled == true;

    public bool HasCentral => local.CentralDescription is not null;

    public string CentralText => local.CentralDescription is { } central
        ? $"Zentrale Datenbank: {central}"
        : "Keine zentrale Datenbank eingetragen. Alle Daten liegen nur auf diesem PC; die tägliche Sicherung ist dann die einzige Kopie.";

    public string StateText => Status switch
    {
        null => "Stand wird geladen …",
        { Enabled: false } => "Kein Abgleich.",
        { LastError: not null } => "Der letzte Abgleich ist fehlgeschlagen; der nächste Versuch folgt von selbst.",
        { LastRun: null } => "Der erste Abgleich läuft gleich. Beim ersten Mal wird der ganze Bestand übertragen.",
        { LastRun: { } run } => $"Zuletzt abgeglichen {Local(run)}.",
    };

    public string? ErrorText => Status?.LastError;

    public bool HasError => Status?.LastError is not null;

    public string PendingText => Status is { Enabled: true, PendingChanges: var n }
        ? n == 0 ? "Alle Änderungen dieses PCs sind übertragen." : $"{n} Änderung(en) warten auf den nächsten Abgleich."
        : "";

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var result = await api.GetAsync<SyncStatus>("api/system/sync").ConfigureAwait(true);
        if (result.Value is not { } status)
        {
            return;
        }

        Status = status;
        Conflicts.Clear();
        foreach (var conflict in status.Conflicts)
        {
            Conflicts.Add(new SyncConflictItem(conflict, Local(conflict.DetectedAt)));
        }

        HasConflicts = Conflicts.Count > 0;
        ConflictsChanged?.Invoke(Conflicts.Count);
    }

    private bool CanConnect => CanConfigure && !IsBusy && ConnectionInput.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        // Ohne Code und ohne gespeicherten: diese Stelle ist die erste und bekommt einen neuen, der für alle weiteren gilt.
        string? created = null;
        try
        {
            if (ShareCodeInput.Trim().Length > 0)
            {
                local.SetShareCode(ShareCodeInput);
            }
            else if (local.ShareCode is null)
            {
                created = local.SetShareCode(null);
            }
        }
        catch (LocalServerException ex)
        {
            Message = ex.Message;
            return;
        }

        ShareCodeInput = "";
        ShownCode = created;
        if (await ApplyAsync(ConnectionInput).ConfigureAwait(true) is { } restarted)
        {
            ConnectionInput = "";
            Message = restarted
                ? "Gespeichert. Der Abgleich startet jetzt; beim ersten Mal wird der ganze Bestand übertragen."
                : "Gespeichert. Der Abgleich startet mit dem nächsten Start der App.";
        }
    }

    private bool CanDisconnect => CanConfigure && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        if (await ApplyAsync(null).ConfigureAwait(true) is not null)
        {
            Message = "Abgleich ausgeschaltet. Die Daten bleiben auf diesem PC, die zentrale Datenbank behält ihren Stand.";
        }
    }

    /// <summary>Den gespeicherten Schlüsselcode zeigen, um eine weitere Stelle zu verbinden.</summary>
    [RelayCommand]
    private void ShowCode()
    {
        if (CanConfigure)
        {
            ShownCode = local.ShareCode;
        }
    }

    [RelayCommand]
    private void CodeNoted() => ShownCode = null;

    [RelayCommand]
    private async Task ResolveAsync(SyncConflictItem item)
    {
        if (!CanResolve)
        {
            return;
        }

        var result = await api.PostAsync<object>($"api/system/sync/conflicts/{item.Conflict.Id}/resolve", null).ConfigureAwait(true);
        Message = result.Ok ? null : result.Error;
        await RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Eintragen oder austragen und die API neu starten; <c>null</c> bei einem Fehler, der dann in <see cref="Message"/> steht.</summary>
    private async Task<bool?> ApplyAsync(string? connectionString)
    {
        IsBusy = true;
        Message = "Speichert und startet AISPA Studio auf diesem PC neu …";
        try
        {
            var restarted = await local.SetCentralAsync(connectionString).ConfigureAwait(true);
            CentralVersion++;
            await RefreshAsync().ConfigureAwait(true);
            return restarted;
        }
        catch (LocalServerException ex)
        {
            Message = ex.Message;
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string Local(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, time.LocalTimeZone).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
}
