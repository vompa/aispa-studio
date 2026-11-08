using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Person in der Auswahl „Wer meldet sich an?“ am Empfangsgerät.</summary>
public sealed partial class QuickSwitchChoice(QuickSwitchPerson person) : ObservableObject
{
    public QuickSwitchPerson Person { get; } = person;

    public string Name => Person.DisplayName;

    public string Role => ShellViewModel.RoleText(Person.Role);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Anmeldung. Im Server-Betrieb mit dem Einmal-Code, den man im Browser auf der Seite „Anmelden“ des Servers (<c>/anmelden/</c>) unter „Code für die Windows-App“ bekommt;
/// im Einzelplatz-Betrieb mit Benutzername und Passwort (ADR 0041), ein Startpasswort wird gleich hier geändert.
/// Ist dieser PC ein Empfangsgerät (ADR 0042), wählt man stattdessen seinen Namen und gibt die PIN ein.
/// Läuft die API in der Entwicklungsumgebung, gibt es zusätzlich die Testkonten des Beispiel-Spas zur Auswahl.
/// </summary>
public partial class LoginViewModel(IWorkstationApi api, Func<CurrentUser, Task> signedIn) : ViewModelBase
{
    /// <summary>Einzelplatz: statt einzurichten aus einer Sicherung wiederherstellen (ADR 0041, Schritt 5).</summary>
    public RestoreViewModel? Restore { get; init; }

    /// <summary>Einzelplatz: statt einzurichten mit einem bestehenden Spa verbinden (zentrale Datenbank und Schlüsselcode).</summary>
    public JoinViewModel? Join { get; init; }

    /// <summary>Abstand, in dem der Startbildschirm beim Verbinden nachsieht, ob das Spa schon da ist; <c>Zero</c> schaltet das ab (Tests).</summary>
    public TimeSpan JoinPoll { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Abstand, in dem die Anmeldung in der Entwicklung nachsieht, ob das Beispiel-Spa fertig eingespielt ist; <c>Zero</c> schaltet das ab (Tests).</summary>
    public TimeSpan StartupPoll { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Gesetzt, wenn dieser PC als Empfangsgerät angemeldet ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsQuickSwitch), nameof(DeviceText), nameof(ShowCodeForm), nameof(ShowPasswordLogin))]
    public partial SharedDeviceDto? Device { get; private set; }

    public bool IsQuickSwitch => Device is not null;

    public string DeviceText => Device is { } d ? $"Empfangsgerät „{d.Name}“ · {d.LocationName}" : "";

    public ObservableCollection<QuickSwitchChoice> People { get; } = [];

