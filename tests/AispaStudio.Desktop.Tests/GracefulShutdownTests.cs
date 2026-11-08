using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>Beenden ohne Hänger (T-053): Der Server stoppt im Hintergrund, der UI-Thread wartet nicht darauf.</summary>
public sealed class GracefulShutdownTests
{
    private readonly TaskCompletionSource _stopping = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _shutDown = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _stops;
    private int _shutdowns;
    private readonly List<Exception> _errors = [];

    private GracefulShutdown Create(Func<Task>? stop = null) => new(
        stop ?? (() =>
        {
            _stops++;
            return _stopping.Task;
        }),
        () =>
        {
            _shutdowns++;
            _shutDown.TrySetResult();
        },
        _errors.Add);

    private static async Task Within(Task task) => await task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Die_erste_Beenden_Anfrage_wird_abgebrochen_und_wartet_nicht_auf_den_Server()
    {
        var shutdown = Create();

        var cancel = shutdown.OnShutdownRequested();

        // Der Server stoppt noch (die Aufgabe ist offen), die Anfrage ist trotzdem schon zurück.
        Assert.True(cancel);
        Assert.Equal(1, _stops);
        Assert.Equal(0, _shutdowns);

        _stopping.SetResult();
        await Within(_shutDown.Task);
        Assert.Equal(1, _shutdowns);
    }

    [Fact]
    public async Task Eine_zweite_Anfrage_waehrend_des_Stoppens_stoppt_nicht_noch_einmal_und_beendet_noch_nicht()
    {
        var shutdown = Create();
        shutdown.OnShutdownRequested();

        var cancel = shutdown.OnShutdownRequested();

        Assert.True(cancel);
        Assert.Equal(1, _stops);
        Assert.Equal(0, _shutdowns);

        _stopping.SetResult();
        await Within(_shutDown.Task);
    }

    [Fact]
    public async Task Nach_dem_Stoppen_laesst_die_erneute_Anfrage_das_Beenden_durch()
    {
        var shutdown = Create();
        shutdown.OnShutdownRequested();
        _stopping.SetResult();
        await Within(_shutDown.Task);

        var cancel = shutdown.OnShutdownRequested();

        Assert.False(cancel);
        Assert.Equal(1, _stops);
        Assert.Equal(1, _shutdowns);
    }

    [Fact]
    public async Task Scheitert_das_Stoppen_wird_der_Fehler_gemeldet_und_trotzdem_beendet()
    {
        var shutdown = Create(() => Task.FromException(new IOException("pg_ctl hängt")));

        Assert.True(shutdown.OnShutdownRequested());

        await Within(_shutDown.Task);
        Assert.Equal(1, _shutdowns);
        Assert.Equal("pg_ctl hängt", Assert.Single(_errors).Message);
        Assert.False(shutdown.OnShutdownRequested());
    }

    [Fact]
    public async Task Wirft_das_Stoppen_gleich_beim_Aufruf_wird_trotzdem_beendet()
    {
        var shutdown = Create(() => throw new InvalidOperationException("sofort"));

        Assert.True(shutdown.OnShutdownRequested());

        await Within(_shutDown.Task);
        Assert.Equal("sofort", Assert.Single(_errors).Message);
    }
}
