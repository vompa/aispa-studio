using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Betrags- und Zahlenfelder melden einen gültigen Wert schon beim Tippen (Slice 130, Ä311); Anzeigeformat und Fehler kommen erst beim
/// Bestätigen, Esc stellt den bestätigten Wert her. Datum und Uhrzeit melden weiter erst beim Bestätigen.
/// </summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Betragsfeld_meldet_getippte_5_sofort_und_zeigt_5_00_erst_nach_Tab() => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = new MoneyField { CommittedText = "10,00" };
        var window = await ShowAsync("hell", anchor, field);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("5");
        await Pump();
        var typedText = field.Text;
        var typedValue = field.Value;
        var typedCommitted = field.CommittedText;
        await Blur(anchor);

        Assert.Equal("5", typedText);
        Assert.Equal(5m, typedValue);
        Assert.Equal("5,00", typedCommitted);
        Assert.Equal("5,00", field.Text);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Betragsfeld_mit_12_345_meldet_keinen_Wert_und_den_Fehler_erst_beim_Verlassen() => Session.Dispatch(async () =>
    {
        var anchor = new Button { Content = "Anker" };
        var field = new MoneyField { CommittedText = "10,00" };
        var window = await ShowAsync("hell", anchor, field);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("12,345");
        await Pump();
        var typedValue = field.Value;
        var typedError = field.Error;
        await Blur(anchor);

        Assert.Null(typedValue);
        Assert.Null(typedError);
        Assert.Equal("Höchstens zwei Nachkommastellen.", field.Error);
        Assert.Null(field.Value);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Betragsfeld_Esc_nach_getippten_35_stellt_20_00_auch_in_der_Bindung_her() => Session.Dispatch(async () =>
    {
        var holder = new AmountHolder { Amount = 20m };
        var field = new MoneyField();
        field.Bind(MoneyField.ValueProperty, new Binding(nameof(AmountHolder.Amount)) { Source = holder, Mode = BindingMode.TwoWay });
        var window = await ShowAsync("hell", field);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("35");
        await Pump();
        var whileTyping = holder.Amount;
        var caretWhileTyping = field.CaretIndex;
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();

        Assert.Equal(35m, whileTyping);
        // Das Melden setzt den Text nicht neu: die Marke steht weiter hinter der getippten 35.
        Assert.Equal(2, caretWhileTyping);
        Assert.Equal("20,00", field.Text);
        Assert.Equal(20m, holder.Amount);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Betragsfeld_meldet_leer_sobald_die_getippten_Ziffern_geloescht_sind() => Session.Dispatch(async () =>
    {
        var field = new MoneyField { CommittedText = "10,00" };
        var window = await ShowAsync("hell", field);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("7");
        await Pump();
        var typedValue = field.Value;
        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        await Pump();

        Assert.Equal(7m, typedValue);
        Assert.Equal("", field.Text);
        Assert.Null(field.Value);
        Assert.Equal("", field.CommittedText);
        Assert.Null(field.Error);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Zahlenfeld_meldet_erst_einen_Wert_wenn_der_getippte_Text_im_Bereich_liegt() => Session.Dispatch(async () =>
    {
        var field = new NumberField { Minimum = 10, Maximum = 99, CommittedText = "20" };
        var window = await ShowAsync("hell", field);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("1");
        await Pump();
        var belowMinimum = field.Value;
        window.KeyTextInput("5");
        await Pump();

        Assert.Null(belowMinimum);
        Assert.Equal(15m, field.Value);
        Assert.Equal("15", field.Text);
        Assert.Null(field.Error);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Uhrzeitfeld_meldet_beim_Tippen_noch_nichts() => Session.Dispatch(async () =>
    {
        var field = new TimeField { CommittedText = "09:00" };
        var window = await ShowAsync("hell", field);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("14");
        await Pump();

        Assert.Equal("14", field.Text);
        Assert.Equal("09:00", field.CommittedText);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Datumsfeld_meldet_beim_Tippen_noch_nichts() => Session.Dispatch(async () =>
    {
        var field = new DateField { TodayOverride = FieldToday, CommittedText = "21.10.2026" };
        var window = await ShowAsync("hell", field);

        field.Focus(NavigationMethod.Tab);
        window.KeyTextInput("1");
        await Pump();

        Assert.Equal("1", field.Text);
        Assert.Equal("21.10.2026", field.CommittedText);
        Assert.Equal(new DateOnly(2026, 10, 21), field.Value);
        window.Close();
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Kassa_Wertgutschein_verkaufen_geht_mit_getipptem_Betrag_ohne_Feldwechsel()
    {
        var stub = Api().On($"POST /api/pos/bills/{SpaSample.BillId}/lines", SpaSample.Bill(withArticle: false, voucher: 50m));
        return Render("hell", "kassa-wertgutschein-getippt", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            shell.Pos.IsSalesColumnOpen = true;
            await shell.CheckoutAsync(SpaSample.Paying);
            await Pump(Settling);
            var page = window.GetVisualDescendants().OfType<PosView>().First();
            var field = (MoneyField)page.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == "Wert (EUR)").Content!;
            var sell = page.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && ReferenceEquals(b.Command, shell.Pos.SellVoucherCommand));
            field.BringIntoView();
            await Pump();

            field.Focus(NavigationMethod.Tab);
            window.KeyTextInput("50");
            await Pump();
            var enabledWhileTyping = sell.IsEffectivelyEnabled;
            var focusStayed = field.IsFocused;
            ClickCenter(window, sell);
            await Pump(Settling);

            Assert.True(focusStayed);
            Assert.True(enabledWhileTyping);
            var body = stub.Calls.Single(c => c.Route.EndsWith("/lines", StringComparison.Ordinal)).Body!;
            Assert.Contains("\"voucherKind\":\"MultiPurpose\"", body, StringComparison.Ordinal);
            Assert.Matches("\"unitPrice\":50(\\.0+)?[,}]", body);
            return shell.Pos;
        }, stub);
    }

    /// <summary>Ein gebundener Betrag wie in einer Maske (TwoWay an <c>Value</c>).</summary>
    private sealed class AmountHolder
    {
        public decimal? Amount { get; set; }
    }
}
