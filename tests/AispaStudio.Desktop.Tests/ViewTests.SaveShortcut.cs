using System.Xml.Linq;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Strg+S speichert in allen Masken der Seitenleiste wie ihr Speichern-Knopf (Auftraggeber, 29.10.2026), nur wenn der Knopf bedienbar ist.</summary>
public sealed partial class ViewTests
{
    /// <summary>Primärknöpfe der Seitenleiste, die kein Speichern sind: Listen, Assistenten und Abschlüsse ohne eigene Eingaben („Befehl@Vorlage“, wenn der Name mehrdeutig ist).</summary>
    private static readonly string[] NotSaveCommands =
    [
        "BookActiveCommand", "ApplyCommand", "BookCommand@Waitlist", "CloseCommand", "FinishCommand", "ConfirmApprovalCommand", "Item.ActionCommand",
    ];

    private static readonly XNamespace UiNamespace = "using:AispaStudio.Desktop.Controls";

    private static void PressSave(Window window) => Press(window, PhysicalKey.S, RawInputModifiers.Control);

    private static int CallsTo(StubApi api, string route) => api.Calls.Count(c => c.Route == route);

    [Fact]
    public void Jeder_Speichern_Knopf_der_Seitenleiste_ist_fuer_Strg_S_markiert_und_nennt_das_Kuerzel()
    {
        var views = Path.Combine(MessageBarTests.FindRepoRoot(), "src", "AispaStudio.Desktop", "Views");
        var calendar = XDocument.Load(Path.Combine(views, "CalendarView.axaml"));
        var primaries = calendar.Descendants().Where(e => e.Name.LocalName == "DataTemplate" && e.Attribute("DataType") is not null)
            .SelectMany(t => t.Descendants().Where(e => e.Name.LocalName == "Button" && (e.Attribute("Classes")?.Value.Split(' ').Contains("primary") ?? false))
                .Select(b => (Template: t.Attribute("DataType")!.Value.Replace("vm:", "", StringComparison.Ordinal).Replace("ViewModel", "", StringComparison.Ordinal), Button: b)))
            .ToList();
        Assert.NotEmpty(primaries);
        foreach (var (template, button) in primaries)
        {
            var command = button.Attribute("Command")?.Value.Replace("{Binding ", "", StringComparison.Ordinal).TrimEnd('}') ?? "";
            var marked = button.Attribute(UiNamespace + "SaveShortcut.IsSave")?.Value == "True";
            var exempt = NotSaveCommands.Contains(command) || NotSaveCommands.Contains($"{command}@{template}");
            Assert.True(marked ^ exempt, $"{template}: {command} ist {(marked ? "markiert und ausgenommen" : "weder markiert noch ausgenommen")}");
            if (marked)
            {
                Assert.Contains("(Strg+S", button.Attribute("ToolTip.Tip")?.Value);
            }
        }

        Assert.Contains(calendar.Descendants(), e => e.Attribute("Classes")?.Value == "sidepanel" && e.Attribute(UiNamespace + "SaveShortcut.IsScope")?.Value == "True");
        var form = XDocument.Load(Path.Combine(views, "CustomerContactForm.axaml"));
        Assert.Equal("True", form.Root!.Attribute(UiNamespace + "SaveShortcut.IsScope")?.Value);
        Assert.Contains(form.Descendants(), e => e.Attribute(UiNamespace + "SaveShortcut.IsSave")?.Value == "True" && e.Attribute("ToolTip.Tip")?.Value == "Speichern (Strg+S)");
    }

