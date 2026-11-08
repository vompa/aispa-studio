using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Runde 5b, Ä45: Geburtstag eines Gastes im Kalender der Windows-App. Brigitte Müller (Termin 10:30) hat am Tag Geburtstag und wird 42, Hannah Schmid (17:00) in zwei Tagen.
/// Bilder für das Handbuch (Eintrag, Panel) und die PRs, wenn AISPA_SCREENSHOTS gesetzt ist.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly Guid BirthdayGuest = SpaSample.Selected.CustomerId;

    /// <summary>Der Beispieltag, an dem Brigitte Müller Geburtstag hat und Hannah Schmid in zwei Tagen.</summary>
    private static StubApi BirthdayApi()
    {
        var day = SpaSample.CalendarDay();
        var near = day.Appointments.First(a => a.CustomerName == "Hannah Schmid").CustomerId;
        day = day with
        {
            Guests = [.. day.Guests!.Select(g =>
                g.CustomerId == BirthdayGuest ? g with { Birthday = new GuestBirthday(SpaSample.Day, 0, 42, 42) }
                : g.CustomerId == near ? g with { Birthday = new GuestBirthday(SpaSample.Day.AddDays(2), 2, 38, 37) }
                : g)],
        };
        return Api().On(DayRoute, day).On($"GET /api/customers/{BirthdayGuest}/summary", SpaSample.Summary());
    }

    private static List<Icon> GiftIcons(Visual root) =>
        [.. root.GetVisualDescendants().OfType<Icon>().Where(i => i.Classes.Contains("birthday") && i.IsEffectivelyVisible)];

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Der_Eintrag_zeigt_am_Tag_des_Geburtstags_ein_kleines_Geschenk_ohne_Text_und_mit_lesbarem_Hinweis(string theme) => Render(theme, "kalender-geburtstag", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump(Settling);

        var gifts = GiftIcons(window);
        var gift = Assert.Single(gifts);
        Assert.Equal(SpaSample.Selected.Id, Assert.IsType<CalendarItem>(gift.DataContext).Appointment!.Id);
        Assert.Equal(Icons.Gift, gift.Data);
        Assert.Equal((16, 16), (gift.Width, gift.Height));
        Assert.Equal(ColorOf(window, "AccentBrush"), ColorOf(gift.Foreground));
        Assert.Equal("Geburtstag heute", ToolTip.GetTip(gift));
        Assert.Equal("Geburtstag heute", AutomationProperties.GetName(gift));

        // Der Hinweis ist ein Kind des Symbols: seine Schrift muss auf der Hinweisfläche lesbar sein (Kind-Selektor, Skill fluent2-avalonia).
        ToolTip.SetIsOpen(gift, true);
        await Pump(2);
        var popup = window.GetVisualDescendants().OfType<ToolTip>().Single(t => Equals(t.Content, "Geburtstag heute"));
        var text = popup.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.True(Contrast(ColorOf(popup.Background), ColorOf(text.Foreground)) >= 4.5, $"Text {ColorOf(text.Foreground)} auf {ColorOf(popup.Background)}");
        ToolTip.SetIsOpen(gift, false);

        // Am Eintrag steht kein Alter und kein Text; Hannah (in zwei Tagen) hat kein Geschenk.
        var entry = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("appt") && b.DataContext is CalendarItem { Appointment.Id: var id } && id == SpaSample.Selected.Id);
        Assert.DoesNotContain("42", TextsIn(entry));
        Assert.DoesNotContain(TextsIn(entry), t => t.Contains("Geburtstag", StringComparison.Ordinal));
        Assert.DoesNotContain(gifts, g => g.DataContext is CalendarItem { Title: "Hannah Schmid" });
        return calendar;
    }, BirthdayApi());

    [Fact]
    public Task Die_Hover_Karte_nennt_Geburtstag_heute_mit_Alter_und_bei_Hannah_nur_das_Datum() => Render("hell", "kalender-geburtstag-karte", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var view = window.GetVisualDescendants().OfType<CalendarView>().Single();

        async Task<List<string?>> CardTextsAsync(string guest)
        {
            var border = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("appt") && b.DataContext is CalendarItem { Title: var title } && title == guest);
            var card = Assert.IsType<HoverCard>(ToolTip.GetTip(border));
            var content = view.DataTemplates.First(t => t.Match(card)).Build(card)!;
            content.DataContext = card;
            var cardWindow = new Window { Width = 380, SizeToContent = SizeToContent.Height, Content = new Border { Padding = new Thickness(12), Child = content } };
            cardWindow.Show();
            await Pump();
            Assert.Contains(cardWindow.GetVisualDescendants().OfType<Icon>(), i => i.Data == Icons.Gift && i.IsVisible);
            return [.. cardWindow.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)];
        }

        Assert.Contains("Geburtstag heute · 42", await CardTextsAsync("Brigitte Müller"));
        Assert.Contains("Geburtstag am 21.10.", await CardTextsAsync("Hannah Schmid"));
        return calendar;
    }, BirthdayApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Das_Termin_Panel_nennt_den_Geburtstag_des_Gastes(string theme) => Render(theme, "termin-geburtstag", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        // Zuerst Hannah (Geburtstag in zwei Tagen: nur das Datum), zuletzt Brigitte (heute, mit Alter): ihr Panel ist das Bild.
        var hannah = calendar.Columns.SelectMany(c => c.Items).First(i => i.Title == "Hannah Schmid").Appointment!;
        calendar.OpenAppointment(hannah);
        await Pump(Settling);
        Assert.Contains("Geburtstag am 21.10.", TextsIn(Named<Border>(window, "SidePanel")));

        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");
        Assert.Contains("Geburtstag heute · 42", TextsIn(panel));
        Assert.DoesNotContain(TextsIn(panel), t => t.Contains("21.10.", StringComparison.Ordinal));
        Assert.Contains(panel.GetVisualDescendants().OfType<Icon>(), i => i.Data == Icons.Gift && i.IsEffectivelyVisible);
        return calendar;
    }, BirthdayApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Der_Tagesueberblick_nennt_Geburtstag_heute_oberhalb_von_Als_Naechstes(string theme) => WithOverview(theme, "kalender-geburtstag-ueberblick", (window, calendar, content) =>
    {
        Assert.Equal("Geburtstag heute: Brigitte Müller (10:30)", calendar.BirthdayLine);
        var texts = content.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).ToList();
        var line = texts.Single(t => t.Text == "Geburtstag heute: Brigitte Müller (10:30)");
        var next = texts.Single(t => t.Text == "Als Nächstes");
        Assert.True(TopOf(window, line) < TopOf(window, next));
        Assert.Contains(content.GetVisualDescendants().OfType<Icon>(), i => i.Data == Icons.Gift && i.IsEffectivelyVisible);
        return Task.CompletedTask;
    }, BirthdayApi());

    [Fact]
    public Task Ohne_Geburtstag_am_Tag_gibt_es_weder_Symbol_noch_Zeile() => Render("hell", "kalender-ohne-geburtstag", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump(Settling);

        Assert.Empty(GiftIcons(window));
        Assert.Null(calendar.BirthdayLine);
        return calendar;
    });
}
