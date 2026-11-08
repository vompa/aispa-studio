using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace AispaStudio.LiveTests;

/// <summary>
/// Ein Lauf gegen die echte Windows-App: startet sie gegen den Server, führt benannte Schritte sichtbar aus, macht je
/// Schritt ein Bild des Fensters und schreibt den Bericht. Elemente werden über ihren Namen (Beschriftung, Text,
/// <c>AutomationProperties.Name</c>) oder ihre <c>AutomationId</c> (<c>x:Name</c>) gesucht.
/// </summary>
internal sealed class LiveSession : IDisposable
{
    private static readonly TimeSpan FindTimeout = TimeSpan.FromSeconds(15);
    private readonly LiveOptions _options;
    private readonly UIA3Automation _automation = new();
    private readonly Application _app;
    private readonly LiveControl _control;
    private readonly string _folder;
    private int _step;
    private int _extraShot;

    private LiveSession(LiveOptions options, Application app, string folder, string role)
    {
        _options = options;
        _app = app;
        _folder = folder;
        Report = new LiveReport(folder, role, options);
        _control = new LiveControl(options.StartPaused, Report.AddEvent);
        Window = app.GetMainWindow(_automation, TimeSpan.FromSeconds(60))
                 ?? throw new InvalidOperationException("Das Fenster der App ist nicht erschienen.");
        Console.WriteLine($"Live-Test {role}: Bericht in {folder}");
        Console.WriteLine("Tasten: Leertaste Pause/Weiter, Enter Einzelschritt, S Bild, Q Stopp");
    }

    public LiveReport Report { get; }

    public Window Window { get; }

