using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Knopfnamen mit Bezug statt Zahl, Uhrzeit oder Initialen (Runde R01, Slice 20: W14, W20, W61, W47, W-Muster 6).</summary>
public sealed partial class ViewTests
{
    private sealed class EmptyPage : ViewModelBase;

    private static string ButtonName(Control control) => ControlAutomationPeer.CreatePeerForElement(control).GetName() ?? "";

    private static List<Button> ButtonsFor(Visual root, System.Windows.Input.ICommand command) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(b => ReferenceEquals(b.Command, command))];

    [Fact]
    public void Kennzahl_und_Zeilen_haben_einen_Namen_mit_Bezug()
    {
        var appointment = SpaSample.Selected;
        var upcoming = new UpcomingItem("13:00", "Eva Muster", "Massage · Lena Huber", "#2f6f5e", appointment);
        Assert.Equal("13:00 Eva Muster, Massage · Lena Huber", upcoming.AccessibleName);

        var part = new GroupPartRow(appointment, "14:00–14:50", "Paul Muster", null, "Massage", "Anna", "Raum Zeder", false);
        Assert.Equal($"14:00–14:50 Paul Muster, Massage · Anna · Raum Zeder, {part.Status}", part.AccessibleName);

        var unbilled = new UnbilledItem("10:30", "Brigitte Müller", "Hot-Stone-Massage", "€ 95,00", false);
        Assert.Equal("10:30 Brigitte Müller, Hot-Stone-Massage, € 95,00", unbilled.AccessibleName);

        var nav = new NavItem("Posteingang", "", new EmptyPage());
        Assert.Equal("Posteingang", nav.AccessibleName);
        nav.Badge = "3";
        Assert.Equal("Posteingang, 3 offen", nav.AccessibleName);
    }

    [Fact]
    public Task Kennzahl_Termine_mit_Wert_24_heisst_Termine_24() => WithOverview("hell", "kalender-tagesueberblick-namen", (window, calendar, content) =>
    {
        var overview = calendar.Overview!;
        var stats = content.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("stat")).Select(ButtonName).ToList();
        Assert.Equal(
            [$"Termine, {overview.Appointments}", $"Eingecheckt, {overview.CheckedIn}", $"Noch erwartet, {overview.Open}", $"Auslastung Team, {overview.Utilization}"],
            stats);
        // Der Beispieltag hat 24 Termine; die Abnahme „Termine, 12“ ist dasselbe Muster mit einem anderen Wert.
        Assert.Equal("Termine, 24", stats[0]);

        // Noch nicht zugesagt und Überfällig: Beschriftung und Zahl, nicht nur die Beschriftung.
        var counters = ButtonsFor(content, calendar.FocusOnCommand).Where(b => !b.Classes.Contains("stat")).Select(ButtonName).ToList();
        Assert.Equal([$"Noch nicht zugesagt, {overview.Unconfirmed}", $"Überfällig, {overview.Overdue}"], counters);

        // Als Nächstes: Uhrzeit, Gast und Behandlung statt nur der Uhrzeit.
        var entries = UpcomingEntries(content);
        Assert.NotEmpty(entries);
        Assert.Equal(calendar.Upcoming.Select(u => u.AccessibleName), entries.Select(ButtonName));
        return Task.CompletedTask;
    });

    [Fact]
    public Task Navigationszeile_Anfragen_mit_3_offenen_heisst_Anfragen_3_offen() => Render("hell", "navigation-namen", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        // Der Posteingang heißt in der Navigation „Anfragen“.
        var inbox = shell.Pages.First(p => p.Page == shell.Inbox);
        inbox.Badge = "3";
        await Pump();
        var row = window.GetVisualDescendants().OfType<ListBoxItem>().Single(i => ReferenceEquals(i.DataContext, inbox));
        Assert.Equal("Anfragen, 3 offen", ButtonName(row));

        inbox.Badge = null;
        await Pump();
        Assert.Equal("Anfragen", ButtonName(row));

        // Farbschema und Konto: was der Knopf ist, nicht „Hell“ oder die Initialen.
        var theme = Assert.Single(ButtonsFor(window, shell.NextThemeCommand));
        Assert.Equal($"Farbschema: {shell.ThemeText}", ButtonName(theme));
        var account = window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("navbtn") && b.Flyout is not null);
        Assert.Equal($"Konto {shell.UserText.Replace("\n", ", ", StringComparison.Ordinal)}", ButtonName(account));
        Assert.DoesNotContain("\n", ButtonName(account), StringComparison.Ordinal);
        return shell;
    });

    [Fact]
    public Task Erledigt_im_Abgleich_nennt_Bereich_und_Zeit()
    {
        var stub = Api().On("GET /api/system/sync", new SyncStatus(true, SpaSample.At(11, 39), null, 2,
        [
            new SyncConflict(1, "Termine", "Appointments", "{}", "Zentral abgelehnt: Raum, Person oder Gerät ist zur selben Zeit schon gebucht.",
                SpaSample.At(10, 12)),
        ]));
        return Render("hell", "abgleich-namen", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Sync);
            await shell.Sync!.RefreshAsync();
            await Pump();
            var conflict = shell.Sync.Conflicts[0];
            var button = Assert.Single(ButtonsFor(window, shell.Sync.ResolveCommand));
            Assert.Equal($"Erledigt: {conflict.Area} {conflict.When}", ButtonName(button));
            Assert.StartsWith("Erledigt:", ButtonName(button), StringComparison.Ordinal);
            Assert.Contains("10:12", ButtonName(button), StringComparison.Ordinal);
            Assert.Equal("Abgleich, 1 offen", ButtonName(window.GetVisualDescendants().OfType<ListBoxItem>().Single(i => ReferenceEquals(i.DataContext, shell.SelectedPage))));
            return shell.Sync;
        }, stub, local: new SampleLocal());
    }

    [Fact]
    public Task Erledigt_im_Banner_nennt_die_Aenderung()
    {
        var offline = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        return Render("hell", "banner-namen", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            var id = Guid.NewGuid();
            const string Description = "Sabine Gruber, Mo. 19.10. 13:00: verschieben";
            offline.Enqueue(new PendingChange(id, "api/appointments", "{}", Description, DateTimeOffset.UtcNow, null));
            offline.Complete(id, new FailedChange(id, Description, "Lena Huber hat zu dieser Zeit schon einen Termin.", DateTimeOffset.UtcNow));
            await Pump();
            Assert.True(shell.HasFailed);
            var button = Assert.Single(ButtonsFor(window, shell.DismissFailedCommand));
            Assert.Equal($"Erledigt: {Description}", ButtonName(button));
            return shell;
        }, offline: offline);
    }

    [Fact]
    public Task Entfernen_Knoepfe_der_Gruppenbuchung_nennen_Gast_und_Abschnitt() => Render("hell", "kalender-gruppe-namen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenGroupBookingCommand.Execute(null);
        var group = calendar.GroupBooking!;
        group.AddGuestCommand.Execute(null);
        group.AddStepCommand.Execute(null);
        await Pump();

        var panel = SidePanelOf(window);
        var guests = group.Guests.Where(g => g.CanRemove).ToList();
        Assert.NotEmpty(guests);
        Assert.Equal(guests.Select(g => $"{g.Label} entfernen"), guests.Select(g => ButtonName(Assert.Single(ButtonsFor(panel, g.RemoveCommand)))));
        Assert.Equal(["Abschnitt 1 entfernen", "Abschnitt 2 entfernen"], group.Steps.Select(s => ButtonName(Assert.Single(ButtonsFor(panel, s.RemoveCommand)))));
        return calendar;
    });

    [Fact]
    public Task Baustein_entfernen_nennt_den_Baustein() => Render("hell", "kalender-wunsch-namen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenRequestCommand.Execute(null);
        calendar.Request!.Text = "heute ab 17 Uhr eine kräftige Aromaöl-Massage, nicht bei Marco";
        await calendar.Request.SubmitAsync();
        await Pump(Settling);

        var panel = SidePanelOf(window);
        var chips = panel.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("chipx") && b.DataContext is WishChip { CanRemove: true }).ToList();
        Assert.NotEmpty(chips);
        Assert.All(chips, chip => Assert.Equal($"Baustein „{((WishChip)chip.DataContext!).Text}“ entfernen", ButtonName(chip)));
        return calendar;
    }, Api().On($"POST /api/locations/{SpaSample.LocationId}/calendar-request", SpaSample.CalendarRequest()));

    [Fact]
    public Task Kassa_Zeilen_und_offene_Termine_nennen_Gast_und_Position() => Render("hell", "kassa-namen", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        var pos = shell.Pos;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump();

        var page = window.GetVisualDescendants().OfType<PosView>().First();
        // Seit Slice 137 sind die Zeilen von „Heute abzurechnen“ Einträge einer Liste (Pfeiltasten), keine Knöpfe mehr.
        var unbilled = Named<ListBox>(page, "TodayList").GetVisualDescendants().OfType<ListBoxItem>().ToList();
        Assert.NotEmpty(unbilled);
        Assert.All(unbilled, row => Assert.Equal(((UnbilledItem)row.DataContext!).AccessibleName, ButtonName(row)));

        var line = pos.Lines[0];
        var remove = ButtonsFor(page, pos.RemoveLineCommand).Single(b => ReferenceEquals(b.DataContext, line));
        Assert.Equal($"{line.Description} entfernen", ButtonName(remove));
        var discount = ButtonsFor(page, pos.EditDiscountCommand).Single(b => ReferenceEquals(b.DataContext, line));
        Assert.Equal($"Rabatt auf {line.Description}", ButtonName(discount));
        return pos;
    });

    [Fact]
    public Task Weitere_Adresse_entfernen_nennt_die_Adresse()
    {
        var customer = SpaSample.Customers[0];
        var privat = new CustomerEmailDto(Guid.NewGuid(), "brigitte@privat.example");
        var stub = Api().On($"GET /api/customers/{customer.Id}/emails", new List<CustomerEmailDto> { privat });
        return Render("hell", "kunden-adressen-namen", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await shell.ShowCustomerAsync(customer.Id);
            await Pump();
            var customers = shell.Customers;
            for (var i = 0; i < 100 && customers.OtherEmails.Count == 0; i++)
            {
                await Pump();
            }

            var row = Assert.Single(customers.OtherEmails);
            var button = Assert.Single(ButtonsFor(window, row.RemoveCommand));
            Assert.Equal("Adresse brigitte@privat.example entfernen", ButtonName(button));
            return customers;
        }, stub);
    }

    /// <summary>Given die Kundenkarte mit zwei Adressen When der Namens-Scan läuft Then heißen die Knöpfe je mit ihrer Adresse und lassen sich unterscheiden (W40).</summary>
    [Fact]
    public Task Zwei_weitere_Adressen_heissen_je_mit_ihrer_Adresse()
    {
        var customer = SpaSample.Customers[0];
        var stub = Api().On($"GET /api/customers/{customer.Id}/emails",
            new List<CustomerEmailDto> { new(Guid.NewGuid(), "brigitte@privat.example"), new(Guid.NewGuid(), "b.mueller@firma.example") });
        return Render("hell", "kunden-adressen-zwei", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await shell.ShowCustomerAsync(customer.Id);
            await Pump();
            var customers = shell.Customers;
            for (var i = 0; i < 100 && customers.OtherEmails.Count < 2; i++)
            {
                await Pump();
            }

            var names = customers.OtherEmails.Select(row => ButtonName(Assert.Single(ButtonsFor(window, row.RemoveCommand)))).ToList();
            Assert.Equal(["Adresse brigitte@privat.example entfernen", "Adresse b.mueller@firma.example entfernen"], names);
            return customers;
        }, stub);
    }
}
