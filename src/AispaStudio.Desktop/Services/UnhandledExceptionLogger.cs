namespace AispaStudio.Desktop.Services;

/// <summary>
/// Globale Ausnahmebehandlung nach der Avalonia-Dokumentation („Setting unhandled exceptions“): nur protokollieren, nicht retten.
/// </summary>
public sealed class UnhandledExceptionLogger(CrashLog log)
{
    /// <summary>
    /// Fehler im UI-Thread: protokollieren; <c>true</c> heißt „behandelt“, die App läuft dann weiter. Die Kassa soll wegen eines Fehlers
    /// in einer Ansicht nicht mitten im Verkauf verschwinden; die Daten liegen in der API, die App zeigt nur an.
    /// </summary>
    public bool OnUiThread(Exception error)
    {
        log.Write("UI-Thread", error);
        return true;
    }

    /// <summary>Fehler in einer Aufgabe, die niemand abgewartet hat: protokollieren und als beobachtet markieren.</summary>
    public void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        log.Write("Aufgabe ohne Abwarten", e.Exception);
        e.SetObserved();
    }

    /// <summary>Fehler in einem anderen Thread: protokollieren; der Prozess endet danach trotzdem.</summary>
    public void OnDomain(object? sender, UnhandledExceptionEventArgs e) =>
        log.Write(e.IsTerminating ? "Hintergrund-Thread (App endet)" : "Hintergrund-Thread", e.ExceptionObject as Exception);

    /// <summary>Fehler, der aus der Hauptschleife der App herausläuft (Program.Main).</summary>
    public void OnMain(Exception error) => log.Write("Hauptschleife (App endet)", error);
}
