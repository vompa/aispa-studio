using System.Drawing;

namespace AispaStudio.LiveTests;

/// <summary>
/// Einstellungen eines Laufs, aus der Befehlszeile oder von der Starter-App. <see cref="Topics"/> nennt die Schlüssel der
/// gewählten Themen (leer: alle). <see cref="AppArea"/> ist die Bildschirmfläche in Pixeln, auf die das App-Fenster gesetzt
/// wird (null: wie die App selbst startet).
/// </summary>
internal sealed record LiveOptions(
    string AppPath,
    Uri ApiUrl,
    TimeSpan StepDelay,
    bool StartPaused,
    string OutputRoot,
    IReadOnlyList<string> Topics,
    bool ConsoleKeys = true,
    Rectangle? AppArea = null)
{
    public static LiveOptions Parse(string[] args)
    {
        string? app = null;
        var api = new Uri("http://localhost:5086/");
        var delay = TimeSpan.FromMilliseconds(1500);
        var paused = false;
        var output = Path.Combine(".test-logs", "live");
        IReadOnlyList<string> topics = [];
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--app": app = Value(args, ref i); break;
                case "--api": api = new Uri(Value(args, ref i)); break;
                case "--speed": delay = SpeedOf(Value(args, ref i)); break;
                case "--step": paused = true; break;
                case "--out": output = Value(args, ref i); break;
                case "--topics": topics = Value(args, ref i).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); break;
                default: throw new ArgumentException($"Unbekannte Option: {args[i]}");
            }
        }

        if (app is null || !File.Exists(app))
        {
            throw new ArgumentException($"--app fehlt oder die Datei gibt es nicht: {app}");
        }

        return new LiveOptions(Path.GetFullPath(app), api, delay, paused, Path.GetFullPath(output), topics);
    }

    /// <summary>Pause je Schritt für slow, normal oder fast.</summary>
    public static TimeSpan SpeedOf(string speed) => speed switch
    {
        "slow" => TimeSpan.FromMilliseconds(1500),
        "normal" => TimeSpan.FromMilliseconds(500),
        "fast" => TimeSpan.Zero,
        _ => throw new ArgumentException($"--speed erwartet slow, normal oder fast, nicht {speed}"),
    };

    private static string Value(string[] args, ref int index) =>
        ++index < args.Length ? args[index] : throw new ArgumentException($"Wert fehlt nach {args[index - 1]}");
}