    /// <summary>Wartet auf den Server, startet die App im Server-Betrieb und legt den Laufordner an.</summary>
    public static LiveSession Start(LiveOptions options, string role)
    {
        WaitForServer(options.ApiUrl);
        var folder = Path.Combine(options.OutputRoot, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{role.ToLowerInvariant()}");
        Directory.CreateDirectory(folder);
        var start = new ProcessStartInfo(options.AppPath, "--server") { UseShellExecute = false };
        start.Environment["AISPA_API_URL"] = options.ApiUrl.ToString();
        return new LiveSession(options, Application.Launch(start), folder, role);
    }

    /// <summary>Führt den Ablauf aus und liefert den Exitcode: 0 alles bestanden, 1 ein Schritt gescheitert, 3 mit Q gestoppt.</summary>
    public int Run(Action journey)
    {
        try
        {
            journey();
            Report.Finish($"Bestanden: alle {_step} Schritte.");
            Console.WriteLine($"Bestanden: alle {_step} Schritte.");
            return 0;
        }
        catch (OperationCanceledException ex)
        {
            Report.Finish($"Gestoppt nach {_step} Schritten: {ex.Message}");
            return 3;
        }
        catch (Exception ex)
        {
            Report.Finish($"Nicht bestanden in Schritt {_step}: {ex.Message}");
            Console.WriteLine($"Nicht bestanden in Schritt {_step}.");
            return 1;
        }
    }

    /// <summary>Ein benannter Schritt: wartet bei Pause, führt aus, macht ein Bild und trägt das Ergebnis ein.</summary>
    public void Step(string name, Action action)
    {
        _control.WaitBeforeStep(name, () => Shot("bild"));
        var number = ++_step;
        var started = DateTime.Now;
        var watch = Stopwatch.StartNew();
        Console.Write($"{number,3}. {name} … ");
        try
        {
            action();
            Thread.Sleep(_options.StepDelay);
            Report.AddStep(number, name, started, watch.Elapsed, true, Shot($"{number:00}-{name}"), null);
            Console.WriteLine("✓");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Report.AddStep(number, name, started, watch.Elapsed, false, Shot($"{number:00}-{name}-fehler"), ex.Message);
            Console.WriteLine($"✗ {ex.Message}");
            throw;
        }

        if (_control.TakeShotRequest())
        {
            Shot("bild");
        }
    }

    /// <summary>Sucht ein sichtbares Element über Name oder AutomationId, wartet dabei bis zu 15 Sekunden.</summary>
    public AutomationElement Find(string nameOrId) =>
        TryFind(nameOrId, FindTimeout) ?? throw new InvalidOperationException($"„{nameOrId}“ nicht gefunden.");

    /// <summary>Sucht ein sichtbares Element, auf das <paramref name="match"/> zutrifft; <paramref name="what"/> steht in der Fehlermeldung.</summary>
    public AutomationElement Find(Func<AutomationElement, bool> match, string what) =>
        TryFind(match, FindTimeout) ?? throw new InvalidOperationException($"{what} nicht gefunden.");

    /// <summary>Wie <see cref="Find(string)"/>, liefert aber null nach <paramref name="timeout"/>.</summary>
    public AutomationElement? TryFind(string nameOrId, TimeSpan timeout) =>
        TryFind(element => element.Properties.Name.ValueOrDefault == nameOrId || element.Properties.AutomationId.ValueOrDefault == nameOrId, timeout);

    /// <summary>Sucht das erste sichtbare Element, auf das <paramref name="match"/> zutrifft, in allen Fenstern der App.</summary>
    public AutomationElement? TryFind(Func<AutomationElement, bool> match, TimeSpan timeout) =>
        Retry.WhileNull(() => FindOnce(match), timeout, TimeSpan.FromMilliseconds(200)).Result;

    private AutomationElement? FindOnce(Func<AutomationElement, bool> match)
    {
        try
        {
            return _app.GetAllTopLevelWindows(_automation)
                .SelectMany(window => window.FindAllDescendants())
                .FirstOrDefault(element => !element.Properties.IsOffscreen.ValueOrDefault && match(element));
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Der Baum hat sich beim Lesen geändert (Start, Seitenwechsel); der nächste Versuch liest neu.
            return null;
        }
    }

    /// <summary>Hebt das Element kurz hervor und klickt mit der Maus darauf.</summary>
    public void Click(string nameOrId) => Click(Find(nameOrId));

    public void Click(AutomationElement element)
    {
        Highlight(element);
        element.Click(moveMouse: true);
    }

    /// <summary>
    /// Öffnet die Auswahl <paramref name="comboName"/> und wählt den Eintrag, der mit <paramref name="itemName"/> beginnt.
    /// Gesucht wird nur unter den Einträgen der Auswahl (<c>ComboBoxItem</c>), sonst träfe der Lauf gleichnamige Texte im
    /// Kalender. Danach muss die Auswahl den Eintrag anzeigen.
    /// </summary>
    public void Choose(string comboName, string itemName)
    {
        var combo = Find(comboName);
        Click(combo);
        var item = TryFind(element => IsComboItem(element) && StartsWith(element, itemName), FindTimeout);
        if (item is null)
        {
            var offered = string.Join(", ", VisibleComboItems());
            Press(VirtualKeyShort.ESCAPE);
            throw new InvalidOperationException($"Eintrag „{itemName}“ nicht in der Auswahl „{comboName}“. Sichtbar: {offered}");
        }

        Click(item);
        if (Retry.WhileFalse(() => combo.FindAllDescendants().Any(text => StartsWith(text, itemName)), FindTimeout).Result is false)
        {
            throw new InvalidOperationException($"Die Auswahl „{comboName}“ zeigt nach dem Klick nicht „{itemName}“.");
        }
    }

    private static bool IsComboItem(AutomationElement element) => element.Properties.ClassName.ValueOrDefault == "ComboBoxItem";

    private static bool StartsWith(AutomationElement element, string text) =>
        element.Properties.Name.ValueOrDefault?.StartsWith(text, StringComparison.Ordinal) == true
        || element.FindAllDescendants().Any(child => child.Properties.Name.ValueOrDefault?.StartsWith(text, StringComparison.Ordinal) == true);

    private List<string> VisibleComboItems()
    {
        try
        {
            return _app.GetAllTopLevelWindows(_automation)
                .SelectMany(window => window.FindAllDescendants())
                .Where(IsComboItem)
                // Der Name des Eintrags ist die ToString-Ausgabe des Datensatzes; lesbar ist erst der erste angezeigte Text.
                .Select(element => element.FindAllDescendants()
                    .Select(child => child.Properties.Name.ValueOrDefault)
                    .FirstOrDefault(name => !string.IsNullOrEmpty(name)) ?? "?")
                .ToList();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return ["(nicht lesbar)"];
        }
    }

    /// <summary>Klickt in das Feld, markiert den alten Inhalt und tippt den Text Taste für Taste.</summary>
    public void Type(string field, string text)
    {
        var element = Find(field);
        Highlight(element);
        element.Click(moveMouse: true);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(text);
    }

    /// <summary>Drückt eine Taste in der App.</summary>
    public static void Press(VirtualKeyShort key) => Keyboard.Type(key);

    /// <summary>Wartet, bis das Element sichtbar ist, und hebt es hervor (Prüfschritt).</summary>
    public void Expect(string nameOrId) => Highlight(Find(nameOrId));

    /// <summary>Wartet, bis ein Element sichtbar ist, auf das <paramref name="match"/> zutrifft; <paramref name="what"/> steht in der Fehlermeldung.</summary>
    public void Expect(Func<AutomationElement, bool> match, string what) => Highlight(Find(match, what));

    public void Dispose()
    {
        try
        {
            _app.Close();
            if (!_app.HasExited)
            {
                _app.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(5));
            }
        }
        finally
        {
            if (!_app.HasExited)
            {
                _app.Kill();
            }

            _app.Dispose();
            _automation.Dispose();
        }
    }

    private void Highlight(AutomationElement element)
    {
        if (_options.StepDelay > TimeSpan.Zero)
        {
            element.DrawHighlight(true, Color.OrangeRed, TimeSpan.FromMilliseconds(Math.Min(700, _options.StepDelay.TotalMilliseconds)));
        }
    }

    private string Shot(string name)
    {
        var file = name == "bild" ? $"bild-{++_extraShot:00}.png" : $"{Slug(name)}.png";
        Capture.Element(Window).ToFile(Path.Combine(_folder, file));
        if (name == "bild")
        {
            Report.AddEvent($"Bild auf Wunsch: [{file}]({file})");
        }

        return file;
    }

    private static string Slug(string name)
    {
        var chars = name.ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal).Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal).Replace("ß", "ss", StringComparison.Ordinal)
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray();
        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    private static void WaitForServer(Uri api)
    {
        using var http = new HttpClient { BaseAddress = api, Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var status = JsonDocument.Parse(http.GetStringAsync("api/system/status").GetAwaiter().GetResult());
                if (status.RootElement.TryGetProperty("startup", out var startup)
                    && startup.GetProperty("phase").GetString() == "Ready")
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Server noch nicht erreichbar; weiter warten.
            }
            catch (TaskCanceledException)
            {
                // Zeitüberschreitung einer Anfrage; weiter warten.
            }

            Thread.Sleep(2000);
        }

        throw new InvalidOperationException($"Der Server unter {api} ist nach drei Minuten nicht bereit (docker compose up -d?).");
    }
}
