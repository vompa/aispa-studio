namespace AispaStudio.LiveTests;

/// <summary>
/// Steuerung eines Laufs: Pause/Weiter, Einzelschritt in der Pause, Bild jetzt und Stopp. Die Starter-App ruft die Befehle
/// über ihre Knöpfe auf; im Konsolenbetrieb liest ein eigener Thread die Tasten (Leertaste, Enter, S, Q) und ruft dieselben
/// Befehle. Ohne Konsoleneingabe (umgeleitet) läuft der Lauf ohne Steuerung durch.
/// </summary>
internal sealed class LiveControl
{
    private readonly Lock _gate = new();
    private readonly Action<string> _note;
    private readonly bool _console;
    private bool _paused;
    private bool _stepOnce;
    private volatile bool _stopRequested;
    private volatile bool _shotRequested;

    public LiveControl(bool startPaused, bool readConsoleKeys, Action<string> note)
    {
        _paused = startPaused;
        _note = note;
        _console = readConsoleKeys;
        if (readConsoleKeys && !Console.IsInputRedirected)
        {
            new Thread(ReadKeys) { IsBackground = true, Name = "Live-Steuerung" }.Start();
        }
    }

    /// <summary>Ob der Lauf gerade angehalten ist.</summary>
    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return _paused;
            }
        }
    }

    /// <summary>Hält an oder lässt weiterlaufen; liefert den neuen Zustand.</summary>
    public bool TogglePause()
    {
        bool paused;
        lock (_gate)
        {
            _paused = !_paused;
            paused = _paused;
        }

        _note(paused ? "Pause" : "Weiter");
        return paused;
    }

    /// <summary>Lässt in der Pause genau einen Schritt laufen.</summary>
    public void StepOnce()
    {
        lock (_gate)
        {
            _stepOnce = true;
        }
    }

    /// <summary>Wünscht ein Bild; es entsteht vor dem nächsten Schritt oder sofort in der Pause.</summary>
    public void RequestShot() => _shotRequested = true;

    /// <summary>Beendet den Lauf vor dem nächsten Schritt.</summary>
    public void Stop()
    {
        _stopRequested = true;
        _note("Stopp");
    }

    /// <summary>Liefert einmal true, wenn seit dem letzten Aufruf ein Bild gewünscht wurde.</summary>
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
                throw new OperationCanceledException("Lauf gestoppt.");
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

            if (_console && !announced)
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
                case ConsoleKey.Spacebar: TogglePause(); break;
                case ConsoleKey.Enter: StepOnce(); break;
                case ConsoleKey.S: RequestShot(); break;
                case ConsoleKey.Q: Stop(); break;
            }
        }
    }
}
