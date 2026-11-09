using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kassenlade (L6): Den Tab „Entnahme“ sieht nur, wer die Kassenaufsicht hat; die Rezeption sieht nur „Einlage“ und kommt nicht erst beim Buchen an die Serverregel.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Entnahme_in_der_Kassenlade_sieht_nur_die_Kassenaufsicht() => Render("hell", "kassa-einlage-ohne-entnahme", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        var pos = shell.Pos;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump();
        pos.ShowClosingCommand.Execute(null);
        await pos.LoadReportAsync();
        await Pump();

        TabButton Tab(string content) => window.GetVisualDescendants().OfType<TabButton>().Single(t => Equals(t.Content, content));
        Assert.True(pos.MaySuperviseCash);
        Assert.True(Tab("Entnahme").IsEffectivelyVisible);
        Assert.Equal("Einlage oder Entnahme", pos.MovementTitle);

        pos.MovementIsWithdrawal = true;
        pos.MaySuperviseCash = false;
        await Pump();

        Assert.False(Tab("Entnahme").IsEffectivelyVisible);
        // Befund B19: Bei nur einer Option steht kein Reiter „Einlage“ unter der Überschrift „Einlage“.
        Assert.False(Tab("Einlage").IsEffectivelyVisible);
        Assert.False(pos.MovementIsWithdrawal);
        Assert.Equal("Einlage", pos.MovementTitle);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Einlage" && t.Classes.Contains("h2") && t.IsEffectivelyVisible);
        return pos;
    });
}
