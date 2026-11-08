using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Runde 5, Ä33: Notiz zum Termin im Termin-Panel (Link, Hinweisfläche mit Stift, mehrzeiliges Feld mit Zähler).</summary>
public sealed partial class ViewTests
{
    /// <summary>Der Beispieltermin ohne Notiz (gleiche Id wie <see cref="SpaSample.Selected"/>, damit die Stubs ihn treffen).</summary>
    private static readonly AppointmentDto NoteFree = SpaSample.Selected with { Note = null };

    private static StubApi NoteApi(string? saved)
    {
        var day = SpaSample.CalendarDay();
        day = day with { Appointments = [.. day.Appointments.Select(a => a.Id == NoteFree.Id ? NoteFree : a)] };
        return Api().On(DayRoute, day).On($"PUT /api/appointments/{NoteFree.Id}/note", NoteFree with { Note = saved, UpdatedAt = SpaSample.At(11, 41) });
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Termin_Panel_ohne_Notiz_zeigt_unter_den_Terminangaben_den_Link_Notiz_hinzufuegen(string theme) => Render(theme, "termin-notiz-link", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(NoteFree);
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");

        var link = Named<Button>(panel, "AddNoteLink");
        Assert.True(link.IsEffectivelyVisible);
        Assert.Contains("link", link.Classes);
        Assert.Equal("Notiz hinzufügen", link.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        Assert.Equal(ColorOf(window, "AccentBrush"), ColorOf(link.Foreground));
        // Unter den Terminangaben (Mitarbeiter, Raum, Preis) und links bündig mit ihnen.
        var price = panel.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("num") && t.Text == calendar.Selected!.Price);
        Assert.True(TopOf(window, link) > TopOf(window, price));
        Assert.True(LeftOf(window, link) < LeftOf(window, price));
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("hint") && b.Classes.Contains("warn") && b.IsEffectivelyVisible && b.DataContext is AppointmentViewModel);
        Assert.False(Named<Button>(panel, "EditNoteButton").IsEffectivelyVisible);
        return calendar;
    }, NoteApi(null));

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Termin_Panel_mit_Notiz_zeigt_die_gelbe_Hinweisflaeche_mit_Stift(string theme) => Render(theme, "termin-notiz-hinweis", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");

        Assert.False(Named<Button>(panel, "AddNoteLink").IsEffectivelyVisible);
        var hint = panel.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("hint") && b.Classes.Contains("warn") && b.IsEffectivelyVisible);
        Assert.Contains(hint.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Lavendelöl, wenig Gespräch, Fokus Nacken");
        var pencil = Named<Button>(hint, "EditNoteButton");
        Assert.True(pencil.IsEffectivelyVisible);
        Assert.Equal((28, 28), (pencil.Bounds.Width, pencil.Bounds.Height));
        Assert.Equal("Notiz ändern", Avalonia.Automation.AutomationProperties.GetName(pencil));
        Assert.Equal("Notiz ändern", ToolTip.GetTip(pencil));
        Assert.Contains(pencil.GetVisualDescendants().OfType<Icon>(), i => i.Data == Icons.Pencil);
        // Das Stift-Symbol steht rechts in der Hinweisfläche, die Notiz-Fläche behält die Warnfarben.
        Assert.True(LeftOf(window, pencil) > LeftOf(window, hint) + (hint.Bounds.Width / 2));
        Assert.Equal(ColorOf(window, "WarnSoftBrush"), ColorOf(hint.Background));
        return calendar;
    });

    [Fact]
    public Task Notiz_schreiben_im_Panel_Feld_mit_Zaehler_Strg_Enter_speichert_und_Esc_geht_in_Stufen() => Render("hell", "termin-notiz-schreiben", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(NoteFree);
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");

        // Klick auf den Link: das Feld geht auf und hat den Fokus (Marke am Ende).
        ClickCenter(window, Named<Button>(panel, "AddNoteLink"));
        await Pump(Settling);
        var box = Assert.IsType<TextBox>(Focused(window));
        Assert.Equal("AppointmentNoteBox", box.Name);
        Assert.Equal((68, true, 500), (box.Height, box.AcceptsReturn, box.MaxLength));
        var field = panel.GetVisualDescendants().OfType<FormField>().Single(f => f.IsEffectivelyVisible && f.Label == "Notiz zum Termin");
        var counter = field.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Counter");
        Assert.Equal("noch 500 Zeichen", counter.Text);
        Assert.Equal("Nur für diesen Termin.", field.Message);
        Assert.False(Named<Button>(panel, "AddNoteLink").IsEffectivelyVisible);

        // Zeilenumbruch mit Enter, Zähler zählt mit.
        window.KeyTextInput("Lavendelöl");
        Press(window, PhysicalKey.Enter);
        window.KeyTextInput("wenig Gespräch");
        Assert.Equal("Lavendelöl\nwenig Gespräch", calendar.Selected!.NoteText.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal($"noch {500 - calendar.Selected.NoteText.Length} Zeichen", counter.Text);

        // Esc: erst zurückstellen, dann abbrechen; der Fokus geht zum Link zurück.
        Press(window, PhysicalKey.Escape);
        await Pump();
        Assert.Equal("", calendar.Selected.NoteText);
        Assert.True(calendar.Selected.IsEditingNote);
        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.False(calendar.Selected.IsEditingNote);
        Assert.Equal("AddNoteLink", Assert.IsType<Button>(Focused(window)).Name);
        Assert.NotNull(calendar.Selected);

        // Strg+Enter im Feld speichert.
        ClickCenter(window, Named<Button>(panel, "AddNoteLink"));
        await Pump(Settling);
        window.KeyTextInput("Lavendelöl");
        Press(window, PhysicalKey.Enter, RawInputModifiers.Control);
        await Pump(Settling);
        Assert.Equal("Lavendelöl", calendar.Selected!.Note);
        Assert.False(calendar.Selected.IsEditingNote);
        Assert.Equal("EditNoteButton", Assert.IsType<Button>(Focused(window)).Name);
        return calendar;
    }, NoteApi("Lavendelöl"));

    [Fact]
    public Task Notiz_aendern_ueber_den_Stift_laedt_den_Text_und_zeigt_Fehler_am_Feld() => Render("hell", "termin-notiz-aendern", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        var panel = Named<Border>(window, "SidePanel");

        ClickCenter(window, Named<Button>(panel, "EditNoteButton"));
        await Pump(Settling);

        var box = Assert.IsType<TextBox>(Focused(window));
        Assert.Equal("Lavendelöl, wenig Gespräch, Fokus Nacken", box.Text);
        Assert.Equal(box.Text!.Length, box.CaretIndex);
        Assert.Equal("Notiz speichern", panel.GetVisualDescendants().OfType<Button>().First(b => b.IsDefault && b.IsEffectivelyVisible).Content);
        // Der Hinweis mit dem Stift ist während des Schreibens weg, die Notiz steht nur im Feld.
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("hint") && b.Classes.Contains("warn") && b.IsEffectivelyVisible);

        // Zu langer Text: Fehler am Feld, der Text bleibt.
        calendar.Selected!.NoteText = new string('x', 501);
        await calendar.Selected.SaveNoteAsync();
        await Pump();
        var field = panel.GetVisualDescendants().OfType<FormField>().Single(f => f.IsEffectivelyVisible && f.Label == "Notiz zum Termin");
        Assert.Equal(("Höchstens 500 Zeichen.", FieldMessageKind.Error), (field.Message, field.MessageKind));
        Assert.Equal(501, calendar.Selected.NoteText.Length);
        return calendar;
    });

    /// <summary>Ä281 (Slice 119): Inzwischen hat ein anderer Arbeitsplatz die Notiz geschrieben; der Server lehnt den alten Stand ab.</summary>
    private static StubApi StaleNoteApi()
    {
        var day = SpaSample.CalendarDay();
        day = day with { Appointments = [.. day.Appointments.Select(a => a.Id == NoteFree.Id ? NoteFree : a)] };
        var current = NoteFree with { Note = "Vom anderen Arbeitsplatz", UpdatedAt = SpaSample.At(11, 41), Version = 9 };
        return Api().On(DayRoute, day).On($"PUT /api/appointments/{NoteFree.Id}/note",
            new BookingRejected("Der Termin wurde inzwischen an einem anderen Arbeitsplatz geändert. Bitte den aktuellen Stand prüfen.", [], Current: current),
            System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public Task Notiz_von_altem_Stand_zeigt_Hinweis_oben_im_Panel_und_den_aktuellen_Stand() => Render("hell", "termin-notiz-konflikt", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(NoteFree);
        await Pump(Settling);
        ClickCenter(window, Named<Button>(Named<Border>(window, "SidePanel"), "AddNoteLink"));
        await Pump(Settling);
        window.KeyTextInput("Lavendelöl");

        ClickCenter(window, Named<Border>(window, "SidePanel").GetVisualDescendants().OfType<Button>().First(b => b.IsDefault && b.IsEffectivelyVisible));
        await Pump(Settling);

        var message = "Der Termin wurde inzwischen an einem anderen Arbeitsplatz geändert. Bitte den aktuellen Stand prüfen.";
        var panel = Named<Border>(window, "SidePanel");
        Assert.Contains(panel.GetVisualDescendants().OfType<InfoBar>(), b => b.IsEffectivelyVisible && b.Message == message);
        Assert.Equal("Vom anderen Arbeitsplatz", calendar.Selected!.Note);
        Assert.False(calendar.Selected.IsEditingNote);
        return calendar;
    }, StaleNoteApi());
}
