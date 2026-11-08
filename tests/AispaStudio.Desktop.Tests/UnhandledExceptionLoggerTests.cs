using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>Globale Ausnahmebehandlung (T-052): Fehler, die niemand abfängt, landen im Protokoll statt spurlos die App zu beenden.</summary>
public sealed class UnhandledExceptionLoggerTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aispa-crash-");

    public void Dispose() => _root.Delete(recursive: true);

    private string LogFile => Path.Combine(_root.FullName, "protokolle", "app.log");

    private UnhandledExceptionLogger Logger() => new(new CrashLog(LogFile, new FixedTime(new DateTimeOffset(2026, 11, 7, 9, 30, 0, TimeSpan.Zero))));

    [Fact]
    public void Ein_Fehler_im_UI_Thread_wird_protokolliert_und_gilt_als_behandelt()
    {
        var handled = Logger().OnUiThread(new InvalidOperationException("Kassa kaputt", new IOException("Platte voll")));

        Assert.True(handled);
        var text = File.ReadAllText(LogFile);
        Assert.Contains("2026-11-07 09:30:00Z", text, StringComparison.Ordinal);
        Assert.Contains("UI-Thread", text, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException: Kassa kaputt", text, StringComparison.Ordinal);
        Assert.Contains("IOException: Platte voll", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_nicht_abgewarteter_Task_Fehler_wird_protokolliert_und_als_beobachtet_markiert()
    {
        var args = new UnobservedTaskExceptionEventArgs(new AggregateException(new TimeoutException("zu langsam")));

        Logger().OnUnobservedTask(null, args);

        Assert.True(args.Observed);
        Assert.Contains("TimeoutException: zu langsam", File.ReadAllText(LogFile), StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_Fehler_in_einem_anderen_Thread_wird_protokolliert()
    {
        Logger().OnDomain(null, new UnhandledExceptionEventArgs(new ArgumentException("Hintergrund"), isTerminating: true));

        var text = File.ReadAllText(LogFile);
        Assert.Contains("Hintergrund-Thread", text, StringComparison.Ordinal);
        Assert.Contains("ArgumentException: Hintergrund", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_Fehler_aus_der_Hauptschleife_wird_protokolliert()
    {
        Logger().OnMain(new NotSupportedException("Start gescheitert"));

        var text = File.ReadAllText(LogFile);
        Assert.Contains("Hauptschleife", text, StringComparison.Ordinal);
        Assert.Contains("NotSupportedException: Start gescheitert", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Mehrere_Fehler_stehen_untereinander_im_selben_Protokoll()
    {
        var logger = Logger();

        logger.OnUiThread(new InvalidOperationException("erster"));
        logger.OnUiThread(new InvalidOperationException("zweiter"));

        var text = File.ReadAllText(LogFile);
        Assert.Contains("erster", text, StringComparison.Ordinal);
        Assert.Contains("zweiter", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_Fehler_beim_Protokollieren_verdeckt_den_urspruenglichen_nicht()
    {
        // Der Pfad der Protokolldatei ist ein vorhandener Ordner: Schreiben scheitert.
        var logger = new UnhandledExceptionLogger(new CrashLog(_root.FullName, TimeProvider.System));

        var handled = logger.OnUiThread(new InvalidOperationException("egal"));

        Assert.True(handled);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
