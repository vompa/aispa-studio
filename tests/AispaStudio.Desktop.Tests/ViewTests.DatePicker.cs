using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Datumswahl (Kalenderblatt im Flyout): Tage des Nachbarmonats bleiben lesbar (Brief 10: fg-3 nie als Text unter 24 px, W60).</summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Datumswahl_zeigt_Tage_des_Nachbarmonats_in_Fg2(string theme) =>
        InWindow(theme, () => new Calendar { DisplayDate = new DateTime(2026, 10, 15), SelectedDate = new DateTime(2026, 10, 15) }, 400, 400, async window =>
        {
            var outside = window.GetVisualDescendants().OfType<CalendarDayButton>().Where(d => d.IsEffectivelyVisible && d.Classes.Contains(":inactive")).ToList();
            Assert.NotEmpty(outside);
            foreach (var day in outside)
            {
                var presenter = day.GetVisualDescendants().OfType<ContentPresenter>().Single(p => p.Name == "PART_ContentPresenter");
                Assert.Equal(ColorOf(window, "Fg2Brush"), ColorOf(presenter.Foreground));
            }

            await Task.CompletedTask;
        });
}
