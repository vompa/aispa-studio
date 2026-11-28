using System.Diagnostics;
using System.Globalization;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace AispaStudio.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        // Wochentage und Monate im Datumswähler auf Deutsch, unabhängig von der Sprache von Windows.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("de-AT");
        AvaloniaXamlLoader.Load(this);
        ToolTipBehavior.Register();
        RadioGroupBehavior.Register();
        TabStripBehavior.Register();
        TabOnceBehavior.Register();
        ShortcutHints.Register();
        AccessibleNames.Register();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services.ReducedMotion.Apply(this);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            HelpContent.ShowWindow = Views.HelpWindow.Show;
            ISessionStore store = OperatingSystem.IsWindows() ? new WindowsSessionStore() : new MemorySessionStore();
            IOfflineStore offline = OperatingSystem.IsWindows() ? new WindowsOfflineStore() : new MemoryOfflineStore();
            // Empfangsgerät (Schnellwechsel per PIN, ADR 0042): eigenes Kennzeichen, das Abmelden und Personenwechsel überdauert.
            ISessionStore device = OperatingSystem.IsWindows() ? new WindowsSessionStore("empfangsgeraet.bin") : new MemorySessionStore();
            // Vor Resolve gelesen: die Schalter speichern „Betriebsart gewählt“, die Startauswahl gilt nur, solange das fehlt.
            var args = desktop.Args ?? [];
            var apiUrl = Environment.GetEnvironmentVariable("AISPA_API_URL");
            var showStartChoice = StartChoice.IsDue(args, FileSettingsStore.Default.Load(), apiUrl);
            var local = StandaloneMode.Resolve(args, FileSettingsStore.Default)
                ? new LocalServer(LocalServerPaths.Default, new SystemProcessRunner(),
                    OperatingSystem.IsWindows() ? new DpapiProtector() : new PlainProtector(), demo: StandaloneMode.IsDemo(args))
                : null;
            // Adresse der API im Server-Betrieb: Umgebungsvariable AISPA_API_URL, sonst der eingetragene Server oder Haupt-PC, sonst localhost.
            var server = ServerChoice.Resolve(FileSettingsStore.Default.Load(), apiUrl);
            // Im Einzelplatz kein Offline-Stand: die Datenbank liegt ja auf diesem PC.
            var api = local is null
                ? new ApiClient(server.Address, store, offline: new OfflineCache(offline), deviceStore: device, certificateCode: server.Code)
                : new ApiClient(local.ApiAddress, store, deviceStore: device);
            // Die Oberfläche nennt nur Problem und Weg; der Systemtext mit Host und Port steht im Protokoll der App.
            api.TransportFailed += error => CrashLog.Default.Write("Server nicht erreichbar", error);
            api.LocalStateFailed += error => CrashLog.Default.Write("Lokale Abmeldung", error);
            var theme = new ThemeController(FileSettingsStore.Default);
            theme.Apply();
            MainViewModel? viewModel = null;
            var connection = new ConnectionSettings(FileSettingsStore.Default, () => Restart(desktop), apiUrl,
                () => viewModel!.EndConnectionContextAsync(api.ForgetSignIn),
                (title, message) => viewModel!.ShowLocalSessionFailure(title, message),
                error => CrashLog.Default.Write("Serverwechsel", error));
            viewModel = new MainViewModel(api, () => new LiveUpdates(api), theme: theme, local: local, facts: new SystemFacts())
            {
                CloseApp = () => Dispatcher.UIThread.Post(() => desktop.Shutdown()),
                LogDeviceLoadFailure = error => CrashLog.Default.Write("Geräte laden", error),
                Connect = local is null ? new NetworkConnectViewModel(FileSettingsStore.Default, () => Restart(desktop)) { Connection = connection } : null,
                ServerConnect = local is null ? new ServerConnectViewModel(FileSettingsStore.Default, () => Restart(desktop)) { Connection = connection } : null,
                ServerLink = local is null ? new ServerLinkViewModel(FileSettingsStore.Default, server, () => Restart(desktop)) { Connection = connection } : null,
                StartChoice = local is null && showStartChoice ? new StartChoiceViewModel(FileSettingsStore.Default, demo => Restart(desktop, demo)) : null,
            };
            if (local is not null)
            {
                // Beim Beenden API und Datenbank geordnet herunterfahren, ohne den UI-Thread bis zu 20 s zu blockieren:
                // erst abbrechen, im Hintergrund stoppen, dann das Beenden erneut auslösen (T-053).
                var shutdown = new GracefulShutdown(local.StopAsync, () => Dispatcher.UIThread.Post(() => desktop.Shutdown()),
                    error => CrashLog.Default.Write("Beenden: Server stoppen", error));
                desktop.ShutdownRequested += (_, e) => e.Cancel = shutdown.OnShutdownRequested();
            }

            // Protokolliert und als behandelt markiert wird in Program; hier nur die sichtbare Meldung (T-055).
            Dispatcher.UIThread.UnhandledException += (_, _) => viewModel.ReportUiError();

            // Maximiert, damit der Startbildschirm schon die Fläche der Arbeit füllt (die Kassa steht sofort, das Fenster wird nicht animiert).
            desktop.MainWindow = new MainWindow { DataContext = viewModel, WindowState = Avalonia.Controls.WindowState.Maximized };
            _ = viewModel.StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Neu starten, damit die App mit der neuen Server-Adresse arbeitet; <paramref name="demo"/> startet das Beispiel-Spa.</summary>
    private static void Restart(IClassicDesktopStyleApplicationLifetime desktop, bool demo = false)
    {
        if (Environment.ProcessPath is { } app)
        {
            var start = new ProcessStartInfo(app) { UseShellExecute = false };
            if (demo)
            {
                start.ArgumentList.Add("--demo");
            }

            Process.Start(start)?.Dispose();
        }

        desktop.Shutdown();
    }
}
