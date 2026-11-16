using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 95 (Ä206): native Maske „Schließzeiten“ im Fenster: Eingabe per Maus und Tastatur, Strg+S, Absagen und Löschen über die Rückfrage, Scans.</summary>
public sealed partial class ViewTests
{
    private static readonly Guid VacationId = Guid.Parse("00000000-0000-0000-0000-0000000c1051");
    private static readonly Guid OutageId = Guid.Parse("00000000-0000-0000-0000-0000000c1052");

    private static ClosureDto Vacation(int open = 2) => new(VacationId, SpaSample.LocationId, new(2026, 12, 24), null, new(2026, 12, 26), null, SpaSample.At(2), SpaSample.At(2).AddDays(3),
        ClosureKind.Vacation, "Inventur", "Wegen Betriebsurlaub geschlossen.", ClosurePhase.Planned, "Katrin Aigner", SpaSample.At(8), open, 0, 0);

    private static readonly ClosureDto PastOutage = new(OutageId, SpaSample.LocationId, new(2026, 9, 4), null, new(2026, 9, 5), null, SpaSample.At(2), SpaSample.At(2).AddDays(2),
        ClosureKind.Outage, "Wasserschaden", null, ClosurePhase.Ended, "Katrin Aigner", SpaSample.At(8), 0, 1, 0);

    private static string ClosureRoute(string tail = "") => $"/api/locations/{SpaSample.LocationId}/closures{tail}";

    private static StubApi ClosuresApi(StubApi? api = null, int open = 2)
    {
        var offering = Vacation(open);
        return (api ?? Api())
            .On($"GET {ClosureRoute()}", new List<ClosureDto> { offering, PastOutage })
            .On($"GET /api/locations/{SpaSample.LocationId}/guest-message-delay", new GuestMessageDelayDto(10))
            .On($"GET {ClosureRoute("/" + OutageId + "/notices")}", new List<ClosureNoticeDto>
            {
                new(Guid.NewGuid(), OutageId, Guid.NewGuid(), "Eva Müller", "Klassische Massage", SpaSample.At(10), ClosureChannel.Email, ClosureNoticeState.Sent, null, "Absage", "Text", false, false,
                    "Katrin Aigner", SpaSample.At(9), SpaSample.At(9)),
            })
            .On($"GET {ClosureRoute("/" + VacationId + "/cancellation")}", new ClosureCancellationPreview(offering,
                [new(Guid.NewGuid(), "Eva Müller", "Klassische Massage", SpaSample.At(10), SpaSample.At(11), "Lena Huber", AppointmentStatus.Booked, ClosureChannel.Email, false, false)],
                "Absage Ihres Termins: {Behandlung}, {Zeit}", "Guten Tag {Anrede}, leider fällt {Behandlung} am {Zeit} aus.", false))
            .OnJson($"POST {ClosureRoute()}", body =>
            {
                var input = JsonSerializer.Deserialize<ClosureInput>(body!, ApiClient.Json)!;
                return new ClosureDto(Guid.NewGuid(), SpaSample.LocationId, input.FromDate, input.FromTime, input.UntilDate, input.UntilTime, SpaSample.At(2), SpaSample.At(2).AddDays(3),
                    input.Kind, input.InternalReason, input.GuestText, ClosurePhase.Planned, "Katrin Aigner", SpaSample.At(8), 0, 0, 0);
            })
            .On($"DELETE {ClosureRoute("/" + VacationId)}", null, System.Net.HttpStatusCode.NoContent);
    }

    private static async Task<ShellViewModel> OpenClosures(Window window, MainViewModel main)
    {
        var shell = await OpenAdminPage(window, main);
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Schließzeiten");
        await Pump(Settling);
        await Pump(Settling);
        Assert.Same(shell.AdminPage.ClosuresPage, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Closures!.IsLoaded);
        return shell;
    }

    /// <summary>Der Abschnitt „Schließzeiten“ der Seite (ADR 0098). Kopf, Liste und Zeitraum heißen im Abschnitt „Raumsperren“ gleich; Tests suchen deshalb im Abschnitt.</summary>
    private static ClosuresView ClosuresPart(Window window) => window.GetVisualDescendants().OfType<ClosuresView>().Single();

    /// <summary>Der Abschnitt „Raumsperren“ der Seite.</summary>
    private static RoomClosuresView RoomClosuresPart(Window window) => window.GetVisualDescendants().OfType<RoomClosuresView>().Single();

