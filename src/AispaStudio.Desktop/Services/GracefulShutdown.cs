namespace AispaStudio.Desktop.Services;

/// <summary>
/// Beenden ohne Hänger des UI-Threads: <c>ShutdownRequested</c> wird abgebrochen, der lokale Server stoppt im Hintergrund (bis zu 20 s),
/// danach löst die App das Beenden erneut aus und lässt es dann durch. Ein Merker verhindert die Endlosschleife.
/// </summary>
/// <param name="stop">Stoppt den Server; läuft ab dem ersten <c>await</c> nicht mehr im UI-Thread.</param>
/// <param name="shutdown">Löst das Beenden erneut aus; muss im UI-Thread laufen (in der App über den Dispatcher).</param>
/// <param name="onError">Bekommt einen Fehler beim Stoppen; beendet wird trotzdem.</param>
public sealed class GracefulShutdown(Func<Task> stop, Action shutdown, Action<Exception> onError)
{
    private const int Idle = 0;
    private const int Stopping = 1;
    private const int Stopped = 2;

    private int _state = Idle;

    /// <summary>Für <c>ShutdownRequested</c>; <c>true</c> heißt „abbrechen“.</summary>
    public bool OnShutdownRequested()
    {
        switch (Interlocked.CompareExchange(ref _state, Stopping, Idle))
        {
            case Idle:
                _ = StopThenShutdownAsync();
                return true;
            case Stopping:
                return true;
            default:
                return false;
        }
    }

    private async Task StopThenShutdownAsync()
    {
        try
        {
            await stop().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            onError(ex);
        }
        finally
        {
            Volatile.Write(ref _state, Stopped);
            shutdown();
        }
    }
}
