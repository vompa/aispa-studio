using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Datumsfeld (F6, Design-Brief 5.17) mit der echten Vorlage: tolerantes Lesen, Fehler, Pfeile, Grenzen, Geburtstag in der Kundendaten-Maske. Heute ist der 21.10.2026.</summary>
public sealed partial class ViewTests
{
    private static readonly DateOnly FieldToday = new(2026, 10, 21);

    [Theory]
    [InlineData("21102026", "21.10.2026")]
    [InlineData("211026", "21.10.2026")]
    [InlineData("21.10.26", "21.10.2026")]
    [InlineData("21.10.", "21.10.2026")]
    [InlineData("1.2.26", "01.02.2026")]
    [InlineData("1/2/2027", "01.02.2027")]
    public Task Datumsfeld_liest_tolerant_und_schreibt_nach_dem_Verlassen_TTMMJJJJ(string typed, string shown) => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = new DateField { TodayOverride = FieldToday };
        var window = await ShowAsync("hell", anchor, field);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput(typed);
        await Blur(anchor);

        Assert.Equal(shown, field.Text);
        Assert.Equal(shown, field.CommittedText);
        Assert.Equal(DateInputParser.TryParse(shown, FieldToday), field.Value);
        Assert.Null(field.Error);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("30.02.2026", "Den 30.02.2026 gibt es nicht.")]
    [InlineData("31.4.26", "Den 31.04.2026 gibt es nicht.")]
    [InlineData("21.10.2", DateInputParser.FormatError)]
    public Task Ungueltiges_Datum_zeigt_den_Fehler_am_Feld_und_laesst_den_Text_stehen(string typed, string error) => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = new DateField { TodayOverride = FieldToday, CommittedText = "21.10.2026" };
        var form = new FormField { Label = "Tag", Content = field };
        var window = await ShowAsync("hell", anchor, form);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput(typed);
        await Blur(anchor);

        Assert.Equal(error, field.Error);
        Assert.Equal(typed, field.Text);
        Assert.Equal(typed, field.CommittedText);
        Assert.Null(field.Value);
        Assert.Equal(error, form.Message);
        Assert.Contains("invalid", field.Classes);
        SaveFieldShot(window, "datumsfeld-fehler");
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Datumsfeld_Pfeile_Tag_Woche_Monat_Jahr_und_ohne_Wert_der_heutige_Tag() => Session.Dispatch(async () =>
    {
        var field = new DateField { TodayOverride = FieldToday };
        var window = await ShowAsync("hell", field);
        field.Focus(NavigationMethod.Tab);
        await Pump();

        // Ohne Wert setzt ↑ den heutigen Tag.
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Assert.Equal("21.10.2026", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Assert.Equal("22.10.2026", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.Equal("20.10.2026", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Shift);
        Assert.Equal("27.10.2026", field.Text);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        Assert.Equal("27.11.2026", field.Text);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        Assert.Equal("27.09.2026", field.Text);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.Control);
        Assert.Equal("27.09.2027", field.Text);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.Control);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.Control);
        Assert.Equal("27.09.2025", field.Text);
        Assert.Equal(new DateOnly(2025, 9, 27), field.Value);
        Assert.Equal((0, 10), (field.SelectionStart, field.SelectionEnd));
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Datumsfeld_Grenzen_Fehler_nennt_die_Grenzen_und_Pfeile_begrenzen() => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = new DateField
        {
            TodayOverride = FieldToday,
            CommittedText = "22.10.2026",
            Minimum = new DateOnly(2026, 10, 22),
            Maximum = new DateOnly(2027, 10, 21),
        };
        var window = await ShowAsync("hell", anchor, field);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("21.10.2026");
        await Blur(anchor);
        Assert.Equal("Das Datum liegt zwischen 22.10.2026 und 21.10.2027.", field.Error);

        // Die Pfeiltaste begrenzt auf die Grenze statt eines Fehlers; kein Umlauf.
        field.Revert();
        field.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.Equal("22.10.2026", field.Text);
        Assert.Null(field.Error);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Geburtstag_verlangt_das_Jahr_und_reicht_nie_in_die_Zukunft() => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var birthday = new DateField { RequireYear = true, NotInFuture = true, TodayOverride = FieldToday };
        var window = await ShowAsync("hell", anchor, new FormField { Label = "Geburtstag", Content = birthday });

        birthday.Focus(NavigationMethod.Tab);
        window.KeyTextInput("14.3.85");
        await Blur(anchor);
        Assert.Equal("14.03.1985", birthday.Text);

        birthday.Focus(NavigationMethod.Tab);
        window.KeyTextInput("22.10.2026");
        await Blur(anchor);
        Assert.Equal("Das Datum liegt am 21.10.2026 oder früher.", birthday.Error);

        birthday.Focus(NavigationMethod.Tab);
        window.KeyTextInput("14.3.");
        await Blur(anchor);
        Assert.Equal(DateInputParser.YearMissingError, birthday.Error);
        window.Close();
        return true;
    }, CancellationToken.None);

    /// <summary>Die Kundendaten-Maske (masken.md, Abschnitt 7) nutzt den Feldrahmen: Stern in Textfarbe statt rot, Geburtstag als Datumsfeld.</summary>
    [Fact]
    public Task Kundendaten_Maske_nutzt_Feldrahmen_mit_Pflichtstern_in_Textfarbe_und_Datumsfeld() => Render("hell", "kunden-bearbeiten-feldrahmen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.ShowCustomerAsync(SpaSample.Customers[0].Id);
        await Pump();
        shell.Customers.EditContactCommand.Execute(null);
        await Pump();
        var form = window.GetVisualDescendants().OfType<CustomerContactForm>().First(f => f.IsEffectivelyVisible);

        var fields = form.GetVisualDescendants().OfType<FormField>().ToList();
        Assert.Contains(fields, f => f.Label == "Vorname" && f.IsRequired);
        Assert.Contains(fields, f => f.Label == "Nachname" && f.IsRequired);
        Assert.Equal(2, fields.Count(f => f.IsRequired));
        var birthday = form.GetVisualDescendants().OfType<DateField>().Single();
        Assert.True(birthday.RequireYear);
        Assert.True(birthday.NotInFuture);
        Assert.Equal(160, birthday.Bounds.Width, 1);

        // Enter in einem einzeiligen Feld löst den Standardknopf aus (Brief 5.16): Speichern.
        Assert.True(ButtonWithText(form, "Speichern").IsDefault);

        // Kein roter Stern mehr: der Stern steht in der Textfarbe (Design-Brief 5.2).
        var stars = form.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text == "*" && t.IsEffectivelyVisible).ToList();
        Assert.Equal(2, stars.Count);
        Assert.All(stars, star => Assert.Equal(Resolve(form, "FgBrush"), ((Avalonia.Media.ISolidColorBrush)star.Foreground!).Color));
        return calendar;
    }, WithPlainCustomer());

    [Fact]
    public Task Neuer_Termin_hat_ein_Datumsfeld_mit_Minimum_heute() => Render("hell", "kalender-termin-datum", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        var booking = calendar.Booking!;
        await Pump();

        var date = Assert.Single(SidePanelOf(window).GetVisualDescendants().OfType<DateField>());
        Assert.Equal(booking.Today, date.Minimum);
        Assert.Equal(SpaSample.Day, booking.Today);
        return calendar;
    });
}
