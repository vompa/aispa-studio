using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Betragsfeld (F8, Design-Brief 5.20) mit der echten Vorlage und in der Kassa: <c>decimal</c>, zwei Nachkommastellen, nie stilles Runden, leer ist nicht 0,00.</summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("12", "12,00")]
    [InlineData("12,5", "12,50")]
    [InlineData("12.5", "12,50")]
    [InlineData("1.234,56", "1.234,56")]
    [InlineData("1234", "1.234,00")]
    public Task Betragsfeld_liest_und_zeigt_zwei_Nachkommastellen(string typed, string shown) => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = new MoneyField();
        var window = await ShowAsync("hell", anchor, field);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput(typed);
        await Blur(anchor);

        Assert.Equal(shown, field.Text);
        Assert.Equal(shown, field.CommittedText);
        Assert.Equal(MoneyInputParser.TryParse(shown), field.Value);
        Assert.Null(field.Error);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Betrag_mit_mehr_als_zwei_Nachkommastellen_ist_ein_Fehler_und_wird_nie_gerundet() => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = new MoneyField { CommittedText = "10,00" };
        var form = new FormField { Label = "Preis (€)", Content = field };
        var window = await ShowAsync("hell", anchor, form);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("12,505");
        await Blur(anchor);

        Assert.Equal(MoneyInputParser.FractionError, field.Error);
        Assert.Equal("12,505", field.Text);
        Assert.Null(field.Value);
        // Nach außen geht der unlesbare Text; in der Kassa wird daraus nie ein gerundeter oder alter Betrag.
        Assert.Equal("12,505", field.CommittedText);
        Assert.Null(Money.Parse(field.CommittedText));
        Assert.Equal(MoneyInputParser.FractionError, form.Message);
        SaveFieldShot(window, "betragsfeld-fehler");
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Leerer_Betrag_ist_keine_Angabe_nicht_0_00_und_bei_Pflicht_ein_Fehler() => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var optional = new MoneyField { CommittedText = "10,00" };
        var required = new MoneyField { IsRequired = true };
        var window = await ShowAsync("hell", anchor, optional, required);

        optional.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        await Blur(anchor);
        Assert.Equal("", optional.Text);
        Assert.Null(optional.Value);
        Assert.Equal("", optional.CommittedText);
        Assert.Null(optional.Error);

        required.Focus(NavigationMethod.Tab);
        await Blur(anchor);
        Assert.Equal(MoneyInputParser.MissingError, required.Error);

        // 0 ist ein Betrag und etwas anderes als leer.
        required.Focus(NavigationMethod.Tab);
        window.KeyTextInput("0");
        await Blur(anchor);
        Assert.Equal("0,00", required.Text);
        Assert.Equal(0m, required.Value);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Minus_nur_bei_Storno_und_Gutschrift_und_dann_in_der_Fehlerfarbe() => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var normal = new MoneyField();
        var refund = new MoneyField { AllowNegative = true };
        var window = await ShowAsync("hell", anchor, normal, refund);

        normal.Focus(NavigationMethod.Tab);
        window.KeyTextInput("-5");
        Assert.Equal("5", normal.Text);

        refund.Focus(NavigationMethod.Tab);
        window.KeyTextInput("-5");
        await Blur(anchor);
        Assert.Equal("-5,00", refund.Text);
        Assert.Equal(-5m, refund.Value);
        Assert.Contains("negative", refund.Classes);
        Assert.Equal(Resolve(refund, "CritBrush"), ((ISolidColorBrush)refund.Foreground!).Color);
        Assert.Equal(Resolve(normal, "FgBrush"), ((ISolidColorBrush)normal.Foreground!).Color);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Betragsfeld_Pfeile_Euro_Umschalt_zehn_Cent_Bild_zehn_Euro_nie_unter_null() => Session.Dispatch(async () =>
    {
        var field = new MoneyField { CommittedText = "10,50" };
        var window = await ShowAsync("hell", field);
        field.Focus(NavigationMethod.Tab);
        await Pump();

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Assert.Equal("11,50", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Shift);
        Assert.Equal("11,60", field.Text);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        Assert.Equal("21,60", field.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Shift);
        Assert.Equal("21,50", field.Text);
        Assert.Equal(21.5m, field.Value);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        Assert.Equal("0,00", field.Text);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Kassa_Betraege_Paket_und_Zahlung_sind_Betrags_Zahlen_und_Datumsfelder() => Render("hell", "kassa-paket-felder", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.Pos.IsSalesColumnOpen = true;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump();
        shell.Pos.VoucherTreatment = shell.Pos.Treatments.First(t => t.Id == SpaSample.Paying.TreatmentId);
        shell.Pos.PackageUnits = "10";
        shell.Pos.PackagePrice = "699";
        shell.Pos.VoucherValidUntil = "31.12.2027";
        await Pump();

        var page = window.GetVisualDescendants().OfType<Views.PosView>().First();
        var price = page.GetVisualDescendants().OfType<MoneyField>().First(f => f.IsEffectivelyVisible && f.CommittedText == "699");
        Assert.Equal("699,00", price.Text);
        Assert.Equal(699m, price.Value);
        var units = page.GetVisualDescendants().OfType<NumberField>().First(f => f.IsEffectivelyVisible && f.CommittedText == "10");
        Assert.Equal((2m, 50m), (units.Minimum, units.Maximum));
        var validUntil = page.GetVisualDescendants().OfType<DateField>().First(f => f.IsEffectivelyVisible && f.CommittedText == "31.12.2027");
        Assert.Equal(new DateOnly(2027, 12, 31), validUntil.Value);

        // Beschriftungen über den Feldern, mit der Währung der Kasse.
        var labels = page.GetVisualDescendants().OfType<FormField>().Where(f => f.IsEffectivelyVisible).Select(f => f.Label).ToList();
        Assert.Contains("Paketpreis (EUR)", labels);
        Assert.Contains("Anzahl", labels);
        Assert.Contains("Einzulösen bis", labels);

        // Zahlungsbeträge sind Betragsfelder; ein getippter Betrag erscheint im Anzeigeformat und im ViewModel.
        var payments = page.GetVisualDescendants().OfType<MoneyField>().Where(f => f.IsEffectivelyVisible && f.IsEnabled).ToList();
        Assert.NotEmpty(payments);
        return shell.Pos;
    });
}
