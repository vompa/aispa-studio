using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task Zeigt_Version_wenn_Server_antwortet()
    {
        var api = new StubApi().On("GET /api/system/status", new SystemStatus("1.2.3+abc", "Development", DateTimeOffset.UtcNow));
        var vm = new MainViewModel(api.Client());

        await vm.CheckStatusCommand.ExecuteAsync(null);

        Assert.True(vm.IsConnected);
        Assert.Equal("Verbunden · Version 1.2.3 · Development", vm.StatusText);
    }

    [Fact]
    public async Task Meldet_wenn_Server_nicht_erreichbar()
    {
        // Ein Stub statt einer geschlossenen Adresse: unter Windows dauert eine verweigerte Verbindung zwei Sekunden je Adresse und endet im Zeitlimit des Starts.
        var vm = new MainViewModel(new StubApi { Down = true }.Client()) { ProbeWindow = TimeSpan.Zero };

        await vm.CheckStatusCommand.ExecuteAsync(null);

        Assert.False(vm.IsConnected);
        Assert.StartsWith("Server nicht erreichbar", vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Anmeldung_startet_die_Anmeldung_mit_Anmeldung_der_Kalender_in_der_Navigation()
    {
        var api = new StubApi().On("GET /api/system/status", new SystemStatus("1", "Test", DateTimeOffset.UtcNow));
        var signedOut = new MainViewModel(api.Client());
        await signedOut.StartAsync();

        api.On("GET /api/auth/me", TestData.Reception()).On("GET /api/locations", new List<LocationDto>());
        var signedIn = new MainViewModel(api.Client());
        await signedIn.StartAsync();

        Assert.IsType<LoginViewModel>(signedOut.Content);
        Assert.IsType<CalendarViewModel>(Assert.IsType<ShellViewModel>(signedIn.Content).SelectedPage?.Page);
        Assert.Equal("Rita · Test-Spa", signedIn.UserText);
    }

    [Fact]
    public async Task Anmeldung_mit_Code_oeffnet_den_Kalender_und_erklaert_falsche_Codes()
    {
        var api = new StubApi().On("POST /api/auth/device-code/redeem", null, HttpStatusCode.Unauthorized);
        CurrentUser? signedIn = null;
        var login = new LoginViewModel(api.Client(), user =>
        {
            signedIn = user;
            return Task.CompletedTask;
        });

        login.Code = "abcd efg";
        var tooShort = login.SignInCommand.CanExecute(null);
        login.Code = "abcd-efgh";
        await login.SignInCommand.ExecuteAsync(null);
        var error = login.Error;
        api.On("POST /api/auth/device-code/redeem", TestData.Reception());
        await login.SignInCommand.ExecuteAsync(null);

        Assert.False(tooShort);
        Assert.Contains("Einen neuen Code gibt es im Browser auf der Seite „Anmelden“ des Spas mit „Code für die Windows-App“.", error, StringComparison.Ordinal);
        Assert.Equal("Rita", signedIn?.DisplayName);
        Assert.Contains("\"code\":\"abcd-efgh\"", api.Calls[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_Seite_Anmelden_steht_unter_der_Web_Adresse_des_Servers_und_nie_unter_einer_API_ohne_Web()
    {
        // Slice 103 (Review): im Serverbetrieb liefert die Adresse der API keine Seite /anmelden/ aus.
        var named = new StubApi().On("GET /api/system/team-web", new TeamWebInfo("http://localhost:5173/"));
        var withWeb = new LoginViewModel(named.Client(), _ => Task.CompletedTask);
        await withWeb.LoadAsync();
        Assert.Equal(new Uri("http://localhost:5173/anmelden/"), withWeb.SignInPage);
        Assert.Equal("Im Browser http://localhost:5173/anmelden/ öffnen und anmelden.", withWeb.CodeStep);

        var apiOnly = new LoginViewModel(new StubApi().Client(), _ => Task.CompletedTask);
        await apiOnly.LoadAsync();
        Assert.Null(apiOnly.SignInPage);
        Assert.Equal("Im Browser die Seite „Anmelden“ des Spas öffnen und anmelden.", apiOnly.CodeStep);

        var ownWeb = new LoginViewModel(new StubApi().OnHtml("GET /anmelden/").Client(), _ => Task.CompletedTask);
        await ownWeb.LoadAsync();
        Assert.Equal(new Uri("http://localhost/anmelden/"), ownWeb.SignInPage);
    }

    [Fact]
    public async Task Testkonten_nur_wenn_die_API_sie_anbietet()
    {
        var production = new StubApi().On("GET /api/system/status", new SystemStatus("1", "Production", DateTimeOffset.UtcNow));
        var withoutAccounts = new MainViewModel(production.Client());
        await withoutAccounts.StartAsync();

        var development = new StubApi()
            .On("GET /api/system/status", new SystemStatus("1", "Development", DateTimeOffset.UtcNow))
            .On("GET /api/auth/dev/accounts", new List<DevAccount>
            {
                new("inhaberin@demo.aispa.test", "Katrin Aigner", StaffRole.Owner, ["alle Standorte"]),
                new("empfang.wien@demo.aispa.test", "Nina Kovač", StaffRole.Reception, ["Wien Innere Stadt"]),
            })
            .On("POST /api/auth/dev/login", TestData.Reception())
            .On("GET /api/locations", new List<LocationDto>());
        var withAccounts = new MainViewModel(development.Client());
        await withAccounts.StartAsync();
        var login = Assert.IsType<LoginViewModel>(withAccounts.Content);
        await login.SignInAsTestAccountCommand.ExecuteAsync(null);

        Assert.False(Assert.IsType<LoginViewModel>(withoutAccounts.Content).HasTestAccounts);
        Assert.True(login.HasTestAccounts);
        Assert.Equal("inhaberin@demo.aispa.test", login.TestAccount?.Email);
        Assert.Contains("\"persistent\":true", development.Calls.Single(c => c.Route == "POST /api/auth/dev/login").Body, StringComparison.Ordinal);
        Assert.IsType<ShellViewModel>(withAccounts.Content);
    }
}
