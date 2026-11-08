using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

public sealed class LoginStartupTests
{
    private static readonly List<DevAccount> Accounts =
    [
        new("inhaberin@demo.aispa.test", "Katrin Aigner", StaffRole.Owner, ["alle Standorte"]),
        new("empfang.wien@demo.aispa.test", "Nina Kovač", StaffRole.Reception, ["Wien Innere Stadt"]),
    ];

    private static StubApi Api(string environment, StartupPhase phase, string text, List<DevAccount>? accounts = null) => new StubApi()
        .On("GET /api/system/status", new SystemStatus("1", environment, DateTimeOffset.UtcNow, new StartupInfo(phase, text)))
        .On("GET /api/auth/dev/accounts", accounts ?? []);

    private static LoginViewModel Login(StubApi api, TimeSpan poll) => new(api.Client(), _ => Task.CompletedTask) { StartupPoll = poll };

    [Fact]
    public async Task Waehrend_das_Beispiel_Spa_eingespielt_wird_steht_der_Stand_da_und_die_Testkonten_kommen_danach_von_selbst()
    {
        var api = Api("Development", StartupPhase.Seeding, "Beispiel-Spa wird eingerichtet (seit 12 s)");
        var login = Login(api, TimeSpan.Zero);

        await login.LoadAsync();

        Assert.Equal("Beispiel-Spa wird eingerichtet (seit 12 s)", login.StartupNotice);
        Assert.True(login.HasStartupNotice);
        Assert.False(login.HasTestAccounts);

        api.On("GET /api/system/status", new SystemStatus("1", "Development", DateTimeOffset.UtcNow, new StartupInfo(StartupPhase.Ready, "Bereit")))
            .On("GET /api/auth/dev/accounts", Accounts);
        await login.RefreshStartupAsync();

        Assert.Null(login.StartupNotice);
        Assert.True(login.HasTestAccounts);
        Assert.Equal("inhaberin@demo.aispa.test", login.TestAccount?.Email);
        Assert.Equal(2, login.TestAccounts.Count);
    }

    [Fact]
    public async Task Fehler_beim_Einspielen_steht_als_Text_da()
    {
        var login = Login(Api("Development", StartupPhase.Failed, "Beispieldaten unvollständig – Datenbank zurücksetzen"), TimeSpan.Zero);

        await login.LoadAsync();

        Assert.Equal("Beispieldaten unvollständig – Datenbank zurücksetzen", login.StartupNotice);
        Assert.False(login.HasTestAccounts);
    }

    [Fact]
    public async Task Ausserhalb_der_Entwicklung_und_ohne_Startzustand_gibt_es_keinen_Hinweis()
    {
        var production = Login(Api("Production", StartupPhase.Seeding, "Beispiel-Spa wird eingerichtet (seit 1 s)"), TimeSpan.Zero);
        var older = Login(new StubApi().On("GET /api/system/status", new SystemStatus("1", "Development", DateTimeOffset.UtcNow))
            .On("GET /api/auth/dev/accounts", Accounts), TimeSpan.Zero);

        await production.LoadAsync();
        await older.LoadAsync();

        Assert.False(production.HasStartupNotice);
        Assert.False(older.HasStartupNotice);
        Assert.True(older.HasTestAccounts);
    }

    [Theory]
    [InlineData(StaffRole.Reception, "empfang.wien@demo.aispa.test")]
    [InlineData(StaffRole.Owner, "inhaberin@demo.aispa.test")]
    public async Task Schnellanmeldung_als_Rezeption_oder_Inhaberin_mit_einem_Klick(StaffRole role, string email)
    {
        var api = Api("Development", StartupPhase.Ready, "Bereit", Accounts).On("POST /api/auth/dev/login", TestData.Reception());
        var signedIn = new List<CurrentUser>();
        var login = new LoginViewModel(api.Client(), user =>
        {
            signedIn.Add(user);
            return Task.CompletedTask;
        });
        await login.LoadAsync();

        Assert.True(login.HasReceptionAccount);
        Assert.True(login.HasOwnerAccount);
        await (role == StaffRole.Owner ? login.SignInAsOwnerCommand : login.SignInAsReceptionCommand).ExecuteAsync(null);

        Assert.Contains($"\"email\":\"{email}\"", api.Calls.Single(c => c.Route == "POST /api/auth/dev/login").Body, StringComparison.Ordinal);
        Assert.Single(signedIn);
    }

    [Fact]
    public async Task Ohne_Konto_der_Rolle_gibt_es_keinen_Schnellknopf()
    {
        var login = Login(Api("Development", StartupPhase.Ready, "Bereit", [Accounts[0]]), TimeSpan.Zero);

        await login.LoadAsync();

        Assert.True(login.HasOwnerAccount);
        Assert.False(login.HasReceptionAccount);
    }

    [Fact]
    public async Task Die_Anmeldung_fragt_von_selbst_nach_und_laedt_die_Testkonten_sobald_alles_bereit_ist()
    {
        var api = Api("Development", StartupPhase.Seeding, "Beispiel-Spa wird eingerichtet (seit 1 s)");
        var login = Login(api, TimeSpan.FromMilliseconds(1));
        await login.LoadAsync();
        Assert.True(login.HasStartupNotice);

        api.On("GET /api/system/status", new SystemStatus("1", "Development", DateTimeOffset.UtcNow, new StartupInfo(StartupPhase.Ready, "Bereit")))
            .On("GET /api/auth/dev/accounts", Accounts);
        for (var i = 0; i < 500 && !login.HasTestAccounts; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(login.HasTestAccounts);
        Assert.False(login.HasStartupNotice);
    }
}
