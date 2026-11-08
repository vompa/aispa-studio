using System.Collections.ObjectModel;
using System.Text;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Auswahl „kein Terminal“ oder ein Kartenterminal im Netz.</summary>
public sealed record TerminalChoice(string Title, PaymentTerminalDto? Terminal);

/// <summary>Protokoll eines neuen Terminals, wie es der Zahlungsdienstleister eingerichtet hat.</summary>
public sealed record ProtocolChoice(string Title, TerminalProtocol Protocol);

/// <summary>
/// Geräte an diesem Empfangs-PC: Bondrucker im Netz und das Kartenterminal, das dieser PC ansteuert.
/// Gespeichert wird je Arbeitsplatz (einstellungen.json), nicht je Person.
/// </summary>
public partial class DevicesViewModel(
    IWorkstationApi api,
    ISettingsStore settings,
    Func<string, IReceiptPrinter>? printer = null,
    Func<PaymentTerminalDto, ICardTerminalDriver>? driver = null) : ViewModelBase, IAsyncDisposable
{
    private readonly Func<string, IReceiptPrinter> _printer = printer ?? (address => new NetworkReceiptPrinter(address));
    private readonly Func<PaymentTerminalDto, ICardTerminalDriver>? _driver = driver;
    private Task? _initialization;

    public ObservableCollection<TerminalChoice> Terminals { get; } = [];

    // ---------- Empfangsgerät (Schnellwechsel per PIN, ADR 0042) ----------

    /// <summary>Nur mit Recht „Zugänge verwalten“ (Inhaber, Standortleitung) lässt sich dieser PC als Empfangsgerät anmelden.</summary>
    public bool CanManageDevice { get; init; }

    /// <summary>Nach Anmelden oder Abmelden des Empfangsgeräts: Leerlauf-Abmeldung und „Person wechseln“ nachziehen.</summary>
    public Action<SharedDeviceDto?>? DeviceChanged { get; init; }

    public ObservableCollection<LocationDto> Locations { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSharedDevice), nameof(SharedDeviceText))]
    public partial SharedDeviceDto? SharedDevice { get; set; }

    public bool IsSharedDevice => SharedDevice is not null;

    public string SharedDeviceText => SharedDevice is { } d
        ? $"Dieser PC ist Empfangsgerät „{d.Name}“ in {d.LocationName}. Nach {d.IdleMinutes} Minuten ohne Eingabe meldet die App ab."
        : "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterDeviceCommand))]
    public partial string DeviceName { get; set; } = "Empfang";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterDeviceCommand))]
    public partial LocationDto? DeviceLocation { get; set; }

    public IReadOnlyList<int> IdleChoices { get; } = [2, 5, 10, 15, 30, 60];

    [ObservableProperty]
    public partial int DeviceIdleMinutes { get; set; } = SharedDeviceInput.DefaultIdleMinutes;

    [ObservableProperty]
    public partial string? DeviceError { get; private set; }

    private bool CanRegisterDevice => CanManageDevice && DeviceName.Trim().Length > 0 && DeviceLocation is not null;

    [RelayCommand(CanExecute = nameof(CanRegisterDevice))]
    private async Task RegisterDeviceAsync()
    {
        var result = await api.RegisterDeviceAsync(new SharedDeviceInput(DeviceName.Trim(), DeviceLocation!.Id, DeviceIdleMinutes)).ConfigureAwait(true);
        DeviceError = result.Error;
        if (result.Value is { } device)
        {
            SharedDevice = device;
            DeviceChanged?.Invoke(device);
        }
    }

    [RelayCommand]
    private async Task RevokeDeviceAsync()
    {
        var result = await api.RevokeDeviceAsync().ConfigureAwait(true);
        DeviceError = result.Error;
        if (result.Ok)
        {
            SharedDevice = null;
            DeviceChanged?.Invoke(null);
        }
    }

    // ---------- Kartenterminal anlegen (Recht „Standort verwalten“) ----------

    public bool CanManageTerminals { get; init; }

    /// <summary>Das Webmodul Dashboard ist eingeschaltet (ADR 0041, Slice 105); sonst gibt es keine Anzeigen zu koppeln.</summary>
    public bool DashboardOn { get; init; } = true;

    public bool CanManageDisplays => CanManageTerminals && DashboardOn;

    public IReadOnlyList<ProtocolChoice> Protocols { get; } =
    [
        new("ZVT (Deutschland, Österreich; meist Port 20007)", TerminalProtocol.Zvt),
        new("OPI (z. B. Worldline, Schweiz; meist Port 20002)", TerminalProtocol.Opi),
    ];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddTerminalCommand))]
    public partial string NewTerminalName { get; set; } = "Empfang";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddTerminalCommand))]
    public partial LocationDto? NewTerminalLocation { get; set; }

    [ObservableProperty]
    public partial ProtocolChoice? NewTerminalProtocol { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddTerminalCommand))]
    public partial string NewTerminalAddress { get; set; } = "";

    [ObservableProperty]
    public partial string? TerminalText { get; private set; }

    private bool CanAddTerminal => CanManageTerminals && NewTerminalName.Trim().Length > 0 && NewTerminalAddress.Trim().Length > 0
        && NewTerminalLocation is not null;

    /// <summary>Terminal im Netz am Server anlegen und hier auswählen; angesteuert wird es nach „Speichern“ von diesem PC.</summary>
    [RelayCommand(CanExecute = nameof(CanAddTerminal))]
    private async Task AddTerminalAsync()
    {
        var location = NewTerminalLocation!;
        var result = await api.PostAsync<PaymentTerminalDto>("api/pos/terminals", new PaymentTerminalInput(location.Id, NewTerminalName.Trim(),
            TerminalProvider.Local, NewTerminalAddress.Trim(), NewTerminalProtocol?.Protocol ?? TerminalProtocol.Zvt)).ConfigureAwait(true);
        if (result.Value is not { } terminal)
        {
            TerminalText = result.Error;
            return;
        }

        var choice = Choice(terminal, location);
        Terminals.Add(choice);
        Terminal = choice;
        NewTerminalAddress = "";
        TerminalText = $"„{terminal.Name}“ angelegt und ausgewählt. Mit „Speichern“ steuert dieser PC das Terminal an.";
    }

    private bool CanRemoveTerminal => CanManageTerminals && Terminal?.Terminal is not null;

    /// <summary>„Gewähltes Terminal entfernen“ fragt nach (Rückfragedialog, Design-Brief 5.27).</summary>
    [RelayCommand(CanExecute = nameof(CanRemoveTerminal))]
    private async Task AskRemoveTerminalAsync()
    {
        if (Terminal is not { Terminal: { } terminal })
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest("Terminal entfernen?",
            $"„{terminal.Name}“ steht danach an keinem PC und in keiner Kassa mehr zur Wahl; bisherige Zahlungen bleiben.", "Entfernen")).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await RemoveTerminalAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Gewähltes Terminal stilllegen; es steht danach an keinem PC und in keiner Kassa mehr zur Wahl.</summary>
    [RelayCommand(CanExecute = nameof(CanRemoveTerminal))]
    private async Task RemoveTerminalAsync()
    {
        if (Terminal is not { Terminal: { } terminal } choice)
        {
            return;
        }

        var result = await api.DeleteAsync<object>($"api/pos/terminals/{terminal.Id}").ConfigureAwait(true);
        if (!result.Ok)
        {
            TerminalText = result.Error;
            return;
        }

        Terminal = Terminals[0];
        Terminals.Remove(choice);
        TerminalText = $"„{terminal.Name}“ entfernt.";
        await SaveAsync().ConfigureAwait(true);
    }

    partial void OnTerminalChanged(TerminalChoice? value)
    {
        RemoveTerminalCommand.NotifyCanExecuteChanged();
        AskRemoveTerminalCommand.NotifyCanExecuteChanged();
    }

    // ---------- Anzeigen im Pausenraum (Recht „Standort verwalten“, Slice 104) ----------

    /// <summary>Gekoppelte Anzeigen aller Standorte; die Anzeige selbst zeigt einen Code, den die Leitung hier eintippt.</summary>
    public ObservableCollection<DisplayDeviceDto> Displays { get; } = [];

    public bool HasDisplays => Displays.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PairDisplayCommand))]
    public partial string DisplayCode { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PairDisplayCommand))]
    public partial string DisplayName { get; set; } = "Pausenraum";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PairDisplayCommand))]
    public partial LocationDto? DisplayLocation { get; set; }

    [ObservableProperty]
    public partial string? DisplayError { get; private set; }

    [ObservableProperty]
    public partial string? DisplayText { get; private set; }

    private bool CanPairDisplay => CanManageDisplays && DisplayCode.Trim().Length > 0 && DisplayName.Trim().Length > 0 && DisplayLocation is not null;

    [RelayCommand(CanExecute = nameof(CanPairDisplay))]
    private async Task PairDisplayAsync()
    {
        var result = await api.PostAsync<DisplayDeviceDto>("api/displays",
            new DisplayPairInput(DisplayCode.Trim(), DisplayName.Trim(), DisplayLocation!.Id)).ConfigureAwait(true);
        DisplayError = result.Error;
        if (result.Value is not { } display)
        {
            return;
        }

        Displays.Add(display);
        OnPropertyChanged(nameof(HasDisplays));
        DisplayCode = "";
        DisplayText = $"„{display.Name}“ gekoppelt. Die Anzeige zeigt den Tag in wenigen Sekunden.";
    }

    /// <summary>„Widerrufen“ fragt nach; die Anzeige meldet sich danach bei ihrer nächsten Anfrage ab.</summary>
    [RelayCommand]
    private async Task AskRevokeDisplayAsync(DisplayDeviceDto display)
    {
        var answer = await AskAsync(new ConfirmRequest("Anzeige widerrufen?",
            $"„{display.Name}“ meldet sich ab und zeigt wieder einen Code. Zum erneuten Anzeigen den Code hier wieder koppeln.", "Widerrufen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await api.DeleteAsync<object>($"api/displays/{display.Id}").ConfigureAwait(true);
        DisplayError = result.Error;
        if (result.Ok)
        {
            Displays.Remove(display);
            OnPropertyChanged(nameof(HasDisplays));
            DisplayText = $"„{display.Name}“ widerrufen.";
        }
    }

    private static TerminalChoice Choice(PaymentTerminalDto terminal, LocationDto location) =>
        new($"{terminal.Name} · {location.Name} · {CardTerminalDrivers.ProtocolName(terminal.Protocol)} {terminal.Address}", terminal);

    public IReadOnlyList<int> Widths { get; } = [42, 48, 32];

    [ObservableProperty]
    public partial string PrinterAddress { get; set; } = "";

    [ObservableProperty]
    public partial int PrinterWidth { get; set; } = 42;

    [ObservableProperty]
    public partial bool PrintAutomatically { get; set; } = true;

    [ObservableProperty]
    public partial TerminalChoice? Terminal { get; set; }

    [ObservableProperty]
    public partial string? PrinterText { get; private set; }

    [ObservableProperty]
    public partial string? SavedText { get; private set; }

    /// <summary>Läuft, wenn dieser PC ein Terminal ansteuert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAgent))]
    public partial TerminalAgent? Agent { get; private set; }

    public bool HasAgent => Agent is not null;

    /// <summary>Einstellungen laden und, falls hier ein Terminal gewählt ist, die Ansteuerung starten.</summary>
    public Task InitializeAsync() => _initialization ??= LoadAsync();

    private async Task LoadAsync()
    {
        var current = settings.Load();
        PrinterAddress = current.PrinterAddress ?? "";
        PrinterWidth = current.PrinterWidth;
        PrintAutomatically = current.PrintAutomatically;

        Terminals.Clear();
        Terminals.Add(new TerminalChoice("Keines (Karte am eigenständigen Gerät oder anderer PC)", null));
        var locations = await api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
        Locations.Clear();
        foreach (var location in locations.Value ?? [])
        {
            Locations.Add(location);
        }

        DeviceLocation ??= Locations.FirstOrDefault(l => l.Id == SharedDevice?.LocationId) ?? Locations.FirstOrDefault();
        NewTerminalLocation ??= DeviceLocation;
        NewTerminalProtocol ??= Protocols[0];
        DisplayLocation ??= DeviceLocation;
        await LoadDisplaysAsync().ConfigureAwait(true);
        foreach (var location in locations.Value ?? [])
        {
            var terminals = await api.GetAsync<List<PaymentTerminalDto>>($"api/pos/terminals?locationId={location.Id}").ConfigureAwait(true);
            foreach (var terminal in (terminals.Value ?? []).Where(t => t.Provider == TerminalProvider.Local))
            {
                Terminals.Add(Choice(terminal, location));
            }
        }

        Terminal = Terminals.FirstOrDefault(t => t.Terminal?.Id == current.TerminalId) ?? Terminals[0];
        await RestartAgentAsync(Terminal.Terminal).ConfigureAwait(true);
    }

    private async Task LoadDisplaysAsync()
    {
        Displays.Clear();
        if (!CanManageDisplays)
        {
            return;
        }

        foreach (var location in Locations)
        {
            var displays = await api.GetAsync<List<DisplayDeviceDto>>($"api/locations/{location.Id}/displays").ConfigureAwait(true);
            foreach (var display in displays.Value ?? [])
            {
                Displays.Add(display);
            }
        }

        OnPropertyChanged(nameof(HasDisplays));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var address = PrinterAddress.Trim();
        settings.Save(settings.Load() with
        {
            PrinterAddress = address.Length == 0 ? null : address,
            PrinterWidth = PrinterWidth,
            PrintAutomatically = PrintAutomatically,
            TerminalId = Terminal?.Terminal?.Id,
        });
        await RestartAgentAsync(Terminal?.Terminal).ConfigureAwait(true);
        SavedText = "Gespeichert für diesen PC.";
    }

    /// <summary>Kurzer Probedruck direkt an den Drucker, ohne Beleg und ohne Server.</summary>
    [RelayCommand]
    private async Task TestPrintAsync()
    {
        var address = PrinterAddress.Trim();
        if (address.Length == 0)
        {
            PrinterText = "Erst die Adresse des Druckers eintragen.";
            return;
        }

        PrinterText = "Druckt …";
        PrinterText = await _printer(address).PrintAsync(TestPage(), CancellationToken.None).ConfigureAwait(true) ?? "Probedruck geschickt.";
    }

    private async Task RestartAgentAsync(PaymentTerminalDto? terminal)
    {
        if (Agent is { } running)
        {
            if (running.TerminalId == terminal?.Id)
            {
                return;
            }

            Agent = null;
            await running.DisposeAsync().ConfigureAwait(true);
        }

        if (terminal is not null)
        {
            var driver = _driver?.Invoke(terminal)
                ?? CardTerminalDrivers.Create(terminal, Locations.FirstOrDefault(l => l.Id == terminal.LocationId)?.Currency ?? "EUR");
            Agent = new TerminalAgent(api, terminal.Id, driver);
            Agent.Start();
        }
    }

    /// <summary>ESC/POS: Initialisieren, zentriert, Text, Vorschub und Schnitt.</summary>
    private static byte[] TestPage()
    {
        List<byte> bytes = [0x1B, 0x40, 0x1B, 0x61, 0x01];
        bytes.AddRange(Encoding.Latin1.GetBytes("AISPA Studio\nProbedruck\n" + DateTime.Now.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) + "\n\n\n\n"));
        bytes.AddRange([0x1D, 0x56, 0x42, 0x00]);
        return [.. bytes];
    }

    public async ValueTask DisposeAsync()
    {
        if (Agent is { } agent)
        {
            Agent = null;
            await agent.DisposeAsync().ConfigureAwait(true);
        }

        GC.SuppressFinalize(this);
    }
}
