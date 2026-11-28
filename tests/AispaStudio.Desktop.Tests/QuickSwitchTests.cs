using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Schnellwechsel per PIN am Empfangsgerät (ADR 0042) in der Windows-App.</summary>
public sealed class QuickSwitchTests
{
    private sealed class MovingTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = TestData.At(10);

        public override DateTimeOffset GetUtcNow() => Now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    internal static readonly SharedDeviceDto Device = new(Guid.NewGuid(), "Empfang links", TestData.LocationId, "Wien", TestData.At(8), 5);

    internal static readonly List<QuickSwitchPerson> People =
    [
        new("u1", "Rita", StaffRole.Reception),
        new("u2", "Nina Kovač", StaffRole.Reception),
        new("u3", "Katrin Aigner", StaffRole.Manager),
    ];

    private static StubApi Api() => new StubApi()
        .On("GET /api/system/status", new SystemStatus("1", "Test", DateTimeOffset.UtcNow))
        .On("GET /api/auth/quick-switch", new QuickSwitchState(Device, People))
        .On("GET /api/locations", new List<LocationDto>());

    [Fact]
    public async Task Am_Empfangsgeraet_Name_waehlen_und_PIN_eingeben()
    {
        var api = Api().On("POST /api/auth/quick-switch", null, HttpStatusCode.Unauthorized);
        var main = new MainViewModel(api.Client());
        await main.StartAsync();

        var login = Assert.IsType<LoginViewModel>(main.Content);
        Assert.True(login.IsQuickSwitch);
        Assert.False(login.ShowCodeForm);
        Assert.Equal("Empfangsgerät „Empfang links“ · Wien", login.DeviceText);
        Assert.Equal(3, login.People.Count);

        login.ChoosePersonCommand.Execute(login.People[0]);
        Assert.True(login.People[0].IsSelected);
        login.Pin = "12a3";
        Assert.Equal("123", login.Pin);
        Assert.False(login.QuickSwitchCommand.CanExecute(null));
        login.Pin = "1234";
        await login.QuickSwitchCommand.ExecuteAsync(null);
        Assert.Contains("PIN falsch", login.Error, StringComparison.Ordinal);
        Assert.Equal("", login.Pin);

        api.On("POST /api/auth/quick-switch", TestData.Reception()).On("GET /api/auth/me", TestData.Reception());
        login.Pin = "1234";
        await login.QuickSwitchCommand.ExecuteAsync(null);
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        Assert.Equal("Person wechseln", shell.SignOutText);
        Assert.Contains("{\"userId\":\"u1\",\"pin\":\"1234\"}", api.Calls.Last(c => c.Route == "POST /api/auth/quick-switch").Body, StringComparison.Ordinal);

        // „Anderes Konto“ klappt die Anmeldung mit Code auf.
        login.RequestCodeCommand.Execute(null);
        Assert.True(login.ShowCodeForm);
    }

    [Fact]
    public async Task Ohne_Eingabe_warnt_die_App_und_meldet_dann_ab()
    {
        var time = new MovingTime();
        var api = Api().On("GET /api/auth/me", TestData.Reception()).On("POST /api/auth/logout", null);
        var main = new MainViewModel(api.Client(), time: time);
        await main.StartAsync();
        Assert.IsType<ShellViewModel>(main.Content);
        Assert.Equal(Device, main.Device);

        time.Now += TimeSpan.FromSeconds(4 * 60 + 40);
        await main.TickAsync();
        Assert.Equal(20, main.IdleSecondsLeft);
        Assert.Equal("Ohne Eingabe wird in 20 Sekunden abgemeldet.", main.IdleText);

        main.KeepWorkingCommand.Execute(null);
        Assert.False(main.IsIdleWarning);
        time.Now += TimeSpan.FromMinutes(4);
        await main.TickAsync();
        Assert.IsType<ShellViewModel>(main.Content);

        time.Now += TimeSpan.FromMinutes(1);
        await main.TickAsync();
        Assert.Contains(api.Calls, c => c.Route == "POST /api/auth/logout");
        Assert.True(Assert.IsType<LoginViewModel>(main.Content).IsQuickSwitch);
    }

