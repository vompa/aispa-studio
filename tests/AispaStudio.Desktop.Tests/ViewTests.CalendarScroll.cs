using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R04 Slice 86: Nach dem Speichern bleibt der Kalender an seiner Scrollstelle, der neue Eintrag bleibt im Bild (Ä152);
/// verborgene Spalten links und rechts zeigt ein Verlauf mit rundem Knopf und „+n“ (Ä154, Variante 2).
/// </summary>
public sealed partial class ViewTests
{
    private static readonly EmployeeDto Hanna = new(Guid.NewGuid(), "Hanna Mayr", "#8a5cc2", [], []);
    private static readonly EmployeeDto Vera = new(Guid.NewGuid(), "Vera Koller", "#2d8a8a", [], []);
    private static readonly EmployeeDto Petra = new(Guid.NewGuid(), "Petra Wolf", "#5b8c3a", [], []);

    /// <summary>Neun Personen mit Dienst; Hanna, Vera und Petra stehen vor Sophie, damit Sophie bei schmalem Fenster rechts außerhalb liegt.</summary>
    private static readonly LocationResources WideTeam = SpaSample.Resources with
    {
        Employees = [SpaSample.Lena, SpaSample.Marco, Hanna, Vera, Petra, SpaSample.Sophie, SpaSample.Malee, SpaSample.Siriporn, SpaSample.Elif],
    };

    private static CalendarDay WideTeamDay(CalendarNoteDto? note)
    {
        var day = SpaSample.CalendarDay();
        return day with
        {
            Shifts = [.. day.Shifts, new EmployeeShift(Hanna.Id, SpaSample.At(9), SpaSample.At(17)), new EmployeeShift(Vera.Id, SpaSample.At(9), SpaSample.At(17)),
                new EmployeeShift(Petra.Id, SpaSample.At(9), SpaSample.At(17))],
            Notes = note is null ? day.Notes : [.. day.Notes ?? [], note],
        };
    }

    /// <summary>Test-API mit neun Personen; eine gespeicherte Notiz „Arzt“ kommt beim nächsten Laden des Tages mit.</summary>
    private static StubApi NoteApi(ResourceKind kind, Guid resourceId, bool wideTeam = true)
    {
        CalendarNoteDto? saved = null;
        var stub = Api()
            .OnJson($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", _ => wideTeam ? WideTeamDay(saved) : saved is null
                ? SpaSample.CalendarDay()
                : SpaSample.CalendarDay() with { Notes = [.. SpaSample.CalendarDay().Notes ?? [], saved] })
            .OnJson($"POST /api/locations/{SpaSample.LocationId}/calendar-notes", _ =>
            {
                saved = new CalendarNoteDto(Guid.NewGuid(), SpaSample.Day, "Arzt", kind, resourceId, SpaSample.At(16, 40), false, SpaSample.At(9), SpaSample.At(16, 55));
                return saved;
            }, HttpStatusCode.Created);
        return wideTeam ? stub.On($"GET /api/locations/{SpaSample.LocationId}/resources", WideTeam) : stub;
    }

