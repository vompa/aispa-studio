using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kassieren ohne Verbindung in der Ansicht (ADR 0096): Hinweis beim Bezahlen und der Ersatzbeleg.</summary>
public partial class ViewTests
{
    private static Task<bool> SubstitutePos(string name, Func<Window, PosViewModel, Task> act)
    {
        var stub = Api();
        var offline = new OfflineCache(new MemoryOfflineStore(), new SampleTime());
        return Render("hell", name, async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await shell.Pos.InitializeAsync(SpaSample.LocationId);
            stub.Down = true;
            await shell.CheckoutAsync(SpaSample.Paying);
            await Pump(Settling);
            await act(window, shell.Pos);
            return shell.Pos;
        }, stub, offline);
    }

    [Fact]
    public Task Kassa_Given_keine_Verbindung_When_Termin_kassiert_wird_Then_steht_beim_Bezahlen_der_Hinweis_Ersatzbeleg() =>
        SubstitutePos("kassa-ersatzbeleg-bezahlen", async (window, pos) =>
        {
            Assert.True(pos.IsSubstitute);
            var info = window.GetVisualDescendants().OfType<InfoBar>().Single(b => b.Name == "SubstituteInfo");
            Assert.True(info.IsEffectivelyVisible);
            Assert.Equal("Ersatzbeleg", info.Title);
            Assert.Equal(PosViewModel.SubstituteHint, info.Message);
            pos.FillRestCommand.Execute(pos.Payments.First(p => p.Method == PaymentMethod.Cash));
            await Pump();
            Assert.True(pos.CanPay);
        });

    [Fact]
    public Task Kassa_Given_keine_Verbindung_When_bar_bezahlt_Then_zeigt_die_Kassa_den_Ersatzbeleg_mit_Nummer_und_ohne_Signatur() =>
        SubstitutePos("kassa-ersatzbeleg", async (window, pos) =>
        {
            pos.Payments.First(p => p.Method == PaymentMethod.Cash).Amount = "100";
            await pos.PayCommand.ExecuteAsync(null);
            await Pump(Settling);

            Assert.True(pos.Receipt!.IsSubstitute);
            var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
            Assert.Contains(texts, t => t?.StartsWith("Ersatzbeleg E-WIEN-1-", StringComparison.Ordinal) == true);
            Assert.Contains("Ersatzbeleg, der signierte Beleg folgt", texts);
            Assert.DoesNotContain("Signiert nach RKSV", texts);
        });

    [Fact]
    public Task Kassa_Given_Ersatzbeleg_ausgegeben_When_Belege_geoeffnet_Then_steht_er_in_der_Liste_Ersatzbelege_und_laesst_sich_zeigen() =>
        SubstitutePos("kassa-ersatzbelege", async (window, pos) =>
        {
            pos.Payments.First(p => p.Method == PaymentMethod.Cash).Amount = "100";
            await pos.PayCommand.ExecuteAsync(null);
            pos.ShowReceiptsCommand.Execute(null);
            await Pump(Settling);

            // Then steht der Block „Ersatzbelege“ über den Belegen des Tages
            var list = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "SubstituteList");
            Assert.True(list.IsEffectivelyVisible);
            var texts = () => window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
            Assert.Contains("Ersatzbelege, noch nicht nacherfasst (1)", texts());
            Assert.Contains(PosViewModel.SubstituteWaiting, texts());

            // When er gewählt wird, Then steht er rechts mit „Drucken“, ohne Storno
            pos.SelectedSubstitute = pos.Substitutes[0];
            await Pump(Settling);
            Assert.Contains("Ersatzbeleg, der signierte Beleg folgt", texts());
            Assert.DoesNotContain("Beleg stornieren", texts());
        });
}
