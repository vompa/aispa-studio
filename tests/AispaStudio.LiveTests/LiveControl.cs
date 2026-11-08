namespace AispaStudio.LiveTests;

/// <summary>
/// Steuerung über die Konsole neben der App: Leertaste hält an oder läuft weiter, Enter macht in der Pause einen Schritt,
/// S nimmt sofort ein Bild auf, Q beendet den Lauf. Ohne Konsoleneingabe (umgeleitet) läuft der Lauf ohne Steuerung durch.
/// </summary>
internal sealed class LiveControl
{
    private readonly Lock _gate = new();
    private readonly Action<string> _note;
    private bool _paused;
    private bool _stepOnce;
    private volatile bool _stopRequested;
    private volatile bool _shotRequested;

    public LiveControl(bool startPaused, Action<string> note)
    {
        _paused = startPaused;
        _note = note;
        if (!Console.IsInputRedirected)
        {
            new Thread(ReadKeys) { IsBackground = true, Name = "Live-Steuerung" }.Start();
        }
    }

    /// <summary>Liefert einmal true, wenn seit dem letzten Aufruf S gedrückt wurde.</summary>
    public bool TakeShotRequest()
    {
        var requested = _shotRequested;
        _shotRequested = false;
        return requested;
    }

    /// <summary>Wartet vor einem Schritt, solange angehalten ist; ein Bildwunsch in der Pause ruft <paramref name="onShot"/>.</summary>
    public void WaitBeforeStep(string stepName, Action onShot)
    {
        var announced = false;
        while (true)
        {
            if (_stopRequested)
            {
                throw new OperationCanceledException("Lauf mit Q gestoppt.");
            }

            if (TakeShotRequest())
            {
                onShot();
            }

            lock (_gate)
            {
                if (!_paused)
                {
                    return;
                }

                if (_stepOnce)
                {
                    _stepOnce = false;
                    return;
                }
            }

            if (!announced)
            {
                Console.WriteLine($"    Pause vor „{stepName}“: Leertaste weiter, Enter ein Schritt, S Bild, Q Stopp");
                announced = true;
            }

            Thread.Sleep(100);
        }
    }

    private void ReadKeys()
    {
        while (!_stopRequested)
        {
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.Spacebar:
                    bool paused;
                    lock (_gate)
                    {
                        _paused = !_paused;
                        paused = _paused;
                    }

                    _note(paused ? "Pause" : "Weiter");
                    break;
                case ConsoleKey.Enter:
                    lock (_gate)
                    {
                        _stepOnce = true;
                    }

                    break;
                case ConsoleKey.S:
                    _shotRequested = true;
                    break;
                case ConsoleKey.Q:
                    _stopRequested = true;
                    _note("Stopp");
                    break;
            }
        }
    }
}