    [Fact]
    public async Task Nur_die_bestaetigte_Geraetevorgabe_bleibt_beim_Abmelden_und_bis_zum_Widerruf_erhalten()
    {
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store);
        var server = Api().On("POST /api/auth/logout", null);
        var api = server.Client(cache);
        await api.GetAsync<QuickSwitchState>("api/auth/quick-switch", TestContext.Current.CancellationToken);
        Assert.Equal(Device, store.Load().SharedDevice);
        Assert.DoesNotContain("api/auth/quick-switch", store.Load().Responses.Keys);

        await api.SignOutAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Device, store.Load().SharedDevice);
        server.Down = true;
        Assert.False((await api.RevokeDeviceAsync(TestContext.Current.CancellationToken)).Ok);
        Assert.Equal(Device, store.Load().SharedDevice);
        server.Down = false;
        server.On("DELETE /api/auth/shared-device", null, HttpStatusCode.NoContent);
        Assert.True((await api.RevokeDeviceAsync(TestContext.Current.CancellationToken)).Ok);
        Assert.Null(store.Load().SharedDevice);
    }

    [Fact]
    public async Task Ein_gemerktes_Empfangsgeraet_ohne_lokale_Leerlaufvorgabe_braucht_den_Server()
    {
        var session = new MemorySessionStore();
        session.Save("test-session");
        var cookie = new MemorySessionStore();
        cookie.Save("test-device");
        var cache = new OfflineCache(new MemoryOfflineStore());
        var server = Api().On("GET /api/auth/me", TestData.Reception());
        var api = new ApiClient(new Uri("http://localhost/"), session, server, cache, deviceStore: cookie);
        Assert.NotNull((await api.GetAsync<CurrentUser>("api/auth/me", TestContext.Current.CancellationToken)).Value);
        server.Down = true;
        var main = new MainViewModel(api);

        await main.StartAsync();

        Assert.Null(main.Content);
        Assert.False(main.IsSignedIn);
        Assert.Contains("Leerlaufzeit", main.StatusText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Fehler_beim_Pruefen_des_Empfangsgeraets_nennt_den_HTTP_Status(HttpStatusCode status)
    {
        var session = new MemorySessionStore();
        session.Save("test-session");
        var device = new MemorySessionStore();
        device.Save("test-device");
        var server = Api().On("GET /api/auth/me", TestData.Reception()).On("GET /api/auth/quick-switch", null, status);
        var api = new ApiClient(new Uri("http://localhost/"), session, server, deviceStore: device);
        var main = new MainViewModel(api);
        await main.StartAsync();
        Assert.Null(main.Content);
        Assert.False(main.CanShowContent);
        Assert.True(main.IsConnected);
        Assert.Contains($"HTTP {(int)status}", main.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Empfangsgeraet_meldet_die_App_nicht_ab()
    {
        var time = new MovingTime();
        var api = Api().On("GET /api/auth/me", TestData.Reception()).On("GET /api/auth/quick-switch", new QuickSwitchState(null, []));
        var main = new MainViewModel(api.Client(), time: time);
        await main.StartAsync();

        time.Now += TimeSpan.FromHours(3);
        await main.TickAsync();

        var shell = Assert.IsType<ShellViewModel>(main.Content);
        Assert.Equal("Abmelden", shell.SignOutText);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/auth/logout");
    }

    [Fact]
    public async Task Eigene_PIN_festlegen()
    {
        var api = Api().On("PUT /api/auth/pin", null, HttpStatusCode.NoContent);
        var shell = new ShellViewModel(api.Client(), TestData.Reception(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()),
            "", () => Task.CompletedTask);

        string? focus = null;
        shell.FocusRequested += field => focus = field;
        shell.PinInput = "12";
        // Zu kurz ist „Speichern“ aktiv; der Klick meldet den Fehler am Feld, setzt den Fokus dorthin und sendet nichts.
        Assert.True(shell.SavePinCommand.CanExecute(null));
        await shell.SavePinCommand.ExecuteAsync(null);
        Assert.Equal(("Bitte eine PIN mit mindestens 4 Ziffern eintragen.", ShellViewModel.PinField), (shell.PinError, focus));
        Assert.DoesNotContain(api.Calls, c => c.Route == "PUT /api/auth/pin");
        shell.PinInput = "4711";
        Assert.Null(shell.PinError);
        await shell.SavePinCommand.ExecuteAsync(null);

        Assert.True(shell.HasPin);
        Assert.Equal("", shell.PinInput);
        Assert.StartsWith("PIN festgelegt", shell.PinText, StringComparison.Ordinal);
        Assert.Contains("\"pin\":\"4711\"", api.Calls.Last().Body, StringComparison.Ordinal);
        Assert.Equal("Neue PIN", shell.PinLabel);
        Assert.Equal("4 bis 8 Ziffern.", shell.PinHelp);
        shell.Dispose();
    }

    [Fact]
    public async Task Eigene_PIN_entfernen()
    {
        var api = Api().On("PUT /api/auth/pin", null, HttpStatusCode.NoContent).On("DELETE /api/auth/pin", null, HttpStatusCode.NoContent);
        var shell = new ShellViewModel(api.Client(), TestData.Reception(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()),
            "", () => Task.CompletedTask);
        var before = shell.RemovePinCommand.CanExecute(null);
        shell.PinInput = "4711";
        await shell.SavePinCommand.ExecuteAsync(null);

        await shell.RemovePinCommand.ExecuteAsync(null);

        Assert.False(before);
        Assert.False(shell.HasPin);
        Assert.False(shell.RemovePinCommand.CanExecute(null));
        Assert.Equal("DELETE /api/auth/pin", api.Calls.Last().Route);
        Assert.StartsWith("PIN entfernt", shell.PinText, StringComparison.Ordinal);
        Assert.Equal("PIN", shell.PinLabel);
        Assert.Equal("4 bis 8 Ziffern.", shell.PinHelp);
        shell.Dispose();
    }

    [Fact]
    public async Task Leitung_meldet_den_PC_als_Empfangsgeraet_an_und_wieder_ab()
    {
        var api = Api()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On("POST /api/auth/shared-device", Device)
            .On("DELETE /api/auth/shared-device", null, HttpStatusCode.NoContent);
        SharedDeviceDto? changed = Device;
        var devices = new DevicesViewModel(api.Client(), new MemorySettingsStore()) { CanManageDevice = true, DeviceChanged = d => changed = d };
        await devices.InitializeAsync();

        Assert.Equal(TestData.LocationId, devices.DeviceLocation?.Id);
        devices.DeviceName = "Empfang links";
        await devices.RegisterDeviceCommand.ExecuteAsync(null);
        Assert.True(devices.IsSharedDevice);
        Assert.Equal(Device, changed);
        Assert.Contains("\"idleMinutes\":5", api.Calls.Last(c => c.Route == "POST /api/auth/shared-device").Body, StringComparison.Ordinal);

        await devices.RevokeDeviceCommand.ExecuteAsync(null);
        Assert.False(devices.IsSharedDevice);
        Assert.Null(changed);
        await devices.DisposeAsync();
    }

    [Fact]
    public async Task Ohne_Recht_kein_Anmelden_als_Empfangsgeraet()
    {
        var devices = new DevicesViewModel(Api().Client(), new MemorySettingsStore());
        await devices.InitializeAsync();
        Assert.False(devices.RegisterDeviceCommand.CanExecute(null));
        await devices.DisposeAsync();
    }
}
