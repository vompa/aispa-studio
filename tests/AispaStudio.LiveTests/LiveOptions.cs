namespace AispaStudio.LiveTests;

/// <summary>Einstellungen eines Laufs aus der Befehlszeile.</summary>
internal sealed record LiveOptions(string AppPath, Uri ApiUrl, TimeSpan StepDelay, bool StartPaused, string OutputRoot)
{
    public static LiveOptions Parse(string[] args)
    {
        string? app = null;
        var api = new Uri("http://localhost:5086/");
        var delay = TimeSpan.FromMilliseconds(1500);
        var paused = false;
        var output = Path.Combine(".test-logs", "live");
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--app": app = Value(args, ref i); break;
                case "--api": api = new Uri(Value(args, ref i)); break;
                case "--speed": delay = SpeedOf(Value(args, ref i)); break;
                case "--step": paused = true; break;
                case "--out": output = Value(args, ref i); break;
                default: throw new ArgumentException($"Unbekannte Option: {args[i]}");
            }
        }

        if (app is null || !File.Exists(app))
        {
            throw new ArgumentException($"--app fehlt oder die Datei gibt es nicht: {app}");
        }

        return new LiveOptions(Path.GetFullPath(app), api, delay, paused, Path.GetFullPath(output));
    }

    private static string Value(string[] args, ref int index) =>
        ++index < args.Length ? args[index] : throw new ArgumentException($"Wert fehlt nach {args[index - 1]}");

    private static TimeSpan SpeedOf(string speed) => speed switch
    {
        "slow" => TimeSpan.FromMilliseconds(1500),
        "normal" => TimeSpan.FromMilliseconds(500),
        "fast" => TimeSpan.Zero,
        _ => throw new ArgumentException($"--speed erwartet slow, normal oder fast, nicht {speed}"),
    };
}
