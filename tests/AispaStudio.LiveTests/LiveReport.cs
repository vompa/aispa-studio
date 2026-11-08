using System.Globalization;
using System.Text;

namespace AispaStudio.LiveTests;

/// <summary>
/// Bericht eines Laufs als Markdown (<c>bericht.md</c> im Laufordner). Er wird nach jedem Eintrag neu geschrieben,
/// damit auch ein abgebrochener Lauf einen vollständigen Bericht bis zum letzten Schritt hinterlässt.
/// </summary>
internal sealed class LiveReport
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");
    private readonly Lock _gate = new();
    private readonly List<string> _steps = [];
    private readonly List<string> _events = [];
    private readonly string _file;
    private readonly string _header;
    private string _result = "läuft";

    public LiveReport(string folder, string role, LiveOptions options)
    {
        _file = Path.Combine(folder, "bericht.md");
        _header = $"# Live-Test {role}, {DateTime.Now.ToString("dd.MM.yyyy HH:mm", German)}\n\n"
                  + $"- App: `{options.AppPath}`\n- Server: {options.ApiUrl}\n- Pause je Schritt: {options.StepDelay.TotalSeconds.ToString("0.0", German)} s\n";
        Write();
    }

    public void AddStep(int number, string name, DateTime started, TimeSpan duration, bool passed, string? image, string? error)
    {
        var link = image is null ? "" : $"[Bild]({image})";
        var row = $"| {number} | {Escape(name)} | {started:HH:mm:ss} | {duration.TotalSeconds.ToString("0.0", German)} s | {(passed ? "✓" : "✗")} | {link} |";
        lock (_gate)
        {
            _steps.Add(row);
            if (error is not null)
            {
                _events.Add($"{DateTime.Now:HH:mm:ss} Fehler in Schritt {number}: {error}");
            }

            Write();
        }
    }

    public void AddEvent(string text)
    {
        lock (_gate)
        {
            _events.Add($"{DateTime.Now:HH:mm:ss} {text}");
            Write();
        }
    }

    public void Finish(string result)
    {
        lock (_gate)
        {
            _result = result;
            Write();
        }
    }

    private void Write()
    {
        var text = new StringBuilder(_header)
            .Append("\n## Schritte\n\n| Nr | Schritt | Start | Dauer | Ergebnis | Bild |\n|---|---|---|---|---|---|\n");
        _steps.ForEach(row => text.Append(row).Append('\n'));
        if (_events.Count > 0)
        {
            text.Append("\n## Ereignisse\n\n");
            _events.ForEach(line => text.Append("- ").Append(Escape(line)).Append('\n'));
        }

        text.Append("\n## Ergebnis\n\n").Append(_result).Append('\n');
        File.WriteAllText(_file, text.ToString());
    }

    private static string Escape(string text) => text.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
}
