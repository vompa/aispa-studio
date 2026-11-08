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
        Assert.Same(shell.AdminPage.Closures, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Closures!.IsLoaded);
        return shell;
    }

    /// <summary>Holt das Steuerelement in den sichtbaren Teil der Maske und klickt es an.</summary>
    private static async Task ClickScrolled(Window window, string name)
    {
        var control = Named<Control>(window, name);
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
        Assert.Equal("Aktuell (1)", closures.CurrentTabLabel);
        Assert.True(Shows(window, "2 Termine liegen in der Schließzeit"));
        Assert.Equal("Inventur", Named<TextBox>(window, "InternalReasonField").Text);
        return shell.AdminPage;
    }, ClosuresApi());

    [Fact]
    public Task Schliesszeit_anlegen_ueber_Klick_und_Speichern()
    {
        var api = ClosuresApi();
        return Render("hell", "verwaltung-schliesszeiten-anlegen", async (window, main) =>
        {
            var shell = await OpenClosures(window, main);
            var closures = shell.AdminPage.Closures!;

            ClickCenter(window, Named<Button>(window, "NewButton"));
            await Pump(Settling);
            Assert.True(closures.IsCreating);
            Assert.True(Shows(window, "Neue Schließzeit"));

            await TypeInto(window, "FromDateField", "24.12.2026");
            await TypeInto(window, "UntilDateField", "26.12.2026");
            await TypeInto(window, "GuestTextField", "Frohe Weihnachten");
            await ClickScrolled(window, "SaveButton");

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
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Der_Reiter_Beendet_zeigt_das_Protokoll_der_Absagen_ohne_Loeschen() => Render("hell", "verwaltung-schliesszeiten-protokoll", async (window, main) =>
    {
        var shell = await OpenClosures(window, main);
        var closures = shell.AdminPage.Closures!;

        ClickCenter(window, Named<Button>(window, "EndedTab"));
        await Pump(Settling);
        Assert.Equal(["Ausfall"], closures.Rows.Select(r => r.Title));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "DeleteButton" && b.IsEffectivelyVisible);

        var help = Assert.Single(window.GetVisualDescendants().OfType<HelpButton>(), b => b.Topic == "schliesszeiten" && b.Section == "protokoll");
        Assert.DoesNotContain(help.GetVisualAncestors(), a => a is Avalonia.Controls.Primitives.ToggleButton);
        Assert.Equal("Hilfe zu Protokoll der Absagen", AutomationProperties.GetName(help));

        await ClickScrolled(window, "ProtocolHeader");
        Assert.True(Shows(window, "Eva Müller"));
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
            await AssertNoBindingErrors(log, "Reiter Beendet", async () => await closures.ShowEndedCommand.ExecuteAsync(null));
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
        await closures.ShowEndedCommand.ExecuteAsync(null);
        await Pump(Settling);
        Scan("Beendet");

        Assert.True(problems.Count == 0, $"{problems.Count} Elemente:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return shell.AdminPage;
    }, ClosuresApi());
}
