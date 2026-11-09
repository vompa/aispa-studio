using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Startet die echte App ohne Bildschirm und zeichnet mit Skia, damit Ansichten wie unter Windows gerendert werden.</summary>
public sealed class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
        // Vor dem Start der App (sie liest die Einstellung in OnFrameworkInitializationCompleted): die Tests laufen immer mit „Animationen an“,
        // egal was der PC oder der Runner von GitHub meldet. Tests für „aus“ setzen die Quelle selbst.
        ReducedMotion.SystemSource = () => false;
        // Bildtests öffnen das Fenster mit echten Timern: unter Last dauerte der Start sonst länger als 300 ms, und der Startbildschirm läge im Bild.
        MainViewModel.DefaultStartupScreenDelay = Timeout.InfiniteTimeSpan;
        return AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}

/// <summary>
/// Rendert die Hauptansichten mit einem vollen Beispieltag. Prüft, dass alle Bindungen auflösen und nichts wirft;
/// mit der Umgebungsvariable AISPA_SCREENSHOTS (Ordner) entstehen dabei die Bildschirmfotos für PRs und Doku.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));

    private sealed class SampleTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    /// <summary>Bildtests und Handbuchbilder melden sich als Inhaberin an (alle Rechte), wie der Auftraggeber beim Prüfen; Rechte-Ausblenden prüft <see cref="ReceptionApi"/>.</summary>
    private static CurrentUser Owner() => new("u1", "inhaberin@demo.aispa.test", "Katrin Aigner", 1,
        new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Owner, true, []), null, [.. Enum.GetValues<AccessRight>()]);

    /// <summary>Rezeption ohne Verwaltungsrechte, für Tests, die zeigen, was ausgeblendet bleibt.</summary>
    private static CurrentUser Reception() => new("u2", "empfang.wien@demo.aispa.test", "Rita Steiner", 1,
        new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Reception, true, []), null,
        [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus]);

    private static StubApi ReceptionApi() => Api().On("GET /api/auth/me", Reception());

    private static StubApi Api() => new StubApi()
        .On("GET /api/system/status", new SystemStatus("0.9.0+abc", "Development", DateTimeOffset.UtcNow))
        .On("GET /api/auth/me", Owner())
        .On("GET /api/tenant", SpaTenant)
        .On("GET /api/tenant/history", CompanyHistory)
        .On("GET /api/tenant/embedding", new EmbeddingSettings([]))
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna, SpaSample.Salzburg })
        .On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.Resources)
        .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.CalendarDay())
        .On("GET /api/customers?search=m%C3%BCl", SpaSample.Customers.ToList())
        .On($"GET /api/customers/{SpaSample.Customers[0].Id}/anamnesis/check?treatmentId={SpaSample.Treatments[2].Id}", new AnamnesisCheck(true))
        .On("GET /api/customers?search=", SpaSample.Customers.ToList())
        .On($"GET /api/customers/{SpaSample.Customers[0].Id}", SpaSample.Customers[0])
        .On($"GET /api/customers/{SpaSample.Customers[0].Id}/appointments", SpaSample.CustomerHistory())
        .On($"GET /api/customers/{SpaSample.Customers[0].Id}/insights", SpaSample.Insights())
        .On($"GET /api/customers/{SpaSample.Selected.CustomerId}/summary", SpaSample.Summary(finance: true))
        .On($"GET /api/customers/{SpaSample.Customers[0].Id}/flags", SpaSample.CustomerFlags())
        .On($"GET /api/reports/customers/{SpaSample.Customers[0].Id}/preferences", SpaSample.Wishes())
        .On("GET /api/employees", SpaSample.Resources.Employees.ToList())
        .On($"POST /api/appointments/{SpaSample.Selected.Id}/series/preview", SeriesPreview())
        .On($"GET /api/customers/{SpaSample.Customers[0].Id}/booking-preferences/suggestion", new CustomerPreferenceSuggestion(
            5, SpaSample.Lena.Id, SpaSample.Lena.DisplayName, 4, DayOfWeek.Tuesday, 4, new TimeOnly(12, 0), new TimeOnly(17, 0),
            "4 von 5 Besuchen bei Lena Huber · meist Dienstag · meist nachmittags"))
        .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
        .On("GET /api/pos/articles", SpaSample.Articles)
        .On("GET /api/treatments", SpaSample.Treatments.ToList())
        .On($"PUT /api/pos/bills/{SpaSample.BillId}/lines/{SpaSample.TreatmentLineId}/discount", SpaSample.Bill(discount: 10m))
        .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList(SpaSample.UnbilledVisits()))
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto> { SpaSample.Parked })
        .On("POST /api/pos/bills", SpaSample.Bill(withArticle: false))
        .On($"POST /api/pos/bills/{SpaSample.BillId}/lines", SpaSample.Bill())
        .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", SpaSample.Receipt())
        .On($"GET /api/pos/terminals?locationId={SpaSample.LocationId}", new List<PaymentTerminalDto> { Terminal })
        .On($"POST /api/pos/bills/{SpaSample.BillId}/card-payments", Card(CardPaymentStatus.Pending))
        .On($"GET /api/pos/card-payments/{CardId}", Card(CardPaymentStatus.Approved))
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/receipts?date=2026-10-19", DayReceipts())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/day-report", DayReport())
        .On($"POST /api/locations/{SpaSample.LocationId}/appointment-groups/availability", SpaSample.CoupleSlots())
        .On($"GET /api/pos/customers/{SpaSample.Paying.CustomerId}/packages", SpaSample.Packages())
        .On($"GET /api/locations/{SpaSample.LocationId}/areas?date=2026-10-19", SpaSample.Areas())
        .On($"GET /api/locations/{SpaSample.LocationId}/availability?treatmentId={SpaSample.Treatments[1].Id}&date=2026-10-19&minSlots=3&days=14", SpaSample.FreeSlots())
        .On($"GET /api/locations/{SpaSample.LocationId}/waitlist", SpaSample.Waitlist())
        .On("GET /api/inbox", SpaSample.Inbox())
        .On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMail())
        .On("GET /api/customers?search=br", new List<CustomerDto> { SpaSample.Customers[0] })
        .On("GET /api/customers?search=be", new List<CustomerDto> { SpaSample.Customers[1] })
        .On($"POST /api/appointments/{SpaSample.Selected.Id}/move", SpaSample.Selected with
        {
            Start = SpaSample.At(10),
            End = SpaSample.At(11, 20),
            BlockedFrom = SpaSample.At(10),
            BlockedUntil = SpaSample.At(11, 30),
            RoomId = SpaSample.Cedar.Id,
            UpdatedAt = SpaSample.At(11, 40),
        });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kalender_mit_ausgewaehltem_Termin(string theme) => Render(theme, "kalender", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        return calendar;
    });

    [Fact]
    public Task Kalender_Panel_Mehr_zeigt_die_seltenen_Aktionen() => Render("hell", "kalender-mehr", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();
        bool Visible(string text) => window.GetVisualDescendants().OfType<Button>().Any(b => b.IsEffectivelyVisible && b.Content as string == text);

        // Stornieren steht ohne „Mehr“ im Aktionsblock (Ä81); Erledigt und Nicht erschienen stehen hinter „Mehr“.
        Assert.True(Visible("Termin stornieren …"));
        Assert.False(Visible("Erledigt"));
        var more = window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(t => t.Name == "MoreToggle");
        more.IsChecked = true;
        await Pump();

        Assert.True(Visible("Erledigt"));
        Assert.True(Visible("Nicht erschienen"));

        // Kalenderdatei für den Gast (Export/Import, Aufgabe C).
        Assert.True(window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CalendarFileButton").IsEffectivelyVisible);
        return calendar;
    });

    [Fact]
    public Task Kalender_beim_Ziehen_und_in_der_Raumansicht() => Render("hell", "kalender-ziehen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        var item = calendar.Columns.SelectMany(c => c.Items).First(i => i.Appointment?.Id == SpaSample.Selected.Id);
        calendar.BeginDrag(item, 20);
        calendar.DragTo(DayLayout.ColumnLeft(1) + 40, item.Top + 20 - 25 * DayLayout.PixelsPerMinute);
        return calendar;
    });

    [Fact]
    public Task Kalender_Einfachklick_oeffnet_den_Termin_rechts() => Render("hell", "kalender-klick", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        Assert.Equal("Montag, 19. Oktober 2026", calendar.DateShort);
        var at = CenterOf(window, SpaSample.Selected.Id);

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        Assert.Equal(SpaSample.Selected.Id, calendar.Selected?.Appointment.Id);

        // Die Maus bleibt stehen, das Panel verschiebt den Kalender darunter: Windows meldet trotzdem eine Bewegung.
        window.MouseMove(at);
        await Pump();
        Assert.Null(calendar.Ghost);

        // Das Panel macht den Kalender schmaler; über dem neu aufgebauten Termin darf keine Buchungsvorschau erscheinen.
        var over = CenterOf(window, SpaSample.Selected.Id);
        window.MouseMove(over);
        window.MouseMove(over + new Point(3, 2));
        await Pump();
        Assert.Null(calendar.Ghost);
        return calendar;
    });

    [Fact]
    public Task Kalender_Klick_baut_die_Spalten_nicht_neu_auf()
    {
        var output = TestContext.Current.TestOutputHelper;
        return Render("hell", "kalender-messung", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump();
            var rebuilds = 0;
            calendar.PropertyChanged += (_, e) => rebuilds += e.PropertyName == nameof(CalendarViewModel.Columns) ? 1 : 0;
            var other = calendar.Columns.SelectMany(c => c.Items).Select(i => i.Appointment).OfType<AppointmentDto>()
                .First(a => a.Id != SpaSample.Selected.Id && a.Status != AppointmentStatus.Cancelled).Id;

            var firstMs = Click(window, CenterOf(window, SpaSample.Selected.Id));
            var firstRebuilds = rebuilds;
            Assert.Equal(SpaSample.Selected.Id, calendar.Selected?.Appointment.Id);
            await Pump();
            rebuilds = 0;
            var secondMs = Click(window, CenterOf(window, other));
            Assert.Equal(other, calendar.Selected?.Appointment.Id);
            var selected = calendar.Columns.SelectMany(c => c.Items).Where(i => i.IsSelected).Select(i => i.Id).ToList();
            Assert.Equal([other], selected);

            // Die Zeiten sind nur zur Ansicht (unter Last wackelig); geprüft wird, dass kein Klick die Spalten neu aufbaut.
            output?.WriteLine($"Erster Klick (Panel geht auf): {firstMs:F1} ms, {firstRebuilds}x Columns neu");
            output?.WriteLine($"Klick auf anderen Termin bei offenem Panel: {secondMs:F1} ms, {rebuilds}x Columns neu");
            // Beim Öffnen des Panels wird die Kopfzeile schmaler und höher; bei „Tag einpassen“ ändert die Höhe die Minutenhöhe, das ist ein Neuaufbau.
            Assert.True(firstRebuilds <= 1);
            Assert.Equal(0, rebuilds);
            return calendar;
        });
    }

    /// <summary>Klickt und misst bis Layout und Aufträge nach dem Loslassen durchgelaufen sind (ohne die festen Wartezeiten von <see cref="Pump"/>).</summary>
    private static double Click(Window window, Point at)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return watch.Elapsed.TotalMilliseconds;
    }

    [Fact]
    public Task Kalender_ohne_Mausbewegung_nach_dem_Klick_keine_Hover_Karte() => Render("hell", "kalender-klick-hover", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var at = CenterOf(window, SpaSample.Selected.Id);
        var card = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("appt") && b.DataContext is CalendarItem { Appointment.Id: var id } && id == SpaSample.Selected.Id);
        Assert.True(ToolTip.GetServiceEnabled(card));

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        Assert.True(calendar.HoverCardsPaused);
        Assert.False(ToolTip.GetServiceEnabled(card));

        // Die Maus bewegt sich wirklich: die Karten sind wieder da.
        window.MouseMove(at);
        window.MouseMove(at + new Point(30, 30));
        await Pump();
        Assert.False(calendar.HoverCardsPaused);
        Assert.True(ToolTip.GetServiceEnabled(card));
        return calendar;
    });

    [Fact]
    public Task Kalender_Hover_Karte_in_Abschnitten() => Render("hell", "kalender-hoverkarte", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var border = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("appt") && b.DataContext is CalendarItem { Appointment.Id: var id } && id == SpaSample.Selected.Id);
        var card = Assert.IsType<HoverCard>(ToolTip.GetTip(border));

        // Die Karte mit der Vorlage der Kalenderansicht in einem eigenen Fenster zeichnen, wie sie im Tooltip erscheint.
        var view = window.GetVisualDescendants().OfType<CalendarView>().Single();
        var template = view.DataTemplates.First(t => t.Match(card));
        var content = template.Build(card)!;
        content.DataContext = card;
        var cardWindow = new Window { Width = 380, SizeToContent = SizeToContent.Height, Content = new Border { Padding = new Thickness(12), Child = content } };
        cardWindow.Show();
        await Pump();

        var blocks = cardWindow.GetVisualDescendants().OfType<TextBlock>().ToList();
        var texts = blocks.Select(t => t.Text).ToList();
        Assert.Contains("Brigitte Müller", texts);
        Assert.Contains(cardWindow.GetVisualDescendants().OfType<AispaStudio.Desktop.Controls.Icon>(), i => i.Data == AispaStudio.Desktop.Controls.Icons.Female && i.IsVisible);
        Assert.Contains("Stammgast", texts);
        Assert.Contains(texts, t => t?.Contains("Lena Huber · Raum Lotus", StringComparison.Ordinal) == true);
        Assert.Contains("Gast", texts);
        if (Environment.GetEnvironmentVariable("AISPA_SCREENSHOTS") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            cardWindow.CaptureRenderedFrame()?.Save(Path.Combine(folder, "kalender-hoverkarte-karte.png"), PngBitmapEncoderOptions.Default);
        }

        cardWindow.Close();
        return calendar;
    });

    [Fact]
    public Task Kalender_Panel_Zeiten_je_Termin_mit_Zahlenfeldern_und_Esc() => Render("hell", "kalender-panel-zeiten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        calendar.Selected!.BeginEditTimes();
        await Pump();

        var boxes = window.GetVisualDescendants().OfType<NumberField>().Where(n => n.IsEffectivelyVisible).ToList();
        Assert.Equal(2, boxes.Count);
        Assert.Equal(((decimal?)calendar.Selected.TimesPreparation, (decimal?)calendar.Selected.TimesFollowUp), (boxes[0].Value, boxes[1].Value));
        Assert.All(boxes, b => Assert.Equal((0m, 120m, 5m), (b.Minimum!.Value, b.Maximum!.Value, b.Increment)));

        // Esc im Zahlenfeld bricht nur das Ändern ab; das Panel bleibt.
        boxes[0].Focus();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.False(calendar.Selected!.IsEditingTimes);
        Assert.NotNull(calendar.Selected);
        calendar.Selected.BeginEditTimes();
        return calendar;
    });

    [Fact]
    public Task Kalender_Einwirkzeit_als_Band_und_zwei_Termine_nebeneinander()
    {
        var day = SpaSample.ProcessingDay();
        var quick = day.Appointments.Single(a => a.CustomerName == "Karin Ebner");
        var facial = day.Appointments.Single(a => a.HandsOnMinutes > 0);
        return Render("hell", "kalender-einwirkzeit", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump();

            // Meldungen und „Heute“ nehmen Höhe; mit dem Zoom ist das Band hoch genug für seinen Text, wie bei einem kleinen Fenster.
            while (calendar.Scale.PixelsPerMinute < 1.2)
            {
                calendar.ZoomInCommand.Execute(null);
            }

            await Pump();

            // Das Band liegt im Termin der Gesichtsbehandlung, die beiden Termine teilen sich Sophies Spalte zur Hälfte.
            var items = calendar.Columns.SelectMany(c => c.Items).Where(i => i.Appointment?.Id == facial.Id || i.Appointment?.Id == quick.Id).ToList();
            Assert.All(items, i => Assert.Equal(2, i.LaneCount));
            var cards = window.GetVisualDescendants().OfType<Border>()
                .Where(b => b.Classes.Contains("appt") && b.DataContext is CalendarItem { LaneCount: 2 }).ToList();
            Assert.Equal(2, cards.Count);
            Assert.All(cards, c => Assert.Equal(DayLayout.LaneWidth(calendar.Scale.ItemWidth, new LanePlace(0, 2)), c.Bounds.Width, 1.0));
            var band = Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("processing") && b.IsEffectivelyVisible);
            Assert.Equal(45 * calendar.Scale.PixelsPerMinute, band.Bounds.Height, 1.0);
            Assert.Contains(band.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Einwirkzeit 45 Min. · frei" && t.IsEffectivelyVisible);
            return calendar;
        }, Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", day));
    }

    private static readonly string[] FocusChoices = ["Eins", "Zwei"];

    /// <summary>Design-Brief 4.5 (A11): Tastaturfokus zeigt einen 2-px-Rahmen in der Akzentfarbe; die Maus löst ihn nicht aus.</summary>
    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Tastaturfokus_zeigt_einen_Rahmen_in_der_Akzentfarbe(string theme) => Session.Dispatch(async () =>
    {
        Application.Current!.RequestedThemeVariant = theme == "dunkel" ? ThemeVariant.Dark : ThemeVariant.Light;
        var anchor = new Button { Content = "Anker" };
        var button = new Button { Content = "Speichern" };
        var toggle = new Avalonia.Controls.Primitives.ToggleButton { Content = "Mehr" };
        var box = new TextBox();
        var combo = new ComboBox { ItemsSource = FocusChoices };
        var check = new CheckBox { Content = "Aktiv" };
        var list = new ListBox { ItemsSource = FocusChoices };
        var window = new Window { Width = 400, Height = 400, Content = new StackPanel { Margin = new Thickness(40), Spacing = 12, Children = { anchor, button, toggle, box, combo, check, list } } };
        window.Show();
        await Pump();

        foreach (var control in new Control[] { button, toggle, box, combo, check, list.ContainerFromIndex(0)! })
        {
            control.Focus(NavigationMethod.Pointer);
            await Pump();
            Assert.Empty(RingsOf(control));

            anchor.Focus(NavigationMethod.Pointer);
            control.Focus(NavigationMethod.Tab);
            await Pump();
            var ring = Assert.Single(RingsOf(control));
            Assert.Equal(new Thickness(2), ring.BorderThickness);
            Assert.True(control.TryFindResource("AccentBrush", control.ActualThemeVariant, out var accent));
            Assert.Equal(((ISolidColorBrush)accent!).Color, ((ISolidColorBrush)ring.BorderBrush!).Color);
            Assert.Equal(new Thickness(-4), ring.Margin);
            Assert.False(Avalonia.Controls.Primitives.AdornerLayer.GetIsClipEnabled(ring), "Der Rahmen liegt außerhalb des Elements und darf nicht beschnitten werden");
            Assert.True(ring.Bounds.Width >= control.Bounds.Width + 8, $"{control.GetType().Name}: Rahmen {ring.Bounds} um {control.Bounds}");
        }

        if (Environment.GetEnvironmentVariable("AISPA_SCREENSHOTS") is { Length: > 0 } folder)
        {
            anchor.Focus(NavigationMethod.Pointer);
            button.Focus(NavigationMethod.Tab);
            await Pump(Settling);
            Directory.CreateDirectory(folder);
            window.CaptureRenderedFrame()?.Save(Path.Combine(folder, $"tastaturfokus{(theme == "hell" ? "" : "-" + theme)}.png"), PngBitmapEncoderOptions.Default);
        }

        window.Close();
        return true;
    }, CancellationToken.None);

    /// <summary>Design-Brief 5.1 (A13): „Gefährlich, bestätigt“ ist rot gefüllt mit heller Schrift, der einfache gefährliche Knopf bleibt Umriss.</summary>
    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Gefaehrlicher_Knopf_ist_Umriss_und_bestaetigt_gefuellt(string theme) => Session.Dispatch(async () =>
    {
        Application.Current!.RequestedThemeVariant = theme == "dunkel" ? ThemeVariant.Dark : ThemeVariant.Light;
        var outline = new Button { Content = "Stornieren", Classes = { "danger" } };
        var filled = new Button { Content = "Stornieren bestätigen", Classes = { "danger", "filled" } };
        var window = new Window { Width = 400, Height = 200, Content = new StackPanel { Margin = new Thickness(40), Spacing = 12, Children = { outline, filled } } };
        window.Show();
        await Pump();

        Color Resolved(Control owner, string key) =>
            owner.TryFindResource(key, owner.ActualThemeVariant, out var brush) ? ((ISolidColorBrush)brush!).Color : throw new InvalidOperationException(key);
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)outline.Background!).Color);
        Assert.Equal(Resolved(outline, "CritBrush"), ((ISolidColorBrush)outline.Foreground!).Color);
        Assert.Equal(Resolved(filled, "CritBrush"), ((ISolidColorBrush)filled.Background!).Color);
        Assert.Equal(Resolved(filled, "AccentFgBrush"), ((ISolidColorBrush)filled.Foreground!).Color);
        Assert.Equal(Resolved(filled, "CritBrush"), ((ISolidColorBrush)filled.BorderBrush!).Color);

        window.Close();
        return true;
    }, CancellationToken.None);

    private static List<Border> RingsOf(Control control) =>
        Avalonia.Controls.Primitives.AdornerLayer.GetAdornerLayer(control)?.Children.SelectMany(c => c.GetSelfAndVisualDescendants().OfType<Border>())
            .Where(b => b.BorderThickness == new Thickness(2) && b.Margin == new Thickness(-4)).ToList() ?? [];

    [Fact]
    public Task Kalender_Termin_in_der_schmalen_Bahn_laesst_sich_mit_der_Maus_ziehen()
    {
        var day = SpaSample.ProcessingDay();
        var quick = day.Appointments.Single(a => a.CustomerName == "Karin Ebner");
        var stub = Api()
            .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", day)
            .On($"POST /api/appointments/{quick.Id}/move", quick with { EmployeeId = SpaSample.Malee.Id, UpdatedAt = SpaSample.At(11, 40) });
        return Render("hell", "kalender-einwirkzeit-ziehen", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump();
            var from = CenterOf(window, quick.Id);
            var to = new Point(from.X + calendar.Scale.ColumnWidth + 8, from.Y);

            window.MouseDown(from, MouseButton.Left);
            for (var step = 1; step <= 5; step++)
            {
                window.MouseMove(from + (to - from) * (step / 5.0));
                await Pump();
            }

            Assert.True(calendar.IsDragging);
            window.MouseUp(to, MouseButton.Left);
            await Pump();
            Assert.Contains(stub.Calls, c => c.Route == $"POST /api/appointments/{quick.Id}/move");
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Kalender_Termin_mit_der_Maus_ziehen()
    {
        var stub = Api();
        return Render("hell", "kalender-maus-ziehen", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            calendar.ByRoom = true;
            await Pump();
            var from = CenterOf(window, SpaSample.Selected.Id);
            var to = new Point(from.X + calendar.Scale.ColumnWidth + 8, from.Y - 30 * calendar.Scale.PixelsPerMinute);

            window.MouseDown(from, MouseButton.Left);
            for (var step = 1; step <= 5; step++)
            {
                window.MouseMove(from + (to - from) * (step / 5.0));
                await Pump();
            }

            Assert.True(calendar.IsDragging);
            window.MouseUp(to, MouseButton.Left);
            await Pump();

            Assert.Contains(stub.Calls, c => c.Route == $"POST /api/appointments/{SpaSample.Selected.Id}/move");
            Assert.True(calendar.CanUndo);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Kalender_Esc_beim_Ziehen_bricht_ab_und_der_Zeiger_wechselt()
    {
        var stub = Api();
        return Render("hell", "kalender-ziehen-abgebrochen", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            calendar.ByRoom = true;
            await Pump();
            var view = window.GetVisualDescendants().OfType<CalendarView>().Single();
            var from = CenterOf(window, SpaSample.Selected.Id);
            var to = new Point(from.X + calendar.Scale.ColumnWidth + 8, from.Y - 30 * calendar.Scale.PixelsPerMinute);

            // Über einem verschiebbaren Termin: Hand; beim Ziehen: Kreuz.
            window.MouseMove(from);
            await Pump();
            Assert.Equal(StandardCursorType.Hand, view.PointerShape);
            window.MouseDown(from, MouseButton.Left);
            for (var step = 1; step <= 5; step++)
            {
                window.MouseMove(from + (to - from) * (step / 5.0));
                await Pump();
            }

            Assert.True(calendar.IsDragging);
            Assert.Equal(StandardCursorType.SizeAll, view.PointerShape);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.MouseUp(to, MouseButton.Left);
            await Pump();

            Assert.False(calendar.IsDragging);
            Assert.Null(calendar.Ghost);
            Assert.Null(calendar.Selected);
            Assert.DoesNotContain(stub.Calls, c => c.Route.EndsWith("/move", StringComparison.Ordinal));
            Assert.Equal(StandardCursorType.Arrow, view.PointerShape);
            Assert.Equal(SpaSample.Selected.Start, calendar.Columns.SelectMany(c => c.Items).First(i => i.Appointment?.Id == SpaSample.Selected.Id).Appointment!.Start);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Kalender_Zusagen_ueber_den_Link()
    {
        // Gut die Hälfte der noch erwarteten Gäste hat gestern Abend über den Link in der Erinnerung zugesagt.
        var day = SpaSample.CalendarDay();
        var confirmedAt = SpaSample.At(18, 12).AddDays(-1);
        var booked = day.Appointments.Where(a => a.Status == AppointmentStatus.Booked).OrderBy(a => a.Start).ToList();
        var confirmed = booked.Where((a, i) => i % 3 != 2).Select(a => a.Id).ToHashSet();
        day = day with { Appointments = [.. day.Appointments.Select(a => confirmed.Contains(a.Id) ? a with { ConfirmedAt = confirmedAt } : a)] };
        var stub = Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", day);
        return Render("hell", "kalender-zusagen", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            calendar.FocusOnCommand.Execute(CalendarFocus.Unconfirmed);
            Assert.Contains($"· {booked.Count - confirmed.Count} unbestätigt", calendar.OverviewChip, StringComparison.Ordinal);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Kalender_Termin_auf_anderen_Tag() => Render("hell", "kalender-anderer-tag", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        var panel = calendar.Selected!;
        panel.AskMoveToDayCommand.Execute(null);
        panel.MoveDaysCommand.Execute("1");
        panel.MoveDaysCommand.Execute("1");
        panel.MoveTimeText = "15:00";
        Assert.True(panel.IsMovingToDay);
        return calendar;
    });

    [Fact]
    public Task Kalender_mit_Tastatur() => Render("hell", "kalender-markiert", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();

        // Die Taste kommt über das Fenster, wie an der Rezeption: Pfeil ab markiert den nächsten anstehenden Termin.
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.NotNull(calendar.MarkedId);
        Assert.Null(calendar.Selected);
        return calendar;
    });

    /// <summary>Taste über das Fenster, wie an der Rezeption.</summary>
    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
    }

    [Fact]
    public Task Kalender_Pfeiltasten_wirken_ohne_Klick_und_nach_Klick_auf_freie_Stelle() => Render("hell", "kalender-pfeile-frei", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var day = calendar.Date;
        Press(window, PhysicalKey.ArrowRight);
        Assert.Equal(day.AddDays(1), calendar.Date);
        Press(window, PhysicalKey.ArrowLeft);
        Assert.Equal(day, calendar.Date);

        // Klick auf eine freie Stelle in der Spalte öffnet das Eingabefenster; Esc schließt es, dann gehen die Pfeile wieder.
        var host = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "Host");
        window.MouseDown(host.TranslatePoint(new Point(30, 700), window)!.Value, MouseButton.Left);
        window.MouseUp(host.TranslatePoint(new Point(30, 700), window)!.Value, MouseButton.Left);
        await Pump();
        Press(window, PhysicalKey.Escape);
        Press(window, PhysicalKey.ArrowRight);
        Assert.Equal(day.AddDays(1), calendar.Date);
        return calendar;
    });

    [Fact]
    public Task Kalender_Pfeiltasten_wirken_nach_Klick_auf_einen_Termin() => Render("hell", "kalender-pfeile-termin", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var at = CenterOf(window, SpaSample.Selected.Id);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        Assert.Equal(SpaSample.Selected.Id, calendar.MarkedId);
        var focused = TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement();
        var before = calendar.MarkedId;
        Press(window, PhysicalKey.ArrowDown);
        await Pump();
        var afterDown = calendar.MarkedId;
        Press(window, PhysicalKey.ArrowRight);
        await Pump();
        Assert.True(before != afterDown || calendar.MarkedId != afterDown, $"Pfeile ohne Wirkung, Fokus: {focused}");
        return calendar;
    });

    [Fact]
    public Task Kalender_Pfeiltasten_wirken_nach_Klick_auf_Kopfzeilenknopf() => Render("hell", "kalender-pfeile-knopf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var day = calendar.Date;
        var next = window.GetVisualDescendants().OfType<Button>().First(b => ToolTip.GetTip(b) as string == "Nächster Tag (→)");
        var at = next.TranslatePoint(new Point(10, 10), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        Assert.Equal(day.AddDays(1), calendar.Date);
        Press(window, PhysicalKey.ArrowRight);
        Assert.Equal(day.AddDays(2), calendar.Date);
        return calendar;
    });

    [Fact]
    public Task Kalender_Pfeiltasten_wirken_nach_Klick_in_die_Navigation() => Render("hell", "kalender-pfeile-navigation", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        await Pump();
        ListBoxItem Nav(string title) => window.GetVisualDescendants().OfType<ListBoxItem>().First(i => i.DataContext is NavItem { Title: var t } && t == title);
        void ClickNav(string title)
        {
            var item = Nav(title);
            var at = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value;
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
        }

        ClickNav("Kunden");
        await Pump();
        ClickNav("Kalender");
        await Pump();
        Assert.Same(shell.Calendar, shell.SelectedPage!.Page);
        var day = calendar.Date;
        Press(window, PhysicalKey.ArrowRight);
        await Pump();
        Assert.Same(shell.Calendar, shell.SelectedPage!.Page);
        Assert.Equal(day.AddDays(1), calendar.Date);
        return calendar;
    });

    [Fact]
    public Task Kalender_Pfeiltasten_wirken_nach_dem_Schliessen_des_Panels_per_Maus() => Render("hell", "kalender-pfeile-panel-zu", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var at = CenterOf(window, SpaSample.Selected.Id);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        Assert.NotNull(calendar.Selected);

        // Der Schließen-Knopf des Panels hat den Fokus und verschwindet mit dem Panel; danach darf kein Fokus im Leeren hängen.
        var panel = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "SidePanel");
        var close = panel.GetVisualDescendants().OfType<Button>().First(b => (ToolTip.GetTip(b) as string)?.StartsWith("Schließen", StringComparison.Ordinal) == true && b.IsEffectivelyVisible);
        var closeAt = close.TranslatePoint(new Point(close.Bounds.Width / 2, close.Bounds.Height / 2), window)!.Value;
        window.MouseDown(closeAt, MouseButton.Left);
        window.MouseUp(closeAt, MouseButton.Left);
        await Pump();
        Assert.Null(calendar.Selected);
        calendar.HandleKey(Key.Escape, KeyModifiers.None);
        Assert.Null(calendar.MarkedId);

        var day = calendar.Date;
        Press(window, PhysicalKey.ArrowRight);
        Assert.Equal(day.AddDays(1), calendar.Date);
        return calendar;
    });

    /// <summary>Die Beispielwoche ab Montag, 19.10.: je Tag eigene Termine.</summary>
    private static StubApi WeekApi(StubApi? api = null) => Enumerable.Range(1, 13).Aggregate(api ?? Api(), (stub, days) =>
        stub.On($"GET /api/locations/{SpaSample.LocationId}/calendar?date={SpaSample.Day.AddDays(days):yyyy-MM-dd}", SpaSample.CalendarDayAfter(days)));

    /// <summary>Kopf einer Person oder eines Raums im Kalender, in Fensterkoordinaten.</summary>
    private static Point HeaderOf(Window window, string title)
    {
        var header = window.GetVisualDescendants().OfType<Grid>()
            .First(g => g.DataContext is CalendarColumn column && column.Title == title && g.Classes.Contains("filterable"));
        return header.TranslatePoint(new Point(40, header.Bounds.Height / 2), window)!.Value;
    }

    [Fact]
    public Task Kalender_Klick_auf_den_Kopf_zeigt_die_Termine_der_Person_ueber_sieben_Tage() => Render("hell", "kalender-woche", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var at = HeaderOf(window, "Lena Huber");
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        await Pump();
        Assert.Equal("Lena Huber · 7 Tage", calendar.FilterText);
        Assert.Equal(7, calendar.Columns.Count);
        Assert.All(calendar.Columns, c => Assert.True(c.IsDay));

        // Klick auf einen Termin der Woche öffnet das Panel, die Ansicht bleibt.
        var item = calendar.Columns[0].Items.First(i => i.Appointment is not null);
        var click = CenterOf(window, item.Appointment!.Id);
        window.MouseDown(click, MouseButton.Left);
        window.MouseUp(click, MouseButton.Left);
        await Pump();
        Assert.Equal(item.Appointment.Id, calendar.Selected?.Appointment.Id);
        Assert.True(calendar.IsFiltered);
        calendar.HandleKey(Key.Escape, KeyModifiers.None);
        await Pump();
        Assert.True(calendar.IsFiltered);
        return calendar;
    }, WeekApi());

    [Fact]
    public Task Kalender_Filter_aufheben_per_Knopf_und_Esc() => Render("hell", "kalender-woche-zurueck", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var at = HeaderOf(window, "Lena Huber");
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        await Pump();
        Assert.True(calendar.IsFiltered);

        var clear = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Command == calendar.ClearFilterCommand);
        var clearAt = clear.TranslatePoint(new Point(clear.Bounds.Width / 2, clear.Bounds.Height / 2), window)!.Value;
        window.MouseDown(clearAt, MouseButton.Left);
        window.MouseUp(clearAt, MouseButton.Left);
        await Pump();
        await Pump();
        Assert.False(calendar.IsFiltered);
        Assert.Equal(6, calendar.Columns.Count);

        // Noch einmal und diesmal mit Esc.
        at = HeaderOf(window, "Marco Berger");
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        await Pump();
        Assert.Equal("Marco Berger · 7 Tage", calendar.FilterText);
        Press(window, PhysicalKey.Escape);
        await Pump();
        await Pump();
        Assert.False(calendar.IsFiltered);
        Assert.Equal(6, calendar.Columns.Count);
        return calendar;
    }, WeekApi());

    [Fact]
    public Task Kalender_Raumansicht_Klick_auf_einen_Raum_zeigt_dessen_Termine_ueber_sieben_Tage() => Render("hell", "kalender-woche-raum", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        await Pump();
        var at = HeaderOf(window, "Raum Lotus");
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        await Pump();
        Assert.Equal("Raum Lotus · 7 Tage", calendar.FilterText);
        Assert.Equal(7, calendar.Columns.Count);
        return calendar;
    }, WeekApi());

    [Fact]
    public Task Kalender_Raum_Info_im_Panel() => Render("hell", "kalender-raum-info", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();

        var room = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && ToolTip.GetTip(b) as string == "Raum-Infos");
        var at = room.TranslatePoint(new Point(room.Bounds.Width / 2, room.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        var flyout = Avalonia.Controls.Primitives.FlyoutBase.GetAttachedFlyout(room)!;
        Assert.True(flyout.IsOpen);
        Assert.Equal("Raum Lotus", calendar.Selected!.RoomInfo!.Name);
        Assert.NotEmpty(calendar.Selected.RoomInfo.DayRows);
        Assert.True(calendar.Selected.RoomInfo.CanEdit);

        // Die Karte steht links neben der Seitenleiste und verdeckt weder ihren Kopf noch die Karten darin (Auftraggeber, 27.10.2026).
        var panel = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "SidePanel");
        var content = ((Flyout)flyout).Content as Control ?? throw new InvalidOperationException("Flyout ohne Inhalt");
        Assert.True(content.Bounds.Width >= 380, $"Karte ist {content.Bounds.Width} breit");
        var panelLeft = panel.TranslatePoint(default, window)!.Value.X;
        var cardRight = content.TranslatePoint(new Point(content.Bounds.Width, 0), window)!.Value.X;
        Assert.True(cardRight <= panelLeft + 1, $"Karte endet bei {cardRight}, Seitenleiste beginnt bei {panelLeft}");

        // Der Screenreader nennt Raum und Art, jeder Text hat mindestens die Größe des Fließtexts (14), nur die Pille der Raumart ist 12.
        Assert.Equal("Raum Lotus, " + calendar.Selected.RoomInfo.Kind, Avalonia.Automation.AutomationProperties.GetName(content));
        Assert.All(content.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)
            && !t.GetVisualAncestors().OfType<Border>().Any(b => b.Classes.Contains("pill"))), t => Assert.True(t.FontSize >= 14, $"{t.Text}: {t.FontSize}"));
        return calendar;
    });

    [Fact]
    public Task Kalender_Symbol_bleibt_beim_Kuerzen_des_Namens_stehen() => Render("hell", "kalender-langer-name", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var card = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("appt") && b.DataContext is CalendarItem { Appointment.Id: var id } && id == SpaSample.Selected.Id);
        var icon = card.GetVisualDescendants().OfType<Icon>().First(i => i.Data == Icons.Female && i.IsEffectivelyVisible);
        var name = card.GetVisualDescendants().OfType<TextBlock>()
            .First(t => t.IsEffectivelyVisible && t.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Any(r => r.Text == LongName) == true);
        // Der Name ist zu lang für die Spalte und wird gekürzt; das Symbol steht in seiner eigenen Spalte vollständig daneben.
        Assert.True(name.Bounds.Width < name.DesiredSize.Width || name.TextLayout.TextLines.Count > 0);
        var iconLeft = icon.TranslatePoint(new Point(0, 0), card)!.Value.X;
        Assert.True(name.TranslatePoint(new Point(name.Bounds.Width, 0), card)!.Value.X <= iconLeft + 0.5, "Name überlappt das Symbol");
        Assert.True(iconLeft + icon.Bounds.Width <= card.Bounds.Width, "Symbol ragt aus der Karte");
        return calendar;
    }, Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", LongNameDay()));

    private const string LongName = "Brigitte Müller-Hohenwarter-Schönbrunner";

    private static CalendarDay LongNameDay()
    {
        var day = SpaSample.CalendarDay();
        return day with { Appointments = [.. day.Appointments.Select(a => a.Id == SpaSample.Selected.Id ? a with { CustomerName = LongName } : a)] };
    }

    [Fact]
    public Task Kalender_Rechtsklick_auf_den_Streifen_bietet_Vor_und_Nachbereitung_bearbeiten() => Render("hell", "kalender-streifen-menue", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var stripe = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("stripe") && b.DataContext is CalendarStripe { Appointment.Id: var id } && id == SpaSample.Selected.Id && b.IsEffectivelyVisible);
        var at = stripe.TranslatePoint(new Point(stripe.Bounds.Width / 2, stripe.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Right);
        window.MouseUp(at, MouseButton.Right);
        await Pump();

        // Es öffnet sich das Menü des Streifens, keine neue Buchung.
        Assert.Null(calendar.NewEntry);
        var items = window.GetVisualDescendants().OfType<MenuItem>().Select(m => m.Header as string ?? (m.Header as Grid)?.Children.OfType<TextBlock>().FirstOrDefault()?.Text).ToList();
        Assert.Contains("Behandlungszeiten bearbeiten …", items);
        AssertMenuTextsFit(window.GetVisualDescendants().OfType<ContextMenu>().Single());
        return calendar;
    });

    [Fact]
    public Task Kalender_Raumansicht_mit_Paar() => Render("hell", "kalender-raeume", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        calendar.Mark(calendar.Columns.SelectMany(c => c.Items).First(i => i.Title == "Nina Weiss"));
        Assert.Contains(calendar.Columns.SelectMany(c => c.Items), i => i.Title == "Jakob Weiss" && i.IsGroupHighlighted);
        Assert.Equal([false, true], calendar.Columns.Take(2).Select(c => c.IsCouple));
        return calendar;
    }, Api().On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.ResourcesWithCoupleRoom));

    [Fact]
    public Task Kalender_mit_Pausen_Abwesenheiten_und_Notizen() => Render("hell", "kalender-tagesleiste", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        Assert.Equal(2, calendar.DayNotes.Count);
        Assert.Contains(calendar.Columns, c => c.IsSick && c.Affected == 3);
        Assert.Contains(calendar.Columns.SelectMany(c => c.Items), i => i.IsLegalBreak);
        Assert.Single(calendar.Columns.SelectMany(c => c.Notes));
        calendar.OpenNoteAt(DayLayout.ColumnLeft(2, calendar.Scale) + 20, calendar.Hours.Single(h => h.Label == "16:00").Top + 4);
        calendar.Entry!.Text = "Gutschein für Frau Berger an der Rezeption hinterlegt";
        return calendar;
    }, Api()
        .On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.ResourcesWithAbsent)
        .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.BusyDay()));

    [Fact]
    public Task Ausfall_Assistent_fuer_krank_gemeldete_Person() => Render("hell", "kalender-ausfall", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAbsence(calendar.Columns.Single(c => c.IsSick));
        await Pump();
        Assert.Equal(3, calendar.Absence!.Rows.Count);
        return calendar;
    }, Api()
        .On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.ResourcesWithAbsent)
        .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.BusyDay())
        .On($"POST /api/locations/{SpaSample.LocationId}/absence/proposal", SpaSample.AbsenceProposal()));

    [Fact]
    public Task Ausfall_Assistent_mit_Ersatz_an_anderem_Standort() => Render("hell", "kalender-ausfall-standort", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAbsence(calendar.Columns.Single(c => c.IsSick));
        await Pump();
        var assistant = calendar.Absence!;
        Assert.Equal(2, assistant.Rows.Count);
        var other = Assert.Single(assistant.OtherRows);
        Assert.False(other.IsChosen);
        Assert.Equal("in Baden zur gleichen Zeit bei Malee Chaiyasit, Thai-Raum", other.After);

        // Der Abschnitt „Andere Standorte“ steht mit Überschrift und Hinweis im Fenster.
        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Andere Standorte", texts);
        Assert.Contains(texts, t => t is not null && t.StartsWith("Neu: in Baden", StringComparison.Ordinal));
        return calendar;
    }, Api()
        .On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.ResourcesWithAbsent)
        .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.BusyDay())
        .On($"POST /api/locations/{SpaSample.LocationId}/absence/proposal", SpaSample.AbsenceProposalWithOtherLocation()));

    [Fact]
    public Task Kalender_Raumansicht_mit_Vorbereitung_und_Raumpflege() => Render("hell", "kalender-raumpflege", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        Assert.Contains(calendar.Columns.SelectMany(c => c.Stripes), s => s.LongText is not null && s.LongText.EndsWith("· Steine auf 55 °C aufheizen", StringComparison.Ordinal));
        Assert.Contains(calendar.Columns.SelectMany(c => c.Stripes), s => s.Text == "Nachb. 10 · Raumpflege 10 Min.");
        return calendar;
    }, Api()
        .On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.ResourcesWithAbsent)
        .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.BusyDay()));

    /// <summary>Beispieltag mit Betrieb im Haus (ADR 0065): Raumpflege, Tagesstart, Bereitstellen, Handbuch-Hinweise, Lagerwarnung.</summary>
    private static StubApi OperationsApi() => Api()
        .On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.ResourcesWithAbsent)
        .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.BusyDayWithCare())
        .On($"GET /api/locations/{SpaSample.LocationId}/room-start?date=2026-10-19", SpaSample.RoomStart())
        .On($"GET /api/locations/{SpaSample.LocationId}/servings?date=2026-10-19", SpaSample.Servings())
        .On("GET /api/handbook/reminders?date=2026-10-19", SpaSample.Reminders())
        .On($"GET /api/stock/locations/{SpaSample.LocationId}/demand?from=2026-10-19&until=2026-10-19", SpaSample.StockDemand());

    [Fact]
    public Task Kalender_ohne_offene_Seitenleiste_zeigt_nur_den_Zaehler_der_Meldungen() => Render("hell", "kalender-betrieb-zu", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        Assert.False(calendar.HasPanel);
        Assert.True(calendar.MessageCount > 0);
        return calendar;
    }, OperationsApi());

    [Fact]
    public Task Kalender_Termin_mit_Bereitstellen_und_Hinweis_nach_dem_Einchecken() => Render("hell", "kalender-bereitstellen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();
        Assert.True(calendar.Selected!.HasServings);
        Assert.Equal(["Kräutertee", "Früchteteller"], calendar.Selected.Servings!.Rows.Select(r => r.Text));
        return calendar;
    }, OperationsApi());

    [Fact]
    public Task Kalender_Klick_auf_den_Raumpflege_Streifen_hakt_ab()
    {
        var api = OperationsApi();
        foreach (var task in SpaSample.BusyDayWithCare().RoomCare!.Tasks)
        {
            api.On($"PUT /api/appointments/{task.AppointmentId}/room-care", null, System.Net.HttpStatusCode.NoContent);
        }

        return Render("hell", "kalender-raumpflege-abhaken", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            calendar.ByRoom = true;
            await Pump();
            var stripe = window.GetVisualDescendants().OfType<Border>()
                .First(b => b.Classes.Contains("stripe") && b.IsEffectivelyVisible && b.DataContext is CalendarStripe { Care.State: RoomCareState.Overdue });
            var at = stripe.TranslatePoint(new Point(stripe.Bounds.Width / 2, stripe.Bounds.Height / 2), window)!.Value;
            var care = ((CalendarStripe)stripe.DataContext!).Care!;

            // Kein Kästchen im Streifen: nur Text und Zustandsfarbe (Ä13).
            Assert.Empty(stripe.GetVisualDescendants().OfType<Border>());
            Assert.Empty(stripe.GetVisualDescendants().OfType<Icon>());

            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            await Pump();

            Assert.Contains(api.Calls, c => c.Route == $"PUT /api/appointments/{care.AppointmentId}/room-care" && c.Body!.Contains("\"done\":true", StringComparison.Ordinal));
            // Ein Klick beginnt keine Buchung an dieser Stelle.
            Assert.Null(calendar.NewEntry);
            return calendar;
        }, api);
    }

    /// <summary>Beispieltag mit Raumcheck (ADR 0068): der überfällige Raumpflege-Streifen trägt zwei offene Zeilen des Raumschranks.</summary>
    private static CalendarDay BusyDayWithRoomCheck()
    {
        var day = SpaSample.BusyDayWithCare();
        RestockLine[] lines =
        [
            new(Guid.NewGuid(), "Massageöl neutral", "ml", 200, 500, 300, RestockState.Open),
            new(Guid.NewGuid(), "Handtuch", "Stk", 4, 6, 2, RestockState.Open),
        ];
        var overdue = day.RoomCare!.Tasks.First(t => t.State == RoomCareState.Overdue);
        return day with { RoomCare = day.RoomCare with { Tasks = [.. day.RoomCare.Tasks.Select(t => t == overdue ? t with { Restock = lines } : t)], RoomsBelowMinimum = 2 } };
    }

    [Fact]
    public Task Kalender_Klick_auf_den_Streifen_mit_Zeilen_oeffnet_den_Raumcheck_in_der_Seitenleiste()
    {
        var api = OperationsApi().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", BusyDayWithRoomCheck());
        return Render("hell", "kalender-raumcheck", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            calendar.ByRoom = true;
            await Pump();
            var stripe = window.GetVisualDescendants().OfType<Border>()
                .First(b => b.Classes.Contains("stripe") && b.IsEffectivelyVisible && b.DataContext is CalendarStripe { Care.State: RoomCareState.Overdue });
            var at = stripe.TranslatePoint(new Point(stripe.Bounds.Width / 2, stripe.Bounds.Height / 2), window)!.Value;

            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            await Pump();

            Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
            Assert.NotNull(calendar.RoomCarePanel);
            var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).ToList();
            Assert.Equal(2, buttons.Count(b => b.Content as string == "Aufgefüllt"));
            Assert.Equal(2, buttons.Count(b => b.Content as string == "Reicht noch"));
            Assert.False(buttons.Single(b => b.Content as string == "Raumpflege erledigt").IsEffectivelyEnabled);
            return calendar;
        }, api);
    }

    [Fact]
    public Task Seitenleisten_folgen_denselben_Kurzbefehlen_und_Fokusregeln() => Render("hell", "leisten-kuerzel", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        await Pump();
        Control? Focused() => TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement() as Control;

        // Strg+Leertaste öffnet KI und Hilfe, ohne Bewegung, der Fokus geht ins Fragefeld.
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        await Pump();
        Assert.True(shell.Companion.IsOpen);
        Assert.True(shell.Companion.Presence.UsingKeyboard);
        Assert.Equal("CompanionQuestion", Focused()?.Name);

        // Esc schließt die oberste Leiste; der Fokus geht zum Knopf unten rechts zurück.
        Press(window, PhysicalKey.Escape);
        await Pump();
        Assert.False(shell.Companion.IsOpen);
        var fab = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CompanionFab");
        Assert.True(fab.IsEffectivelyVisible);
        Assert.Equal("CompanionFab", Focused()?.Name);

        // Strg+N im Kalender öffnet „Freie Zeit suchen“ ohne Bewegung; der Fokus geht in die Behandlung.
        Press(window, PhysicalKey.Escape);
        calendar.HandleKey(Key.Escape, KeyModifiers.None);
        window.GetVisualDescendants().OfType<CalendarView>().Single().Focus();
        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.N, RawInputModifiers.Control);
        await Pump();
        Assert.NotNull(calendar.FreeTime);
        Assert.True(calendar.Presence.UsingKeyboard);
        Assert.Equal("FreeTimeTreatment", Focused()?.Name);

        // Ein Mausklick (hier auf „Heute“) schaltet die Bewegung wieder ein.
        var today = window.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Heute" && b.IsEffectivelyVisible);
        var at = today.TranslatePoint(new Point(today.Bounds.Width / 2, today.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        Assert.False(calendar.Presence.UsingKeyboard);

        // Mit offener Leiste im Kalender wählt Strg+Leertaste den Reiter „KI und Hilfe“; die Freie Zeit bleibt als Reiter stehen (Ä66) und kommt mit Esc zurück.
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        await Pump();
        Assert.True(shell.Companion.IsOpen);
        Assert.True(calendar.Presence.UsingKeyboard);
        Assert.Same(shell.Companion, calendar.ActivePanel);
        Assert.Contains(calendar.OpenPanels, t => t.Kind == PanelKind.FreeTime);
        Assert.Equal("CompanionQuestion", Focused()?.Name);
        Press(window, PhysicalKey.Escape);
        await Pump();
        Assert.False(shell.Companion.IsOpen);
        Assert.NotNull(calendar.FreeTime);
        Assert.Single(calendar.OpenPanels);
        return calendar;
    });

    [Fact]
    public Task Freie_Zeit_suchen() => Render("hell", "kalender-freie-zeit", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        Assert.True(calendar.HandleKey(Key.N, KeyModifiers.Control));
        calendar.FreeTime!.Treatment = calendar.FreeTime.Treatments.First(t => t.Id == SpaSample.Treatments[1].Id);
        await Pump();
        calendar.FreeTime.Move(1);
        Assert.Equal(8, calendar.FreeTime.Slots.Count);
        return calendar;
    });

    [Fact]
    public Task Freie_Zeit_Doppelklick_auf_einen_Vorschlag_waehlt_nur_aus() => Render("hell", "kalender-freie-zeit-doppelklick", async (window, main) =>
    {
        // Auftraggeber, 28.10.2026: das Durchklicken der Vorschläge buchte; gebucht wird nur über den Knopf (oder Enter).
        var calendar = await SignedInCalendar(main);
        Assert.True(calendar.HandleKey(Key.N, KeyModifiers.Control));
        calendar.FreeTime!.Treatment = calendar.FreeTime.Treatments.First(t => t.Id == SpaSample.Treatments[1].Id);
        await Pump();
        var row = window.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.DataContext is FreeSlotRow).ElementAt(1);
        var at = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();

        Assert.Equal(1, calendar.FreeTime!.ActiveIndex);
        Assert.Null(calendar.NewEntry);
        return calendar;
    });

    [Fact]
    public Task Freie_Zeit_in_einer_Sprache() => Render("hell", "kalender-sprache-quelle", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        Assert.True(calendar.HandleKey(Key.N, KeyModifiers.Control));
        var freeTime = calendar.FreeTime!;
        freeTime.Treatment = freeTime.Treatments.First(t => t.Id == SpaSample.Treatments[1].Id);
        freeTime.Language = freeTime.Languages.Single(l => l.Code == "it");
        await Pump();
        Assert.Equal("spricht Italienisch", freeTime.Slots[0].SpeaksLanguage);
        Assert.Contains(calendar.Columns.SelectMany(c => c.Items), i => i.Flags.Any(f => f.Symbol == "IT") && i.MetaText.Contains("Online", StringComparison.Ordinal));
        return calendar;
    });

    [Fact]
    public Task KI_Anfrage_im_Kalender() => Render("hell", "kalender-ki-anfrage", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        Assert.True(calendar.HandleKey(Key.K, KeyModifiers.Control));
        calendar.Request!.Text = "heute ab 17 Uhr eine kräftige Aromaöl-Massage, nicht bei Marco";
        await calendar.Request.SubmitAsync();
        await Pump();
        Assert.Equal(3, calendar.SlotMarks.Count);
        return calendar;
    }, Api()
        .On($"POST /api/locations/{SpaSample.LocationId}/calendar-request", SpaSample.CalendarRequest()));

    [Fact]
    public Task Kalender_Tastenhilfe() => Render("hell", "kalender-tastenhilfe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ShowKeyHelp = true;
        return calendar;
    });

    [Fact]
    public Task Kalender_Stornieren_mit_Rueckfrage() => Render("hell", "kalender-stornieren", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.Mark(calendar.Columns.SelectMany(c => c.Items).First(i => i.Appointment?.Id == SpaSample.Selected.Id));
        Assert.True(calendar.HandleKey(Key.Delete, KeyModifiers.None));
        await Pump();
        // Der Rückfragedialog steht über dem Fenster (Titel als Frage, Folge zuerst, Feld für den Grund).
        var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
        Assert.Equal("Termin stornieren?", dialog.Title);
        Assert.True(dialog.HasInput);
        return calendar;
    });

    [Fact]
    public Task Neuer_Termin() => Render("hell", "kalender-buchen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBooking(calendar.Columns[1], 330 * DayLayout.PixelsPerMinute);
        calendar.Booking!.CustomerQuery = "mül";
        await Pump();
        calendar.Booking.Customer = calendar.Booking.Customers.FirstOrDefault();
        calendar.Booking.Treatment = calendar.Booking.Treatments.First(t => t.CheckAnamnesis);
        await calendar.Booking.LoadAnamnesisCheckAsync();
        Assert.True(calendar.Booking.CheckAnamnesis);
        return calendar;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Paar_und_Spa_Tag(string theme) => Render(theme, "kalender-paar", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenGroupBookingCommand.Execute(null);
        var group = calendar.GroupBooking!;
        group.Steps[0].Entries[0].Choice = group.Steps[0].Entries[0].Choices[2];
        group.Steps[0].Entries[1].Choice = group.Steps[0].Entries[1].Choices[2];
        group.AddStepCommand.Execute(null);
        group.Steps[1].Entries[0].Choice = group.Steps[1].Entries[0].Choices[6];
        // Baukasten: der zweite Gast geht in der Zeit in den Ruheraum.
        group.Steps[1].Entries[1].Choice = group.Steps[1].Entries[1].Choices.First(c => c.AreaId == SpaSample.Relax.Id && c.Minutes == 60);
        group.Gap = 10;
        await group.SearchSlotsAsync();
        group.Guests[0].Search.Query = "br";
        group.Guests[1].Search.Query = "be";
        await Pump();
        group.Guests[0].Search.Customer = group.Guests[0].Search.Results[0];
        group.Guests[1].Search.Customer = group.Guests[1].Search.Results[0];
        group.Slot = group.Slots[1];
        Assert.True(group.BookCommand.CanExecute(null));
        Assert.Contains(group.Plan, line => line.EndsWith(": Ruheraum", StringComparison.Ordinal));
        return calendar;
    });

    [Fact]
    public Task Gruppe_mit_Gaesten_ohne_Namen() => Render("hell", "kalender-gruppe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenGroupBookingCommand.Execute(null);
        var group = calendar.GroupBooking!;
        group.AddGuestCommand.Execute(null);
        group.AddGuestCommand.Execute(null);
        foreach (var entry in group.Steps[0].Entries)
        {
            entry.Choice = entry.Choices[2];
        }

        group.Guests[0].Search.Query = "br";
        await Pump();
        group.Guests[0].Search.Customer = group.Guests[0].Search.Results[0];
        foreach (var guest in group.Guests.Skip(1))
        {
            guest.NameLater = true;
        }

        await group.SearchSlotsAsync();
        Assert.NotNull(group.NameLaterNote);
        return calendar;
    });

    [Fact]
    public Task Gemeinsamer_Termin() => Render("hell", "kalender-gemeinsam", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var couple = calendar.Columns.SelectMany(c => c.Items).First(i => i.Appointment?.GroupId == SpaSample.CoupleId).Appointment!;
        calendar.OpenAppointment(couple);
        Assert.True(calendar.Selected!.CanMoveGroup);
        return calendar;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Sauna_und_Ruheraum(string theme) => Render(theme, "sauna", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAreasCommand.Execute(null);
        await Pump();
        var areas = calendar.Areas!;
        Assert.Equal(2, areas.Areas.Count);
        areas.Search.Query = "br";
        await Pump();
        areas.Search.Customer = areas.Search.Results[0];
        return calendar;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Posteingang(string theme) => Render(theme, "posteingang", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Inbox);
        await Pump();
        Assert.Equal("2", shell.Pages.First(p => p.Page == shell.Inbox).Badge);
        shell.Inbox.SelectedRow = shell.Inbox.Mails[0];
        await Pump();
        Assert.NotNull(shell.Inbox.Detail);
        // Offene Terminanfrage: rechts steht die freie Zeit mit „Vormerken und vorschlagen“ (ADR 0083).
        for (var i = 0; i < 20 && shell.Inbox.Detail!.FreeTime is not { HasSlots: true }; i++)
        {
            await Pump(Settling);
        }

        Assert.True(shell.Inbox.Detail!.FreeTime is { HasSlots: true });
        return shell.Inbox;
    }, Api()
        .On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMail() with { CanRepropose = true })
        .On($"GET /api/locations/{SpaSample.LocationId}/availability?treatmentId={SpaSample.Treatments[2].Id}&date=2026-10-22&minSlots=3&days=14",
            SpaSample.InboxMail().Proposals.ToList()));

    [Fact]
    public Task Posteingang_uebersetzt_mit_KI_Ueberarbeitung() => Render("hell", "posteingang-uebersetzt", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Inbox);
        await Pump();
        shell.Inbox.SelectedRow = shell.Inbox.Mails[0];
        await Pump();
        Assert.True(shell.Inbox.Detail!.HasTranslation);
        Assert.False(shell.Inbox.Detail.ShowsTranslation);
        shell.Inbox.Detail.ToggleOriginal();
        Assert.True(shell.Inbox.Detail.ShowsTranslation);
        Assert.True(shell.Inbox.Detail.CanRevise);

        // Bis zur Antwort scrollen: Kontrollübersetzung und Knöpfe zum Überarbeiten.
        var detail = window.GetVisualDescendants().OfType<ScrollViewer>().Where(s => s.IsEffectivelyVisible).MaxBy(s => s.Extent.Height)!;
        detail.Offset = new Vector(0, 560);
        return shell.Inbox;
    }, Api()
        .On("GET /api/inbox", new List<InboundMailSummary>
        {
            SpaSample.Inbox()[0] with { From = "giulia.romano@example.it", FromName = "Giulia Romano", Subject = "Massaggio giovedì?" },
        })
        .On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMailItalian()));

    [Fact]
    public Task Posteingang_verknuepfte_Antwort_des_Gasts() => Render("hell", "posteingang-neu-vorschlagen", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Inbox);
        await Pump();
        shell.Inbox.SelectedRow = shell.Inbox.Mails[0];
        await Pump();
        var detail = shell.Inbox.Detail!;
        await Pump();
        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Where(s => s.IsEffectivelyVisible).MaxBy(s => s.Extent.Height)!;
        scroll.Offset = new Vector(0, 120);
        return shell.Inbox;
    }, Api()
        .On("GET /api/inbox", new List<InboundMailSummary>
        {
            SpaSample.Inbox()[0] with { Subject = "Re: Hot-Stone am Donnerstag?", Summary = "Hot-Stone-Massage Fr. 23.10. am Nachmittag" },
        })
        .On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMailFollowUp()));

    [Fact]
    public Task Posteingang_wer_ist_das() => Render("hell", "posteingang-wer-ist-das", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Inbox);
        await Pump();
        shell.Inbox.SelectedRow = shell.Inbox.Mails[0];
        await Pump();
        var detail = shell.Inbox.Detail!;
        Assert.True(detail.HasCandidates);
        detail.RememberContact = true;
        await Pump();
        return shell.Inbox;
    }, Api()
        .On("GET /api/inbox", new List<InboundMailSummary>
        {
            SpaSample.Inbox()[0] with { Status = InboundMailStatus.NeedsReview },
        })
        .On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMailWhoIsThis()));

    [Fact]
    public Task Posteingang_treatwell_buchung() => Render("hell", "posteingang-treatwell", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Inbox);
        await Pump();
        shell.Inbox.SelectedRow = shell.Inbox.Mails[0];
        await Pump();
        var detail = shell.Inbox.Detail!;
        Assert.True(detail.ShowAcceptPending);
        Assert.True(detail.HasCandidates);
        await Pump();
        return shell.Inbox;
    }, Api()
        .On("GET /api/inbox", new List<InboundMailSummary>
        {
            SpaSample.Inbox()[0] with
            {
                Channel = BookingChannel.Treatwell,
                From = "bookings@partner.treatwell.com",
                FromName = null,
                Subject = "Neue Buchung über Treatwell",
                Status = InboundMailStatus.Booked,
                Kind = InboundMailKind.TreatwellBooking,
                Summary = "Treatwell TW-9823711: Hot-Stone-Massage am 22.10.2026 17:00, Claudia Haider",
            },
        })
        .On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMailTreatwell()));

    [Fact]
    public Task Kalender_termin_aus_treatwell_vorab_bezahlt() => Render("hell", "kalender-treatwell-termin", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var appointment = SpaSample.TreatwellAppointment();
        calendar.OpenAppointment(appointment);
        var panel = calendar.Selected!;
        Assert.Equal("Vorab über Treatwell bezahlt: 35,00 €", panel.PaymentText);
        Assert.True(panel.ShowAcceptPending);
        await Pump();
        return calendar;
    }, Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.CalendarDay() with
    {
        Appointments = [.. SpaSample.CalendarDay().Appointments.Select(a => a.Id == SpaSample.Selected.Id ? SpaSample.TreatwellAppointment() : a)],
    }));

    [Fact]
    public Task Kalender_Termin_wiederholen() => Render("hell", "kalender-serie", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        var panel = calendar.Selected!;
        panel.EditSeriesCommand.Execute(null);
        panel.SeriesCount = "5";
        await panel.PreviewSeriesCommand.ExecuteAsync(null);
        Assert.Equal(5, panel.SeriesRows.Count);
        await Pump();
        foreach (var scroller in window.GetVisualDescendants().OfType<ScrollViewer>().Where(v => v.Content is StackPanel))
        {
            scroller.Offset = new Avalonia.Vector(0, 330);
        }

        await Pump();
        return calendar;
    });

    [Fact]
    public Task Kalender_hebt_noch_erwartete_hervor() => Render("hell", "kalender-noch-erwartet", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.FocusOnCommand.Execute(CalendarFocus.Expected);
        Assert.True(calendar.HasFocus);
        return calendar;
    });

    [Fact]
    public Task Warteliste() => Render("hell", "kalender-warteliste", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        Assert.Equal(4, calendar.Waitlist.Count);
        calendar.OpenWaitlistCommand.Execute(null);
        return calendar;
    });

    [Fact]
    public Task Hilfe_beim_Abschnitt() => Render("hell", "hilfe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        Assert.True(shell.ShowHelp("kassa", "barzahlungsrabatt"));
        await Pump();
        Assert.Same(shell.Help, shell.SelectedPage!.Page);
        Assert.Contains("abschnitt=barzahlungsrabatt", shell.Help.Address?.Query);
        return calendar;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task KI_und_Hilfe_rechts(string theme) => Render(theme, "ki-und-hilfe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        Assert.False(shell.Companion.IsOpen);

        // Strg+Leertaste gilt auch, wenn der Kalender den Fokus hat.
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.Control);
        Assert.True(shell.Companion.IsOpen);
        await shell.Companion.AskExampleCommand.ExecuteAsync("Wie kassiere ich einen Termin?");
        Assert.Equal("seite-kassa", shell.Companion.FirstHelp?.Topic);
        return calendar;
    }, Api().On("POST /api/assistant", new AssistantAnswer(AssistantAudience.Reception,
        "So geht es in der Kassa:", [], [],
        "Antwort einer KI. Zahlen und Zeiten kommen aus Kalender und Kassa.",
        [new AssistantHelpLink("seite-kassa", "kassieren", "Kassieren", "Termin wählen und kassieren."),
            new AssistantHelpLink("seite-kassa", "abschluss", "Kassenlade und Abschluss", "Am Abend abschließen.")])));

    [Fact]
    public Task KI_und_Hilfe_bucht_einen_Vorschlag() => Render("hell", "ki-und-hilfe-buchen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.Companion.ToggleCommand.Execute(null);
        await shell.Companion.AskExampleCommand.ExecuteAsync("Wann ist morgen Nachmittag eine Massage frei?");
        Assert.Equal(["Buchen …", "Kunde öffnen"], shell.Companion.Suggestions.Select(x => x.Action));
        await shell.Companion.UseSuggestionCommand.ExecuteAsync(shell.Companion.Suggestions[0]);
        await shell.Companion.Guest!.SearchAsync("br");
        shell.Companion.Guest.Customer = shell.Companion.Guest.Results[0];
        Assert.True(shell.Companion.BookCommand.CanExecute(null));
        await Pump();

        // Die Frage steht rechts neben der KI-Pille und bricht um, statt über den Kartenrand zu laufen.
        // Der Fragetext steht auch unter „Zuletzt gefragt“: gemeint ist die Frage im Kopf der Antwortkarte.
        var card = Named<Border>(window, "AnswerCard");
        var asked = card.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == shell.Companion.Asked && t.IsEffectivelyVisible);
        var askedRight = asked.TranslatePoint(new Point(asked.Bounds.Width, 0), window)!.Value.X;
        var cardRight = card.TranslatePoint(new Point(card.Bounds.Width, 0), window)!.Value.X;
        Assert.True(askedRight <= cardRight - card.Padding.Right, $"Frage endet bei {askedRight}, Karte bei {cardRight} (Innenabstand {card.Padding.Right})");
        Assert.True(asked.Bounds.Height > 16, $"Frage bricht um (Höhe {asked.Bounds.Height})");
        return calendar;
    }, Api().On("POST /api/assistant", new AssistantAnswer(AssistantAudience.Reception,
        "Morgen Nachmittag ist eine Hot-Stone-Massage frei:", [],
        [new AssistantSuggestion(AssistantSuggestionKind.BookSlot, "Di., 14:00 Hot-Stone-Massage bei Lena Huber", SpaSample.LocationId,
            SpaSample.Treatments[1].Id, SpaSample.Lena.Id, null, null, SpaSample.At(14).AddDays(1)),
         new AssistantSuggestion(AssistantSuggestionKind.OpenCustomer, "Maria Brunner (Stammgast)", null, null, null, SpaSample.Customers[0].Id, null, null)],
        "Antwort einer KI. Zahlen und Zeiten kommen aus Kalender und Kassa.", [])));

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kundenkarte(string theme) => Render(theme, "kunden", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        Assert.Same(shell.Customers, shell.SelectedPage!.Page);
        Assert.StartsWith("7 Besuche in 12 Monaten · zuletzt am 21.9.", shell.Customers.StatsLine, StringComparison.Ordinal);
        Assert.Equal("1× nicht erschienen · 1 späte Absage", shell.Customers.StatsWarning);
        return calendar;
    });

    [Fact]
    public Task Kundenkarte_Vorlieben_bearbeiten() => Render("hell", "kunden-vorlieben", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        await shell.Customers.TakeSuggestionCommand.ExecuteAsync(null);
        shell.Customers.BookingFixed = true;
        shell.Customers.BookingGender = GenderChoice.All[1];
        await Pump();
        Assert.True(shell.Customers.IsEditingBooking);
        return calendar;
    });

    [Fact]
    public Task Kundenkarte_Kennzahlen_und_Vermerk() => Render("hell", "kunden-kennzahlen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.EditReceptionNoteCommand.Execute(null);
        shell.Customers.ReceptionNoteDraft = "Zahlt meist mit der Firmenkarte. Allergie auf Nüsse.";
        await Pump();
        Assert.Equal("★★", shell.Customers.StarsGlyph);
        Assert.NotNull(shell.Customers.ReceptionNoteWarning);
        return calendar;
    });

    [Fact]
    public Task Kundenkarte_Kennzeichen() => Render("hell", "kunden-kennzeichen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.AddFlagCommand.Execute(null);
        await Pump();
        Assert.Equal(2, shell.Customers.Flags.Count);
        Assert.Equal(CustomerFlagKind.MoreTime, shell.Customers.NewFlag!.Kind);
        return calendar;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kassa_mit_offener_Rechnung(string theme) => Render(theme, "kassa", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await shell.Pos.AddArticleCommand.ExecuteAsync(SpaSample.Articles[0]);
        Assert.Equal(2, shell.Pos.Lines.Count);
        shell.Pos.EditDiscountCommand.Execute(shell.Pos.Lines[0]);
        shell.Pos.DiscountInput = "10";
        await shell.Pos.ApplyDiscountCommand.ExecuteAsync(null);
        Assert.Equal("20 % USt · Rabatt −10,00 €", shell.Pos.Lines[0].Tax);
        shell.Pos.Payments.First(p => p.IsVoucher).VoucherCode = "GS-7K3M-Q9";
        shell.Pos.Payments.First(p => p.IsVoucher).Amount = "50";
        shell.Pos.Payments.First(p => p.Method == PaymentMethod.Cash).Amount = "100";
        Assert.True(shell.Pos.CanPay);
        Assert.Equal("Rückgeld: 21,20 €", shell.Pos.ChangeText);
        return shell.Pos;
    });

    [Fact]
    public Task Kassa_Paket_abziehen_und_verkaufen() => Render("hell", "kassa-paket", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump();
        var hint = Assert.Single(shell.Pos.PackageHints);
        shell.Pos.UsePackageCommand.Execute(hint);
        Assert.True(shell.Pos.CanPay);
        shell.Pos.VoucherTreatment = shell.Pos.Treatments.First(t => t.Id == SpaSample.Paying.TreatmentId);
        shell.Pos.PackagePrice = "699";
        shell.Pos.PackageValidUntil = "31.12.2027";
        return shell.Pos;
    });

    [Fact]
    public Task Kassa_Beleg_nach_dem_Bezahlen() => Render("hell", "kassa-beleg", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        shell.Pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "89";
        await shell.Pos.PayCommand.ExecuteAsync(null);
        Assert.NotNull(shell.Pos.Receipt);
        Assert.Equal("Signiert nach RKSV", shell.Pos.Receipt.SignatureText);
        return shell.Pos;
    });

    [Fact]
    public Task Kassa_Eintritt_aus_dem_Bereich()
    {
        var stub = Api()
            .On("POST /api/pos/bills", SpaSample.VisitBill())
            .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList([.. SpaSample.UnbilledVisits().Skip(1)]));
        return Render("hell", "kassa-eintritt", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            calendar.OpenAreasCommand.Execute(null);
            await Pump();
            var row = calendar.Areas!.Areas.SelectMany(a => a.Visits).First(v => v.Name == "Gerhard Lang");
            Assert.True(row.CanCheckout);
            Assert.False(calendar.Areas.Areas.SelectMany(a => a.Visits).First(v => v.IsCancelled).CanCheckout);
            await row.CheckoutCommand.ExecuteAsync(null);
            Assert.Same(shell.Pos, shell.SelectedPage!.Page);
            Assert.Contains("\"areaVisitIds\":[\"", stub.Calls.Last(c => c.Route == "POST /api/pos/bills").Body!, StringComparison.Ordinal);
            var relax = shell.Pos.Unbilled.Single(u => u.IsVisit);
            Assert.Equal("Eintritt Ruheraum", relax.Treatment);
            Assert.Equal("kein Preis", relax.Price);
            Assert.Single(shell.Pos.Lines);
            return shell.Pos;
        }, stub);
    }

    [Fact]
    public Task Kassa_Rechnung_mit_Empfaenger()
    {
        var receipt = SpaSample.LargeReceipt();
        var stub = Api()
            .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", receipt)
            .On($"POST /api/pos/receipts/{receipt.Id}/recipient", receipt with { Recipient = SpaSample.Recipient with { VatIdCheckId = Guid.NewGuid() } });
        return Render("hell", "kassa-rechnung", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await shell.CheckoutAsync(SpaSample.Paying);
            shell.Pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "480";
            await shell.Pos.PayCommand.ExecuteAsync(null);
            Assert.Contains("Über 400,00 €", shell.Pos.Receipt!.InvoiceHint);
            await shell.Pos.StartInvoiceCommand.ExecuteAsync(shell.Pos.Receipt);
            shell.Pos.RecipientName = SpaSample.Recipient.Name;
            shell.Pos.RecipientAddress = SpaSample.Recipient.Address;
            shell.Pos.RecipientVatId = SpaSample.Recipient.VatId!;
            await shell.Pos.AddRecipientCommand.ExecuteAsync(null);
            Assert.Equal("Rechnung Nr. WIEN-1-1842", shell.Pos.Receipt!.Title);
            return shell.Pos;
        }, stub);
    }

    [Fact]
    public Task Anmeldung() => Render("hell", "anmeldung", async (window, main) =>
    {
        var api = new StubApi()
            .On("GET /api/system/status", new SystemStatus("0.9.0", "Development", DateTimeOffset.UtcNow))
            .On("GET /api/auth/dev/accounts", new List<DevAccount>
            {
                new("inhaberin@demo.aispa.test", "Katrin Aigner", StaffRole.Owner, ["alle Standorte"]),
                new("empfang.wien@demo.aispa.test", "Nina Kovač", StaffRole.Reception, ["Wien Innere Stadt"]),
            });
        var signedOut = new MainViewModel(api.Client());
        window.DataContext = signedOut;
        await signedOut.StartAsync();
        Assert.IsType<LoginViewModel>(signedOut.Content);
        return signedOut;
    });

    [Fact]
    public Task Anmeldung_am_Empfangsgeraet() => Render("hell", "anmeldung-empfang", async (window, main) =>
    {
        var api = new StubApi()
            .On("GET /api/system/status", new SystemStatus("0.9.0", "Production", DateTimeOffset.UtcNow))
            .On("GET /api/auth/quick-switch", new QuickSwitchState(QuickSwitchTests.Device, QuickSwitchTests.People));
        var signedOut = new MainViewModel(api.Client());
        window.DataContext = signedOut;
        await signedOut.StartAsync();
        var login = Assert.IsType<LoginViewModel>(signedOut.Content);
        login.ChoosePersonCommand.Execute(login.People[1]);
        login.Pin = "1234";
        return signedOut;
    });

    private static readonly PaymentTerminalDto Terminal = new(Guid.NewGuid(), SpaSample.LocationId, "Empfang links", TerminalProvider.Local, "192.168.1.50:20007", true);

    private static readonly Guid CardId = Guid.NewGuid();

    /// <summary>Alle 4 Wochen ab dem ausgewählten Termin: einmal andere Uhrzeit, einmal andere Person, einmal nichts frei.</summary>
    private static List<SeriesOccurrence> SeriesPreview()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        var first = DateOnly.FromDateTime(DayLayout.Local(SpaSample.Selected.Start, zone));
        var lena = SpaSample.Lena;
        DateOnly Day(int weeks) => first.AddDays(7 * weeks);
        DateTimeOffset At(int weeks, int hour = 10, int minute = 30) => DayLayout.LocalToUtc(Day(weeks), new TimeOnly(hour, minute), zone);
        return
        [
            new(Day(4), At(4), lena.Id, lena.DisplayName, SeriesOccurrenceState.Free, null),
            new(Day(8), At(8, 12), lena.Id, lena.DisplayName, SeriesOccurrenceState.OtherTime, "Lena Huber: belegt"),
            new(Day(12), At(12), SpaSample.Marco.Id, SpaSample.Marco.DisplayName, SeriesOccurrenceState.OtherEmployee, "Lena Huber: Urlaub"),
            new(Day(16), At(16), null, null, SeriesOccurrenceState.Conflict, "Standort geschlossen"),
            new(Day(20), At(20), lena.Id, lena.DisplayName, SeriesOccurrenceState.Free, null),
        ];
    }

    private static CardPaymentDto Card(CardPaymentStatus status) => new(CardId, SpaSample.BillId, Terminal.Id, 138.80m, "EUR", status, null,
        "Mastercard", "•••• 4711", "AB1234", "42", null, SpaSample.At(11, 40), null);

    /// <summary>Belege des Vormittags: der letzte wurde storniert.</summary>
    private static List<ReceiptDto> DayReceipts()
    {
        var sale = SpaSample.Receipt();
        var voided = sale with
        {
            Id = Guid.NewGuid(),
            Number = 1840,
            IssuedAt = SpaSample.At(10, 5),
            Total = 95m,
            Change = 0,
            VoidedByReceiptId = Guid.NewGuid(),
            Payments = [new(PaymentMethod.Card, 95m, null, "39")]
        };
        var storno = voided with
        {
            Id = voided.VoidedByReceiptId!.Value,
            Number = 1841,
            Kind = ReceiptKind.Void,
            IssuedAt = SpaSample.At(10, 12),
            Total = -95m,
            VoidsReceiptId = voided.Id,
            VoidedByReceiptId = null,
            VoidReason = "Falsche Behandlung gebucht",
            Payments = [new(PaymentMethod.Card, -95m, null, "40")]
        };
        var early = sale with
        {
            Id = Guid.NewGuid(),
            Number = 1839,
            IssuedAt = SpaSample.At(9, 50),
            Total = 69m,
            Change = 0,
            Payments = [new(PaymentMethod.Cash, 69m, null, null)]
        };
        return [sale, storno, voided, early];
    }

    private static DayClosingDto DayReport() => new(Guid.Empty, SpaSample.Register.Id, SpaSample.At(8, 55), SpaSample.At(11, 40), 4, 1, 207.80m,
        [new(PaymentMethod.Cash, 157.80m), new(PaymentMethod.Voucher, 50m)],
        [new(TaxCategory.Normal, 20, 207.80m, 173.17m, 34.63m)],
        [new(SpaSample.Revenue, "4000", "Behandlungen", null, 158m, 131.67m), new(Guid.NewGuid(), "4100", "Handelswaren", null, 49.80m, 41.50m)],
        150m, 50m, 0m, 357.80m, 0m, 0m, null);

    [Fact]
    public Task Kassa_mit_Kartenterminal() => Render("hell", "kassa-terminal", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await shell.Pos.AddArticleCommand.ExecuteAsync(SpaSample.Articles[0]);
        await Pump();
        var card = shell.Pos.Payments.First(p => p.IsCard);
        await shell.Pos.StartCardPaymentCommand.ExecuteAsync(card);
        Assert.True(card.IsApproved);
        Assert.Equal("Stornieren", card.CancelTerminalText);
        Assert.True(shell.Pos.CanPay);
        return shell.Pos;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kassa_Belege_und_Storno(string theme) => Render(theme, "kassa-belege", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        shell.Pos.ShowReceiptsCommand.Execute(null);
        await shell.Pos.LoadReceiptsAsync();
        Assert.Equal(4, shell.Pos.Receipts.Count);
        shell.Pos.VoidReasonInput = "Kundin hat doch mit Karte bezahlt";
        Assert.True(shell.Pos.VoidReceiptCommand.CanExecute(null));
        return shell.Pos;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kassa_Anzahlungsbeleg_ohne_Leitungsrolle_gesperrt(string theme) => Render(theme, "kassa-belege-anzahlung", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        shell.Pos.ShowReceiptsCommand.Execute(null);
        await shell.Pos.LoadReceiptsAsync();
        // Ein Anzahlungsbeleg, angemeldet als Standortleitung oder Rezeption: „Stornieren“ ist gesperrt und nennt den Grund als Tooltip (Ä51).
        shell.Pos.OwnerLevel = false;
        var first = shell.Pos.Receipts[0];
        shell.Pos.SelectedReceiptItem = first with { Receipt = first.Receipt with { IsDepositReceipt = true } };
        shell.Pos.VoidReasonInput = "Falsch kassiert";
        Assert.False(shell.Pos.VoidReceiptCommand.CanExecute(null));
        Assert.Equal("Anzahlungsbelege storniert nur die Leitung.", shell.Pos.VoidDisabledHint);
        return shell.Pos;
    });

    [Fact]
    public Task Kassa_Tagesabschluss() => Render("hell", "kassa-abschluss", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        shell.Pos.ShowClosingCommand.Execute(null);
        await shell.Pos.LoadReportAsync();
        shell.Pos.CountedCash = "356,30";
        Assert.Equal("Differenz −1,50 €", shell.Pos.DifferenceText);
        return shell.Pos;
    });

    [Fact]
    public Task Geraete() => Render("hell", "geraete", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Devices);
        await shell.Devices.InitializeAsync();
        Assert.Equal(2, shell.Devices.Terminals.Count);
        shell.Devices.PrinterAddress = "192.168.1.60";
        shell.Devices.Terminal = shell.Devices.Terminals[1];
        return shell.Devices;
    });

    /// <summary>Einzelplatz mit eingetragener Zentrale; Datenbank und API laufen hier nur als Test-API.</summary>
    private sealed class SampleLocal : ILocalServer
    {
        public NetworkShareInfo? Network { get; set; } = new NetworkShareInfo([new Uri("https://empfang-pc:55443/"), new Uri("https://192.168.1.20:55443/")], "3F9A-12C4-77B0-E1D2-9A0B");

        public Task<bool> SetSharedAsync(bool share, CancellationToken ct = default) => Task.FromResult(false);

        public Uri ApiAddress { get; } = new("http://localhost/");

        public string? CentralDescription => "db.spa-wien.at · aispadb";

        public Task StartAsync(IProgress<string>? progress = null, CancellationToken ct = default) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task RestoreAsync(string dumpFile, string recoveryCode, IProgress<string>? progress = null, CancellationToken ct = default) =>
            Task.CompletedTask;

        public bool HasRecoveryKey { get; private set; }

        public string CreateRecoveryKey()
        {
            HasRecoveryKey = true;
            return "K7QM-2XRT-H9PW-4CNE-YB3D-ZF6A";
        }

        public Task<string> BackupAsync(string folder, CancellationToken ct = default) =>
            Task.FromResult(Path.Combine(folder, "aispa-20261019-114000.dump"));

        public Task<bool> SetCentralAsync(string? connectionString, CancellationToken ct = default) => Task.FromResult(true);

        public string? ShareCode => null;

        public string SetShareCode(string? code) => code ?? "";
    }

    [Fact]
    public Task Abgleich_mit_abgelehntem_Termin()
    {
        var stub = Api().On("GET /api/system/sync", new SyncStatus(true, SpaSample.At(11, 39), null, 2,
        [
            new SyncConflict(1, "Termine", "Appointments", "{}", "Zentral abgelehnt: Raum, Person oder Gerät ist zur selben Zeit schon gebucht.",
                SpaSample.At(10, 12)),
        ]));
        return Render("hell", "abgleich", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Sync);
            await shell.Sync!.RefreshAsync();
            Assert.True(shell.Sync.HasConflicts);
            Assert.Equal("1", shell.SelectedPage.Badge);
            return shell.Sync;
        }, stub, local: new SampleLocal());
    }

    [Fact]
    public Task Sicherung_mit_neuem_Wiederherstellungsschluessel() => Render("hell", "sicherung", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        Assert.True(shell.Backup!.HasReminder);
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Backup);
        shell.Backup.NewRecoveryKeyCommand.Execute(null);
        Assert.True(shell.Backup.HasNewCode);
        return shell.Backup;
    }, local: new SampleLocal());

    [Fact]
    public Task Im_Netz_freigegeben() => Render("hell", "im-netz", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Network);
        Assert.True(shell.Network!.IsShared);
        Assert.Equal("3F9A-12C4-77B0-E1D2-9A0B", shell.Network.Code);
        return shell.Network;
    }, local: new SampleLocal());

    [Fact]
    public Task Weiterer_PC_verbindet_sich_mit_dem_Haupt_PC()
    {
        var stub = Api();
        stub.Down = true;
        var connect = new NetworkConnectViewModel(new MemorySettingsStore(), () => { }, (_, _, _) => Task.FromResult<string?>(
            "Der Verbindungscode passt nicht zu empfang-pc. Bitte am Haupt-PC unter „Im Netz“ nachsehen."))
        {
            Address = "empfang-pc",
            Code = "3F9A-12C4-77B0-E1D2-9A0C",
        };
        return Render("hell", "netz-verbinden", async (window, main) =>
        {
            await main.StartAsync();
            Assert.False(main.IsConnected);
            await connect.ConnectCommand.ExecuteAsync(null);
            Assert.NotNull(connect.Error);
            return connect;
        }, stub, connect: connect);
    }

    [Fact]
    public Task Kalender_ohne_Verbindung()
    {
        var stub = Api();
        var offline = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        return Render("hell", "kalender-offline", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            stub.Down = true;
            await calendar.LoadAsync();
            calendar.OpenAppointment(SpaSample.Selected);
            await calendar.Selected!.CheckInCommand.ExecuteAsync(null);
            var id = Guid.NewGuid();
            offline.Enqueue(new PendingChange(id, "api/appointments", "{}", "Sabine Gruber, Mo. 19.10. 13:00: verschieben auf 14:00–14:50 (Lena Huber)",
                DateTimeOffset.UtcNow, null));
            offline.Complete(id, new FailedChange(id, "Sabine Gruber, Mo. 19.10. 13:00: verschieben auf 14:00–14:50 (Lena Huber)",
                "Lena Huber hat zu dieser Zeit schon einen Termin.", DateTimeOffset.UtcNow));
            await Pump();
            Assert.True(shell.IsOffline);
            Assert.Equal(1, shell.PendingCount);
            Assert.True(shell.HasFailed);
            return calendar;
        }, stub, offline);
    }

    private static async Task<CalendarViewModel> SignedInCalendar(MainViewModel main)
    {
        await main.StartAsync();
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        await shell.Calendar.LoadAsync();
        Assert.Equal(6, shell.Calendar.Columns.Count);
        return shell.Calendar;
    }

    private static Task<bool> Render(string theme, string name, Func<Window, MainViewModel, Task<object>> arrange, StubApi? stub = null,
        OfflineCache? offline = null, ILocalServer? local = null, NetworkConnectViewModel? connect = null, TimeProvider? time = null, ServerLinkViewModel? serverLink = null)
    {
        var folder = Environment.GetEnvironmentVariable("AISPA_SCREENSHOTS");
        var takesPicture = folder is { Length: > 0 };
        return Session.Dispatch(async () =>
        {
            Application.Current!.RequestedThemeVariant = theme == "dunkel" ? ThemeVariant.Dark : ThemeVariant.Light;
            var main = new MainViewModel((stub ?? Api()).Client(offline), time: time ?? new SampleTime(), local: local) { Connect = connect, ServerLink = serverLink };
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
            window.Show();
            await arrange(window, main);
            await Pump();
            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            if (takesPicture)
            {
                // Seitenleisten blenden in 200 ms ein; das Bildschirmfoto soll den Endstand zeigen, nicht die Mitte der Bewegung.
                await Pump(Settling);
                Directory.CreateDirectory(folder!);
                window.CaptureRenderedFrame()!.Save(Path.Combine(folder!, $"{name}{(theme == "hell" ? "" : "-" + theme)}.png"), PngBitmapEncoderOptions.Default);
            }

            window.Close();
            return true;
        }, CancellationToken.None);
    }


    [Fact]
    public Task Neuer_Eintrag_kurze_Luecke_schlaegt_die_Pause_vor() => Render("hell", "kalender-eintrag-pause", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        // Lena: Termin bis 10:00, nächster ab 10:30; keine Behandlung passt in die 30 Minuten.
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "10:00").Top + 1);
        Assert.Equal(NewEntryKind.Break, calendar.NewEntry!.Kind);
        Assert.Equal("10:30", calendar.Entry!.EndText);
        return calendar;
    });

    [Fact]
    public Task Neuer_Eintrag_lange_Luecke_schlaegt_den_Termin_mit_passenden_Behandlungen_vor() => Render("hell", "kalender-eintrag-termin", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        // Lena: Lücke von 12:00 bis 13:00.
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        Assert.Equal(NewEntryKind.Appointment, calendar.NewEntry!.Kind);
        var options = calendar.Booking!.Options;
        Assert.Equal(["Klassische Massage", "Hamam-Ritual", "Fußreflexzonenmassage"], options.Take(3).Select(o => o.Treatment.Name));
        Assert.Equal("passt genau", options[0].Hint);
        Assert.Equal("länger als die freie Zeit", options[3].Hint);
        Assert.Equal("Klassische Massage", calendar.Booking.Treatment?.Name);
        return calendar;
    });

    [Fact]
    public Task Neuer_Eintrag_Umschalten_behaelt_Text_und_Beginn() => Render("hell", "kalender-eintrag-umschalten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenNoteAt(DayLayout.ColumnLeft(2, calendar.Scale) + 20, calendar.Hours.Single(h => h.Label == "16:00").Top + 4);
        calendar.Entry!.Text = "Gutschein für Frau Berger hinterlegt";
        Assert.True(calendar.SelectNewEntry(4));
        Assert.Equal(CalendarEntryKind.Block, calendar.Entry!.Kind);
        Assert.Equal("Gutschein für Frau Berger hinterlegt", calendar.Entry.Text);
        return calendar;
    });

    private static readonly DateOnly[] HeaderDays = [new(2026, 10, 19), new(2026, 10, 26), new(2026, 9, 24), new(2026, 9, 30)];

    private static StubApi HeaderApi() => HeaderDays.Skip(1).Aggregate(Api(), (api, day) =>
        api.On($"GET /api/locations/{SpaSample.LocationId}/calendar?date={day:yyyy-MM-dd}", SpaSample.CalendarDay()));

    /// <summary>Lage der Knöpfe in der Kopfzeile des Kalenders, in Fensterkoordinaten.</summary>
    private static Dictionary<string, Point> HeaderPositions(Window window)
    {
        Control Find(Func<Control, bool> match) => window.GetVisualDescendants().OfType<Control>().First(match);
        Point Where(Control c) => c.TranslatePoint(new Point(0, 0), window)!.Value;
        bool Tip(Control c, string text) => c is Button && ToolTip.GetTip(c) as string == text;
        bool Text(Control c, string text) => c is Button { Content: string content } && content == text;
        return new Dictionary<string, Point>
        {
            ["Heute"] = Where(Find(c => Text(c, "Heute") && c.IsEffectivelyVisible)),
            ["Vortag"] = Where(Find(c => Tip(c, "Vortag (←)"))),
            ["Nächster Tag"] = Where(Find(c => Tip(c, "Nächster Tag (→)"))),
            ["Titel"] = Where(Find(c => c.Name == "DateButton")),
            ["Team"] = Where(Find(c => c.Name == "HeadTeam")),
            ["Räume"] = Where(Find(c => c.Name == "HeadRooms")),
            ["Zähler"] = Where(Find(c => c.Name == "OverviewChip")),
        };
    }

    [Fact]
    public Task Kopfzeile_Knoepfe_bleiben_beim_Weiterschalten_der_Tage_an_derselben_Stelle() => Render("hell", "kalender-kopfzeile-schmal", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        foreach (var (width, withPanel) in new[] { (1440, false), (1440, true), (900, false), (900, true) })
        {
            window.Width = width;
            if (withPanel)
            {
                calendar.StartBooking(calendar.Columns[0], 330 * DayLayout.PixelsPerMinute);
            }
            else
            {
                calendar.HandleKey(Key.Escape, KeyModifiers.None);
            }

            Dictionary<string, Point>? first = null;
            foreach (var day in HeaderDays)
            {
                calendar.Date = day;
                await Pump();
                await Pump();
                var positions = HeaderPositions(window);
                first ??= positions;
                foreach (var (name, at) in positions)
                {
                    Assert.True(Math.Abs(at.X - first[name].X) < 0.5 && Math.Abs(at.Y - first[name].Y) < 0.5,
                        $"{name} springt am {day:dd.MM.} bei Breite {width}{(withPanel ? " mit Panel" : "")}: {first[name]} → {at}");
                }
            }
        }

        calendar.Date = HeaderDays[2];
        await Pump();
        return calendar;
    }, HeaderApi());

    [Fact]
    public Task Kopfzeile_mit_Donnerstag_im_September() => Render("hell", "kalender-kopfzeile", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.Date = HeaderDays[2];
        await Pump();
        return calendar;
    }, HeaderApi());

    /// <summary>Mitte eines Termins im Kalender, in Fensterkoordinaten (für Klicks mit der Maus).</summary>
    private static Point CenterOf(Window window, Guid appointmentId)
    {
        var border = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("appt") && b.DataContext is CalendarItem { Appointment.Id: var id } && id == appointmentId);
        return border.TranslatePoint(new Point(border.Bounds.Width / 2, Math.Min(12, border.Bounds.Height / 2)), window)!.Value;
    }

    /// <summary>Lässt ausstehende Antworten der Test-API und das Layout durchlaufen.</summary>
    /// <summary>Genug Durchläufe für die längste Bewegung der Oberfläche (200 ms).</summary>
    private const int Settling = 16;

    private static async Task Pump(int rounds = 5)
    {
        for (var i = 0; i < rounds; i++)
        {
            // Wer auf eine Bewegung wartet, braucht echte Zeit; sonst geht es nur um ausstehende Antworten und das Layout.
            await Task.Delay(rounds >= Settling ? 20 : 1);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }
}
