using AispaStudio.Desktop.Services;
using Avalonia;
using Avalonia.Threading;

namespace AispaStudio.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // Abendliche Sicherung aus der Windows-Aufgabe (ADR 0041, Schritt 5): ohne Fenster sichern und wieder beenden.
        if (args.Contains(Services.ScheduledBackup.Argument, StringComparer.OrdinalIgnoreCase))
        {
            return Services.ScheduledBackup.RunFromCommandLine();
        }

        var handler = new UnhandledExceptionLogger(CrashLog.Default);
        // Avalonia-Dokumentation „Setting unhandled exceptions“: alle Wege, auf denen ein Fehler sonst spurlos die App beendet.
        Dispatcher.UIThread.UnhandledException += (_, e) => e.Handled = handler.OnUiThread(e.Exception);
        TaskScheduler.UnobservedTaskException += handler.OnUnobservedTask;
        AppDomain.CurrentDomain.UnhandledException += handler.OnDomain;

        Views.EmbeddedBrowser.UseNative = true;
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            handler.OnMain(ex);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
