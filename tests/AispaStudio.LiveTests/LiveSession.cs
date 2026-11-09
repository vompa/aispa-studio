using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
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
    private readonly string _folder;
    private static LiveSession? ActiveSession;
    private readonly List<string> _redTopics = [];
    private readonly List<string> _skippedTopics = [];
    private int _step;
    private int _extraShot;

    private LiveSession(LiveOptions options, Application app, string folder, string role)
    {
        _options = options;
        _app = app;
        _folder = folder;
        ActiveSession = this;
        Report = new LiveReport(folder, role, options);
        Control = new LiveControl(options.StartPaused, options.ConsoleKeys, Report.AddEvent);
        Window = app.GetMainWindow(_automation, TimeSpan.FromSeconds(60))
                 ?? throw new InvalidOperationException("Das Fenster der App ist nicht erschienen.");
        if (options.AppArea is { } area)
        {
            PlaceWindow(area);
        }
    }

    /// <summary>Vor jedem Schritt: Nummer und Name.</summary>
    public event Action<int, string>? StepStarted;

    /// <summary>Nach jedem Schritt, auch bei einem Fehler.</summary>
    public event Action<LiveStepResult>? StepFinished;

    public LiveReport Report { get; }

    public LiveControl Control { get; }

    /// <summary>Laufordner mit Bericht und Bildern.</summary>
    public string Folder => _folder;

    /// <summary>Ergebnis in einem Satz, sobald <see cref="Run"/> fertig ist.</summary>
    public string? Result { get; private set; }

    public Window Window { get; }

    /// <summary>Adresse des Servers, für Proben der Themen über die API.</summary>
    public Uri ApiUrl => _options.ApiUrl;

    /// <summary>Zusätzliches Bild des Fensters mitten in einem Schritt; der Bericht verweist darauf.</summary>
    public string Snapshot(string name)
    {
        var file = Shot(name);
        Report.AddEvent($"Bild: [{file}]({file})");
        return file;
    }

    /// <summary>Namen (Text) aller sichtbaren Elemente, auf die <paramref name="match"/> zutrifft; ohne Bedingung alle Textelemente mit Inhalt.</summary>
    public List<string> VisibleTexts(Func<AutomationElement, bool>? match = null) =>
        AllVisible().Where(e => match?.Invoke(e) ?? e.Properties.ClassName.ValueOrDefault == "TextBlock")
            .Select(e => e.Properties.Name.ValueOrDefault)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>Fährt die Maus auf das Element und wartet, bis der Tooltip da ist (für Bilder von Hinweisen an gesperrten Knöpfen).</summary>
    public static void Hover(AutomationElement element)
    {
        var rect = element.BoundingRectangle;
        Mouse.MoveTo(new Point(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2)));
        Thread.Sleep(1500);
    }

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

    /// <summary>Themen, die rot geworden sind („Schlüssel: Schritt, Fehler“), für das Ergebnis und den Exitcode.</summary>
    public IReadOnlyList<string> RedTopics => _redTopics;

    /// <summary>Themen, die übersprungen wurden, weil ein Thema davor rot war.</summary>
    public IReadOnlyList<string> SkippedTopics => _skippedTopics;

    /// <summary>Der Name des Schritts, der zuletzt gescheitert ist.</summary>
    public string? LastFailedStep { get; private set; }

    /// <summary>Trägt ein rotes Thema ein; der Lauf geht mit dem nächsten Thema weiter (Sichtprüfung 09.11.2026, T8).</summary>
    public void MarkTopicRed(string key, string error)
    {
        _redTopics.Add($"{key}: {LastFailedStep ?? "?"}, {error}");
        Report.AddEvent($"THEMA ROT: {key}. Schritt „{LastFailedStep}“: {error}");
    }

    /// <summary>Trägt ein übersprungenes Thema ein.</summary>
    public void MarkTopicSkipped(string key, IEnumerable<string> blockedBy)
    {
        _skippedTopics.Add(key);
        Report.AddEvent($"THEMA ÜBERSPRUNGEN: {key}, weil vorher rot oder übersprungen: {string.Join(", ", blockedBy)}.");
    }

    /// <summary>Führt den Ablauf aus und liefert den Exitcode: 0 alles bestanden, 1 ein Schritt oder Thema gescheitert, 3 gestoppt.</summary>
    public int Run(Action journey)
    {
        int code;
        try
        {
            journey();
            if (_redTopics.Count > 0)
            {
                Result = $"Nicht bestanden: {_redTopics.Count} Thema/Themen rot ({string.Join("; ", _redTopics)}); "
                         + $"übersprungen: {(_skippedTopics.Count == 0 ? "keines" : string.Join(", ", _skippedTopics))}. Schritte insgesamt: {_step}.";
                code = 1;
            }
            else
            {
                Result = $"Bestanden: alle {_step} Schritte.";
                code = 0;
            }
        }
        catch (OperationCanceledException ex)
        {
            Result = $"Gestoppt nach {_step} {(_step == 1 ? "Schritt" : "Schritten")}: {ex.Message}";
            code = 3;
        }
        catch (Exception ex)
        {
            Result = $"Nicht bestanden in Schritt {_step}: {ex.Message}";
            code = 1;
        }

        Report.Finish(Result);
        return code;
    }

    /// <summary>Ein benannter Schritt: wartet bei Pause, führt aus, macht ein Bild und trägt das Ergebnis ein.</summary>
    public void Step(string name, Action action)
    {
        Control.WaitBeforeStep(name, () => Shot("bild"));
        var number = ++_step;
        var started = DateTime.Now;
        var watch = Stopwatch.StartNew();
        StepStarted?.Invoke(number, name);
        try
        {
            action();
            Thread.Sleep(_options.StepDelay);
            var image = Shot($"{number:00}-{name}");
            Report.AddStep(number, name, started, watch.Elapsed, true, image, null);
            StepFinished?.Invoke(new LiveStepResult(number, name, watch.Elapsed, true, null, image));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastFailedStep = name;
            Report.AddEvent($"Fehlerstelle in „{name}“: {string.Join(" ← ", (ex.StackTrace ?? "").Split((char)10).Take(3).Select(line => line.Trim()))}");
            var image = Shot($"{number:00}-{name}-fehler");
            DumpVisible($"{number:00}-{name}-fehler");
            Report.AddStep(number, name, started, watch.Elapsed, false, image, ex.Message);
            StepFinished?.Invoke(new LiveStepResult(number, name, watch.Elapsed, false, ex.Message, image));
            throw;
        }

        if (Control.TakeShotRequest())
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

    private List<AutomationElement> AllVisible()
    {
        try
        {
            return _app.GetAllTopLevelWindows(_automation)
                .SelectMany(window => window.FindAllDescendants())
                .Where(element => !element.Properties.IsOffscreen.ValueOrDefault)
                .ToList();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return [];
        }
    }

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

    /// <summary>Wartet auf das Element, auf das <paramref name="match"/> zutrifft, und klickt es an.</summary>
    public void Click(Func<AutomationElement, bool> match, string what = "Element") => Click(Find(match, what));

    public void Click(AutomationElement element)
    {
        Highlight(element);
        if (IsSkippedForProbe(element))
        {
            Report.AddEvent($"Gegenprobe: Klick auf „{element.Properties.Name.ValueOrDefault}“ ausgelassen.");
            return;
        }

        var isButton = element.Properties.ClassName.ValueOrDefault == "Button";
        if (isButton && MouseProbe is { } pauseMs)
        {
            // Gegenprobe B8 (AISPA_LIVE_MOUSE=1): echter Mausklick mit Pause davor, kein Invoke.
            Thread.Sleep(pauseMs);
            Report.AddEvent($"Mausklick auf „{element.Properties.Name.ValueOrDefault}“ nach {pauseMs} ms Pause.");
            if (int.TryParse(Environment.GetEnvironmentVariable("AISPA_LIVE_MOUSE_HOVER_MS"), out var hoverMs) && hoverMs > 0)
            {
                // Probe B8: Zeiger zuerst auf den Knopf, Hover abwarten (Tooltip kann öffnen), dann erst klicken (statt Sprung und Sofortklick).
                FlaUI.Core.Input.Mouse.MoveTo(element.GetClickablePoint());
                Thread.Sleep(hoverMs);
                FlaUI.Core.Input.Mouse.Click(FlaUI.Core.Input.MouseButton.Left);
                return;
            }

            element.Click(moveMouse: true);
            return;
        }

        // Sichtprüfung 09.11.2026: Mausklicks auf Knöpfe im Seitenpanel kamen gelegentlich nicht an (nur Hover); Knöpfe lösen deshalb über „Invoke“ aus.
        if (isButton && element.Patterns.Invoke.IsSupported && element.Properties.Name.ValueOrDefault?.StartsWith("Wertgutschein", StringComparison.Ordinal) != true)
        {
            var name = element.Properties.Name.ValueOrDefault;
            try
            {
                element.Patterns.Invoke.Pattern.Invoke();
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                // Der Knopf schließt beim Auslösen seinen Dialog; UI Automation meldet dann, dass der Aufruf kein Element mehr fand (0x80040201). Die Aktion ist geschehen.
                Report.AddEvent($"Knopf „{name}“ war nach dem Auslösen weg ({ex.HResult:X8}).");
            }

            return;
        }

        try
        {
            element.Click(moveMouse: true);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            throw new InvalidOperationException($"Das Element war beim Klicken nicht mehr da ({ex.HResult:X8}); der Baum hat sich geändert.", ex);
        }
    }

    /// <summary>
    /// Pause vor einem Mausklick auf einen Knopf in Millisekunden, wenn <c>AISPA_LIVE_MOUSE=1</c> gesetzt ist (Standard 300,
    /// <c>AISPA_LIVE_MOUSE_PAUSE_MS</c> ändert sie); sonst null, und Knöpfe lösen per Invoke aus.
    /// </summary>
    private static int? MouseProbe =>
        Environment.GetEnvironmentVariable("AISPA_LIVE_MOUSE") == "1"
            ? int.TryParse(Environment.GetEnvironmentVariable("AISPA_LIVE_MOUSE_PAUSE_MS"), out var pause) ? pause : 300
            : null;

    /// <summary>
    /// Gegenprobe (Umgebungsvariable <c>AISPA_LIVE_SKIP_CLICK</c>, Namen mit „|“ getrennt): Ein Klick auf ein Element dieses Namens
    /// wird ausgelassen; ein „*“ am Ende steht für beliebigen Rest des Namens. Die Prüfung dahinter muss dann scheitern; so zeigt ein Lauf, dass eine Prüfung ohne das Ergebnis rot wird.
    /// </summary>
    private static bool IsSkippedForProbe(AutomationElement element)
    {
        var names = Environment.GetEnvironmentVariable("AISPA_LIVE_SKIP_CLICK");
        return !string.IsNullOrEmpty(names)
               && element.Properties.Name.ValueOrDefault is { } name
               && names.Split('|').Any(skipped => skipped.EndsWith('*')
                   ? name.StartsWith(skipped[..^1], StringComparison.Ordinal)
                   : name == skipped);
    }

    /// <summary>
    /// Sucht ein Element in der Zeile von <paramref name="anchor"/>: steigt bis zu fünf Ebenen auf und nimmt unter dem ersten Vorfahren,
    /// der ein Element mit <paramref name="match"/> enthält, dieses. So trifft „Diese Kundin nehmen“ den Knopf der gemeinten Zeile.
    /// </summary>
    public static AutomationElement FindInRow(AutomationElement anchor, Func<AutomationElement, bool> match, string what)
    {
        var level = anchor.Parent;
        for (var i = 0; i < 5 && level is not null; i++, level = level.Parent)
        {
            if (level.FindAllDescendants().FirstOrDefault(match) is { } found)
            {
                return found;
            }
        }

        throw new InvalidOperationException($"{what} in der Zeile von „{anchor.Properties.Name.ValueOrDefault}“ nicht gefunden.");
    }

    /// <summary>Rechtsklick mit der Maus auf die Mitte des Elements.</summary>
    public void RightClick(AutomationElement element)
    {
        Highlight(element);
        element.RightClick(moveMouse: true);
    }

    /// <summary>Rechtsklick auf einen Punkt des Bildschirms (freie Stelle im Kalender).</summary>
    public static void RightClickAt(Point point)
    {
        Mouse.MoveTo(point);
        Mouse.RightClick(point);
    }

    /// <summary>Klick auf einen Punkt des Bildschirms.</summary>
    public static void ClickAt(Point point)
    {
        Mouse.MoveTo(point);
        Mouse.Click(point);
    }

    /// <summary>Klickt den Eintrag des offenen Rechtsklick-Menüs, dessen Name mit <paramref name="itemName"/> beginnt.</summary>
    public void ChooseMenuItem(string itemName) =>
        Click(Find(e => e.Properties.ClassName.ValueOrDefault == "MenuItem"
                        && e.Properties.Name.ValueOrDefault?.StartsWith(itemName, StringComparison.Ordinal) == true,
            $"Menüeintrag „{itemName}“"));

    /// <summary>Die Namen aller sichtbaren Menüeinträge (für Fehlermeldungen und Prüfungen).</summary>
    public List<string> VisibleMenuItems() => AllVisible()
        .Where(e => e.Properties.ClassName.ValueOrDefault == "MenuItem")
        .Select(e => e.Properties.Name.ValueOrDefault ?? "?")
        .ToList();

    /// <summary>
    /// Prüft, dass kein sichtbares Element auf <paramref name="match"/> zutrifft; wartet dazu <paramref name="timeout"/> (Standard
    /// 1,5 Sekunden) und scheitert, sobald eines auftaucht. Immer erst etwas Sichtbares erwarten, das beweist, dass die Maske da ist.
    /// </summary>
    public void ExpectMissing(Func<AutomationElement, bool> match, string what, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMilliseconds(1500));
        do
        {
            if (FindOnce(match) is { } found)
            {
                Highlight(found);
                throw new InvalidOperationException($"{what} ist da, sollte aber fehlen.");
            }

            Thread.Sleep(200);
        }
        while (DateTime.UtcNow < deadline);
    }

    /// <summary>Wartet bis zu <paramref name="timeout"/>, dass kein sichtbares Element mehr auf <paramref name="match"/> zutrifft (etwa eine Meldung, die von selbst verschwindet).</summary>
    public void WaitUntilMissing(Func<AutomationElement, bool> match, string what, TimeSpan timeout)
    {
        if (Retry.WhileFalse(() => FindOnce(match) is null, timeout, TimeSpan.FromMilliseconds(250)).Result is false)
        {
            throw new InvalidOperationException($"{what} ist nach {timeout.TotalSeconds:0} Sekunden noch da.");
        }
    }

    /// <summary>
    /// Schiebt den Kalender seitwärts, bis das Rechteck von <paramref name="element"/> ganz in der Kalenderfläche liegt (bei vielen
    /// Spalten ragt eine Karte sonst über den Rand, und ein Klick daneben trifft nichts).
    /// </summary>
    public void ScrollIntoBody(AutomationElement element)
    {
        var body = Find("BodyScroll").BoundingRectangle;
        for (var i = 0; i < 12; i++)
        {
            var rect = element.BoundingRectangle;
            if (rect.Left >= body.Left && rect.Right <= body.Right)
            {
                return;
            }

            Mouse.MoveTo(new Point(body.X + (body.Width / 2), body.Y + (body.Height / 2)));
            Mouse.HorizontalScroll(rect.Right > body.Right ? 3 : -3);
            Thread.Sleep(250);
        }
    }

    /// <summary>Die Rechtecke aller sichtbaren Elemente, auf die <paramref name="match"/> zutrifft (Bildschirmkoordinaten).</summary>
    public List<Rectangle> VisibleRects(Func<AutomationElement, bool> match) =>
        AllVisible().Where(match).Select(e => e.BoundingRectangle).ToList();

    /// <summary>Prüft, dass kein sichtbares Element den Namen oder die AutomationId <paramref name="nameOrId"/> trägt.</summary>
    public void ExpectMissing(string nameOrId) =>
        ExpectMissing(e => e.Properties.Name.ValueOrDefault == nameOrId || e.Properties.AutomationId.ValueOrDefault == nameOrId, $"„{nameOrId}“");

    /// <summary>
    /// Liest den Text eines Elements: den eingetippten Wert eines Feldes oder, ohne Wert, den Namen. Für Prüfungen auf Zahlen
    /// und Codes, die der Lauf nicht kennt (Gutscheincode, Sollbestand).
    /// </summary>
    public static string TextOf(AutomationElement element)
    {
        if (element.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault is { Length: > 0 } value)
        {
            return value;
        }

        return element.Properties.Name.ValueOrDefault ?? "";
    }

    /// <summary>Wartet auf ein Element und liefert seinen Text (<see cref="TextOf"/>).</summary>
    public string Read(Func<AutomationElement, bool> match, string what) => TextOf(Find(match, what));

    /// <summary>
    /// Holt den Fokus auf die Kalenderfläche: Buchstaben-Kürzel greifen nur, wenn kein Feld im Seitenpanel den Fokus hat. Esc
    /// schließt das Panel (und ein offenes Menü); danach gehört die Taste dem Kalender (<c>OnKeyWithoutFocus</c>).
    /// </summary>
    public static void FocusCalendar()
    {
        Press(VirtualKeyShort.ESCAPE);
        Thread.Sleep(300);
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
    public void Type(string field, string text) => TypeInto(Find(field), text);

    /// <summary>Wie <see cref="Type(string, string)"/> für ein schon gefundenes Feld.</summary>
    public void TypeInto(AutomationElement element, string text)
    {
        Highlight(element);
        element.Click(moveMouse: true);
        EnsureAppInForeground();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        EnsureAppInForeground();
        Keyboard.Type(text);
    }

    /// <summary>Drückt eine Taste in der App; nie in ein fremdes Fenster (<see cref="EnsureAppInForeground"/>).</summary>
    public static void Press(VirtualKeyShort key)
    {
        EnsureAppInForeground();
        Keyboard.Type(key);
    }

    /// <summary>Drückt zwei Tasten zugleich (Strg+F) in der App.</summary>
    public static void Chord(VirtualKeyShort first, VirtualKeyShort second)
    {
        EnsureAppInForeground();
        Keyboard.TypeSimultaneously(first, second);
    }

    /// <summary>Tippt Text in die App, ohne vorher ein Feld anzuklicken.</summary>
    public static void TypeText(string text)
    {
        EnsureAppInForeground();
        Keyboard.Type(text);
    }

    /// <summary>
    /// Sichtprüfung 09.11.2026 (T5): Vor jeder Tasten- und Texteingabe muss das Vordergrundfenster zum Prozess der App gehören; sonst ging
    /// Strg+A an ein Explorer-Fenster. Nach <c>SetForeground</c> wartet die Prüfung bis zu zwei Sekunden, danach endet der Lauf mit klarer
    /// Meldung, und es wurde keine Taste gesendet.
    /// </summary>
    public static void EnsureAppInForeground()
    {
        var session = ActiveSession ?? throw new InvalidOperationException("Kein Lauf aktiv.");
        var appId = session._app.ProcessId;
        if (NativeWindow.ForegroundProcessId() == appId)
        {
            return;
        }

        try
        {
            session.Window.SetForeground();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // Das Fenster ließ sich nicht nach vorn holen; die Prüfung unten entscheidet.
        }

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (NativeWindow.ForegroundProcessId() == appId)
            {
                return;
            }

            Thread.Sleep(100);
        }

        var other = NativeWindow.ProcessName(NativeWindow.ForegroundProcessId());
        throw new ForegroundLostException($"Abbruch: Im Vordergrund liegt nicht die App, sondern {other}. Es wurden keine Tasten gesendet.");
    }

    /// <summary>Wartet, bis das Element sichtbar ist, und hebt es hervor (Prüfschritt).</summary>
    public void Expect(string nameOrId) => Highlight(Find(nameOrId));

    /// <summary>Wartet, bis ein Element sichtbar ist, auf das <paramref name="match"/> zutrifft; <paramref name="what"/> steht in der Fehlermeldung.</summary>
    public void Expect(Func<AutomationElement, bool> match, string what) => Highlight(Find(match, what));

    /// <summary>Setzt das App-Fenster auf die Fläche neben der Starter-App, damit Klicks und Bilder sie nie treffen.</summary>
    private void PlaceWindow(Rectangle area)
    {
        var state = Window.Patterns.Window.PatternOrDefault;
        if (state is not null && state.WindowVisualState.ValueOrDefault != WindowVisualState.Normal)
        {
            state.SetWindowVisualState(WindowVisualState.Normal);
        }

        var transform = Window.Patterns.Transform.Pattern;
        transform.Move(area.X, area.Y);
        transform.Resize(area.Width, area.Height);
    }

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

    /// <summary>Schreibt die sichtbaren Elemente auf Wunsch in <c>liste-&lt;name&gt;.txt</c> im Laufordner; Hilfe beim Schreiben neuer Themen.</summary>
    public void Dump(string name) => DumpVisible($"liste-{name}");

    /// <summary>Schreibt Klasse, AutomationId und Name aller sichtbaren Elemente neben das Fehlerbild; das spart das Raten beim Nachbessern.</summary>
    private void DumpVisible(string name)
    {
        var lines = AllVisible().Select(e =>
            $"{e.Properties.ClassName.ValueOrDefault} | {e.Properties.AutomationId.ValueOrDefault} | {e.Properties.Name.ValueOrDefault}");
        File.WriteAllLines(Path.Combine(_folder, $"{Slug(name)}.txt"), lines);
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