    public bool HasPeople => People.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPerson), nameof(PinTitle))]
    [NotifyCanExecuteChangedFor(nameof(QuickSwitchCommand))]
    public partial QuickSwitchChoice? Person { get; private set; }

    public bool HasPerson => Person is not null;

    public string PinTitle => Person is { } p ? $"PIN für {p.Name}" : "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuickSwitchCommand))]
    public partial string Pin { get; set; } = "";

    /// <summary>Am Empfangsgerät ist die Anmeldung mit Code eingeklappt; „Mit Code anmelden“ klappt sie auf.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCodeForm), nameof(ShowPasswordLogin))]
    public partial bool CodeRequested { get; set; }

    /// <summary>Anmeldung mit Code: nur im Server-Betrieb, am Empfangsgerät erst nach „Anderes Konto“.</summary>
    public bool ShowCodeForm => IsServer && (!IsQuickSwitch || CodeRequested);

    /// <summary>Wo es die Seite „Anmelden“ gibt (Slice 103); ohne bekannte Web-Adresse <c>null</c>, dann nennen die Texte die Seite ohne Adresse.</summary>
    private readonly ServerWeb _web = new(api);

    /// <summary>Seite „Anmelden“ im Browser mit „Code für die Windows-App“; geladen in <see cref="LoadAsync"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CodeStep), nameof(SetupHint))]
    public partial Uri? SignInPage { get; private set; }

    /// <summary>Erster Schritt zum Code: die Seite „Anmelden“ im Browser.</summary>
    public string CodeStep => SignInPage is null
        ? "Im Browser die Seite „Anmelden“ des Spas öffnen und anmelden."
        : $"Im Browser {SignInPage} öffnen und anmelden.";

    /// <summary>Weg aus „Der Code stimmt nicht“: wo es einen neuen Code gibt.</summary>
    private string NewCodeHint => SignInPage is null
        ? "Einen neuen Code gibt es im Browser auf der Seite „Anmelden“ des Spas mit „Code für die Windows-App“."
        : $"Einen neuen Code gibt es im Browser unter {SignInPage} mit „Code für die Windows-App“.";

    public string OtherAccountText => IsStandalone ? "Anderes Konto: mit Passwort anmelden" : "Anderes Konto: mit Code anmelden";

    /// <summary>Ob dieser PC ein Empfangsgerät ist und wer dort per PIN wechseln kann.</summary>
    public async Task LoadQuickSwitchAsync()
    {
        var result = await api.GetAsync<QuickSwitchState>("api/auth/quick-switch").ConfigureAwait(true);
        People.Clear();
        foreach (var person in result.Value?.People ?? [])
        {
            People.Add(new QuickSwitchChoice(person));
        }

        Device = result.Value?.Device;
        OnPropertyChanged(nameof(HasPeople));
    }

    [RelayCommand]
    private void ChoosePerson(QuickSwitchChoice choice)
    {
        foreach (var p in People)
        {
            p.IsSelected = p == choice;
        }

        Person = choice;
        Pin = "";
        Error = null;
    }

    partial void OnPinChanged(string value)
    {
        var digits = new string(value.Where(char.IsAsciiDigit).Take(8).ToArray());
        if (digits != value)
        {
            Pin = digits;
        }
    }

    private bool CanQuickSwitch => Person is not null && Pin.Length >= 4;

    [RelayCommand(CanExecute = nameof(CanQuickSwitch))]
    private async Task QuickSwitchAsync()
    {
        Error = null;
        var result = await api.QuickSwitchAsync(Person!.Person.UserId, Pin).ConfigureAwait(true);
        Pin = "";
        await CompleteAsync(result).ConfigureAwait(true);
    }

    [RelayCommand]
    private void RequestCode() => CodeRequested = !CodeRequested;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string Code { get; set; } = "";

    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Einzelplatz-Betrieb: Anmeldung mit Benutzername und Passwort statt Einmal-Code.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsServer), nameof(ShowPasswordLogin), nameof(ShowCodeForm), nameof(OtherAccountText), nameof(ShowRecovery))]
    public partial bool IsStandalone { get; private set; }

    public bool IsServer => !IsStandalone;

    /// <summary>Einzelplatz-Betrieb, noch kein Spa angelegt: Einrichtung gleich hier (oder im Browser über die lokale API).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPasswordLogin), nameof(ShowSetupForm), nameof(ShowRecovery))]
    public partial bool SetupRequired { get; private set; }

    /// <summary>Noch kein Spa, aber die zentrale Datenbank ist eingetragen: der erste Abgleich holt das bestehende Spa.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSetupForm))]
    public partial bool Joining { get; private set; }

    /// <summary>Warum das Verbinden zuletzt nicht geklappt hat, z. B. falscher Schlüsselcode.</summary>
    [ObservableProperty]
    public partial string? JoinError { get; private set; }

    public bool ShowSetupForm => SetupRequired && !Joining;

    public bool ShowPasswordLogin => IsStandalone && !SetupRequired && !ChangePasswordRequired && !RecoveryRequested
        && (!IsQuickSwitch || CodeRequested);

    /// <summary>Passwort vergessen ohne Leitung: mit dem Wiederherstellungsschlüssel aus der Sicherung ein neues Passwort setzen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPasswordLogin), nameof(ShowRecovery))]
    public partial bool RecoveryRequested { get; private set; }

    public bool ShowRecovery => IsStandalone && !SetupRequired && !ChangePasswordRequired && RecoveryRequested;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecoverPasswordCommand))]
    public partial string RecoveryCode { get; set; } = "";

    [RelayCommand]
    private void ToggleRecovery()
    {
        RecoveryRequested = !RecoveryRequested;
        Error = null;
        RecoveryCode = NewPassword = NewPasswordRepeat = "";
    }

    private bool CanRecoverPassword => UserName.Trim().Length > 0 && KeySeal.IsComplete(RecoveryCode) && NewPassword.Length >= 8
        && NewPasswordRepeat.Length > 0;

    [RelayCommand(CanExecute = nameof(CanRecoverPassword))]
    private async Task RecoverPasswordAsync()
    {
        Error = null;
        if (NewPassword != NewPasswordRepeat)
        {
            Error = "Die beiden neuen Passwörter stimmen nicht überein.";
            return;
        }

        var result = await api.RecoverPasswordAsync(UserName.Trim(), RecoveryCode, NewPassword).ConfigureAwait(true);
        if (result.Ok)
        {
            RecoveryRequested = false;
            RecoveryCode = NewPassword = NewPasswordRepeat = "";
        }

        await CompleteAsync(result).ConfigureAwait(true);
    }

    public string SetupHint => $"Dieser PC betreibt AISPA Studio allein, ohne Server. Legen Sie einmal das Spa und Ihr Inhaber-Konto an; das Team bekommt danach unter „Zugänge“ Benutzername und Startpasswort.{(SignInPage is null ? "" : $" Die Anmeldung im Browser: {SignInPage}")}";

    public static IReadOnlyList<CountryChoice> Countries { get; } =
        [new("AT", "Österreich"), new("DE", "Deutschland"), new("CH", "Schweiz")];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompleteSetupCommand))]
    public partial string SpaName { get; set; } = "";

    [ObservableProperty]
    public partial CountryChoice Country { get; set; } = Countries[0];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompleteSetupCommand))]
    public partial string LocationName { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompleteSetupCommand))]
    public partial string OwnerName { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInWithPasswordCommand), nameof(CompleteSetupCommand), nameof(RecoverPasswordCommand))]
    public partial string UserName { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInWithPasswordCommand), nameof(CompleteSetupCommand))]
    public partial string Password { get; set; } = "";

    /// <summary>Das Startpasswort der Leitung gilt noch; ohne eigenes Passwort geht es nicht weiter.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPasswordLogin), nameof(ShowRecovery))]
    public partial bool ChangePasswordRequired { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand), nameof(RecoverPasswordCommand))]
    public partial string NewPassword { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand), nameof(CompleteSetupCommand), nameof(RecoverPasswordCommand))]
    public partial string NewPasswordRepeat { get; set; } = "";

    /// <summary>Testkonten; bleibt leer, wenn die API nicht in der Entwicklungsumgebung läuft.</summary>
    public ObservableCollection<DevAccount> TestAccounts { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInAsTestAccountCommand))]
    public partial DevAccount? TestAccount { get; set; }

    public bool HasTestAccounts => TestAccounts.Count > 0;

    /// <summary>
    /// Entwicklung: solange die API noch migriert oder das Beispiel-Spa einspielt (oder das scheiterte), steht hier der Stand,
    /// z. B. „Beispiel-Spa wird eingerichtet (seit 12 s)“. Sonst leer.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStartupNotice))]
    public partial string? StartupNotice { get; private set; }

    public bool HasStartupNotice => StartupNotice is not null;

    private bool _signedIn;
    private bool _startupFailed;

    /// <summary>Betriebsart, Einrichtungsstand, Empfangsgerät und Testkonten laden.</summary>
    public async Task LoadAsync()
    {
        var modules = await api.GetAsync<SystemModules>("api/system/modules").ConfigureAwait(true);
        IsStandalone = modules.Value?.Standalone == true;
        SignInPage = await _web.SignInPageAsync().ConfigureAwait(true);
        if (IsStandalone)
        {
            await RefreshSetupAsync().ConfigureAwait(true);
            if (Joining && JoinPoll > TimeSpan.Zero)
            {
                _ = WaitForSpaAsync();
            }
        }

        await LoadQuickSwitchAsync().ConfigureAwait(true);
        await RefreshStartupAsync().ConfigureAwait(true);
        if (StartupNotice is not null && StartupPoll > TimeSpan.Zero)
        {
            _ = WaitForStartupAsync();
        }
    }

    /// <summary>
    /// Startzustand der API lesen (nur in der Entwicklung relevant). Ist alles bereit, werden die Testkonten geladen; vorher wären sie
    /// leer, weil das Beispiel-Spa noch eingespielt wird.
    /// </summary>
    public async Task RefreshStartupAsync()
    {
        var status = (await api.GetAsync<SystemStatus>("api/system/status").ConfigureAwait(true)).Value;
        var startup = status?.Environment == "Development" ? status.Startup : null;
        StartupNotice = startup is { Phase: not StartupPhase.Ready } ? startup.Text : null;
        _startupFailed = startup?.Phase == StartupPhase.Failed;
        if (StartupNotice is null && !HasTestAccounts)
        {
            await LoadTestAccountsAsync().ConfigureAwait(true);
        }
    }

    private async Task WaitForStartupAsync()
    {
        // Ein gescheitertes Einspielen ändert sich nicht mehr von selbst; die Anzeige bleibt, bis die API neu startet.
        while (!_signedIn && StartupNotice is not null && !_startupFailed)
        {
            await Task.Delay(StartupPoll).ConfigureAwait(true);
            await RefreshStartupAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Einrichtungsstand neu lesen; beim Verbinden auch über „Neu prüfen“.</summary>
    [RelayCommand]
    public async Task RefreshSetupAsync()
    {
        var setup = (await api.GetAsync<StandaloneSetupState>("api/auth/standalone/setup").ConfigureAwait(true)).Value;
        SetupRequired = setup?.Required == true;
        Joining = setup?.Joining == true;
        JoinError = setup?.JoinError;
    }

    private async Task WaitForSpaAsync()
    {
        while (Joining)
        {
            await Task.Delay(JoinPoll).ConfigureAwait(true);
            await RefreshSetupAsync().ConfigureAwait(true);
        }
    }

    public async Task LoadTestAccountsAsync()
    {
        var result = await api.GetAsync<List<DevAccount>>("api/auth/dev/accounts").ConfigureAwait(true);
        foreach (var account in result.Value ?? [])
        {
            TestAccounts.Add(account);
        }

        // Inhaberin zuerst: beim Entwickeln und Ausprobieren sind damit alle Seiten samt Stammdaten erreichbar (Auftraggeber, 21.10.2026).
        TestAccount = TestAccounts.FirstOrDefault(a => a.Role == StaffRole.Owner) ?? TestAccounts.FirstOrDefault();
        OnPropertyChanged(nameof(HasTestAccounts));
        OnPropertyChanged(nameof(HasReceptionAccount));
        OnPropertyChanged(nameof(HasOwnerAccount));
    }

    private bool CanSignIn => Code.Count(char.IsLetterOrDigit) == 8;

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync()
    {
        Error = null;
        var result = await api.SignInWithCodeAsync(Code).ConfigureAwait(true);
        await CompleteAsync(result.Status == System.Net.HttpStatusCode.Unauthorized ? result with { Error = $"{result.Error} {NewCodeHint}" } : result).ConfigureAwait(true);
    }

    private bool CanSignInWithPassword => UserName.Trim().Length > 0 && Password.Length > 0;

    [RelayCommand(CanExecute = nameof(CanSignInWithPassword))]
    private async Task SignInWithPasswordAsync()
    {
        Error = null;
        await CompleteAsync(await api.SignInWithPasswordAsync(UserName.Trim(), Password).ConfigureAwait(true)).ConfigureAwait(true);
    }

    private bool CanCompleteSetup => SpaName.Trim().Length > 0 && LocationName.Trim().Length > 0 && OwnerName.Trim().Length > 0
        && UserName.Trim().Length > 0 && Password.Length >= 8 && NewPasswordRepeat.Length > 0;

    /// <summary>Einrichtung beim ersten Start; die Wiederholung des Passworts steht in <see cref="NewPasswordRepeat"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanCompleteSetup))]
    private async Task CompleteSetupAsync()
    {
        Error = null;
        if (Password != NewPasswordRepeat)
        {
            Error = "Die beiden Passwörter stimmen nicht überein.";
            return;
        }

        var result = await api.CompleteStandaloneSetupAsync(new StandaloneSetup(SpaName.Trim(), Country.Code, LocationName.Trim(), OwnerName.Trim(),
            UserName.Trim(), Password)).ConfigureAwait(true);
        if (result.Ok)
        {
            SetupRequired = false;
            Password = NewPasswordRepeat = "";
        }

        await CompleteAsync(result).ConfigureAwait(true);
    }

    private bool CanChangePassword => NewPassword.Length >= 8 && NewPasswordRepeat.Length > 0;

    [RelayCommand(CanExecute = nameof(CanChangePassword))]
    private async Task ChangePasswordAsync()
    {
        Error = null;
        if (NewPassword != NewPasswordRepeat)
        {
            Error = "Die beiden neuen Passwörter stimmen nicht überein.";
            return;
        }

        // Das bisherige Passwort ist das Startpasswort aus der Anmeldung eben.
        var result = await api.ChangePasswordAsync(Password, NewPassword).ConfigureAwait(true);
        if (!result.Ok || result.Value is null)
        {
            Error = result.Error ?? "Das Passwort konnte nicht geändert werden.";
            return;
        }

        ChangePasswordRequired = false;
        Password = "";
        _signedIn = true;
        await signedIn(result.Value).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasTestAccount))]
    private async Task SignInAsTestAccountAsync()
    {
        Error = null;
        await CompleteAsync(await api.SignInAsTestAccountAsync(TestAccount!.Email).ConfigureAwait(true)).ConfigureAwait(true);
    }

    private bool HasTestAccount => TestAccount is not null;

    /// <summary>Schnellanmeldung beim Entwickeln: die erste Rezeption bzw. die Inhaberin des Beispiel-Spas mit einem Klick.</summary>
    public bool HasReceptionAccount => TestAccountOf(StaffRole.Reception) is not null;

    public bool HasOwnerAccount => TestAccountOf(StaffRole.Owner) is not null;

    [RelayCommand]
    private Task SignInAsReceptionAsync() => SignInAsAsync(StaffRole.Reception);

    [RelayCommand]
    private Task SignInAsOwnerAsync() => SignInAsAsync(StaffRole.Owner);

    private DevAccount? TestAccountOf(StaffRole role) => TestAccounts.FirstOrDefault(a => a.Role == role);

    private async Task SignInAsAsync(StaffRole role)
    {
        if (TestAccountOf(role) is not { } account)
        {
            return;
        }

        TestAccount = account;
        Error = null;
        await CompleteAsync(await api.SignInAsTestAccountAsync(account.Email).ConfigureAwait(true)).ConfigureAwait(true);
    }

    private async Task CompleteAsync(ApiResult<CurrentUser> result)
    {
        if (!result.Ok || result.Value is null)
        {
            Error = result.Error ?? "Anmeldung fehlgeschlagen.";
            return;
        }

        if (result.Value.Access is null)
        {
            Error = "Für dieses Konto ist kein Zugang eingerichtet oder er wurde gesperrt.";
            return;
        }

        if (result.Value.MustChangePassword)
        {
            ChangePasswordRequired = true;
            return;
        }

        _signedIn = true;
        await signedIn(result.Value).ConfigureAwait(true);
    }
}

/// <summary>Land zur Auswahl bei der Einrichtung.</summary>
public sealed record CountryChoice(string Code, string Label)
{
    public override string ToString() => Label;
}
