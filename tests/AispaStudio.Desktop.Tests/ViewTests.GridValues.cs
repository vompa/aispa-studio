using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Runde R02, Slice 32 (W23, W41, W59): Hinweise, Seitenleisten und Formularabstände folgen dem Raster des Design-Briefs (4.1, 4.2, 5.9, 5.12).</summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task InfoBar_hat_Radius_8_und_Innenabstand_10_12(string theme) => InWindow(theme,
        () => new InfoBar { Severity = InfoBarSeverity.Warning, Message = "Nur mit Verbindung." }, 400, 120, async window =>
        {
            var bar = (InfoBar)window.Content!;
            await Pump();
            Assert.Equal(new CornerRadius(8), bar.CornerRadius);
            Assert.Equal((12.0, 10.0, 10.0), (bar.Padding.Left, bar.Padding.Top, bar.Padding.Bottom));
        });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Seitenleiste_hat_Rahmen_links_ohne_Schatten_und_Radius(string theme) => InWindow(theme,
        () => new Border { Classes = { "sidepanel", "open" }, Width = 360 }, 600, 400, async window =>
        {
            var panel = (Border)window.Content!;
            await Pump();
            Assert.Equal(new Thickness(1, 0, 0, 0), panel.BorderThickness);
            Assert.Equal(new CornerRadius(0), panel.CornerRadius);
            Assert.Equal(default, panel.BoxShadow);
            Assert.Equal(ColorOf(window, "SurfaceBrush"), ColorOf(panel.Background));
            Assert.Equal(ColorOf(window, "LineBrush"), ColorOf(panel.BorderBrush));
        });

    [Fact]
    public Task Kundenformular_haelt_12_20_8_und_den_Hilfetext_auf_4() => InWindow("hell",
        () => new CustomerContactForm { DataContext = new CustomerContactFormViewModel(Api().Client(), SpaSample.Customers[0], [FiscalCountry.AT], _ => Task.CompletedTask, () => { }) },
        640, 900, async window =>
        {
            var form = (CustomerContactForm)window.Content!;
            await Pump();
            var groups = form.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "Fields");
            var nameGroup = (StackPanel)groups.Children[0];
            var rows = (StackPanel)nameGroup.Children[1];
            Assert.Equal((20.0, 8.0, 12.0), (groups.Spacing, nameGroup.Spacing, rows.Spacing));

            // Hilfetext 4 px unter der Namenszeile (Brief 4.1 „Feld zu Hilfetext 4“), trotz Zeilenabstand 12.
            var nameRow = rows.Children[0];
            var help = rows.Children[1];
            Assert.Equal(4, help.Bounds.Top - nameRow.Bounds.Bottom, 0.5);
        });
}
