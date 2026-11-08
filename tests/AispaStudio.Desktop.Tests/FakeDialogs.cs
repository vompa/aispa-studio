using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Beantwortet Rückfragen (Design-Brief 5.27) mit vorbereiteten Antworten und merkt sich, was gefragt wurde; ohne Antwort gilt „Abbrechen“.</summary>
internal sealed class FakeDialogs : IConfirmDialogs
{
    private readonly Queue<ConfirmResult> _answers = new();

    public List<ConfirmRequest> Asked { get; } = [];

    public FakeDialogs Answer(ConfirmChoice choice, string? input = null)
    {
        _answers.Enqueue(new ConfirmResult(choice, input));
        return this;
    }

    private static readonly string[] ForbiddenAnswers = ["Ja", "Nein", "OK", "Ok"];

    /// <summary>Die Regeln für jede Rückfrage: Titel als Frage, Antworten in ein bis vier Wörtern und höchstens 26 Zeichen, nie „Ja“, „Nein“ oder „OK“, die Folge im Text.</summary>
    public static void AssertRules(ConfirmRequest request)
    {
        Assert.EndsWith("?", request.Title, StringComparison.Ordinal);
        foreach (var answer in new[] { request.ConfirmText, request.CancelText, request.AlternativeText }.OfType<string>())
        {
            Assert.DoesNotContain(answer, ForbiddenAnswers);
            Assert.True(answer.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4, answer);
            Assert.True(answer.Length <= 26, answer);
        }

        Assert.NotEmpty(request.Message);
        Assert.False(request.Message.StartsWith(request.Title.TrimEnd('?'), StringComparison.Ordinal), "Der Text wiederholt den Titel nicht");
    }

    /// <summary>Alles, was gefragt wurde, folgt den Regeln.</summary>
    public FakeDialogs AssertRules()
    {
        Assert.NotEmpty(Asked);
        Asked.ForEach(AssertRules);
        return this;
    }

    public Task<ConfirmResult> AskAsync(ConfirmRequest request)
    {
        Asked.Add(request);
        return Task.FromResult(_answers.Count > 0 ? _answers.Dequeue() : ConfirmResult.Cancelled);
    }

    /// <summary>Der offene Dialog mit eigenem Inhalt (z. B. die Absage wegen einer Schließzeit); der Test bedient ihn wie eine Person und beantwortet damit die Rückfrage.</summary>
    public ConfirmDialogViewModel? Open { get; private set; }

    /// <summary>Alle gezeigten Dialoge mit eigenem Inhalt.</summary>
    public List<ConfirmDialogViewModel> Shown { get; } = [];

    public Task<ConfirmResult> ShowAsync(Func<Action<ConfirmResult>, ConfirmDialogViewModel> create)
    {
        var source = new TaskCompletionSource<ConfirmResult>();
        var dialog = create(result =>
        {
            Open = null;
            source.TrySetResult(result);
        });
        Open = dialog;
        Shown.Add(dialog);
        return source.Task;
    }
}