    private static ScrollViewer BodyScrollOf(Window window) =>
        window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "BodyScroll");

    /// <summary>Angemeldet, Kalender des Beispieltags geladen, Fenster auf seine Mindestbreite: nicht alle Spalten passen ins Bild.</summary>
    private static async Task<CalendarViewModel> NarrowCalendar(Window window, MainViewModel main)
    {
        await main.StartAsync();
        var calendar = Assert.IsType<ShellViewModel>(main.Content).Calendar;
        await calendar.LoadAsync();
        window.Width = window.MinWidth;
        await Pump();
        await Pump(Settling);
        return calendar;
    }

    /// <summary>Höhe von 16:40 im Raster: frei bei Sophie und in der Kabine.</summary>
    private static double At1640(CalendarViewModel calendar) => calendar.Hours.Single(h => h.Label == "16:00").Top + (40 * calendar.Scale.PixelsPerMinute);

    /// <summary>Notiz „Arzt“ über „Neuer Termin“ an dieser Stelle des Rasters anlegen und mit dem Knopf speichern, wie der Auftraggeber es tat.</summary>
    private static async Task SaveNoteByMouse(Window window, CalendarViewModel calendar, double x, double y)
    {
        var spot = InHost(window, x, y);
        window.MouseMove(spot);
        await Pump();
        _ = Click(window, spot);
        await Pump(Settling);
        Assert.True(calendar.NewEntry!.Select(NewEntryKind.Note));
        await Pump(Settling);
        calendar.Entry!.Text = "Arzt";
        await Pump();
        ClickCenter(window, ButtonWithText(SidePanelOf(window), "Notiz speichern"));
        await Pump();
        await Pump(Settling);
    }

    /// <summary>Die Notiz „Arzt“ liegt ganz im sichtbaren Ausschnitt des Rasters.</summary>
    private static void AssertNoteVisible(Window window)
    {
        var body = BodyScrollOf(window);
        var sticky = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("sticky") && b.DataContext is CalendarNoteItem { Text: "Arzt" });
        var at = sticky.TranslatePoint(new Point(0, 0), body)!.Value;
        Assert.InRange(at.X, 0, body.Viewport.Width - sticky.Bounds.Width);
        Assert.InRange(at.Y, 0, body.Viewport.Height - sticky.Bounds.Height);
    }

    // ---------- Ä152: Scrollstelle bleibt ----------

    [Fact]
    public Task Sophie_rechts_ausserhalb_und_zu_ihr_gescrollt_Notiz_in_ihrer_Spalte_speichern_laesst_Scrollstelle_und_Notiz_im_Bild() =>
        Render("hell", "kalender-scrollstelle-notiz", async (window, main) =>
        {
            var calendar = await NarrowCalendar(window, main);
            var body = BodyScrollOf(window);
            var left = DayLayout.ColumnLeft(calendar.Columns.ToList().FindIndex(c => c.Title == "Sophie Gruber"), calendar.Scale);
            Assert.True(left + calendar.Scale.ColumnWidth > body.Viewport.Width, $"Sophie liegt zu Beginn nicht rechts außerhalb ({left}, {body.Viewport.Width})");

            body.Offset = new Vector(left, 0);
            await Pump();
            var before = body.Offset;
            Assert.True(before.X > 0);

            calendar.OpenNoteAt(left + 10, At1640(calendar));
            await Pump(Settling);
            calendar.Entry!.Text = "Arzt";
            await calendar.Entry.SaveAsync();
            await Pump();
            await Pump(Settling);

            Assert.Equal(before.X, body.Offset.X, 1);
            Assert.Equal(before.Y, body.Offset.Y, 1);
            AssertNoteVisible(window);
            return calendar;
        }, NoteApi(ResourceKind.Employee, SpaSample.Sophie.Id));

    [Fact]
    public Task Angefangener_Termin_bei_Lena_dann_Notiz_bei_Sophie_mit_der_Maus_speichern_laesst_Sophie_und_die_Notiz_im_Bild() =>
        Render("hell", "kalender-scrollstelle-notiz-maus", async (window, main) =>
        {
            var calendar = await NarrowCalendar(window, main);
            var body = BodyScrollOf(window);

            // Vorher war „Neuer Termin“ bei Lena angefangen (der Auftraggeber testete davor „Neuer Termin“); das Seitenpanel macht den Ausschnitt schmaler.
            var lena = InHost(window, 20, FreeY(calendar, 0));
            window.MouseMove(lena);
            await Pump();
            _ = Click(window, lena);
            await Pump(Settling);
            Assert.True(calendar.NewEntry!.Select(NewEntryKind.Appointment));
            await Pump(Settling);
            var left = DayLayout.ColumnLeft(calendar.Columns.ToList().FindIndex(c => c.Title == "Sophie Gruber"), calendar.Scale);
            body.Offset = new Vector(left, 0);
            await Pump();
            var before = body.Offset;

            await SaveNoteByMouse(window, calendar, left + 20, At1640(calendar));

            // Das Panel ist zu, der Ausschnitt breiter: weiter als bis zum Ende kann er nicht stehen, sonst bleibt er.
            Assert.Equal(Math.Min(before.X, body.Extent.Width - body.Viewport.Width), body.Offset.X, 1);
            AssertNoteVisible(window);
            return calendar;
        }, NoteApi(ResourceKind.Employee, SpaSample.Sophie.Id));

    [Fact]
    public Task Raumansicht_Notiz_in_der_Kabine_rechts_speichern_wenn_das_Panel_die_Spalten_schmaler_macht_laesst_die_Notiz_im_Bild() =>
        Render("hell", "kalender-scrollstelle-raum", async (window, main) =>
        {
            await main.StartAsync();
            var calendar = Assert.IsType<ShellViewModel>(main.Content).Calendar;
            await calendar.LoadAsync();
            calendar.ByRoom = true;
            await Pump(Settling);
            var body = BodyScrollOf(window);
            var cabin = calendar.Columns.ToList().FindIndex(c => c.Id == SpaSample.Cabin.Id);
            var wide = calendar.Scale.ColumnWidth;
            Assert.True(wide > 160, $"Spalten sind schon schmal ({wide})");
            var left = DayLayout.ColumnLeft(cabin, calendar.Scale);
            body.Offset = new Vector(Math.Min(left, body.Extent.Width - body.Viewport.Width), 0);
            await Pump();

            await SaveNoteByMouse(window, calendar, left + 20, At1640(calendar));

            // Mit offenem Panel waren die Spalten schmaler; danach wieder breit. Die Kabine und die Notiz bleiben trotzdem im Bild.
            Assert.Equal(wide, calendar.Scale.ColumnWidth, 0.5);
            AssertNoteVisible(window);
            return calendar;
        }, NoteApi(ResourceKind.Room, SpaSample.Cabin.Id, wideTeam: false));

    // ---------- Ä154: weitere Spalten am Rand ----------

    private static readonly string[] EdgeParts = ["EdgePrevious", "EdgeNext", "EdgeFadePrevious", "EdgeFadeNext"];

    private static Button EdgeButton(Window window, string name) =>
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);

    /// <summary>Spalten, die im Ausschnitt nicht ganz zu sehen sind, gemessen an den Spaltenköpfen gegen den sichtbaren Bereich.</summary>
    private static (List<string> Before, List<string> After) HiddenColumns(Window window, CalendarViewModel calendar)
    {
        var body = BodyScrollOf(window);
        var heads = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "HeaderScroll")
            .GetVisualDescendants().OfType<Grid>().Where(g => g.DataContext is CalendarColumn && g.Height == 38).ToList();
        var before = new List<string>();
        var after = new List<string>();
        foreach (var head in heads)
        {
            var at = head.TranslatePoint(new Point(0, 0), body)!.Value.X;
            if (at < -1)
            {
                before.Add(((CalendarColumn)head.DataContext!).Title);
            }

            if (at + head.Bounds.Width > body.Viewport.Width + 1)
            {
                after.Add(((CalendarColumn)head.DataContext!).Title);
            }
        }

        Assert.Equal(calendar.Columns.Count, heads.Count);
        return (before, after);
    }

    /// <summary>Im Beispiel liegen in der Raumansicht zwei Spalten außerhalb; ein siebter Behandlungsraum macht drei daraus.</summary>
    [Fact]
    public Task Raumansicht_mit_drei_Raeumen_ausserhalb_zeigt_den_Knopf_Weiter_mit_plus_3_und_keinen_Zurueck_Knopf() =>
        Render("hell", "kalender-weitere-spalten", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            calendar.ByRoom = true;
            await Pump(Settling);

            var (before, after) = HiddenColumns(window, calendar);
            Assert.Empty(before);
            Assert.Equal(["Salzraum", "Finnische Sauna", "Ruheraum"], after);
            var next = EdgeButton(window, "EdgeNext");
            Assert.True(next.IsEffectivelyVisible);
            Assert.Contains(next.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "+3");
            Assert.Contains(next.GetVisualDescendants().OfType<Icon>(), i => i.Data == Icons.ChevronRight);
            Assert.False(EdgeButton(window, "EdgePrevious").IsEffectivelyVisible);

            // Der Verlauf deckt die Scrollbalken des Rasters nicht zu.
            var fade = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "EdgeFadeNext");
            var fadeBox = new Rect(fade.TranslatePoint(new Point(0, 0), window)!.Value, fade.Bounds.Size);
            foreach (var bar in BodyScrollOf(window).GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>().Where(b => b.IsEffectivelyVisible))
            {
                var barBox = new Rect(bar.TranslatePoint(new Point(0, 0), window)!.Value, bar.Bounds.Size);
                Assert.False(fadeBox.Intersects(barBox), $"Verlauf {fadeBox} über Scrollbalken {barBox}");
            }

            // Hinweis und Screenreader nennen die verborgenen Räume.
            var tip = $"Weiter zu 3 Spalten: {string.Join(", ", after)}";
            Assert.Equal(tip, ToolTip.GetTip(next));
            Assert.Equal(tip, AutomationProperties.GetName(next));
            return calendar;
        }, Api().On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.Resources with
        {
            Rooms = [.. SpaSample.Resources.Rooms.Take(6), SpaSample.Lotus with { Id = Guid.NewGuid(), Name = "Salzraum" }, .. SpaSample.Resources.Rooms.Skip(6)],
        }));

    [Fact]
    public Task Klick_auf_Weiter_rueckt_das_Raster_um_eine_Breite_weiter_und_zeigt_den_Zurueck_Knopf() =>
        Render("hell", "kalender-weitere-spalten-weiter", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            calendar.ByRoom = true;
            await Pump(Settling);
            var body = BodyScrollOf(window);
            var width = body.Viewport.Width;
            Assert.Equal(0, body.Offset.X);

            ClickCenter(window, EdgeButton(window, "EdgeNext"));
            await Pump(Settling);

            Assert.Equal(Math.Min(width, body.Extent.Width - width), body.Offset.X, 1);
            var previous = EdgeButton(window, "EdgePrevious");
            Assert.True(previous.IsEffectivelyVisible);
            Assert.Contains(previous.GetVisualDescendants().OfType<Icon>(), i => i.Data == Icons.ChevronLeft);
            var (before, _) = HiddenColumns(window, calendar);
            Assert.Contains(previous.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == $"+{before.Count}");
            Assert.Equal($"Zurück zu {before.Count} Spalten: {string.Join(", ", before)}", AutomationProperties.GetName(previous));
            return calendar;
        });

    [Fact]
    public Task Teamansicht_ohne_verborgene_Spalten_zeigt_keinen_Verlauf_und_keinen_Knopf() => Render("hell", "kalender-keine-weiteren-spalten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump(Settling);

        var (before, after) = HiddenColumns(window, calendar);
        Assert.Empty(before);
        Assert.Empty(after);
        foreach (var name in EdgeParts)
        {
            Assert.False(window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name).IsEffectivelyVisible, name);
        }

        return calendar;
    });
}
