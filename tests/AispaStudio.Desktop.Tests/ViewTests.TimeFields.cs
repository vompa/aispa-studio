using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Uhrzeitfeld (F5, Design-Brief 5.18) mit der echten Vorlage: tolerantes Lesen, Fehler am Feld, Pfeile, Rad, Ende nach Beginn, Esc und Enter.</summary>
public sealed partial class ViewTests
{
    private static async Task Blur(Control anchor)
    {
        anchor.Focus(NavigationMethod.Pointer);
        await Pump();
    }

    [Theory]
    [InlineData("9", "09:00")]
    [InlineData("930", "09:30")]
    [InlineData("0930", "09:30")]
    [InlineData("9:30", "09:30")]
    [InlineData("9.30", "09:30")]
    [InlineData("9,30", "09:30")]
    [InlineData("9:5", "09:05")]
    public Task Uhrzeitfeld_liest_tolerant_und_schreibt_nach_dem_Verlassen_HHmm(string typed, string shown) => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = new TimeField();
        var window = await ShowAsync("hell", anchor, field);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput(typed);
        Assert.Equal(typed, field.Text);
        Assert.Null(field.Value);
        await Blur(anchor);

        Assert.Equal(shown, field.Text);
        Assert.Equal(shown, field.CommittedText);
        Assert.Equal(TimeInputParser.TryParse(shown), field.Value);
        Assert.Null(field.Error);
        window.Close();
        return true;
    }, CancellationToken.None);

    /// <summary>Regressionstest Ä20 an der Oberfläche: eine falsche Uhrzeit ergibt einen Fehlertext am Feld, der Text bleibt, nichts stürzt ab.</summary>
    [Theory]
    [InlineData("24", TimeInputParser.RangeError)]
    [InlineData("9:60", TimeInputParser.RangeError)]
    [InlineData("99", TimeInputParser.RangeError)]
    [InlineData("12345", TimeInputParser.FormatError)]
    [InlineData("9:", TimeInputParser.FormatError)]
    public Task Falsche_Uhrzeit_im_Feld_zeigt_den_Fehler_am_Feld_und_laesst_den_Text_stehen(string typed, string error) => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = new TimeField { CommittedText = "09:30" };
        var form = new FormField { Label = "Beginn", Content = field };
        var window = await ShowAsync("hell", anchor, form);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput(typed);
        await Blur(anchor);

        Assert.Equal(error, field.Error);
        Assert.Equal(typed, field.Text);
        // Nach außen geht der unlesbare Text ohne Wert, kein alter gültiger Wert bleibt zurück, der beim Speichern unbemerkt gälte.
        Assert.Equal(typed, field.CommittedText);
        Assert.Null(field.Value);
        Assert.Contains("invalid", field.Classes);
        Assert.Equal(FieldMessageKind.Error, form.MessageKind);
        Assert.Equal(error, form.Message);
        SaveFieldShot(window, "zeitfeld-fehler");

        // Sofort beim Tippen neu prüfen: der Fehler verschwindet, sobald der Wert gültig ist.
        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("10");
        Assert.Null(field.Error);
        Assert.DoesNotContain("invalid", field.Classes);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Uhrzeitfeld_nimmt_nur_Ziffern_Doppelpunkt_Punkt_und_Komma_an_und_hoechstens_fuenf_Zeichen() => Session.Dispatch(async () =>
    {
        var field = new TimeField();
        var window = await ShowAsync("hell", field);
        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("a9b:3c0x");
        Assert.Equal("9:30", field.Text);
        window.KeyTextInput("5");
        Assert.Equal("9:305", field.Text);
        window.KeyTextInput("1");
        Assert.Equal("9:305", field.Text);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Uhrzeitfeld_Pfeile_Raster_Umschalt_Minute_Bild_Stunde_ohne_Umlauf() => Session.Dispatch(async () =>
    {
        var field = new TimeField { CommittedText = "09:30" };
        var window = await ShowAsync("hell", field);
        field.Focus(NavigationMethod.Tab);
        await Pump();

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Assert.Equal("09:35", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Shift);
        Assert.Equal("09:36", field.Text);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        Assert.Equal("10:36", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.Equal("10:31", field.Text);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        Assert.Equal("09:31", field.Text);
        Assert.Equal(new TimeOnly(9, 31), field.Value);
        Assert.Equal((0, 5), (field.SelectionStart, field.SelectionEnd));

        // Kein Umlauf: am Tagesende bzw. um Mitternacht bleibt der Wert stehen.
        field.CommittedText = "23:58";
        field.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Assert.Equal("23:59", field.Text);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        Assert.Equal("23:59", field.Text);
        field.CommittedText = "00:03";
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.Equal("00:00", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.Equal("00:00", field.Text);

        // Pos1/Ende nur mit Grenzen; die Rasterweite ist einstellbar.
        field.Minimum = new TimeOnly(8, 0);
        field.Maximum = new TimeOnly(20, 0);
        field.StepMinutes = 15;
        field.CommittedText = "09:00";
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Assert.Equal("20:00", field.Text);
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        Assert.Equal("08:00", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Assert.Equal("08:15", field.Text);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Uhrzeitfeld_Rad_wirkt_nur_bei_Fokus() => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = new TimeField { CommittedText = "09:30" };
        var window = await ShowAsync("hell", anchor, field);
        var at = field.TranslatePoint(new Point(40, 17), window)!.Value;

        anchor.Focus(NavigationMethod.Pointer);
        window.MouseWheel(at, new Vector(0, 1));
        await Pump();
        Assert.Equal("09:30", field.Text);

        field.Focus(NavigationMethod.Tab);
        window.MouseWheel(at, new Vector(0, 1));
        await Pump();
        Assert.Equal("09:35", field.Text);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Ende_vor_dem_Beginn_ist_ein_Fehler_am_Ende_Feld_und_Abhaengiges_rechnet_erst_nach_dem_Bestaetigen() => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var start = new TimeField { CommittedText = "10:00" };
        var end = new TimeField { CommittedText = "11:00" };
        end.Bind(TimeField.NotBeforeProperty, new Avalonia.Data.Binding(nameof(TimeField.Value)) { Source = start });
        var window = await ShowAsync("hell", anchor, start, end);

        // Beim Tippen ändert sich nach außen nichts; erst die Bestätigung meldet den Wert.
        var committed = 0;
        end.PropertyChanged += (_, e) => committed += e.Property == ParsedField.CommittedTextProperty ? 1 : 0;
        end.Focus(NavigationMethod.Tab);
        window.KeyTextInput("9");
        Assert.Equal(0, committed);
        Assert.Equal("11:00", end.CommittedText);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal(TimeInputParser.EndBeforeStartError, end.Error);
        Assert.Equal(1, committed);
        Assert.Equal("9", end.CommittedText);
        Assert.Null(end.Value);
        Assert.True(end.IsFocused, "bei einem Fehler bleibt der Fokus im Feld");

        end.SelectAll();
        window.KeyTextInput("10");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal(TimeInputParser.EndEqualsStartError, end.Error);

        end.SelectAll();
        window.KeyTextInput("12");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Null(end.Error);
        Assert.Equal("12:00", end.CommittedText);
        Assert.Equal(new TimeOnly(12, 0), end.Value);
        Assert.Equal(3, committed);

        // Wird der Beginn danach später gelegt als das Ende, meldet sich das Ende-Feld.
        start.CommittedText = "13:00";
        await Pump();
        Assert.Equal(TimeInputParser.EndBeforeStartError, end.Error);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Esc_im_geaenderten_Zeitfeld_stellt_den_Wert_her_und_Enter_mit_Fehler_loest_den_Standardknopf_nicht_aus() => Session.Dispatch(async () =>
    {
        var saved = new Counter();
        var field = new TimeField { CommittedText = "09:30" };
        var window = await ShowAsync("hell", field, DefaultButton(saved));
        var unhandled = WatchUnhandledKeys(window);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("24");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Pump();
        Assert.Equal(TimeInputParser.RangeError, field.Error);
        Assert.Equal(0, saved.Value);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.Equal("09:30", field.Text);
        Assert.Null(field.Error);
        Assert.Equal((0, 5), (field.SelectionStart, field.SelectionEnd));
        Assert.DoesNotContain(Key.Escape, unhandled);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Contains(Key.Escape, unhandled);

        // Enter mit gültigem Wert bestätigt und löst den Standardknopf aus.
        window.KeyTextInput("1015");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Pump();
        Assert.Equal("10:15", field.Text);
        Assert.Equal(1, saved.Value);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Eintrag_Panel_mit_Zeitfeldern_falsche_Zeit_zeigt_den_Fehler_Esc_stellt_her_und_schliesst() => Render("hell", "kalender-eintrag-zeitfelder", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenBlockAt(DayLayout.ColumnLeft(2, calendar.Scale) + 20, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        await Pump();
        var entry = calendar.Entry!;
        var fields = window.GetVisualDescendants().OfType<TimeField>().Where(f => f.IsEffectivelyVisible).ToList();
        Assert.Equal(2, fields.Count);
        var (start, end) = (fields[0], fields[1]);
        Assert.Equal(("12:00", "13:00"), (start.Text, end.Text));
        Assert.Equal(("12:00", "13:00"), (entry.StartText, entry.EndText));
        Assert.All(fields, f => Assert.True(f.IsRequired));

        // Ende vor dem Beginn: Fehler am Feld, im ViewModel bleibt der alte Wert; nichts wirft.
        end.Focus(NavigationMethod.Tab);
        window.KeyTextInput("11");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Pump();
        Assert.Equal(TimeInputParser.EndBeforeStartError, end.Error);
        Assert.Equal("11", entry.EndText);

        // Speichern mit dem unlesbaren Ende lehnt das ViewModel selbst ab (kein stiller alter Wert).
        entry.Text = "Arzt";
        await entry.SaveAsync();
        Assert.NotNull(entry.Error);
        await Pump(Settling);
        SaveFieldShot(window, "kalender-eintrag-zeitfelder-fehler");

        // Esc: erst der Wert zurück, dann das zweite Esc schließt das Fenster.
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.Equal("13:00", end.Text);
        Assert.NotNull(calendar.Entry);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        // Der Grund „Arzt“ ist eingetragen: das Schließen fragt „Änderungen verwerfen?“ und erst „Verwerfen“ schließt das Fenster.
        Assert.Equal("Änderungen verwerfen?", main.DialogHost.Current?.Title);
        Assert.NotNull(calendar.Entry);
        main.DialogHost.Current!.ConfirmCommand.Execute(null);
        await Pump();
        Assert.Null(calendar.Entry);
        return calendar;
    });

    [Fact]
    public Task Wechsel_von_Termin_zu_Pause_zeigt_die_Pause_Maske_ohne_rotes_Zeitfeld_und_mit_mitgezogenem_Ende() => Render("hell", "kalender-pause-wechsel", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBooking(calendar.Columns[0], calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        calendar.Booking!.StartText = "15:30";
        await Pump();

        Assert.True(calendar.SelectNewEntry(3));
        await Pump();

        var entry = calendar.Entry!;
        Assert.Equal(CalendarEntryKind.Break, entry.Kind);
        Assert.Equal("15:30", entry.StartText);
        Assert.True(TimeInputParser.TryParse(entry.EndText) > new TimeOnly(15, 30), $"Ende {entry.EndText} liegt nicht nach dem Beginn");
        var fields = SidePanelOf(window).GetVisualDescendants().OfType<TimeField>().Where(f => f.IsEffectivelyVisible).ToList();
        Assert.Equal(2, fields.Count);
        Assert.All(fields, f =>
        {
            Assert.Null(f.Error);
            Assert.DoesNotContain("invalid", f.Classes);
        });
        Assert.Equal(entry.EndText, fields[1].Text);
        return calendar;
    });
}
