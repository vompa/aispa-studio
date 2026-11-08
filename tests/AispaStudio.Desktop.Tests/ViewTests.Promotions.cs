using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kassa mit Aktionsvorschlag (ADR 0071) in beiden Farbschemata: Hinweis nach Design-Brief 5.9 mit Knopf „Anwenden“, lesbar, Bild für das Handbuch.</summary>
public sealed partial class ViewTests
{
    private sealed class PromotionTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kassa_zeigt_den_Aktionsvorschlag_als_Hinweis_mit_Knopf_Anwenden(string theme)
    {
        var offer = PosPromotionTests.Offer();
        var stub = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
            .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
            .On("GET /api/pos/articles", SpaSample.Articles)
            .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
            .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>())
            .On("POST /api/pos/bills", SpaSample.Bill())
            .On($"GET /api/pos/bills/{SpaSample.BillId}/promotions", new List<PromotionOfferDto> { offer });
        var pos = new PosViewModel(stub.Client(), new PromotionTime());
        return InWindow(theme, () => new PosView { DataContext = pos }, 1280, 760, async window =>
        {
            await pos.InitializeAsync(SpaSample.LocationId);
            await pos.AddAppointmentAsync(SpaSample.Paying);
            for (var i = 0; i < 100 && !pos.HasPromotionHints; i++)
            {
                await Task.Delay(10);
            }

            await Pump(Settling);

            var button = window.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Aktion „Herbst“ −10 % anwenden");
            Assert.True(button.IsEffectivelyVisible && button.IsEnabled);
            Assert.Equal("Anwenden", button.Content);
            var title = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Aktion „Herbst“ −10 %");
            var detail = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "1 Position, zusammen −8,90 €");
            var box = title.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("hint"));

            // Hinweis nach 5.9 (neutral): weiche Akzentfläche, Rahmen line, Radius 8; Titel und Nebentext bleiben lesbar.
            Assert.Equal(new CornerRadius(8), box.CornerRadius);
            Assert.Equal(ColorOf(window, "AccentSoftBrush"), ColorOf(box.Background));
            Assert.Equal(FontWeight.SemiBold, title.FontWeight);
            Assert.True(Contrast(ColorOf(window, "FgBrush"), ColorOf(box.Background)) >= 4.5, "Titel gegen die Fläche des Hinweises");
            Assert.True(Contrast(ColorOf(window, "Fg2Brush"), ColorOf(box.Background)) >= 4.5, "Nebentext gegen die Fläche des Hinweises");
            Assert.Equal(12, detail.FontSize);

            // Der Hinweis steht in der Fußzeile der Rechnung, über der Summe, und nimmt dem Knopf „Bezahlen“ nichts weg.
            var total = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Summe");
            Assert.True(box.TranslatePoint(new(0, 0), window)!.Value.Y < total.TranslatePoint(new(0, 0), window)!.Value.Y);
            Assert.True(button.Command!.CanExecute(button.CommandParameter));
        }, "kassa-aktion");
    }
}
