using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Einzelplatz-Betrieb (ADR 0041): Anmeldung mit Benutzername und Passwort, Startpasswort gleich ändern.</summary>
public sealed class StandaloneLoginTests
{
    private static readonly SystemModules Standalone = new(true, false, false, false, false, false, false, false);

    private static CurrentUser Rita(bool mustChange) => new("u1", "", "Rita Steiner", 0,
        new CurrentAccess(Guid.NewGuid(), "Spa am See", StaffRole.Reception, true, []), null,
        [AccessRight.ViewCalendar, AccessRight.Book], MustChangePassword: mustChange, UserName: "rita");

    [Fact]
    public async Task Einzelplatz_zeigt_Benutzername_und_Passwort_statt_Code()
    {
        var api = new StubApi()
            .On("GET /api/system/modules", Standalone)
            .On("GET /api/auth/standalone/setup", new StandaloneSetupState(false))
            .On("POST /api/auth/standalone/login", Rita(false));
        CurrentUser? signedIn = null;
        var login = new LoginViewModel(api.Client(), user => { signedIn = user; return Task.CompletedTask; });
        await login.LoadAsync();

        Assert.True(login.IsStandalone);
        Assert.True(login.ShowPasswordLogin);
        Assert.False(login.SignInWithPasswordCommand.CanExecute(null));
        login.UserName = "rita";
        login.Password = "sommer-2026";
        await login.SignInWithPasswordCommand.ExecuteAsync(null);

        Assert.Equal("Rita Steiner", signedIn?.DisplayName);
    }

    [Fact]
    public async Task Passwort_vergessen_mit_dem_Wiederherstellungsschluessel()
    {
        var api = new StubApi()
            .On("GET /api/system/modules", Standalone)
            .On("GET /api/auth/standalone/setup", new StandaloneSetupState(false))
            .On("POST /api/auth/standalone/recover", Rita(false));
        CurrentUser? signedIn = null;
        var login = new LoginViewModel(api.Client(), user => { signedIn = user; return Task.CompletedTask; });
        await login.LoadAsync();

        login.ToggleRecoveryCommand.Execute(null);
        Assert.True(login.ShowRecovery);
        Assert.False(login.ShowPasswordLogin);
        login.UserName = "rita";
        login.RecoveryCode = "K7QM-2XRT";
        login.NewPassword = "winter-2026";
        login.NewPasswordRepeat = "winter-2025";
        Assert.False(login.RecoverPasswordCommand.CanExecute(null));

        login.RecoveryCode = "k7qm 2xrt aaaa bbbb cccc dddd";
        await login.RecoverPasswordCommand.ExecuteAsync(null);
        Assert.Equal("Die beiden neuen Passwörter stimmen nicht überein.", login.Error);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/recover", StringComparison.Ordinal));

        login.NewPasswordRepeat = "winter-2026";
        await login.RecoverPasswordCommand.ExecuteAsync(null);

        Assert.Equal("Rita Steiner", signedIn?.DisplayName);
        Assert.Contains("\"recoveryCode\":\"k7qm 2xrt aaaa bbbb cccc dddd\"", api.Calls.Single(c => c.Route.EndsWith("/recover", StringComparison.Ordinal)).Body,
            StringComparison.Ordinal);
        Assert.False(login.ShowRecovery);
        Assert.Equal("", login.RecoveryCode);
    }

    [Fact]
    public async Task Falsches_Passwort_wird_erklaert_und_Einrichtung_angezeigt()
    {
        var api = new StubApi()
            .On("GET /api/system/modules", Standalone)
            .On("GET /api/auth/standalone/setup", new StandaloneSetupState(true))
            .On("POST /api/auth/standalone/login", null, HttpStatusCode.Unauthorized)
            .OnHtml("GET /anmelden/");
        var login = new LoginViewModel(api.Client(), _ => Task.CompletedTask);
        await login.LoadAsync();

        Assert.True(login.SetupRequired);
        Assert.False(login.ShowPasswordLogin);
        Assert.Contains("Die Anmeldung im Browser: http://localhost/anmelden/", login.SetupHint, StringComparison.Ordinal);

        // Liefert der Einzelplatz keine Web-Oberfläche aus, nennt der Hinweis keine Adresse, die ins Leere führt.
        var withoutWeb = new LoginViewModel(new StubApi().On("GET /api/system/modules", Standalone)
            .On("GET /api/auth/standalone/setup", new StandaloneSetupState(true)).Client(), _ => Task.CompletedTask);
        await withoutWeb.LoadAsync();
        Assert.DoesNotContain("http", withoutWeb.SetupHint, StringComparison.Ordinal);

        login.UserName = "rita";
        login.Password = "falsch";
        await login.SignInWithPasswordCommand.ExecuteAsync(null);
        Assert.Equal("Benutzername oder Passwort stimmt nicht.", login.Error);
    }

