using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Seite „Verlauf“ (Arbeitsanweisung Verlauf, Aufgabe B): Einträge aller Arten in Hell und Dunkel, als Inhaberin und als Rezeption, Leerzustand.</summary>
public sealed partial class ViewTests
{
    private static readonly string ActivityToday = $"GET /api/locations/{SpaSample.LocationId}/history?from=2026-10-19&until=2026-10-19";

    private static readonly string[] AllKindChips = ["Buchungen", "Verschoben", "Stornos", "Status", "Zahlungen", "Neue Kunden", "Kundendaten", "Behandlungen", "Dienstplan", "Zugänge"];

    private static ActivityEntry Activity(long seq, DateTimeOffset at, ActivityKind kind, string text, string? customer = "Brigitte Müller", string? actor = "Lena",
        ActivityChannel channel = ActivityChannel.Reception, bool? late = null) =>
        new(seq, at, kind, text, actor, channel, Guid.NewGuid(), customer, Guid.NewGuid(), null, null, [], late);

    private static ActivityPage ActivitySample(bool leadership)
    {
        var yesterday = SpaSample.At(17, 20).AddDays(-1);
        List<ActivityEntry> items =
        [
            Activity(20, SpaSample.At(11, 12), ActivityKind.Paid, "Bezahlt bar: 85,00 € (Beleg 2026-0412)", actor: "Rita"),
            Activity(19, SpaSample.At(10, 55), ActivityKind.Booked, "Gebucht von Rita: Hot-Stone-Massage, 15:00 bei Lena", customer: "Anna Huber", actor: "Online-Buchung", channel: ActivityChannel.Online),
            Activity(18, SpaSample.At(10, 30), ActivityKind.Cancelled, "Storniert von Rita: Grund „Krank“", late: true),
            Activity(17, SpaSample.At(10, 5), ActivityKind.Restored, "Stornierung zurückgenommen von Rita"),
            Activity(16, SpaSample.At(9, 50), ActivityKind.Moved, "Verschoben von Lena: 14:00 → 15:30"),
            Activity(15, SpaSample.At(9, 40), ActivityKind.StatusChanged, "Eingecheckt von Rita"),
            Activity(14, SpaSample.At(9, 30), ActivityKind.AppointmentChanged, "Geändert von Rita: Raum Zeder → Raum Lotus"),
            Activity(13, SpaSample.At(9, 20), ActivityKind.Deposit, "Anzahlung 30,00 € bar", actor: "Rita"),
            Activity(12, SpaSample.At(9, 10), ActivityKind.ReceiptVoided, "Beleg storniert", actor: "Rita"),
            Activity(11, SpaSample.At(9, 0), ActivityKind.CustomerCreated, "Angelegt von Rita", customer: "Paul Muster"),
            Activity(10, SpaSample.At(8, 50), ActivityKind.CustomerChanged, "Geändert von Rita: Telefon 0664 111 → 0664 222"),
            Activity(9, SpaSample.At(8, 40), ActivityKind.TreatmentChanged, "Preis geändert von Katrin: 79,00 € → 89,00 €", customer: null, actor: "Katrin"),
            Activity(8, SpaSample.At(8, 30), ActivityKind.Booked, "Gebucht über die Terminannahme: Gesichtsbehandlung", actor: null, channel: ActivityChannel.Background),
        ];
        if (leadership)
        {
            items.Add(Activity(7, SpaSample.At(8, 20), ActivityKind.RosterChanged, "Dienst eingetragen von Katrin: Lena 09:00–17:00", customer: null, actor: "Katrin"));
            items.Add(Activity(6, SpaSample.At(8, 10), ActivityKind.AccessChanged, "Rolle geändert von Katrin: Rezeption → Leitung", customer: null, actor: "Katrin"));
        }

        items.Add(Activity(5, yesterday, ActivityKind.Booked, "Gebucht von Rita: Rückenmassage"));
        return new ActivityPage(items, null);
    }

