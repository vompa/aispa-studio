using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Feldnamen und Anrede in Anmeldung, Einrichtung und Verbindung (Runde R01, Slice 06: W1, W2, W6, W7, W21, W-Muster 1 und 11): jedes Feld steht in einem
/// <see cref="FormField"/>, sein Name stammt aus der Beschriftung statt aus dem Platzhalter, und die Texte siezen.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly SystemModules StandaloneModules = new(true, false, false, false, false, false, false, false);

    private static CurrentUser StartPasswordUser() => new("u1", "", "Rita Steiner", 0,
        new CurrentAccess(Guid.NewGuid(), "Spa am See", StaffRole.Reception, true, []), null,
        [AccessRight.ViewCalendar], MustChangePassword: true, UserName: "rita");

    /// <summary>Die Anmeldeseite in einem Zustand: Server mit Code und Testkonten, Empfangsgerät, Einzelplatz-Anmeldung, Wiederherstellung, Einrichtung, Passwort wählen.</summary>
    private static async Task<LoginViewModel> LoginIn(string state)
    {
        var stub = new StubApi()
            .On("GET /api/system/status", new SystemStatus("0.9.0", "Development", DateTimeOffset.UtcNow))
            .On("GET /api/auth/dev/accounts", new List<DevAccount> { new("inhaberin@demo.aispa.test", "Katrin Aigner", StaffRole.Owner, ["alle Standorte"]) });
        if (state.StartsWith("Empfangsgerät", StringComparison.Ordinal))
        {
            stub.On("GET /api/auth/quick-switch", new QuickSwitchState(QuickSwitchTests.Device, QuickSwitchTests.People));
        }
        else if (state != "Anmeldung mit Code")
        {
            stub.On("GET /api/system/modules", StandaloneModules)
                .On("GET /api/auth/standalone/setup", new StandaloneSetupState(state.StartsWith("Einrichtung", StringComparison.Ordinal)))
                .OnSignIn("POST /api/auth/standalone/login", StartPasswordUser());
        }

        var local = new SampleLocal();
        var login = new LoginViewModel(stub.Client(), _ => Task.CompletedTask)
        {
            Restore = new RestoreViewModel(local, () => Task.CompletedTask),
            Join = new JoinViewModel(local, () => Task.CompletedTask),
            JoinPoll = TimeSpan.Zero,
            StartupPoll = TimeSpan.Zero,
        };
        await login.LoadAsync();
        switch (state)
        {
            case "Empfangsgerät":
                login.ChoosePersonCommand.Execute(login.People[1]);
                break;
            case "Empfangsgerät mit Code":
                login.ChoosePersonCommand.Execute(login.People[1]);
                login.RequestCodeCommand.Execute(null);
                break;
            case "Wiederherstellung":
                login.ToggleRecoveryCommand.Execute(null);
                break;
            case "Einrichtung":
                login.Restore!.OpenCommand.Execute(null);
                login.Join!.OpenCommand.Execute(null);
                break;
            case "Einrichtung Schritt 2":
                GoToSetupStep(login, SetupStep.Location);
                break;
            case "Einrichtung Schritt 3":
                GoToSetupStep(login, SetupStep.Account);
                break;
            case "Passwort wählen":
                login.UserName = "rita";
                login.Password = "start-2026";
                await login.SignInWithPasswordCommand.ExecuteAsync(null);
                Assert.True(login.ChangePasswordRequired);
                break;
        }

        return login;
    }

    /// <summary>Geht „Spa einrichten“ mit den nötigen Pflichtfeldern bis zum Schritt vor.</summary>
    private static void GoToSetupStep(LoginViewModel login, SetupStep step)
    {
        while (login.SetupStage < step)
        {
            if (login.SetupStage == SetupStep.Spa)
            {
                login.SpaName = "Spa am See";
            }
            else
            {
                login.LocationName = "Gmunden";
            }

            login.SetupContinueCommand.Execute(null);
        }
    }

    /// <summary>Sichtbare Eingabefelder ohne lesbaren Namen oder mit dem Platzhalter als Namen (den setzte <see cref="AccessibleNames"/> bis R01 Slice 11 als Notbehelf).</summary>
    private static List<string> FieldsWithoutOwnName(Control root) =>
        [.. root.GetVisualDescendants().OfType<Control>()
            .Where(c => c is TextBox or ComboBox && c.IsEffectivelyVisible && c.IsEffectivelyEnabled && c.Focusable)
            .Select(c => (Control: c, Name: ControlAutomationPeerName(c), Placeholder: (c as TextBox)?.PlaceholderText ?? (c as ComboBox)?.PlaceholderText))
            .Where(f => HasUnreadableName(f.Control, out _) || f.Name == f.Placeholder)
            .Select(f => $"{f.Control.GetType().Name} \"{f.Name}\" (Platzhalter \"{f.Placeholder}\")")];

    private static string ControlAutomationPeerName(Control control) => Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(control).GetName() ?? "";

    private static async Task<Window> Show(Control content)
    {
        Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new Window { Content = content, Width = 1200, Height = 1400 };
        window.Show();
        await Pump(Settling);
        return window;
    }

    [Theory]
    [InlineData("Anmeldung mit Code")]
    [InlineData("Empfangsgerät")]
    [InlineData("Einzelplatz")]
    [InlineData("Wiederherstellung")]
    [InlineData("Einrichtung")]
    [InlineData("Einrichtung Schritt 2")]
    [InlineData("Einrichtung Schritt 3")]
    [InlineData("Passwort wählen")]
    public Task Anmeldeseite_jedes_Feld_hat_in_jedem_Zustand_einen_Namen_der_nicht_aus_dem_Platzhalter_stammt(string state) => Session.Dispatch(async () =>
    {
        var login = await LoginIn(state);
        var window = await Show(new LoginView { DataContext = login });
        var fields = window.GetVisualDescendants().OfType<Control>().Count(c => c is TextBox or ComboBox && c.IsEffectivelyVisible);
        var problems = FieldsWithoutOwnName(window);
        var unnamed = window.GetVisualDescendants().OfType<Control>()
            .Where(c => c is Button or ToggleButton or CheckBox && c.IsEffectivelyVisible && c.IsEffectivelyEnabled && c.Focusable && HasUnreadableName(c, out _))
            .Select(c => $"{c.GetType().Name} [{string.Join(" ", c.Classes)}]");
        window.Close();

        Assert.True(fields > 0, $"{state}: kein Feld sichtbar");
        Assert.True(problems.Count == 0 && !unnamed.Any(), $"{state}:{Environment.NewLine}{string.Join(Environment.NewLine, problems.Concat(unnamed))}");
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Anmeldeseite_Pflichtfelder_der_Einrichtung_sind_fuer_den_Screenreader_markiert() => Session.Dispatch(async () =>
    {
        var login = await LoginIn("Einrichtung");
        var window = await Show(new LoginView { DataContext = login });
        var required = new List<string>();
        foreach (var step in new[] { SetupStep.Spa, SetupStep.Location, SetupStep.Account })
        {
            GoToSetupStep(login, step);
            await Pump(Settling);
            required.AddRange(window.GetVisualDescendants().OfType<FormField>()
                .Where(f => f.IsEffectivelyVisible && f.FindAncestorOfType<JoinView>() is null && f.FindAncestorOfType<RestoreView>() is null)
                .Where(f => f.Content is Control c && AutomationProperties.GetIsRequiredForForm(c)).Select(f => f.Label!));
        }

        window.Close();

        Assert.Equal(["Name des Spas", "Land", "Erster Standort", "Ihr Name", "Benutzername", "Passwort", "Passwort wiederholen"], required);
        return true;
    }, CancellationToken.None);

    /// <summary>Rot, wenn alle Felder in einem Zug sichtbar wären oder der Fokus nicht mit dem Schritt wechselt.</summary>
    [Fact]
    public Task Einrichtung_zeigt_in_Schritt_1_nur_dessen_Felder_samt_Sicherung_und_Verbinden_und_setzt_den_Fokus() => Session.Dispatch(async () =>
    {
        var login = await LoginIn("Einrichtung");
        var window = await Show(new LoginView { DataContext = login });
        await Pump(Settling);
        var visibleFields = window.GetVisualDescendants().OfType<FormField>()
            .Where(f => f.IsEffectivelyVisible && f.FindAncestorOfType<JoinView>() is null && f.FindAncestorOfType<RestoreView>() is null)
            .Select(f => f.Label).Where(l => l != "Testkonto").ToList();
        var restoreAndJoin = window.GetVisualDescendants().OfType<Control>().Count(c => c is RestoreView or JoinView && c.IsEffectivelyVisible);
        var focused = TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement() as Control;
        var firstFieldFocused = focused?.Name;
        if (Environment.GetEnvironmentVariable("AISPA_SCREENSHOTS") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            window.CaptureRenderedFrame()!.Save(Path.Combine(folder, "einrichten-schritt-1.png"), PngBitmapEncoderOptions.Default);
        }

        login.SpaName = "Spa am See";
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Content is "Weiter" && b.IsEffectivelyVisible).Command!.Execute(null);
        await Pump(Settling);
        var restoreAndJoinInStep2 = window.GetVisualDescendants().OfType<Control>().Count(c => c is RestoreView or JoinView && c.IsEffectivelyVisible);
        var secondFieldFocused = (TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement() as Control)?.Name;
        window.Close();

        Assert.Equal(["Name des Spas", "Land"], visibleFields);
        Assert.Equal(2, restoreAndJoin);
        Assert.Equal(0, restoreAndJoinInStep2);
        Assert.Equal("SetupSpaNameBox", firstFieldFocused);
        Assert.Equal("SetupLocationBox", secondFieldFocused);
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Verbindungsseite_jedes_Feld_hat_einen_Namen_der_nicht_aus_dem_Platzhalter_stammt() => Session.Dispatch(async () =>
    {
        var connect = new NetworkConnectViewModel(new MemorySettingsStore(), () => { }, (_, _, _) => Task.FromResult<string?>(null));
        var window = await Show(new NetworkConnectView { DataContext = connect });
        var problems = FieldsWithoutOwnName(window);
        var names = window.GetVisualDescendants().OfType<TextBox>().Select(ControlAutomationPeerName).ToList();
        window.Close();

        Assert.Empty(problems);
        Assert.Equal(["Adresse", "Verbindungscode"], names);
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Verbindungsseite_Verbinden_ist_die_primaere_Aktion() => Session.Dispatch(async () =>
    {
        var connect = new NetworkConnectViewModel(new MemorySettingsStore(), () => { }, (_, _, _) => Task.FromResult<string?>(null));
        var window = await Show(new NetworkConnectView { DataContext = connect });
        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content is "Verbinden");
        window.Close();

        Assert.Contains("primary", button.Classes);
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Konto_Flyout_PIN_Feld_heisst_PIN_und_nennt_die_Laenge_als_Hilfe() => Session.Dispatch(async () =>
    {
        await WithApp(async (window, shell) =>
        {
            var account = window.GetVisualDescendants().OfType<Button>()
                .First(b => b.Flyout is Flyout { Content: StackPanel panel } && panel.GetLogicalDescendants().OfType<TextBox>().Any());
            var flyout = (Flyout)account.Flyout!;
            flyout.ShowAt(account);
            await Pump(Settling);
            var pin = ((Control)flyout.Content!).GetVisualDescendants().OfType<TextBox>().Single();

            Assert.Contains(ControlAutomationPeerName(pin), (string[])["PIN", "Neue PIN"]);
            Assert.Equal(shell.PinHelp, AutomationProperties.GetHelpText(pin));
            Assert.EndsWith(" bis 8 Ziffern.", shell.PinHelp, StringComparison.Ordinal);
            flyout.Hide();
        });
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("Views/LoginView.axaml")]
    [InlineData("Views/ShellView.axaml")]
    [InlineData("ViewModels/LoginViewModel.cs")]
    [InlineData("ViewModels/ShellViewModel.cs")]
    public void Anmeldung_und_Konto_Flyout_siezen_ohne_du_und_dein(string file)
    {
        var source = File.ReadAllText(Path.Combine(DesktopSourceDir, file));
        var hits = QuotedText().Matches(source).SelectMany(q => DuForm().Matches(q.Value).Select(m => $"{m.Value} in {q.Value}")).ToList();

        Assert.True(hits.Count == 0, string.Join(Environment.NewLine, hits));
    }

    private static readonly string DesktopSourceDir = Path.Combine(RepoRoot(), "src", "AispaStudio.Desktop");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AispaStudio.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("AispaStudio.slnx nicht gefunden.");
    }

    /// <summary>Texte in Anführungszeichen: Attributwerte in XAML, Zeichenketten in C#.</summary>
    [GeneratedRegex("\"[^\"\\r\\n]*\"")]
    private static partial Regex QuotedText();

    [GeneratedRegex(@"\b(du|dein\w*|dich|dir)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DuForm();
}
