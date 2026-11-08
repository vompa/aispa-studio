using AispaStudio.Desktop.Controls;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Zahlenfeld (F7, Design-Brief 5.19) mit der echten Vorlage: Lesen, Fehler statt stillem Begrenzen, Pfeile, Pfeilspalte, Rad, Screenreader-Bereich.</summary>
public sealed partial class ViewTests
{
    private static NumberField Minutes() => new() { Minimum = 0, Maximum = 120, Increment = 5, LargeIncrement = 15, ShowSpinner = true, CommittedText = "30" };

    [Theory]
    [InlineData("45", "45")]
    [InlineData("0", "0")]
    [InlineData("120", "120")]
    public Task Zahlenfeld_liest_und_schreibt_nach_dem_Verlassen_das_Anzeigeformat(string typed, string shown) => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = Minutes();
        var window = await ShowAsync("hell", anchor, field);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput(typed);
        await Blur(anchor);

        Assert.Equal(shown, field.Text);
        Assert.Equal(decimal.Parse(shown, System.Globalization.CultureInfo.InvariantCulture), field.Value);
        Assert.Null(field.Error);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Zahlenfeld_mit_Tausenderpunkt_und_Dezimalkomma() => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = new NumberField { Minimum = 0, Maximum = 999999, DecimalPlaces = 3 };
        var window = await ShowAsync("hell", anchor, field);
        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("1234,50");
        await Blur(anchor);
        Assert.Equal("1.234,5", field.Text);
        Assert.Equal(1234.5m, field.Value);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("121", "Der Wert liegt zwischen 0 und 120.")]
    [InlineData("12,5", NumberInputParser.WholeError)]
    public Task Zahl_ausserhalb_des_Bereichs_ist_ein_Fehler_statt_stillem_Begrenzen(string typed, string error) => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = Minutes();
        var form = new FormField { Label = "Vorbereitung (min)", Content = field };
        var window = await ShowAsync("hell", anchor, form);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput(typed);
        await Blur(anchor);

        Assert.Equal(error, field.Error);
        Assert.Equal(typed, field.Text);
        Assert.False(field.IsValid);
        Assert.Null(field.Value);
        Assert.Equal(error, form.Message);
        SaveFieldShot(window, "zahlenfeld-fehler");

        // Esc stellt den letzten bestätigten Wert wieder her.
        field.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Equal("30", field.Text);
        Assert.Equal(30m, field.Value);
        Assert.True(field.IsValid);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Zahlenfeld_nimmt_Buchstaben_und_Minus_ohne_negatives_Minimum_nicht_an() => Session.Dispatch(async () =>
    {
        var field = Minutes();
        var window = await ShowAsync("hell", field);
        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("a1-2+3");
        Assert.Equal("123", field.Text);
        field.Minimum = -10;
        window.KeyTextInput("-");
        Assert.Equal("123-", field.Text);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Zahlenfeld_Pfeile_Schritt_Umschalt_eins_Bild_gross_Pos1_Ende_Grenzen_kein_Umlauf() => Session.Dispatch(async () =>
    {
        var field = Minutes();
        var window = await ShowAsync("hell", field);
        field.Focus(NavigationMethod.Tab);
        await Pump();

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Assert.Equal("35", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Shift);
        Assert.Equal("36", field.Text);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        Assert.Equal("51", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.Equal("46", field.Text);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        Assert.Equal("31", field.Text);
        Assert.Equal(31m, field.Value);

        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Assert.Equal("120", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Assert.Equal("120", field.Text);
        Assert.Null(field.Error);
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        Assert.Equal("0", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.Equal("0", field.Text);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Zahlenfeld_Pfeilspalte_nur_fuer_die_Maus_am_Rand_abgeschaltet_und_benannt() => Session.Dispatch(async () =>
    {
        var field = Minutes();
        var window = await ShowAsync("hell", field);
        var buttons = field.GetVisualDescendants().OfType<RepeatButton>().ToList();
        Assert.Equal(2, buttons.Count);
        var (up, down) = (buttons[0], buttons[1]);
        Assert.Equal(["Erhöhen", "Verringern"], buttons.Select(Avalonia.Automation.AutomationProperties.GetName));
        Assert.All(buttons, b => Assert.False(b.IsTabStop));
        Assert.All(buttons, b => Assert.False(b.Focusable));
        Assert.Equal(24, up.Bounds.Width);

        ClickCenter(window, up);
        await Pump();
        Assert.Equal(35m, field.Value);
        Assert.Equal("35", field.Text);

        field.CommittedText = "120";
        await Pump();
        Assert.False(up.IsEnabled);
        Assert.True(down.IsEnabled);
        // Das Feld selbst bleibt am Rand bedienbar.
        Assert.True(field.IsEnabled);
        field.CommittedText = "0";
        await Pump();
        Assert.True(up.IsEnabled);
        Assert.False(down.IsEnabled);
        SaveFieldShot(window, "zahlenfeld-pfeilspalte");

        // Ohne ShowSpinner gibt es keine Pfeilspalte (große Bereiche).
        field.ShowSpinner = false;
        await Pump();
        Assert.Empty(field.GetVisualDescendants().OfType<RepeatButton>());
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Zahlenfeld_Rad_wirkt_nur_bei_Fokus() => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = Minutes();
        var window = await ShowAsync("hell", anchor, field);
        var at = field.TranslatePoint(new Point(30, 17), window)!.Value;

        anchor.Focus(NavigationMethod.Pointer);
        window.MouseWheel(at, new Vector(0, 1));
        await Pump();
        Assert.Equal("30", field.Text);

        field.Focus(NavigationMethod.Tab);
        window.MouseWheel(at, new Vector(0, 1));
        await Pump();
        Assert.Equal("35", field.Text);
        window.MouseWheel(at, new Vector(0, -1));
        await Pump();
        Assert.Equal("30", field.Text);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Zahlenfeld_meldet_Bereich_Wert_und_Schritt_an_den_Screenreader() => Session.Dispatch(async () =>
    {
        var field = Minutes();
        field.CommittedText = "45";
        var window = await ShowAsync("hell", new FormField { Label = "Vorbereitung (min)", Content = field });
        var peer = ControlAutomationPeer.CreatePeerForElement(field);
        var range = Assert.IsAssignableFrom<IRangeValueProvider>(peer);
        Assert.Equal((0d, 120d, 45d, 5d, 15d), (range.Minimum, range.Maximum, range.Value, range.SmallChange, range.LargeChange));
        Assert.Equal("Vorbereitung (min)", peer.GetName());
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Zeiten_je_Termin_Zahlenfeld_ausserhalb_sperrt_Uebernehmen_und_Esc_stellt_erst_den_Wert_her() => Render("hell", "kalender-panel-zeiten-zahlenfelder", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        calendar.Selected!.BeginEditTimes();
        await Pump();
        var boxes = window.GetVisualDescendants().OfType<NumberField>().Where(n => n.IsEffectivelyVisible).ToList();
        var apply = ButtonWithText(window, "Übernehmen");
        Assert.True(apply.IsEnabled);
        var before = calendar.Selected.TimesPreparation;

        boxes[0].Focus(NavigationMethod.Tab);
        window.KeyTextInput("150");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Pump();
        Assert.Equal("Der Wert liegt zwischen 0 und 120.", boxes[0].Error);
        Assert.False(apply.IsEnabled, "mit einem Fehler im Feld gilt kein alter Wert unbemerkt");
        Assert.Equal(before, calendar.Selected.TimesPreparation);
        await Pump(Settling);
        SaveFieldShot(window, "kalender-panel-zeiten-fehler");

        // Esc: erst der Wert zurück, dann das zweite Esc bricht das Ändern ab.
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.True(calendar.Selected.IsEditingTimes);
        Assert.Equal(before.ToString(System.Globalization.CultureInfo.InvariantCulture), boxes[0].Text);
        Assert.True(apply.IsEnabled);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.False(calendar.Selected.IsEditingTimes);
        calendar.Selected.BeginEditTimes();
        return calendar;
    });
}
