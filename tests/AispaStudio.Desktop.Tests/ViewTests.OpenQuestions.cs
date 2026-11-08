using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Slice 56: „Offene Fragen an die Hilfe“ steht auf der Seite „Hilfe“ über dem eingebetteten Browser, nur für Inhaber, Manager und Standortleitung.</summary>
public sealed partial class ViewTests
{
    /// <summary>Tauscht die Ablage der offenen Fragen gegen eine im Speicher mit zwei Fragen und stellt sie danach wieder her.</summary>
    private static async Task<T> WithOpenQuestions<T>(Func<MemoryUnansweredQuestionStore, Task<T>> run)
    {
        var store = new MemoryUnansweredQuestionStore();
        store.Record(new("Wie färbe ich die Wolken lila?", "kalender", new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero)));
        store.Record(new("Wo stelle ich den Duft ein?", "kassa", new(2026, 10, 18, 8, 0, 0, TimeSpan.Zero)));
        var previous = UnansweredQuestionLog.Current;
        UnansweredQuestionLog.Current = store;
        try
        {
            return await run(store);
        }
        finally
        {
            UnansweredQuestionLog.Current = previous;
        }
    }

    [Fact]
    public Task Given_die_Liste_als_Rezeption_When_sie_aufgerufen_wird_Then_ist_sie_nicht_sichtbar() => WithOpenQuestions(_ => Render("hell", "hilfe-offene-fragen-rezeption", async (window, main) =>
    {
        var shell = await OpenPage(main, "Hilfe");

        var view = window.GetVisualDescendants().OfType<HelpView>().Single();
        Assert.False(view.FindControl<Border>("OpenQuestionsBar")!.IsEffectivelyVisible);
        Assert.DoesNotContain("Wie färbe ich die Wolken lila?", TextsIn(view));
        return shell;
    }, ReceptionApi()));

    [Fact]
    public Task Given_die_Liste_als_Leitung_When_sie_aufgerufen_wird_Then_ist_sie_sichtbar_mit_Erledigt_je_Eintrag() => WithOpenQuestions(store => Render("hell", "hilfe-offene-fragen", async (window, main) =>
    {
        var shell = await OpenPage(main, "Hilfe");
        var view = window.GetVisualDescendants().OfType<HelpView>().Single();
        Assert.True(view.FindControl<Border>("OpenQuestionsBar")!.IsEffectivelyVisible);

        var toggle = view.FindControl<ToggleButton>("OpenQuestionsToggle")!;
        Assert.Equal("Offene Fragen an die Hilfe (2)", toggle.Content);
        toggle.IsChecked = true;
        await Pump();

        var list = view.FindControl<ItemsControl>("OpenQuestionList")!;
        var done = list.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Content as string == "Erledigt").ToList();
        Assert.Equal(2, done.Count);
        Assert.Contains("Wie färbe ich die Wolken lila?", TextsIn(list));
        Assert.Contains(TextsIn(list), t => t.Contains("19.10.2026", StringComparison.Ordinal));

        Invoke(done.Single(b => AutomationPropertiesName(b) == "Erledigt: Wo stelle ich den Duft ein?"));
        await Pump();

        Assert.Equal(["Wie färbe ich die Wolken lila?"], store.List(SpaSample.At(11, 40)).Select(q => q.Question));
        Assert.Equal("Offene Fragen an die Hilfe (1)", toggle.Content);
        return shell;
    }, Api().On("GET /api/auth/me", TestData.Manager())));
}
