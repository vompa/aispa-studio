using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Eingabefelder nach Fluent 2 (Block A, Design-Brief 5.2 und 5.16 bis 5.20) mit der echten Vorlage und den echten Stilen: Feldrahmen, Zustände,
/// Tastatur. Bilder je Zustand entstehen mit der Umgebungsvariable AISPA_FIELD_SHOTS (Ordner); sie werden nicht eingecheckt.
/// </summary>
public sealed partial class ViewTests
{
    private static ThemeVariant Variant(string theme) => theme == "dunkel" ? ThemeVariant.Dark : ThemeVariant.Light;

    private static Color Resolve(Control owner, string key) =>
        owner.TryFindResource(key, owner.ActualThemeVariant, out var brush) && brush is ISolidColorBrush solid ? solid.Color : throw new InvalidOperationException(key);

    private static double Luminance(Color color)
    {
        static double Linear(byte value)
        {
            var s = value / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));
    }

    /// <summary>Kontrast nach WCAG; <paramref name="alpha"/> mischt den Vordergrund über den Hintergrund (Deckkraft des Elements).</summary>
    private static double Contrast(Color foreground, Color background, double alpha = 1)
    {
        static byte Mix(byte f, byte b, double a) => (byte)Math.Round((f * a) + (b * (1 - a)));
        var mixed = Color.FromRgb(Mix(foreground.R, background.R, alpha), Mix(foreground.G, background.G, alpha), Mix(foreground.B, background.B, alpha));
        var (hi, lo) = (Math.Max(Luminance(mixed), Luminance(background)), Math.Min(Luminance(mixed), Luminance(background)));
        return (hi + 0.05) / (lo + 0.05);
    }

    private static void SaveFieldShot(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("AISPA_FIELD_SHOTS") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            window.CaptureRenderedFrame()?.Save(Path.Combine(folder, $"{name}.png"), PngBitmapEncoderOptions.Default);
        }
    }

    private static List<Border> Strips(Control field, string cssClass) =>
        [.. field.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains(cssClass))];

    private static async Task<Window> ShowAsync(string theme, params Control[] children)
    {
        Application.Current!.RequestedThemeVariant = Variant(theme);
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 12, Width = 320 };
        panel.Children.AddRange(children);
        var window = new Window { Width = 400, Height = 560, Content = panel };
        window.Show();
        await Pump();
        return window;
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Feldrahmen_zeigt_Beschriftung_oben_Stern_und_ersetzt_den_Hilfetext_durch_den_Fehler(string theme) => Session.Dispatch(async () =>
    {
        var input = new TextBox();
        var field = new FormField { Label = "Vorname", Help = "Der gültige Name wie im Ausweis.", IsRequired = true, Content = input };
        var window = await ShowAsync(theme, field);

        var label = field.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Label");
        var star = field.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "*");
        var message = field.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_MessageText");
        Assert.Equal("Vorname", label.Text);
        Assert.True(star.IsEffectivelyVisible);
        Assert.Equal(Resolve(field, "FgBrush"), ((ISolidColorBrush)star.Foreground!).Color);
        Assert.True(label.TranslatePoint(default, window)!.Value.Y < input.TranslatePoint(default, window)!.Value.Y, "Beschriftung steht über dem Feld");
        Assert.Equal(4, input.TranslatePoint(default, window)!.Value.Y - label.TranslatePoint(new Point(0, label.Bounds.Height), window)!.Value.Y, 1.0);
        Assert.Equal(FieldMessageKind.Help, field.MessageKind);
        Assert.Equal("Der gültige Name wie im Ausweis.", message.Text);
        Assert.Equal(Resolve(field, "Fg2Brush"), ((ISolidColorBrush)message.Foreground!).Color);

        // Automation: Name, Pflicht und Hilfetext hängen am Feld.
        Assert.Same(label, AutomationProperties.GetLabeledBy(input));
        Assert.True(AutomationProperties.GetIsRequiredForForm(input));
        Assert.Equal("Der gültige Name wie im Ausweis.", AutomationProperties.GetHelpText(input));

        // Der Fehler ersetzt den Hilfetext (eine Zeile, kein Springen), mit Symbol und Fehlerfarbe; das Feld bekommt „invalid“.
        field.Error = "Bitte einen Vornamen eingeben.";
        await Pump();
        Assert.Equal(FieldMessageKind.Error, field.MessageKind);
        Assert.Equal("Bitte einen Vornamen eingeben.", message.Text);
        Assert.Single(field.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "PART_MessageText" && t.IsEffectivelyVisible);
        Assert.Equal(Resolve(field, "CritBrush"), ((ISolidColorBrush)message.Foreground!).Color);
        Assert.True(field.GetVisualDescendants().OfType<Icon>().Single(i => i.Name == "PART_MessageIcon").IsEffectivelyVisible);
        Assert.Contains("invalid", input.Classes);
        Assert.Equal("Bitte einen Vornamen eingeben.", AutomationProperties.GetHelpText(input));
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(message));

        field.Error = null;
        await Pump();
        Assert.Equal("Der gültige Name wie im Ausweis.", message.Text);
        Assert.DoesNotContain("invalid", input.Classes);

        // Eine Warnung am Feld: Rahmen warn, Text warn, ersetzt den Hilfetext.
        field.Warning = "Gast existiert schon: Mia Muster";
        await Pump();
        Assert.Equal(FieldMessageKind.Warning, field.MessageKind);
        Assert.Equal(Resolve(field, "WarnBrush"), ((ISolidColorBrush)message.Foreground!).Color);
        Assert.Contains("warning", input.Classes);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Textfeld_hat_Unterkante_fg3_Hover_fg2_und_den_Fokus_Unterstrich_auch_bei_Mausfokus(string theme) => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var input = new TextBox { Text = "Maria Muster" };
        var window = await ShowAsync(theme, anchor, input);

        Assert.Equal(34, input.Bounds.Height);
        Assert.Equal(new CornerRadius(8), input.CornerRadius);
        Assert.Equal(new Thickness(1), input.BorderThickness);
        var edge = Assert.Single(Strips(input, FieldChrome.EdgeClass));
        var underline = Assert.Single(Strips(input, FieldChrome.UnderlineClass));
        Assert.Equal(new Thickness(0, 0, 0, 1), edge.BorderThickness);
        Assert.Equal(Resolve(input, "Fg3Brush"), ((ISolidColorBrush)edge.BorderBrush!).Color);
        Assert.Equal(new Thickness(0, 0, 0, 2), underline.BorderThickness);
        Assert.Equal(Resolve(input, "AccentBrush"), ((ISolidColorBrush)underline.BorderBrush!).Color);
        Assert.DoesNotContain("on", underline.Classes);

        // Hover: Rahmen und Unterkante fg-2.
        var at = input.TranslatePoint(new Point(40, 17), window)!.Value;
        window.MouseMove(at);
        await Pump(Settling);
        Assert.Contains("hover", edge.Classes);
        Assert.Equal(Resolve(input, "Fg2Brush"), ((ISolidColorBrush)edge.BorderBrush!).Color);
        var surface = input.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");
        Assert.Equal(Resolve(input, "Fg2Brush"), ((ISolidColorBrush)surface.BorderBrush!).Color);
        SaveFieldShot(window, $"textfeld-hover-{theme}");

        // Fokus per Tastatur: Unterstrich 2 px accent, Rahmen accent, außerdem der Fokusring (4.5).
        window.MouseMove(new Point(380, 540));
        anchor.Focus(NavigationMethod.Pointer);
        input.Focus(NavigationMethod.Tab);
        await Pump(Settling);
        Assert.Contains("on", underline.Classes);
        Assert.Equal(Resolve(input, "AccentBrush"), ((ISolidColorBrush)surface.BorderBrush!).Color);
        Assert.Equal(1.0, underline.RenderTransform!.Value.M11, 3);
        SaveFieldShot(window, $"textfeld-fokus-tastatur-{theme}");

        // Fokus per Maus: der Unterstrich erscheint ebenso, ein Fokusring nicht.
        anchor.Focus(NavigationMethod.Pointer);
        await Pump(Settling);
        Assert.DoesNotContain("on", underline.Classes);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump(Settling);
        Assert.Contains("on", underline.Classes);
        Assert.Empty(RingsOf(input));
        SaveFieldShot(window, $"textfeld-fokus-maus-{theme}");

        window.Close();
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Textfeld_Zustaende_Bild_und_Kontrast(string theme) => Session.Dispatch(async () =>
    {
        var rest = new FormField { Label = "Ruhe", Content = new TextBox { Text = "Maria Muster" } };
        var placeholder = new FormField { Label = "Platzhalter", Content = new TextBox { PlaceholderText = "z. B. Muster" } };
        var error = new FormField { Label = "Fehler", Error = "Bitte einen Nachnamen eingeben.", Content = new TextBox { Text = "?" } };
        var warning = new FormField { Label = "Warnung", Warning = "Gast existiert schon: Mia Muster", Content = new TextBox { Text = "Mia Muster" } };
        var disabled = new FormField { Label = "Deaktiviert", Help = "Verfügbar, sobald ein Name eingetragen ist.", Content = new TextBox { Text = "Maria Muster", IsEnabled = false } };
        var readOnly = new FormField { Label = "Schreibgeschützt", Content = new TextBox { Text = "AT", IsReadOnly = true } };
        var selected = new FormField { Label = "Markiert", Content = new TextBox { Text = "Maria Muster" } };
        var window = await ShowAsync(theme, rest, placeholder, error, warning, disabled, readOnly, selected);
        window.Height = 640;

        var surface = Resolve(rest, "SurfaceBrush");
        var restBox = (TextBox)rest.Content!;
        Assert.True(Contrast(Resolve(rest, "Fg3Brush"), surface) >= 3.0, "Unterkante fg-3 gegen surface");
        Assert.True(Contrast(Resolve(rest, "LineStrongBrush"), surface) < 3.0, "der Rahmen allein trägt den Kontrast nicht: dafür gibt es die Unterkante");
        Assert.True(Contrast(Resolve(rest, "FgBrush"), surface) >= 4.5);

        // Platzhalter: sichtbare Farbe samt Deckkraft des Textelements gegen die Fläche (Wunsch des Auftraggebers Ä24 und 5.2: fg-2, nie fg-3).
        var hint = placeholder.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Placeholder");
        Assert.Equal(Resolve(rest, "Fg2Brush"), ((ISolidColorBrush)hint.Foreground!).Color);
        Assert.True(Contrast(((ISolidColorBrush)hint.Foreground!).Color, surface, hint.Opacity) >= 4.5, $"Platzhalter: Deckkraft {hint.Opacity}");

        // Markierung: kräftige Fläche (accent) mit Text on-accent; beide lesbar (Wunsch des Auftraggebers Ä24: „kaum sichtbar“).
        var selectedBox = (TextBox)selected.Content!;
        Assert.Equal(Resolve(rest, "AccentBrush"), ((ISolidColorBrush)selectedBox.SelectionBrush!).Color);
        Assert.Equal(Resolve(rest, "AccentFgBrush"), ((ISolidColorBrush)selectedBox.SelectionForegroundBrush!).Color);
        Assert.True(Contrast(Resolve(rest, "AccentFgBrush"), Resolve(rest, "AccentBrush")) >= 4.5, "markierter Text auf der Markierung");
        Assert.True(Contrast(Resolve(rest, "AccentBrush"), surface) >= 3.0, "Markierung gegen die Fläche des Felds");

        // Fehler und Warnung: Rahmen in der Bedeutungsfarbe, auch die Unterkante.
        var errorBox = (TextBox)error.Content!;
        Assert.Equal(Resolve(rest, "CritBrush"), ((ISolidColorBrush)errorBox.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement").BorderBrush!).Color);
        Assert.Equal(Resolve(rest, "CritBrush"), ((ISolidColorBrush)Strips(errorBox, FieldChrome.EdgeClass).Single().BorderBrush!).Color);
        var warningBox = (TextBox)warning.Content!;
        Assert.Equal(Resolve(rest, "WarnBrush"), ((ISolidColorBrush)warningBox.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement").BorderBrush!).Color);

        // Deaktiviert: Deckkraft 0,5 nur am Eingabeelement; Beschriftung und Hilfetext bleiben unverändert.
        Assert.Equal(0.5, ((TextBox)disabled.Content!).Opacity);
        Assert.Equal(1.0, disabled.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Label").Opacity);
        Assert.Equal(1.0, disabled.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_MessageText").Opacity);

        // Schreibgeschützt: Fläche surface-2, Rahmen line.
        var readOnlySurface = ((TextBox)readOnly.Content!).GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");
        Assert.Equal(Resolve(rest, "Surface2Brush"), ((ISolidColorBrush)readOnlySurface.Background!).Color);
        Assert.Equal(Resolve(rest, "LineBrush"), ((ISolidColorBrush)readOnlySurface.BorderBrush!).Color);

        selectedBox.Focus(NavigationMethod.Tab);
        selectedBox.SelectAll();
        await Pump(Settling);
        SaveFieldShot(window, $"textfeld-zustaende-{theme}");
        Assert.Equal(34, restBox.Bounds.Height);
        window.Close();
        return true;
    }, CancellationToken.None);

    /// <summary>Design-Brief 4.6: bei „Animationen aus“ laufen alle Übergänge mit 0 ms, auch der Fokus-Unterstrich der Felder.</summary>
    [Fact]
    public Task Animationen_aus_nimmt_Unterkante_und_Fokus_Unterstrich_alle_Uebergaenge() => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var input = new TextBox { Text = "Maria Muster" };
        var window = await ShowAsync("hell", anchor, input);
        var underline = Assert.Single(Strips(input, FieldChrome.UnderlineClass));
        var edge = Assert.Single(Strips(input, FieldChrome.EdgeClass));
        var surface = input.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");
        Assert.NotEmpty(underline.Transitions!);
        Assert.NotEmpty(edge.Transitions!);
        Assert.NotEmpty(surface.Transitions!);

        window.Styles.AddRange(ReducedMotion.Overrides());
        await Pump();
        Assert.Empty(underline.Transitions!);
        Assert.Empty(edge.Transitions!);
        Assert.Empty(surface.Transitions!);
        input.Focus(NavigationMethod.Tab);
        await Pump();
        Assert.Contains("on", underline.Classes);
        Assert.Empty(underline.Transitions!);

        // Der Unterstrich steht sofort voll da, ohne Wachsen.
        Assert.Equal(1.0, underline.RenderTransform!.Value.M11, 3);
        anchor.Focus(NavigationMethod.Pointer);
        await Pump();
        Assert.Equal(0.0, underline.RenderTransform!.Value.M11, 3);
        window.Close();
        return true;
    }, CancellationToken.None);

    /// <summary>Design-Brief 4.5: der Unterstrich wächst in 200 ms mit (0,0,0,1) und geht in 50 ms mit (1,0,1,1); Dauern und Kurven stehen im Stil.</summary>
    [Fact]
    public Task Fokus_Unterstrich_waechst_in_200_ms_und_geht_in_50_ms() => Session.Dispatch(async () =>
    {
        var input = new TextBox { Text = "Maria Muster" };
        var window = await ShowAsync("hell", input);
        var underline = Assert.Single(Strips(input, FieldChrome.UnderlineClass));

        var leaving = Assert.IsType<Avalonia.Animation.TransformOperationsTransition>(Assert.Single(underline.Transitions!));
        Assert.Equal(TimeSpan.FromMilliseconds(50), leaving.Duration);
        AssertSpline(leaving.Easing, 1, 0, 1, 1);

        input.Focus(NavigationMethod.Tab);
        await Pump();
        var entering = Assert.IsType<Avalonia.Animation.TransformOperationsTransition>(Assert.Single(underline.Transitions!));
        Assert.Equal(TimeSpan.FromMilliseconds(200), entering.Duration);
        AssertSpline(entering.Easing, 0, 0, 0, 1);
        Assert.Equal(new RelativePoint(0.5, 0.5, RelativeUnit.Relative), underline.RenderTransformOrigin);
        window.Close();
        return true;
    }, CancellationToken.None);

    private static void AssertSpline(Avalonia.Animation.Easings.Easing easing, double x1, double y1, double x2, double y2)
    {
        var spline = Assert.IsType<Avalonia.Animation.Easings.SplineEasing>(easing);
        Assert.Equal((x1, y1, x2, y2), (spline.X1, spline.Y1, spline.X2, spline.Y2));
    }

    // F3: Markieren beim Betreten

    /// <summary>Design-Brief 5.16: Tab in ein Textfeld setzt die Marke ans Ende (Bearbeiten), nichts ist markiert; ein Klick setzt nur die Marke.</summary>
    [Fact]
    public Task Tab_in_ein_Textfeld_setzt_die_Marke_ans_Ende_und_Klick_nur_die_Marke() => Session.Dispatch(async () =>
    {
        var before = new Button { Content = "Davor" };
        var box = new TextBox { Text = "Maria Muster" };
        var after = new Button { Content = "Danach" };
        var window = await ShowAsync("hell", before, box, after);

        before.Focus(NavigationMethod.Pointer);
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        await Pump();
        Assert.True(box.IsFocused);
        Assert.Equal((12, 12, 12), (box.SelectionStart, box.SelectionEnd, box.CaretIndex));

        after.Focus(NavigationMethod.Pointer);
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        await Pump();
        Assert.True(box.IsFocused);
        Assert.Equal((12, 12, 12), (box.SelectionStart, box.SelectionEnd, box.CaretIndex));

        // Fokus per Code (z. B. beim Öffnen einer Seitenleiste): ebenso.
        after.Focus(NavigationMethod.Pointer);
        box.Focus();
        await Pump();
        Assert.Equal((12, 12), (box.SelectionStart, box.SelectionEnd));

        // Klick: nur die Marke an der Klickstelle, nichts markiert.
        after.Focus(NavigationMethod.Pointer);
        var at = box.TranslatePoint(new Point(14, 17), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        Assert.True(box.IsFocused);
        Assert.Equal(box.SelectionStart, box.SelectionEnd);
        Assert.True(box.CaretIndex < 12, $"Marke an der Klickstelle statt am Ende ({box.CaretIndex})");
        window.Close();
        return true;
    }, CancellationToken.None);

    /// <summary>Design-Brief 5.16: Tab in Zeit-, Datum-, Zahl- und Betragsfeld markiert alles (ersetzen), ein Klick setzt nur die Marke.</summary>
    [Fact]
    public Task Tab_in_Zeit_Datum_Zahl_und_Betragsfeld_markiert_alles_und_Klick_nur_die_Marke() => Session.Dispatch(async () =>
    {
        var fields = new ParsedField[]
        {
            new TimeField { CommittedText = "09:30" },
            new DateField { CommittedText = "21.10.2026" },
            new NumberField { CommittedText = "45" },
            new MoneyField { CommittedText = "1.234,50" },
        };
        var before = new Button { Content = "Davor" };
        var window = await ShowAsync("hell", [before, .. fields]);
        foreach (var field in fields)
        {
            before.Focus(NavigationMethod.Pointer);
            field.Focus(NavigationMethod.Tab);
            await Pump();
            Assert.Equal((0, field.Text!.Length), (field.SelectionStart, field.SelectionEnd));

            before.Focus(NavigationMethod.Pointer);
            field.Focus();
            await Pump();
            Assert.Equal((0, field.Text.Length), (field.SelectionStart, field.SelectionEnd));

            before.Focus(NavigationMethod.Pointer);
            var at = field.TranslatePoint(new Point(14, 17), window)!.Value;
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            await Pump();
            Assert.Equal(field.SelectionStart, field.SelectionEnd);
        }

        window.Close();
        return true;
    }, CancellationToken.None);

    // F4: Esc und Enter

    private sealed class Counter
    {
        public int Value { get; set; }
    }

    private static Button DefaultButton(Counter counter) => new()
    {
        Content = "Speichern",
        IsDefault = true,
        Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => counter.Value++),
    };

    private static List<Key> WatchUnhandledKeys(Window window)
    {
        var seen = new List<Key>();
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => seen.Add(e.Key), RoutingStrategies.Bubble);
        return seen;
    }

    /// <summary>
    /// Design-Brief 5.16: Esc in einem geänderten Textfeld stellt den Wert beim Betreten wieder her, markiert ihn und verbraucht das Esc;
    /// ein unverändertes Feld lässt Esc durch, damit die Maske schließt.
    /// </summary>
    [Fact]
    public Task Esc_im_geaenderten_Textfeld_stellt_den_Wert_her_und_das_zweite_Esc_geht_an_die_Maske() => Session.Dispatch(async () =>
    {
        var box = new TextBox { Text = "Maria Muster" };
        var window = await ShowAsync("hell", box);
        var unhandled = WatchUnhandledKeys(window);

        box.Focus(NavigationMethod.Tab);
        await Pump();
        window.KeyTextInput(" Huber");
        Assert.Equal("Maria Muster Huber", box.Text);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.Equal("Maria Muster", box.Text);
        Assert.Equal((0, 12), (box.SelectionStart, box.SelectionEnd));
        Assert.DoesNotContain(Key.Escape, unhandled);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.Contains(Key.Escape, unhandled);
        Assert.Equal("Maria Muster", box.Text);
        window.Close();
        return true;
    }, CancellationToken.None);

    /// <summary>Enter in einem einzeiligen Feld löst den Standardknopf aus; im mehrzeiligen Feld ist Enter der Umbruch und Strg+Enter löst aus.</summary>
    [Fact]
    public Task Enter_loest_den_Standardknopf_aus_im_mehrzeiligen_Feld_erst_Strg_Enter() => Session.Dispatch(async () =>
    {
        var saved = new Counter();
        var single = new TextBox { Text = "Maria" };
        var multi = new TextBox { Text = "Zeile", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        var window = await ShowAsync("hell", single, multi, DefaultButton(saved));

        single.Focus(NavigationMethod.Tab);
        await Pump();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Pump();
        Assert.Equal(1, saved.Value);

        multi.Focus(NavigationMethod.Tab);
        await Pump();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Pump();
        Assert.Equal(1, saved.Value);
        Assert.True(multi.Text!.Length > "Zeile".Length, "Enter fügt einen Zeilenumbruch ein");

        var text = multi.Text;
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
        await Pump();
        Assert.Equal(2, saved.Value);
        Assert.Equal(text, multi.Text);
        window.Close();
        return true;
    }, CancellationToken.None);
}
