using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 95 (Ä206), seit ADR 0098 die Seite „Schließzeiten“ im Fenster: eine Liste mit Schließzeiten und Raumsperren, Formular in der Seitenleiste, „Einrichtung“, Toast, Eingabe per Maus
/// und Tastatur, Strg+S, Absagen und Löschen über die Rückfrage, Scans. Die Raumsperre steht in <c>ViewTests.AdminRoomClosures.cs</c>.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly Guid VacationId = Guid.Parse("00000000-0000-0000-0000-0000000c1051");
    private static readonly Guid OutageId = Guid.Parse("00000000-0000-0000-0000-0000000c1052");
    private static readonly Guid MaintenanceId = Guid.Parse("00000000-0000-0000-0000-0000000c2051");

    private static ClosureDto Vacation(int open = 2) => new(VacationId, SpaSample.LocationId, new(2026, 12, 24), null, new(2026, 12, 26), null, new DateTimeOffset(2026, 12, 24, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 27, 0, 0, 0, TimeSpan.Zero),
        ClosureKind.Vacation, "Inventur", "Wegen Betriebsurlaub geschlossen.", ClosurePhase.Planned, "Katrin Aigner", SpaSample.At(8), open, 0, 0);

    private static readonly ClosureDto PastOutage = new(OutageId, SpaSample.LocationId, new(2026, 9, 4), null, new(2026, 9, 5), null, new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero),
        ClosureKind.Outage, "Wasserschaden", null, ClosurePhase.Ended, "Katrin Aigner", SpaSample.At(8), 0, 1, 0);

    private static readonly RoomClosureDto CedarMaintenance = new(MaintenanceId, SpaSample.LocationId, SpaSample.Cedar.Id, SpaSample.Cedar.Name, new(2026, 10, 12), null, new(2026, 10, 21), null,
        new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero), RoomClosureKind.Maintenance, "Lüftung wird gewartet", ClosurePhase.Active, "Katrin Aigner", SpaSample.At(8), 1);

    private static readonly string[] PanelFields = ["FromDateField", "UntilDateField", "GuestTextField"];

    private static string ClosureRoute(string tail = "") => $"/api/locations/{SpaSample.LocationId}/closures{tail}";

    private static string RoomClosureRoute(string tail = "") => $"/api/locations/{SpaSample.LocationId}/room-closures{tail}";

    /// <summary>
    /// Die Daten der Seite „Schließzeiten“: eine geplante Schließzeit mit zwei offenen Terminen, eine laufende Raumsperre mit einem offenen Termin und ein vergangener Ausfall; die Übersicht
    /// und die Einzelabrufe lesen die Listen, POST legt neue an, DELETE löscht.
    /// </summary>
    private static StubApi ClosuresApi(StubApi? api = null, int open = 2)
    {
        var offering = Vacation(open);
        var closures = new List<ClosureDto> { offering, PastOutage };
        var rooms = new List<RoomClosureDto> { CedarMaintenance };
        var stub = (api ?? Api())
            .OnOverview(SpaSample.LocationId, () => closures.Select(ClosuresTestKit.ToItem).Concat(rooms.Select(ClosuresTestKit.ToItem)))
            .OnOverview(SpaSample.Salzburg.Id, () => [])
            .On($"GET /api/locations/{SpaSample.LocationId}/guest-message-delay", new GuestMessageDelayDto(10))
            .On($"GET {ClosureRoute("/" + OutageId + "/notices")}", new List<ClosureNoticeDto>
            {
                new(Guid.NewGuid(), OutageId, Guid.NewGuid(), "Eva Müller", "Klassische Massage", SpaSample.At(10), ClosureChannel.Email, ClosureNoticeState.Sent, null, "Absage", "Text", false, false,
                    "Katrin Aigner", SpaSample.At(9), SpaSample.At(9)),
            })
            .On($"GET {ClosureRoute("/" + VacationId + "/cancellation")}", new ClosureCancellationPreview(offering,
                [new(Guid.NewGuid(), "Eva Müller", "Klassische Massage", SpaSample.At(10), SpaSample.At(11), "Lena Huber", AppointmentStatus.Booked, ClosureChannel.Email, false, false)],
                "Absage Ihres Termins: {Behandlung}, {Zeit}", "Guten Tag {Anrede}, leider fällt {Behandlung} am {Zeit} aus.", false))
            .OnJson($"DELETE {ClosureRoute("/" + VacationId)}", _ =>
            {
                closures.RemoveAll(c => c.Id == VacationId);
                return new object();
            }, System.Net.HttpStatusCode.NoContent)
            .OnJson($"DELETE {RoomClosureRoute("/" + MaintenanceId)}", _ =>
            {
                rooms.RemoveAll(c => c.Id == MaintenanceId);
                return new object();
            }, System.Net.HttpStatusCode.NoContent);
        void ServeClosure(Guid id) => stub.OnJson($"GET {ClosureRoute("/" + id)}", _ => closures.First(c => c.Id == id));
        void ServeRoom(Guid id) => stub.OnJson($"GET {RoomClosureRoute("/" + id)}", _ => rooms.First(c => c.Id == id));
        ServeClosure(VacationId);
        ServeClosure(OutageId);
        ServeRoom(MaintenanceId);
        return stub
            .OnJson($"POST {ClosureRoute()}", body =>
            {
                var input = JsonSerializer.Deserialize<ClosureInput>(body!, ApiClient.Json)!;
                var created = new ClosureDto(Guid.NewGuid(), SpaSample.LocationId, input.FromDate, input.FromTime, input.UntilDate, input.UntilTime, SpaSample.At(2), SpaSample.At(2).AddDays(3),
                    input.Kind, input.InternalReason, input.GuestText, ClosurePhase.Planned, "Katrin Aigner", SpaSample.At(8), 0, 0, 0);
                closures.Add(created);
                ServeClosure(created.Id);
                return created;
            })
            .OnJson($"POST {RoomClosureRoute()}", body =>
            {
                var input = JsonSerializer.Deserialize<RoomClosureInput>(body!, ApiClient.Json)!;
                var room = SpaSample.Resources.Rooms.Single(r => r.Id == input.RoomId);
                var created = new RoomClosureDto(Guid.NewGuid(), SpaSample.LocationId, room.Id, room.Name, input.FromDate, input.FromTime, input.UntilDate, input.UntilTime, SpaSample.At(2),
                    SpaSample.At(2).AddDays(3), input.Kind, input.Reason, ClosurePhase.Planned, "Katrin Aigner", SpaSample.At(8), 0);
                rooms.Add(created);
                ServeRoom(created.Id);
                return created;
            });
    }

    private static async Task<ShellViewModel> OpenClosures(Window window, MainViewModel main)
    {
        var shell = await OpenAdminPage(window, main);
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Schließzeiten");
        await Pump(Settling);
        await Pump(Settling);
        Assert.Same(shell.AdminPage.ClosuresPage, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.ClosuresPage!.IsLoaded);
        return shell;
    }

    /// <summary>Wählt die Zeile mit dem Klick der Person; die Seitenleiste öffnet sich mit dem Formular.</summary>
    private static async Task ClickRow(Window window, string listName, int index)
    {
        var item = Named<ListBox>(window, listName).GetVisualDescendants().OfType<ListBoxItem>().ElementAt(index);
        ClickCenter(window, item);
        await Pump(Settling);
        await Pump(Settling);
    }

    /// <summary>Holt das Steuerelement in den sichtbaren Teil und klickt es an.</summary>
    private static Task ClickScrolled(Window window, string name) => ClickScrolled(window, Named<Control>(window, name));

    private static async Task ClickScrolled(Window window, Control control)
    {
        control.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, control);
        await Pump(Settling);
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Schliesszeiten(string theme) => Render(theme, "verwaltung-schliesszeiten", async (window, main) =>
    {
        var shell = await OpenClosures(window, main);
        var page = shell.AdminPage.ClosuresPage!;

        // ADR 0098: Schließzeit und Raumsperre stehen in einer Liste, nach Beginn; die Art ist ein Kennzeichen; ein Filter statt Reiter, kein Kontrollkästchen „Beendete zeigen“.
        Assert.Equal([("Raum", "Raum Zeder, Wartung"), ("Standort", "Betriebsurlaub")], page.Upcoming.Select(r => (r.TypeText, r.Title)));
        Assert.Equal("Schließzeiten und Raumsperren", Named<TextBlock>(window, "PageTitle").Text);
        Assert.Equal(2, Named<ListBox>(window, "UpcomingList").GetVisualDescendants().OfType<ListBoxItem>().Count());
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Standort");
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Raum");
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<CheckBox>(), c => c.Name == "ShowEndedBox");
        Assert.Equal("Vergangene (1)", Named<Button>(window, "PastToggle").GetVisualDescendants().OfType<TextBlock>().Single(t => t.IsEffectivelyVisible).Text);
        Assert.False(Named<SidePanel>(window, "EditPanel").IsVisible);
        Assert.False(Named<SidePanel>(window, "SetupPanel").IsVisible);

        // Die Wartezeit ist keine Karte über der Liste mehr, sondern liegt hinter „Einrichtung“.
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<NumberField>(), n => n.IsEffectivelyVisible);
        Assert.Equal("Einrichtung", Named<Button>(window, "SetupButton").Content);
        Assert.Equal("Neue Schließzeit", AutomationProperties.GetName(Named<Button>(window, "NewButton")));
        return shell.AdminPage;
    }, ClosuresApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Schliesszeiten_Formular(string theme) => Render(theme, "verwaltung-schliesszeiten-formular", async (window, main) =>
    {
        var shell = await OpenClosures(window, main);
        var page = shell.AdminPage.ClosuresPage!;

        // When die Zeile der Schließzeit gewählt wird, Then öffnet sich ihr Formular in der Seitenleiste neben der Liste.
        await ClickRow(window, "UpcomingList", 1);
        Assert.Same(page.ClosureEditor, page.Editor);
        Assert.True(page.IsPanelOpen);
        var panel = Named<SidePanel>(window, "EditPanel");
        Assert.True(panel.IsVisible);
        Assert.Equal(360, panel.Bounds.Width, 0.5);
        Assert.True(TopOf(window, Named<Control>(window, "UpcomingList")) < BottomOf(window, panel));
        Assert.True(BottomOf(window, Named<ListBox>(window, "UpcomingList")) > TopOf(window, panel));
        Assert.Equal("Inventur", Named<TextBox>(window, "InternalReasonField").Text);
        Assert.True(Shows(window, "2 Termine liegen in der Schließzeit"));
        Assert.True(Shows(window, "Geplant"));

        // Die Liste verliert neben der Leiste nichts: Die Zeilen enden vor der Leiste.
        var list = Named<ListBox>(window, "UpcomingList");
        Assert.True(list.Bounds.Width > 300, $"Liste {list.Bounds.Width:0} px");
        Assert.True(TopOf(window, Named<Control>(window, "Footer")) > TopOf(window, panel));
        return shell.AdminPage;
    }, ClosuresApi());

    [Fact]
    public Task Verwaltung_Schliesszeiten_Formular_bei_Mindestbreite() => Render("hell", "verwaltung-schliesszeiten-schmal", async (window, main) =>
    {
        var shell = await OpenClosures(window, main);
        await SetSize(window, 1100, 680);
        await Pump(Settling);
        var page = shell.AdminPage.ClosuresPage!;

        await ClickRow(window, "UpcomingList", 1);
        Assert.True(page.IsPanelOpen);
        var panel = Named<SidePanel>(window, "EditPanel");
        var list = Named<ListBox>(window, "UpcomingList");

        // Beim Mindestfenster bleibt neben der Leiste eine lesbare Liste, und nichts ragt über den Rand.
        Assert.True(list.Bounds.Width >= 480, $"Liste {list.Bounds.Width:0} px");
        Assert.True(BottomOf(window, panel) <= window.Bounds.Height + 0.5);
        Assert.True(panel.Bounds.Right <= window.Bounds.Width + 0.5);
        foreach (var field in PanelFields)
        {
            var control = Named<Control>(window, field);
            control.BringIntoView();
            await Pump(Settling);
            Assert.True(control.TranslatePoint(new Point(control.Bounds.Width, 0), window)!.Value.X <= panel.TranslatePoint(new Point(panel.Bounds.Width, 0), window)!.Value.X + 0.5,
                $"{field} ragt über die Leiste");
        }

        return shell.AdminPage;
    }, ClosuresApi());

    [Fact]
    public Task Schliesszeit_anlegen_ueber_Klick_und_Speichern()
    {
        var api = ClosuresApi();
        return Render("hell", "verwaltung-schliesszeiten-anlegen", async (window, main) =>
        {
            var shell = await OpenClosures(window, main);
            var page = shell.AdminPage.ClosuresPage!;
            var closures = page.ClosureEditor;

            ClickCenter(window, Named<Button>(window, "NewButton"));
            await Pump(Settling);
            Assert.True(closures.IsCreating);
            Assert.True(Shows(window, "Neue Schließzeit"));
            // Bei einem neuen Eintrag wählt man oben „Ganzer Standort“ oder „Einzelner Raum“.
            Assert.True(Named<StackPanel>(window, "NewKindChoice").IsEffectivelyVisible);
            Assert.True(Named<RadioButton>(window, "NewKindLocation").IsChecked);

            await TypeInto(window, "FromDateField", "24.12.2026");
            await TypeInto(window, "UntilDateField", "26.12.2026");
            await TypeInto(window, "GuestTextField", "Frohe Weihnachten");
            await ClickScrolled(window, "SaveButton");

            var post = Assert.Single(api.Calls, c => c.Route == $"POST {ClosureRoute()}");
            Assert.Equal(new ClosureInput(new(2026, 12, 24), null, new(2026, 12, 26), null, ClosureKind.Vacation, null, "Frohe Weihnachten"), JsonSerializer.Deserialize<ClosureInput>(post.Body!, ApiClient.Json));
            Assert.False(closures.IsCreating);
            Assert.False(closures.HasUnsavedChanges);
            // Nach dem Speichern ein Toast unten, kein Balken; die neue Zeile ist gewählt.
            Assert.Equal("Schließzeit gespeichert", page.Toast);
            Assert.True(Named<Border>(window, "ClosuresToast").IsEffectivelyVisible);
            Assert.True(Shows(window, "Schließzeit gespeichert"));
            Assert.Equal(3, page.Upcoming.Count);
            Assert.NotNull(page.SelectedUpcoming);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Bis_vor_Von_und_Strg_S_zeigt_den_Fehler_am_Feld_und_sendet_nichts()
    {
        var api = ClosuresApi();
        return Render("hell", "verwaltung-schliesszeiten-fehler", async (window, main) =>
        {
            var shell = await OpenClosures(window, main);
            ClickCenter(window, Named<Button>(window, "NewButton"));
            await Pump(Settling);

            await TypeInto(window, "FromDateField", "26.12.2026");
            await TypeInto(window, "UntilDateField", "24.12.2026");
            PressSave(window);
            await Pump(Settling);

            Assert.Equal(0, CallsTo(api, $"POST {ClosureRoute()}"));
            Assert.True(Shows(window, "Das Ende darf nicht vor dem Beginn liegen."));
            Assert.Same(Named<TextBox>(window, "UntilDateField"), Focused(window));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Termine_absagen_oeffnet_die_Rueckfrage_und_Loeschen_fragt_nach()
    {
        var api = ClosuresApi();
        return Render("hell", "verwaltung-schliesszeiten-absagen", async (window, main) =>
        {
            var shell = await OpenClosures(window, main);
            await ClickRow(window, "UpcomingList", 1);

            await ClickScrolled(window, "CancelAppointmentsButton");
            var dialog = Assert.IsType<CancelClosureDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Termine absagen?", dialog.Title);
            Assert.Equal(0, CallsTo(api, $"POST {ClosureRoute("/" + VacationId + "/cancellation")}"));
            Press(window, Avalonia.Input.PhysicalKey.Escape, Avalonia.Input.RawInputModifiers.None);
            await Pump(Settling);

            await ClickScrolled(window, "DeleteButton");
            Assert.Equal("Schließzeit löschen?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);
            Assert.Equal(1, CallsTo(api, $"DELETE {ClosureRoute("/" + VacationId)}"));
            // Statt „Gelöscht.“ als Balken ein Toast; die Seitenleiste ist zu.
            Assert.Equal("Schließzeit gelöscht", shell.AdminPage.ClosuresPage!.Toast);
            Assert.True(Shows(window, "Schließzeit gelöscht"));
            Assert.False(Named<SidePanel>(window, "EditPanel").IsVisible);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Vergangene_klappt_auf_und_das_Protokoll_der_Absagen_zeigt_den_Gast_ohne_Loeschen() => Render("hell", "verwaltung-schliesszeiten-protokoll", async (window, main) =>
    {
        var shell = await OpenClosures(window, main);
        var page = shell.AdminPage.ClosuresPage!;

        // When „Vergangene (1)“ angeklickt wird, Then stehen die beendeten unter den kommenden, mit dem Wort „Beendet“.
        await ClickScrolled(window, "PastToggle");
        Assert.True(page.IsPastOpen);
        Assert.True(Named<ListBox>(window, "PastList").IsEffectivelyVisible);
        Assert.Equal(["Ausfall"], page.Past.Select(r => r.Title));
        Assert.Contains(Named<ListBox>(window, "PastList").GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Beendet");

        // When die beendete Schließzeit gewählt wird, Then ist ihr Formular da, ohne Löschen (es wurden Termine abgesagt).
        await ClickRow(window, "PastList", 0);
        Assert.Equal("Beendet", page.ClosureEditor.PhaseText);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "DeleteButton" && b.IsEffectivelyVisible);

        var help = Assert.Single(window.GetVisualDescendants().OfType<HelpButton>(), b => b.Topic == "schliesszeiten" && b.Section == "protokoll");
        Assert.DoesNotContain(help.GetVisualAncestors(), a => a is Avalonia.Controls.Primitives.ToggleButton);
        Assert.Equal("Hilfe zu Protokoll der Absagen", AutomationProperties.GetName(help));

        await ClickScrolled(window, "ProtocolHeader");
        Assert.True(Shows(window, "Eva Müller"));

        // When „Vergangene“ wieder zugeklappt wird, Then bleibt die Liste der kommenden.
        await ClickScrolled(window, "PastToggle");
        Assert.False(page.IsPastOpen);
        Assert.Empty(page.Past);
        return shell.AdminPage;
    }, ClosuresApi());

    [Fact]
    public Task Der_Filter_zeigt_nur_Raumsperren_oder_nur_Schliesszeiten() => Render("hell", "verwaltung-schliesszeiten-filter", async (window, main) =>
    {
        var shell = await OpenClosures(window, main);
        var page = shell.AdminPage.ClosuresPage!;
        var bar = Named<Border>(window, "FilterBar");
        var buttons = bar.GetVisualDescendants().OfType<TabButton>().ToList();
        Assert.Equal(["Alle 2", "Standort geschlossen 1", "Raum gesperrt 1"], buttons.Select(b => b.Content as string));

        ClickCenter(window, buttons[2]);
        await Pump(Settling);
        Assert.Equal(["Raum Zeder, Wartung"], page.Upcoming.Select(r => r.Title));
        Assert.Single(Named<ListBox>(window, "UpcomingList").GetVisualDescendants().OfType<ListBoxItem>());

        ClickCenter(window, buttons[0]);
        await Pump(Settling);
        Assert.Equal(2, page.Upcoming.Count);
        return shell.AdminPage;
    }, ClosuresApi());

    [Fact]
    public Task Einrichtung_oeffnet_die_Wartezeit_als_Seitenleiste_und_speichert_sie()
    {
        var api = ClosuresApi().OnJson($"PUT /api/locations/{SpaSample.LocationId}/guest-message-delay", body => JsonSerializer.Deserialize<GuestMessageDelayDto>(body!, ApiClient.Json)!);
        return Render("hell", "verwaltung-schliesszeiten-einrichtung", async (window, main) =>
        {
            var shell = await OpenClosures(window, main);
            var page = shell.AdminPage.ClosuresPage!;

            ClickCenter(window, Named<Button>(window, "SetupButton"));
            await Pump(Settling);
            await Pump(Settling);
            Assert.True(page.IsSetupOpen);
            Assert.True(Named<SidePanel>(window, "SetupPanel").IsVisible);
            Assert.Equal("Einrichtung", Named<SidePanel>(window, "SetupPanel").Title);
            var field = Named<NumberField>(window, "GuestDelayField");
            Assert.Equal(10m, field.Value);
            Assert.False(Named<Button>(window, "GuestDelayButton").Command!.CanExecute(null));

            page.Setup.GuestDelayMinutes = 20m;
            await Pump(Settling);
            ClickCenter(window, Named<Button>(window, "GuestDelayButton"));
            await Pump(Settling);

            Assert.Equal(20, JsonSerializer.Deserialize<GuestMessageDelayDto>(api.Calls.Single(c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body!, ApiClient.Json)!.Minutes);
            Assert.Equal("Wartezeit gespeichert", page.Toast);
            Assert.True(Named<SidePanel>(window, "SetupPanel").IsVisible);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Das_Kreuz_schliesst_die_Einrichtung() => Render("hell", "verwaltung-schliesszeiten-einrichtung-zu", async (window, main) =>
    {
        var shell = await OpenClosures(window, main);
        var page = shell.AdminPage.ClosuresPage!;
        ClickCenter(window, Named<Button>(window, "SetupButton"));
        await Pump(Settling);
        await Pump(Settling);
        Assert.True(page.IsSetupOpen);

        ClickCenter(window, Named<SidePanel>(window, "SetupPanel").GetVisualDescendants().OfType<Button>().First(b => ToolTip.GetTip(b) as string == "Schließen (Esc)"));
        await Pump(Settling);

        Assert.False(page.IsSetupOpen);
        Assert.False(Named<SidePanel>(window, "SetupPanel").IsVisible);
        return shell.AdminPage;
    }, ClosuresApi());

    [Fact]
    public Task Ohne_Eintrag_zeigt_die_Seite_den_Leerzustand_mit_den_ersten_Aktionen() => Render("hell", "verwaltung-schliesszeiten-leer", async (window, main) =>
    {
        var shell = await OpenClosures(window, main);
        var page = shell.AdminPage.ClosuresPage!;

        Assert.True(page.ShowEmpty, $"geladen {page.IsLoaded}, Standort {page.SelectedLocation}, kommend {page.UpcomingCount}, vergangen {page.PastCount}, Fehler {page.LoadError}");
        Assert.True(Named<Border>(window, "EmptyState").IsEffectivelyVisible, "EmptyState sichtbar");
        Assert.Equal($"{SpaSample.Vienna.Name} ist durchgehend offen", page.EmptyTitle);
        Assert.False(Named<ListBox>(window, "UpcomingList").IsEffectivelyVisible, "Liste unsichtbar");

        // „Raum sperren“ öffnet gleich das Formular „Einzelner Raum“.
        ClickCenter(window, Named<Button>(window, "EmptyNewRoomButton"));
        await Pump(Settling);
        await Pump(Settling);
        Assert.Same(page.RoomEditor, page.Editor);
        Assert.True(page.NewIsRoom, "NewIsRoom");
        Assert.True(Named<RadioButton>(window, "NewKindRoom").IsChecked, "Optionsfeld");
        return shell.AdminPage;
    }, Api().OnOverview(SpaSample.LocationId, () => []).OnOverview(SpaSample.Salzburg.Id, () => []));

    [Fact]
    public Task Der_Chip_Geschlossen_im_Kalender_oeffnet_die_Seite_Schliesszeiten() => Render("hell", "verwaltung-schliesszeiten-chip", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        shell.ShowAdmin("masterData", "closures", null);
        await Pump(Settling);
        await Pump(Settling);

        Assert.Equal("Schließzeiten", shell.AdminPage.SelectedArea?.Title);
        Assert.True(window.GetVisualDescendants().OfType<ClosuresPageView>().Any());
        return shell.AdminPage;
    }, ClosuresApi());

    [Fact]
    public Task Rezeptionistin_sieht_keine_Schliesszeiten() => Render("hell", "verwaltung-rezeption-schliesszeiten", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        Assert.Null(shell.AdminPage.ClosuresPage);
        Assert.False(window.GetVisualDescendants().OfType<ClosuresPageView>().Any());
        return shell.AdminPage;
    }, ReceptionApi());

    [Fact]
    public Task Schliesszeiten_melden_keine_Bindungsfehler() => Render("hell", "bindungen-schliesszeiten", async (window, main) =>
    {
        await WithBindingLog(async log =>
        {
            var shell = await OpenAdminPage(window, main);
            await AssertNoBindingErrors(log, "Schließzeiten", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Schließzeiten");
                return Task.CompletedTask;
            });
            var page = shell.AdminPage.ClosuresPage!;
            var closures = page.ClosureEditor;
            await AssertNoBindingErrors(log, "Neue Schließzeit", async () => await page.NewCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Eingabe", () => { closures.Period.IsPartial = true; closures.Period.FromTime = "13:00"; closures.GuestText = "x"; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Verworfen", () => { closures.Discard(); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Einzelner Raum", () => { page.NewIsRoom = true; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Zeile", async () => await page.OpenRowAsync(page.Upcoming[0]));
            await AssertNoBindingErrors(log, "Vergangene", async () => await page.TogglePastCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Filter", () => { page.Filters[1].SelectCommand.Execute(null); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Einrichtung", async () => await page.OpenSetupCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Anderer Standort", () => { page.SelectedLocation = page.Locations[1]; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Anderer Bereich", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Behandlungen");
                return Task.CompletedTask;
            });
            log.AssertClean();
            return shell.AdminPage;
        });
        return true;
    }, ClosuresApi());

    [Fact]
    public Task Jede_Seite_Schliesszeiten_hat_fuer_jedes_bedienbare_Element_einen_lesbaren_Namen() => Render("hell", "namen-schliesszeiten", async (window, main) =>
    {
        var shell = await OpenClosures(window, main);
        var page = shell.AdminPage.ClosuresPage!;
        var problems = new SortedSet<string>(StringComparer.Ordinal);

        void Scan(string state)
        {
            foreach (var control in window.GetVisualDescendants().OfType<Control>().Where(c => c is Button or CheckBox or RadioButton or TextBox or ComboBox or ListBoxItem))
            {
                if (control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control.Focusable && HasUnreadableName(control, out var name))
                {
                    problems.Add($"{state}: {control.GetType().Name}#{control.Name} \"{name}\"");
                }
            }
        }

        Scan("Liste");
        await page.OpenRowAsync(page.Upcoming.Single(r => !r.IsRoom));
        await Pump(Settling);
        Scan("Schließzeit");
        page.ClosureEditor.Period.IsPartial = true;
        page.ClosureEditor.GuestText = "Text";
        await Pump(Settling);
        Scan("Teil-Tage");
        page.ClosureEditor.Discard();
        await page.NewCommand.ExecuteAsync(null);
        await Pump(Settling);
        Scan("Neue Schließzeit");
        page.NewIsRoom = true;
        await Pump(Settling);
        await Pump(Settling);
        Scan("Neue Raumsperre");
        await page.TogglePastCommand.ExecuteAsync(null);
        await Pump(Settling);
        Scan("Vergangene");
        await page.OpenSetupCommand.ExecuteAsync(null);
        await Pump(Settling);
        Scan("Einrichtung");

        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return shell.AdminPage;
    }, ClosuresApi());
}