    [Fact]
    public Task Strg_S_speichert_die_Notiz_zum_Termin() => Render("hell", "strg-s-notiz", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(NoteFree);
        await Pump(Settling);
        ClickCenter(window, Named<Button>(Named<Border>(window, "SidePanel"), "AddNoteLink"));
        await Pump(Settling);
        Assert.Equal("AppointmentNoteBox", Assert.IsType<TextBox>(Focused(window)).Name);

        window.KeyTextInput("Lavendelöl");
        PressSave(window);
        await Pump(Settling);

        Assert.Equal("Lavendelöl", calendar.Selected!.Note);
        Assert.False(calendar.Selected.IsEditingNote);
        return calendar;
    }, NoteApi("Lavendelöl"));

    [Fact]
    public Task Strg_S_ausserhalb_der_Seitenleiste_speichert_nichts() => Render("hell", "strg-s-aussen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(NoteFree);
        await Pump(Settling);
        ClickCenter(window, Named<Button>(Named<Border>(window, "SidePanel"), "AddNoteLink"));
        await Pump(Settling);
        window.KeyTextInput("Lavendelöl");

        Named<SearchField>(window, "SearchBox").Focus();
        await Pump();
        PressSave(window);
        await Pump(Settling);

        Assert.True(calendar.Selected!.IsEditingNote);
        Assert.Null(calendar.Selected.Note);
        return calendar;
    }, NoteApi("Lavendelöl"));

    [Fact]
    public Task Strg_S_speichert_die_Tagesnotiz_auch_ohne_Fokus() => Render("hell", "strg-s-tagesnotiz", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        Press(window, PhysicalKey.Period);
        await Pump(Settling);
        window.KeyTextInput("Gruppe kommt um 14 Uhr");

        // Verschwindet das Feld mit dem Fokus, hat im Fenster niemand mehr den Fokus; Strg+S kommt trotzdem an.
        Assert.IsType<TextBox>(Focused(window)).IsVisible = false;
        await Pump();
        Assert.Null(Focused(window));
        PressSave(window);
        await Pump(Settling);

        Assert.False(calendar.MessagesPanel!.IsEditingNote);
        Assert.Contains(calendar.DayNotes, n => n.Text == "Gruppe kommt um 14 Uhr");
        return calendar;
    }, NoNotesApi().On($"POST /api/locations/{SpaSample.LocationId}/calendar-notes",
        new CalendarNoteDto(Guid.NewGuid(), SpaSample.Day, "Gruppe kommt um 14 Uhr", null, null, null, false, SpaSample.At(11, 41)), System.Net.HttpStatusCode.Created));

    [Fact]
    public Task Strg_S_uebernimmt_die_Zeiten_nur_mit_lesbarer_Eingabe_und_nimmt_den_getippten_Wert_mit()
    {
        var route = $"PUT /api/appointments/{SpaSample.Selected.Id}/times";
        var api = Api().On(route, SpaSample.Selected);
        return Render("hell", "strg-s-zeiten", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            calendar.OpenAppointment(SpaSample.Selected);
            await Pump(Settling);
            calendar.Selected!.BeginEditTimesCommand.Execute(null);
            await Pump(Settling);
            var box = Named<NumberField>(window, "PreparationBox");
            box.Focus(NavigationMethod.Tab);
            await Pump();

            // Unlesbar (über dem Höchstwert): der Fehler bleibt am Feld, nichts geht an den Server.
            box.SelectAll();
            window.KeyTextInput("200");
            PressSave(window);
            await Pump(Settling);
            Assert.Equal(0, CallsTo(api, route));
            Assert.NotNull(box.Error);
            Assert.True(calendar.Selected.IsEditingTimes);

            // Lesbar und noch nicht mit Enter bestätigt: Strg+S übernimmt erst das Feld, dann die Zeiten.
            box.SelectAll();
            window.KeyTextInput("20");
            PressSave(window);
            await Pump(Settling);
            Assert.Equal(1, CallsTo(api, route));
            Assert.Contains("20", api.Calls.Single(c => c.Route == route).Body);
            return calendar;
        }, api);
    }

    [Fact]
    public Task Strg_S_speichert_die_Kundendaten_im_Panel_nur_mit_Namen()
    {
        var customer = SpaSample.Customers[0] with { Id = SpaSample.Selected.CustomerId };
        var route = $"PUT /api/customers/{customer.Id}";
        var api = Api().On($"GET /api/customers/{customer.Id}", customer).On(route, customer with { FirstName = "Gitti" });
        return Render("hell", "strg-s-kundendaten", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            calendar.OpenAppointment(SpaSample.Selected);
            await Pump();
            calendar.Selected!.EditCustomerCommand.Execute(null);
            await Pump();
            await Pump();
            var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);
            var formModel = calendar.Selected.CustomerForm!;

            // Ohne Vor- und Nachnamen ist „Speichern“ gesperrt; Strg+S tut dann nichts.
            formModel.FirstName = "";
            formModel.LastName = "";
            form.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "FirstNameBox").Focus();
            await Pump();
            PressSave(window);
            await Pump(Settling);
            Assert.Equal(0, CallsTo(api, route));

            window.KeyTextInput("Gitti");
            PressSave(window);
            await Pump(Settling);
            Assert.Equal(1, CallsTo(api, route));
            return calendar;
        }, api);
    }

    [Fact]
    public Task Strg_S_bucht_in_Neuer_Termin_ohne_Gast_nichts() => Render("hell", "strg-s-neuer-termin", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.NewAppointmentCommand.Execute(null);
        await Pump(Settling);
        var booking = calendar.Booking!;
        Assert.Null(booking.Customer);
        Assert.False(booking.BookCommand.CanExecute(null));

        PressSave(window);
        await Pump(Settling);

        Assert.Same(booking, calendar.Booking);
        Assert.Same(booking, calendar.ActivePanel);
        return calendar;
    });
}
