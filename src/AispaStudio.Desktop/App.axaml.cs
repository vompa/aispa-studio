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
            var local = StandaloneMode.Resolve(desktop.Args ?? [], FileSettingsStore.Default)
                ? new LocalServer(LocalServerPaths.Default, new SystemProcessRunner(),
                    OperatingSystem.IsWindows() ? new DpapiProtector() : new PlainProtector())
                : null;
            // Adresse der API im Server-Betrieb: Umgebungsvariable AISPA_API_URL, sonst der eingetragene Server oder Haupt-PC, sonst localhost.
            var server = ServerChoice.Resolve(FileSettingsStore.Default.Load(), Environment.GetEnvironmentVariable("AISPA_API_URL"));
            // Im Einzelplatz kein Offline-Stand: die Datenbank liegt ja auf diesem PC.
            var api = local is null
                ? new ApiClient(server.Address, store, offline: new OfflineCache(offline), deviceStore: device, certificateCode: server.Code)
                : new ApiClient(local.ApiAddress, store, deviceStore: device);
            var theme = new ThemeController(FileSettingsStore.Default);
            theme.Apply();
            var viewModel = new MainViewModel(api, () => new LiveUpdates(api), theme: theme, local: local)
            {
                Connect = local is null ? new NetworkConnectViewModel(FileSettingsStore.Default, () => Restart(desktop)) : null,
                ServerConnect = local is null ? new ServerConnectViewModel(FileSettingsStore.Default, () => Restart(desktop)) : null,
                ServerLink = local is null ? new ServerLinkViewModel(FileSettingsStore.Default, server, () => Restart(desktop)) : null,
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

    /// <summary>Neu starten, damit die App mit der neuen Server-Adresse arbeitet.</summary>
    private static void Restart(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (Environment.ProcessPath is { } app)
        {
            Process.Start(new ProcessStartInfo(app) { UseShellExecute = false })?.Dispose();
        }

        desktop.Shutdown();
    }
}