    private static List<string?> VisibleChips(Window window) =>
        [.. window.GetVisualDescendants().OfType<ActivityView>().Single().GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("headchip") && b.IsEffectivelyVisible && b.Content is string).Select(b => (string?)b.Content)];

    private static async Task OpenActivity(ShellViewModel shell, Func<bool>? done = null)
    {
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Activity);
        for (var i = 0; i < 40 && !(done?.Invoke() ?? shell.Activity.Rows.Count > 0); i++)
        {
            await Pump();
        }

        await Pump(Settling);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verlauf_Seite_zeigt_als_Inhaberin_Eintraege_aller_Arten_nach_Tagen(string theme) => Render(theme, "verlauf-seite", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await OpenActivity(shell);

        // Navigation: „Verlauf“ direkt nach „Kunden“.
        var titles = shell.Pages.Select(p => p.Title).ToList();
        Assert.Equal(titles.IndexOf("Kunden") + 1, titles.IndexOf("Verlauf"));

        // Alle zehn Art-Chips und die Zeitraum-Chips; Zurücksetzen erst bei einem Filter.
        var chips = VisibleChips(window);
        Assert.Equal(["Heute", "Gestern", "7 Tage", "30 Tage", "Zeitraum", .. AllKindChips], chips);
        Assert.DoesNotContain("Zurücksetzen", chips);

        var headers = shell.Activity.Rows.OfType<ActivityDayHeader>().Select(h => h.Title).ToList();
        Assert.Equal(["Heute, Montag, 19. Oktober", "Gestern, Sonntag, 18. Oktober"], headers);
        var rows = window.GetVisualDescendants().OfType<ActivityView>().Single().GetVisualDescendants().OfType<ListBoxItem>().ToList();
        Assert.Contains(rows, r => ControlAutomationPeer.CreatePeerForElement(r).GetName() == "Bezahlt bar: 85,00 € (Beleg 2026-0412)");
        Assert.All(rows, r => Assert.False(HasUnreadableName(r, out var name), name));
        Assert.Contains(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("pill") && b.IsEffectivelyVisible && b.Child is TextBlock { Text: "Kurzfristig" });
        return calendar;
    }, Api().On(ActivityToday, ActivitySample(leadership: true)));

    [Fact]
    public Task Verlauf_Seite_zeigt_als_Rezeption_keine_Chips_fuer_Dienstplan_und_Zugaenge() => Render("hell", "verlauf-rezeption", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await OpenActivity(shell);

        var chips = VisibleChips(window);
        Assert.Contains("Zahlungen", chips);
        Assert.DoesNotContain("Dienstplan", chips);
        Assert.DoesNotContain("Zugänge", chips);
        Assert.False(shell.Activity.HasPersonChoice);
        return calendar;
    }, ReceptionApi().On(ActivityToday, ActivitySample(leadership: false)));

    [Fact]
    public Task Verlauf_Leerzustand_mit_Filter_bietet_Filter_zuruecksetzen_an() => Render("hell", "verlauf-leer", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await OpenActivity(shell, () => shell.Activity.ShowEmpty);
        var page = window.GetVisualDescendants().OfType<ActivityView>().Single();
        Assert.Equal("Im gewählten Zeitraum ist nichts passiert.", Named<TextBlock>(page, "EmptyText").Text);
        Assert.False(Named<Button>(page, "EmptyResetButton").IsEffectivelyVisible);

        shell.Activity.KindChips.Single(c => c.Label == "Stornos").ToggleCommand.Execute(null);
        for (var i = 0; i < 40 && shell.Activity.IsLoading; i++)
        {
            await Pump();
        }

        await Pump(Settling);
        Assert.Equal("Nichts gefunden. Filter zurücksetzen?", Named<TextBlock>(page, "EmptyText").Text);
        Assert.True(Named<Button>(page, "EmptyResetButton").IsEffectivelyVisible);
        Assert.Contains("Zurücksetzen", VisibleChips(window));
        return calendar;
    }, Api().On(ActivityToday, new ActivityPage([], null))
        .On($"{ActivityToday}&kinds=Cancelled&kinds=Restored", new ActivityPage([], null)));
}