    /// <summary>Holt das Steuerelement in den sichtbaren Teil der Maske und klickt es an.</summary>
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
        var closures = shell.AdminPage.Closures!;
        Assert.Equal(["Betriebsurlaub"], closures.Rows.Select(r => r.Title));
        Assert.True(Shows(window, "2 Termine liegen in der Schließzeit"));

        // ADR 0098: keine Reiter „Aktuell“ und „Beendet“ mehr, sondern das Kontrollkästchen über der Liste; es nennt, wie viele Einträge ausgeblendet sind.
        var part = ClosuresPart(window);
        Assert.Empty(part.GetVisualDescendants().OfType<TabButton>());
        var box = Named<CheckBox>(part, "ShowEndedBox");
        Assert.Equal(("Beendete zeigen (1)", false), (box.Content as string, box.IsChecked));
        Assert.True(TopOf(window, box) < TopOf(window, Named<ListBox>(part, "PeriodListBox")));

        // Beide Abschnitte stehen auf einer Seite, die Raumsperren unter den Schließzeiten; die Liste ist 260 breit (Brief 5.6, mit 16 Abstand zum Formular).
        Assert.Equal(["Schließzeiten", "Raumsperren"], window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "SectionTitle" && t.IsEffectivelyVisible).Select(t => t.Text));
        Assert.True(TopOf(window, RoomClosuresPart(window)) >= BottomOf(window, part));

        // Beim Öffnen steht die Seite oben: Die gewählte Raumsperre weiter unten zieht die Seite nicht zu sich.
        Assert.Equal(0, Named<ScrollViewer>(window, "PageScroll").Offset.Y);
        Assert.Equal(TopOf(window, Named<ScrollViewer>(window, "PageScroll")), TopOf(window, part), 1);
        Assert.Equal(244, Named<ListBox>(part, "PeriodListBox").GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("card")).Bounds.Width, 0.5);
        Assert.Equal("Inventur", Named<TextBox>(window, "InternalReasonField").Text);
        return shell.AdminPage;
    }, RoomClosuresApi(ClosuresApi()));

    [Fact]
    public Task Schliesszeit_anlegen_ueber_Klick_und_Speichern()
    {
        var api = ClosuresApi();
        return Render("hell", "verwaltung-schliesszeiten-anlegen", async (window, main) =>
        {
            var shell = await OpenClosures(window, main);
            var closures = shell.AdminPage.Closures!;

            var part = ClosuresPart(window);
            ClickCenter(window, Named<Button>(part, "NewButton"));
            await Pump(Settling);
            Assert.True(closures.IsCreating);
            Assert.True(Shows(window, "Neue Schließzeit"));

            await TypeInto(window, Named<TextBox>(part, "FromDateField"), "24.12.2026");
            await TypeInto(window, Named<TextBox>(part, "UntilDateField"), "26.12.2026");
            await TypeInto(window, "GuestTextField", "Frohe Weihnachten");
            await ClickScrolled(window, Named<Control>(part, "SaveButton"));

            var post = Assert.Single(api.Calls, c => c.Route == $"POST {ClosureRoute()}");
            Assert.Equal(new ClosureInput(new(2026, 12, 24), null, new(2026, 12, 26), null, ClosureKind.Vacation, null, "Frohe Weihnachten"), JsonSerializer.Deserialize<ClosureInput>(post.Body!, ApiClient.Json));
            Assert.False(closures.IsCreating);
            Assert.False(closures.HasUnsavedChanges);
            Assert.True(Shows(window, "Gespeichert."));
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
            var part = ClosuresPart(window);
            ClickCenter(window, Named<Button>(part, "NewButton"));
            await Pump(Settling);

            await TypeInto(window, Named<TextBox>(part, "FromDateField"), "26.12.2026");
            await TypeInto(window, Named<TextBox>(part, "UntilDateField"), "24.12.2026");
            PressSave(window);
            await Pump(Settling);

            Assert.Equal(0, CallsTo(api, $"POST {ClosureRoute()}"));
            Assert.True(Shows(window, "Das Ende darf nicht vor dem Beginn liegen."));
            Assert.Same(Named<TextBox>(part, "UntilDateField"), Focused(window));
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

            await ClickScrolled(window, "CancelAppointmentsButton");
            var dialog = Assert.IsType<CancelClosureDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Termine absagen?", dialog.Title);
            Assert.Equal(0, CallsTo(api, $"POST {ClosureRoute("/" + VacationId + "/cancellation")}"));
            Press(window, Avalonia.Input.PhysicalKey.Escape, Avalonia.Input.RawInputModifiers.None);
            await Pump(Settling);

            await ClickScrolled(window, Named<Control>(ClosuresPart(window), "DeleteButton"));
            Assert.Equal("Schließzeit löschen?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);
            Assert.Equal(1, CallsTo(api, $"DELETE {ClosureRoute("/" + VacationId)}"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Beendete_zeigen_blendet_die_beendeten_ein_und_zeigt_das_Protokoll_der_Absagen_ohne_Loeschen() => Render("hell", "verwaltung-schliesszeiten-protokoll", async (window, main) =>
    {
        var shell = await OpenClosures(window, main);
        var closures = shell.AdminPage.Closures!;
        var part = ClosuresPart(window);

        // When „Beendete zeigen“ angehakt wird, Then stehen die beendeten nach den aktuellen in der Liste, mit dem Wort „Beendet“, und die Auswahl bleibt.
        ClickCenter(window, Named<CheckBox>(part, "ShowEndedBox"));
        await Pump(Settling);
        Assert.True(closures.ShowEnded);
        Assert.Equal(["Betriebsurlaub", "Ausfall"], closures.Rows.Select(r => r.Title));
        Assert.Equal("Betriebsurlaub", closures.SelectedRow?.Title);
        Assert.Single(part.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Beendet" && t.GetVisualAncestors().OfType<ListBoxItem>().Any());

        // When die beendete Schließzeit gewählt wird, Then gibt es kein Löschen.
        closures.SelectedRow = closures.Rows[1];
        await Pump(Settling);
        Assert.Equal("Beendet", closures.PhaseText);
        Assert.DoesNotContain(part.GetVisualDescendants().OfType<Button>(), b => b.Name == "DeleteButton" && b.IsEffectivelyVisible);

        var help = Assert.Single(window.GetVisualDescendants().OfType<HelpButton>(), b => b.Topic == "schliesszeiten" && b.Section == "protokoll");
        Assert.DoesNotContain(help.GetVisualAncestors(), a => a is Avalonia.Controls.Primitives.ToggleButton);
        Assert.Equal("Hilfe zu Protokoll der Absagen", AutomationProperties.GetName(help));

        await ClickScrolled(window, "ProtocolHeader");
        Assert.True(Shows(window, "Eva Müller"));

        // When der Haken wieder entfernt wird, Then filtert die Liste auf die aktuellen und wählt die erste.
        var box = Named<CheckBox>(part, "ShowEndedBox");
        box.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, box);
        await Pump(Settling);
        Assert.False(closures.ShowEnded);
        Assert.Equal(["Betriebsurlaub"], closures.Rows.Select(r => r.Title));
        Assert.Equal("Betriebsurlaub", closures.SelectedRow?.Title);
        return shell.AdminPage;
    }, ClosuresApi());

    [Fact]
    public Task Der_Chip_Geschlossen_im_Kalender_oeffnet_die_Maske_Schliesszeiten() => Render("hell", "verwaltung-schliesszeiten-chip", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;

        shell.ShowAdmin("masterData", "closures", null);
        await Pump(Settling);
        await Pump(Settling);

        Assert.Equal("Schließzeiten", shell.AdminPage.SelectedArea?.Title);
        Assert.True(window.GetVisualDescendants().OfType<ClosuresView>().Any());
        return shell.AdminPage;
    }, ClosuresApi());

    [Fact]
    public Task Rezeptionistin_sieht_keine_Schliesszeiten() => Render("hell", "verwaltung-rezeption-schliesszeiten", async (window, main) =>
    {
        var shell = await OpenAdminPage(window, main);
        Assert.Null(shell.AdminPage.Closures);
        Assert.False(window.GetVisualDescendants().OfType<ClosuresView>().Any());
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
            var closures = shell.AdminPage.Closures!;
            await AssertNoBindingErrors(log, "Neue Schließzeit", async () => await closures.NewCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Eingabe", () => { closures.Period.IsPartial = true; closures.Period.FromTime = "13:00"; closures.GuestText = "x"; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Verworfen", () => { closures.Discard(); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Beendete zeigen", () => { closures.ShowEnded = true; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Anderer Standort", () => { closures.SelectedLocation = closures.Locations[1]; return Task.CompletedTask; });
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
        var closures = shell.AdminPage.Closures!;
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

        Scan("Schließzeit");
        closures.Period.IsPartial = true;
        closures.GuestText = "Text";
        await Pump(Settling);
        Scan("Teil-Tage");
        closures.Discard();
        await closures.NewCommand.ExecuteAsync(null);
        await Pump(Settling);
        Scan("Neue Schließzeit");
        closures.ShowEnded = true;
        await Pump(Settling);
        Scan("Beendet");

        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return shell.AdminPage;
    }, ClosuresApi());
}