    [Fact]
    public async Task Neuer_PC_wartet_auf_das_bestehende_Spa_und_zeigt_dann_die_Anmeldung()
    {
        var api = new StubApi()
            .On("GET /api/system/modules", Standalone)
            .On("GET /api/auth/standalone/setup", new StandaloneSetupState(true, true, "Mit diesem Schlüsselcode gibt es in der zentralen Datenbank kein Spa."));
        var login = new LoginViewModel(api.Client(), _ => Task.CompletedTask) { JoinPoll = TimeSpan.Zero };
        await login.LoadAsync();

        Assert.True(login.Joining);
        Assert.False(login.ShowSetupForm);
        Assert.False(login.ShowPasswordLogin);
        Assert.StartsWith("Mit diesem Schlüsselcode", login.JoinError, StringComparison.Ordinal);

        api.On("GET /api/auth/standalone/setup", new StandaloneSetupState(false));
        await login.RefreshSetupCommand.ExecuteAsync(null);
        Assert.False(login.Joining);
        Assert.Null(login.JoinError);
        Assert.True(login.ShowPasswordLogin);
    }

    [Fact]
    public async Task Startpasswort_muss_vor_dem_Kalender_geaendert_werden()
    {
        var api = new StubApi()
            .On("GET /api/system/modules", Standalone)
            .On("GET /api/auth/standalone/setup", new StandaloneSetupState(false))
            .On("POST /api/auth/standalone/login", Rita(true))
            .On("POST /api/auth/standalone/password", Rita(false));
        CurrentUser? signedIn = null;
        var login = new LoginViewModel(api.Client(), user => { signedIn = user; return Task.CompletedTask; });
        await login.LoadAsync();
        login.UserName = "rita";
        login.Password = "start-1234";
        await login.SignInWithPasswordCommand.ExecuteAsync(null);

        Assert.Null(signedIn);
        Assert.True(login.ChangePasswordRequired);
        Assert.False(login.ShowPasswordLogin);

        login.NewPassword = "neues-passwort";
        login.NewPasswordRepeat = "anders";
        await login.ChangePasswordCommand.ExecuteAsync(null);
        Assert.Equal("Die beiden neuen Passwörter stimmen nicht überein.", login.Error);

        login.NewPasswordRepeat = "neues-passwort";
        await login.ChangePasswordCommand.ExecuteAsync(null);

        Assert.False(login.ChangePasswordRequired);
        Assert.Equal("Rita Steiner", signedIn?.DisplayName);
        Assert.Contains(api.Calls, c => c.Route == "POST /api/auth/standalone/password" && c.Body!.Contains("start-1234", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Einrichtung_beim_ersten_Start_legt_Spa_an_und_meldet_an()
    {
        var owner = Rita(false) with { DisplayName = "Eva Huber", UserName = "eva" };
        var api = new StubApi()
            .On("GET /api/system/modules", Standalone)
            .On("GET /api/auth/standalone/setup", new StandaloneSetupState(true))
            .On("POST /api/auth/standalone/setup", owner);
        CurrentUser? signedIn = null;
        var login = new LoginViewModel(api.Client(), user => { signedIn = user; return Task.CompletedTask; });
        await login.LoadAsync();

        Assert.True(login.SetupRequired);
        Assert.False(login.CompleteSetupCommand.CanExecute(null));
        login.SpaName = "Spa am See";
        login.Country = LoginViewModel.Countries.Single(c => c.Code == "DE");
        login.LocationName = "Gmunden";
        login.OwnerName = "Eva Huber";
        login.UserName = "eva";
        login.Password = "seeblick-2026";
        login.NewPasswordRepeat = "seeblick-2025";
        Assert.True(login.CompleteSetupCommand.CanExecute(null));

        await login.CompleteSetupCommand.ExecuteAsync(null);
        Assert.Equal("Die beiden Passwörter stimmen nicht überein.", login.Error);
        Assert.Null(signedIn);

        login.NewPasswordRepeat = "seeblick-2026";
        await login.CompleteSetupCommand.ExecuteAsync(null);

        Assert.Equal("Eva Huber", signedIn?.DisplayName);
        Assert.False(login.SetupRequired);
        var body = api.Calls.Single(c => c.Route == "POST /api/auth/standalone/setup").Body!;
        Assert.Contains("\"country\":\"DE\"", body, StringComparison.Ordinal);
        Assert.Contains("\"locationName\":\"Gmunden\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Server_Betrieb_zeigt_den_Einmal_Code()
    {
        var api = new StubApi().On("GET /api/system/modules", new SystemModules(false, true, true, true, true, true, true, true));
        var login = new LoginViewModel(api.Client(), _ => Task.CompletedTask);
        await login.LoadAsync();

        Assert.True(login.IsServer);
        Assert.False(login.ShowPasswordLogin);
    }
}
