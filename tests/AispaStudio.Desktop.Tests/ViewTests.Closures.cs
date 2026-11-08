using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Schließzeiten (ADR 0073) in beiden Farbschemata: das Schließband über allen Spalten mit der Farbe der Art, der Chip „Geschlossen · Betriebsurlaub“ in der Kopfzeile, die Meldung
/// „n Termine in der Schließzeit“ mit dem Knopf „Termine absagen …“ und die Rückfrage (600 breit, Fokus auf Abbrechen, Liste, Text, KI-Pille nur nach dem Umformulieren).
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Montag, 19.10.2026, 0:00 Uhr Wiener Ortszeit (Sommerzeit, UTC+2).</summary>
    private static readonly DateTimeOffset ClosureMidnight = new(2026, 10, 18, 22, 0, 0, TimeSpan.Zero);

    private static readonly Guid ClosureBandId = Guid.Parse("00000000-0000-0000-0000-00000000c1a0");

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    /// <summary>Betriebsurlaub von Montag, 19.10., 0:00 bis Mittwoch, 21.10., Ende (Wiener Ortszeit, UTC+2).</summary>
    private static ClosureBand SampleClosure(ClosureKind kind = ClosureKind.Vacation, string title = "Betriebsurlaub", int open = 4) =>
        new(ClosureBandId, ClosureMidnight, ClosureMidnight.AddDays(3), kind, title, "Wegen Betriebsurlaub geschlossen.", "Inventur", open);

    private static StubApi ClosureApi(ClosureBand band) =>
        Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.CalendarDay() with { Closures = [band], ClosuresDue = [band] });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kalender_zeigt_das_Schliessband_ueber_allen_Spalten_und_den_Chip(string theme) => Render(theme, "kalender-schliessband", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump(Settling);

        var fill = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("closurefill") && b.IsEffectivelyVisible);
        Assert.Equal(calendar.ColumnsWidth, fill.Bounds.Width, 1);
        Assert.Equal(calendar.DayHeight, fill.Bounds.Height, 1);
        Assert.Equal(0.55, fill.Opacity, 0.001);
        Assert.Equal(ColorOf(window, "AccentSoftBrush"), ColorOf(fill.Background));
        var edge = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("closureedge") && b.IsEffectivelyVisible);
        Assert.Equal(new Thickness(0, 1), edge.BorderThickness);
        Assert.Equal(ColorOf(window, "AccentBrush"), ColorOf(edge.BorderBrush));

        // Der Text oben im Band: Art und Grund in einer Kapsel (Fläche der Art, Rand, Radius 999), in der Farbe der Art, lesbar auch über Terminen.
        var label = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("closurelabel") && t.IsEffectivelyVisible);
        var box = label.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("closurelabelbox"));
        Assert.Equal(new CornerRadius(999), box.CornerRadius);
        Assert.Equal(ColorOf(window, "AccentSoftBrush"), ColorOf(box.Background));
        Assert.Equal(ColorOf(window, "AccentBrush"), ColorOf(box.BorderBrush));
        Assert.Equal("Betriebsurlaub · Inventur", label.Text);
        Assert.Equal(ColorOf(window, "AccentBrush"), ColorOf(label.Foreground));
        Assert.True(Contrast(ColorOf(window, "AccentBrush"), ColorOf(window, "AccentSoftBrush")) >= 4.5, "Text gegen die Fläche des Bands");
        Assert.Equal(FontWeight.SemiBold, label.FontWeight);

        // Das Band fängt keinen Klick ab: die Termine darunter bleiben bedienbar.
        Assert.Contains(fill.GetVisualAncestors().OfType<Canvas>(), c => !c.IsHitTestVisible);

        // Chip der Kopfzeile: Kopfzeilen-Chip nach 5.8 (Höhe 34, Radius 999) im Zustand „on“.
        var chip = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HeadClosure" && b.IsEffectivelyVisible);
        Assert.Contains("headchip", chip.Classes);
        Assert.Contains("on", chip.Classes);
        Assert.Equal(34, chip.Bounds.Height, 0.5);
        Assert.Contains(chip.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Geschlossen · Betriebsurlaub");
        Assert.StartsWith("Geschlossen: Betriebsurlaub, Mo. 19.10. bis Mi. 21.10.", AutomationProperties.GetName(chip), StringComparison.Ordinal);
        return calendar;
    }, ClosureApi(SampleClosure()));

    [Theory]
    [InlineData(ClosureKind.SpecialClosure, "Sonderschließung", "warn", "WarnSoftBrush", "WarnBrush")]
    [InlineData(ClosureKind.Outage, "Ausfall", "crit", "CritSoftBrush", "CritBrush")]
    public Task Sonderschliessung_ist_Warnung_Ausfall_Fehler_in_Band_und_Chip(ClosureKind kind, string title, string chipClass, string soft, string strong) =>
        Render("hell", $"kalender-schliessband-{kind}", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump(Settling);

            var fill = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("closurefill") && b.IsEffectivelyVisible);
            Assert.Equal(ColorOf(window, soft), ColorOf(fill.Background));
            var edge = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("closureedge") && b.IsEffectivelyVisible);
            Assert.Equal(ColorOf(window, strong), ColorOf(edge.BorderBrush));
            var label = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("closurelabel") && t.IsEffectivelyVisible);
            Assert.Equal(ColorOf(window, strong), ColorOf(label.Foreground));
            Assert.True(Contrast(ColorOf(window, strong), ColorOf(window, soft)) >= 4.5, "Text gegen die Fläche des Bands");
            var box = label.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("closurelabelbox"));
            Assert.Equal((ColorOf(window, soft), ColorOf(window, strong)), (ColorOf(box.Background), ColorOf(box.BorderBrush)));
            var chip = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HeadClosure" && b.IsEffectivelyVisible);
            Assert.Contains(chipClass, chip.Classes);
            Assert.DoesNotContain("on", chip.Classes);
            Assert.Contains(chip.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == $"Geschlossen · {title}");
            return calendar;
        }, ClosureApi(SampleClosure(kind, title)));

    [Fact]
    public Task Ohne_Schliesszeit_gibt_es_weder_Band_noch_Chip() => Render("hell", "kalender-ohne-schliesszeit", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump(Settling);

        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("closurefill") && b.IsEffectivelyVisible);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "HeadClosure" && b.IsEffectivelyVisible);
        return calendar;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Meldungen_zeigen_n_Termine_in_der_Schliesszeit_mit_dem_Knopf_Termine_absagen(string theme) => Render(theme, "meldung-schliesszeit", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenMessagesCommand.Execute(null);
        await Pump(Settling);

        var message = Assert.Single(calendar.Messages, m => m.Kind == MessageKind.Closure);
        Assert.Equal("4 Termine in der Schließzeit (Betriebsurlaub, Mo. 19.10. bis Mi. 21.10.).", message.Text);
        var action = window.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && b.Content as string == "Termine absagen …");
        Assert.True(action.IsEnabled);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Schließzeit" && t.IsEffectivelyVisible);
        return calendar;
    }, ClosureApi(SampleClosure()));

    [Fact]
    public Task Die_Rezeption_sieht_die_Meldung_ohne_den_Knopf() => Render("hell", "meldung-schliesszeit-rezeption", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenMessagesCommand.Execute(null);
        await Pump(Settling);

        Assert.Single(calendar.Messages, m => m.Kind == MessageKind.Closure);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Termine absagen …");
        return calendar;
    }, ClosureApi(SampleClosure()).On("GET /api/auth/me", Reception()));

    // ---------- Rückfrage ----------

    private static readonly string[] SampleGuests = ["Brigitte Müller", "Sabine Gruber", "Petra Hofer", "Ines Fuchs", "Nina Weiss", "Stefan Koller"];

    private static ClosureCancellationPreview SamplePreview(int rows = 3, bool ai = true) => new(
        new ClosureDto(ClosureBandId, SpaSample.LocationId, new DateOnly(2026, 10, 19), null, new DateOnly(2026, 10, 21), null, ClosureMidnight, ClosureMidnight.AddDays(3), ClosureKind.Vacation,
            "Inventur", null, ClosurePhase.Active, "Katrin Aigner", SpaSample.At(8), rows, 0),
        [.. Enumerable.Range(0, rows).Select(i => new ClosureAppointmentDto(Guid.NewGuid(), SampleGuests[i % SampleGuests.Length],
            "Klassische Massage", SpaSample.At(10 + i), SpaSample.At(10 + i, 50), "Lena Huber", AppointmentStatus.Booked,
            (i % 3) switch { 0 => ClosureChannel.Email, 1 => ClosureChannel.Sms, _ => ClosureChannel.None }, i == 1, false))],
        "Absage Ihres Termins: {Behandlung}, {Zeit}",
        "Guten Tag {Anrede},\n\nwegen unseres Betriebsurlaubs (Mo. 19. Oktober bis Mi. 21. Oktober) haben wir geschlossen. Deshalb müssen wir Ihren Termin leider absagen:\n\n{Behandlung}\n{Zeit}\n\nWir bitten um Ihr Verständnis.\nDay-Spa Wien",
        ai);

    private static Task<(string? Text, string? Problem)> Revised(ReplyRevisionKind kind, string text) =>
        Task.FromResult<(string?, string?)>(($"Herzlich willkommen! {text}", null));

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Rueckfrage_Termine_absagen_hat_Liste_Text_Zahl_und_den_Fokus_auf_Abbrechen(string theme) => Render(theme, "schliesszeit-absage", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var asking = main.DialogHost.ShowAsync(done => new CancelClosureDialogViewModel(SamplePreview(), Vienna, done, Revised));
        await Pump(Settling);

        // Dieselbe Karte wie jede Rückfrage (Radius 12, Innenabstand 24, Rahmen), nur 600 breit, und sie bleibt im Fenster.
        var card = Dialog(window);
        Assert.Equal(600, card.Bounds.Width, 0.5);
        Assert.Equal(new CornerRadius(12), card.CornerRadius);
        Assert.Equal(new Thickness(24), card.Padding);
        Assert.True(card.Bounds.Height <= window.Bounds.Height - 48 + 0.5, $"Karte {card.Bounds.Height} höher als das Fenster");
        Assert.Equal("Termine absagen?", card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DialogTitle").Text);

        // Liste: je Termin ein Kontrollkästchen mit Gast, Zeit und Weg; alle gewählt; Zahl der Nachrichten unter dem Text.
        var rows = card.GetVisualDescendants().OfType<ItemsControl>().Single(i => i.Name == "ClosureRows").GetVisualDescendants().OfType<CheckBox>().ToList();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.True(r.IsChecked));
        Assert.Equal("Brigitte Müller, Mo. 19.10. 10:00 · Klassische Massage, Mail", AutomationProperties.GetName(rows[0]));
        Assert.True((card.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "ClosureAll")).IsChecked);
        var summary = card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ClosureSummary");
        Assert.Equal("2 Nachrichten werden gesendet (1 Mail, 1 SMS). 1 Gast hat keinen Kontakt und wird nicht benachrichtigt.", summary.Text);
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(summary));

        // Text und Betreff sind bearbeitbar; die KI-Knöpfe stehen da, die Pille „KI“ noch nicht.
        var text = card.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "ClosureText");
        Assert.True(text.AcceptsReturn);
        Assert.Contains("Guten Tag {Anrede},", text.Text, StringComparison.Ordinal);
        Assert.Equal("Absage Ihres Termins: {Behandlung}, {Zeit}", card.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "ClosureSubject").Text);
        Assert.Equal(3, card.GetVisualDescendants().OfType<WrapPanel>().Single(w => w.Name == "ClosureRevise").Children.Count);
        Assert.DoesNotContain(card.GetVisualDescendants().OfType<AiPill>(), p => p.IsEffectivelyVisible);

        // Knopfleiste: die Aktion links (rot gefüllt, Zahl der Termine), Abbrechen rechts; zerstörend, also Fokus auf Abbrechen.
        var confirm = DialogButton(window, "ConfirmButton");
        var cancel = DialogButton(window, "CancelButton");
        Assert.Equal("3 Termine absagen", confirm.Content);
        Assert.Contains("danger", confirm.Classes);
        Assert.Contains("filled", confirm.Classes);
        Assert.True(LeftOf(window, confirm) < LeftOf(window, cancel), "Aktion links, Abbrechen rechts");
        Assert.Same(cancel, Focused(window));
        Assert.True(Contrast(ColorOf(confirm.Background), ColorOf(confirm.Foreground)) >= 4.5);

        // Die KI-Knöpfe tragen ein Verb (W-Muster 13); nach „Freundlicher formulieren (KI)“ trägt das Feld die Pille und der Hinweis sagt es.
        var revise = card.GetVisualDescendants().OfType<WrapPanel>().Single(p => p.Name == "ClosureRevise");
        Assert.Equal(["Freundlicher formulieren (KI)", "Mit Entschuldigung (KI)", "Förmlicher formulieren (KI)"],
            revise.Children.OfType<Button>().Select(b => b.Content as string).ToList());
        var friendlier = card.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Freundlicher formulieren (KI)");
        friendlier.Command!.Execute(friendlier.CommandParameter);
        await Pump(Settling);
        Assert.StartsWith("Herzlich willkommen!", text.Text, StringComparison.Ordinal);
        Assert.Contains(card.GetVisualDescendants().OfType<AiPill>(), p => p.IsEffectivelyVisible);

        SaveShot(window, "schliesszeit-absage-dialog-schlicht", theme);
        Press(window, PhysicalKey.Escape);
        await Pump();
        Assert.False((await asking).IsConfirmed);
        Assert.False(main.DialogHost.HasDialog);
        return calendar;
    });

    [Fact]
    public Task Enter_bei_der_Rueckfrage_Termine_absagen_loest_Abbrechen_aus_und_ohne_Termine_gibt_es_nur_Abbrechen() => Render("hell", "schliesszeit-absage-tasten", async (window, main) =>
    {
        await SignedInCalendar(main);
        await Pump();
        var asking = main.DialogHost.ShowAsync(done => new CancelClosureDialogViewModel(SamplePreview(), Vienna, done, Revised));
        await Pump(Settling);
        Press(window, PhysicalKey.Enter);
        await Pump();
        Assert.Equal(ConfirmChoice.Cancelled, (await asking).Choice);

        asking = main.DialogHost.ShowAsync(done => new CancelClosureDialogViewModel(SamplePreview(0), Vienna, done, Revised));
        await Pump(Settling);
        var card = Dialog(window);
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "ClosureEmpty" && t.IsEffectivelyVisible && t.Text == "In der Schließzeit gibt es keinen offenen Termin und keinen Eintrag der Warteliste mehr.");
        Assert.False(DialogButton(window, "ConfirmButton").IsEffectivelyVisible);
        Assert.Same(DialogButton(window, "CancelButton"), Focused(window));
        Press(window, PhysicalKey.Escape);
        await Pump();
        Assert.False((await asking).IsConfirmed);
        return main.Content!;
    });

    [Fact]
    public Task Eine_lange_Rueckfrage_bleibt_im_kleinen_Fenster_und_die_Knoepfe_bleiben_sichtbar() => InWindow("hell", () =>
    {
        var host = new ConfirmDialogHost();
        _ = host.ShowAsync(done => new CancelClosureDialogViewModel(SamplePreview(6), Vienna, done, Revised));
        return new ConfirmDialogView { DataContext = host };
    }, 900, 560, async window =>
    {
        await Pump(Settling);

        var card = Dialog(window);
        Assert.True(card.Bounds.Height <= 560 - 48 + 0.5, $"Karte {card.Bounds.Height} höher als das Fenster");
        var confirm = DialogButton(window, "ConfirmButton");
        var cancel = DialogButton(window, "CancelButton");
        Assert.True(confirm.TranslatePoint(new Point(0, confirm.Bounds.Height), window)!.Value.Y <= 560, "roter Knopf sichtbar");
        Assert.True(cancel.TranslatePoint(new Point(0, cancel.Bounds.Height), window)!.Value.Y <= 560, "Abbrechen sichtbar");
        Assert.Equal("6 Termine absagen", confirm.Content);
    });

    // ---------- Sprachen, Warteliste und Rückzahlung (Runde 5b, Block L2) ----------

    private static ClosureCancellationPreview SamplePreviewLanguages() => SamplePreview() with
    {
        Appointments =
        [
            new(Guid.NewGuid(), "Brigitte Müller", "Klassische Massage", SpaSample.At(10), SpaSample.At(10, 50), "Lena Huber", AppointmentStatus.Booked, ClosureChannel.Email, true, false, "de", 30m),
            new(Guid.NewGuid(), "Emma Smith", "Klassische Massage", SpaSample.At(11), SpaSample.At(11, 50), "Lena Huber", AppointmentStatus.Booked, ClosureChannel.Email, false, false, "en"),
            new(Guid.NewGuid(), "Chloé Martin", "Klassische Massage", SpaSample.At(12), SpaSample.At(12, 50), "Lena Huber", AppointmentStatus.Booked, ClosureChannel.Sms, true, false, "fr", 20m),
            new(Guid.NewGuid(), "Nora Ohne", "Klassische Massage", SpaSample.At(14), SpaSample.At(14, 50), "Lena Huber", AppointmentStatus.Booked, ClosureChannel.None, false, true, "it"),
        ],
        Waitlist =
        [
            new(Guid.NewGuid(), "Wanda Warte", "Klassische Massage", new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 23), DayPart.Any, ClosureChannel.Email, "de"),
            new(Guid.NewGuid(), "Giulia Rossi", "Gesichtsbehandlung", new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 21), DayPart.Morning, ClosureChannel.Email, "it"),
        ],
    };

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Rueckfrage_zeigt_Sprache_je_Gast_den_Abschnitt_Warteliste_und_die_Rueckzahlung(string theme) => Render(theme, "schliesszeit-absage-sprachen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var asking = main.DialogHost.ShowAsync(done => new CancelClosureDialogViewModel(SamplePreviewLanguages(), Vienna, done, Revised, "EUR"));
        await Pump(Settling);

        var card = Dialog(window);
        Assert.Equal(600, card.Bounds.Width, 0.5);
        Assert.True(card.Bounds.Height <= window.Bounds.Height - 48 + 0.5, $"Karte {card.Bounds.Height} höher als das Fenster");

        // Je Gast das Kürzel der Sprache in einer Pille; der Name für Screenreader nennt sie, wenn sie nicht Deutsch ist.
        var rows = card.GetVisualDescendants().OfType<ItemsControl>().Single(i => i.Name == "ClosureRows");
        var pills = rows.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("pill")).Select(b => b.GetVisualDescendants().OfType<TextBlock>().Single().Text).ToList();
        Assert.Equal(["DE", "EN", "FR", "IT"], pills);
        var checks = rows.GetVisualDescendants().OfType<CheckBox>().ToList();
        Assert.Equal("Emma Smith, Mo. 19.10. 11:00 · Klassische Massage, Mail, Englisch", AutomationProperties.GetName(checks[1]));
        Assert.Equal("Brigitte Müller, Mo. 19.10. 10:00 · Klassische Massage, Mail, Anzahlung 30,00 € wird retourniert", AutomationProperties.GetName(checks[0]));

        // Warteliste als eigener Abschnitt mit Überschrift, Satz zur Wirkung, „Alle auswählen“ und je Eintrag Kürzel.
        var waitlist = card.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "ClosureWaitlist");
        Assert.True(waitlist.IsEffectivelyVisible);
        Assert.Contains(waitlist.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Warteliste (2)");
        Assert.Contains(waitlist.GetVisualDescendants().OfType<TextBlock>(), t => t.Text is { } s && s.Contains("bleiben auf der Liste", StringComparison.Ordinal));
        var wishes = waitlist.GetVisualDescendants().OfType<ItemsControl>().Single(i => i.Name == "ClosureWaitlistRows").GetVisualDescendants().OfType<CheckBox>().ToList();
        Assert.Equal(2, wishes.Count);
        Assert.All(wishes, w => Assert.True(w.IsChecked));
        Assert.Equal("Giulia Rossi, Wunsch Mi. 21.10. · Gesichtsbehandlung, Mail, Italienisch", AutomationProperties.GetName(wishes[1]));
        Assert.True(waitlist.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "ClosureWaitlistAll").IsChecked);

        // Zahl der Nachrichten, je Sprache, und die Anzahlungen, die über die Kassa zurückgehen.
        Assert.Equal("5 Nachrichten werden gesendet (4 Mail, 1 SMS). 1 Gast hat keinen Kontakt und wird nicht benachrichtigt.", Text(card, "ClosureSummary"));
        Assert.Equal("Nachrichten nach Sprache: 2 Deutsch, 1 Englisch, 1 Französisch, 1 Italienisch.", Text(card, "ClosureLanguages"));
        Assert.Equal("2 Anzahlungen (50,00 €) gehen über die Kassa zurück (je ein Storno-Beleg, auf dem Zahlungsweg der Einzahlung).", Text(card, "ClosureRefunds"));
        Assert.Contains("Standardvorlage ihrer Sprache", card.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == "Nachricht").Help, StringComparison.Ordinal);

        // Knopfleiste: die Aktion nennt Termine und Einträge, Abbrechen hat weiter den Fokus.
        var confirm = DialogButton(window, "ConfirmButton");
        Assert.Equal("4 Termine absagen, 2 benachrichtigen", confirm.Content);
        Assert.Same(DialogButton(window, "CancelButton"), Focused(window));
        Assert.True(Contrast(ColorOf(confirm.Background), ColorOf(confirm.Foreground)) >= 4.5);

        SaveShot(window, "schliesszeit-absage-dialog", theme);
        // Das Ende der Rückfrage mit den Zeilen zu Sprache und Rückzahlung (der Inhalt scrollt, die Knöpfe bleiben stehen).
        card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "ClosureRefunds").BringIntoView();
        await Pump(Settling);
        SaveShot(window, "schliesszeit-absage-dialog-unten", theme);
        Press(window, PhysicalKey.Escape);
        await Pump();
        Assert.False((await asking).IsConfirmed);
        return calendar;
    });

    private static string? Text(Control owner, string name) => owner.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == name).Text;

    [Fact]
    public Task Rueckfrage_nur_mit_Eintraegen_der_Warteliste_hat_keinen_Textabschnitt() => Render("hell", "schliesszeit-absage-nur-warteliste", async (window, main) =>
    {
        await SignedInCalendar(main);
        await Pump();
        var preview = SamplePreviewLanguages() with { Appointments = [] };
        var asking = main.DialogHost.ShowAsync(done => new CancelClosureDialogViewModel(preview, Vienna, done, Revised, "EUR"));
        await Pump(Settling);

        var card = Dialog(window);
        Assert.DoesNotContain(card.GetVisualDescendants().OfType<TextBox>(), t => t.Name == "ClosureText" && t.IsEffectivelyVisible);
        Assert.True(card.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "ClosureWaitlist").IsEffectivelyVisible);
        Assert.Equal("2 Einträge benachrichtigen", DialogButton(window, "ConfirmButton").Content);
        Assert.Equal("2 Nachrichten werden gesendet (2 Mail).", Text(card, "ClosureSummary"));
        Press(window, PhysicalKey.Escape);
        await Pump();
        Assert.False((await asking).IsConfirmed);
        return main.Content!;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Meldung_ist_ein_Fehler_wenn_die_Schliesszeit_bald_beginnt_und_eine_Warnung_wenn_sie_fern_ist(string theme) => Render(theme, "meldung-schliesszeit-dringend", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenMessagesCommand.Execute(null);
        await Pump(Settling);

        var messages = calendar.Messages.Where(m => m.Kind == MessageKind.Closure).OrderByDescending(m => m.Severity).ToList();
        Assert.Equal([MessageSeverity.Error, MessageSeverity.Warning], messages.Select(m => m.Severity));
        Assert.Equal("2 Termine und 1 Eintrag der Warteliste in der Schließzeit (Betriebsurlaub, Mo. 19.10. bis Mi. 21.10.).", messages[0].Text);
        Assert.Equal("3 Einträge der Warteliste in der Schließzeit (Sonderschließung, Mo. 19.10. bis Mi. 21.10.).", messages[1].Text);
        var headings = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Contains("Fehler", headings);
        Assert.Contains("Warnungen", headings);
        Assert.True(calendar.MessagesHaveError);
        Assert.Equal(2, window.GetVisualDescendants().OfType<Button>().Count(b => b.IsEffectivelyVisible && b.Content as string == "Termine absagen …"));
        return calendar;
    }, Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.CalendarDay() with
    {
        ClosuresDue =
        [
            SampleClosure(open: 2) with { WaitlistEntries = 1, Urgent = true },
            SampleClosure(ClosureKind.SpecialClosure, "Sonderschließung", open: 0) with { Id = Guid.NewGuid(), WaitlistEntries = 3 },
        ],
    }));

    private static readonly DepositReturnInfo SampleDeposit = new(Guid.NewGuid(), "K1 Nr. 12, 19.10.2026", 30m, "Karte", SpaSample.At(9), true);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Termin_Panel_zeigt_dem_Inhaber_den_Abschnitt_Anzahlung_mit_dem_Knopf_Anzahlung_retournieren(string theme) => Render(theme, "termin-anzahlung-retournieren", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");

        var card = Named<Border>(panel, "DepositCard");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Contains("panelcard", card.Classes);
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Anzahlung");
        Assert.Equal("30,00 € bezahlt (Karte), Beleg K1 Nr. 12, 19.10.2026. Beim Retournieren geht das Geld auf diesem Weg zurück: Storno-Beleg über die Kassa, der Grund steht darauf.",
            card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("muted")).Text);
        var button = Named<Button>(card, "ReturnDepositButton");
        Assert.Equal("Anzahlung retournieren …", button.Content);
        Assert.Contains("danger", button.Classes);
        Assert.True(button.IsEnabled);
        return calendar;
    }, Api().On($"GET /api/appointments/{SpaSample.Selected.Id}/deposit-return", SampleDeposit));

    [Fact]
    public Task Die_Rezeption_sieht_den_Abschnitt_Anzahlung_nicht_und_fragt_den_Server_nicht() => Render("hell", "termin-anzahlung-rezeption", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);

        Assert.False(Named<Border>(Named<Border>(window, "SidePanel"), "DepositCard").IsEffectivelyVisible);
        Assert.False(calendar.Selected!.MayReturnDeposit);
        return calendar;
    }, ReceptionApi().On($"GET /api/appointments/{SpaSample.Selected.Id}/deposit-return", SampleDeposit));
}
