using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Offene Fragen an die Hilfe (Slice 56): eine Frage ohne Treffer legt die Windows-App auf diesem PC ab, so wie der Server sie bereinigt hat,
/// mit Seite und Zeit; die Liste sehen nur Inhaber, Manager und Standortleitung.
/// </summary>
public sealed class UnansweredQuestionTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 3, 9, 30, 0, TimeSpan.Zero);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static AssistantAnswer NoHit(string? unanswered) => new(AssistantAudience.Management, "Dazu habe ich nichts gefunden.", [], [],
        "Antwort eines KI-Assistenten.", [], Unanswered: unanswered);

    private static async Task<MemoryUnansweredQuestionStore> AskAsync(AssistantAnswer answer, string question)
    {
        var store = new MemoryUnansweredQuestionStore();
        var stub = new StubApi().On("POST /api/assistant", answer);
        var companion = new CompanionViewModel(stub.Client(), new MemorySettingsStore(), true, AssistantAudience.Management,
            time: new FixedTime(Now), questions: store);
        companion.ShowPage("kassa");
        companion.Question = question;
        await companion.AskCommand.ExecuteAsync(null);
        return store;
    }

    [Fact]
    public async Task Given_eine_Frage_ohne_Antwort_When_der_Assistent_sie_meldet_Then_liegt_sie_ohne_Gaestedaten_mit_Zeit_und_Seite_im_lokalen_Speicher()
    {
        // Der Server hat den Namen schon ersetzt; abgelegt wird sein Text, nie die Eingabe.
        var store = await AskAsync(NoHit("Wie färbe ich die Wolken für [Name] lila?"), "Wie färbe ich die Wolken für Huber lila?");

        var entry = Assert.Single(store.List(Now));
        Assert.Equal(new UnansweredQuestion("Wie färbe ich die Wolken für [Name] lila?", "kassa", Now, 1), entry);
        Assert.DoesNotContain("Huber", entry.Question, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eine_beantwortete_Frage_legt_nichts_ab()
    {
        var store = await AskAsync(NoHit(null), "Wie lange haben wir heute offen?");

        Assert.Empty(store.List(Now));
    }

    [Fact]
    public void Die_Datei_vergisst_nach_90_Tagen_fasst_gleiche_Fragen_zusammen_und_Erledigt_loescht()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"aispa-unanswered-{Guid.NewGuid():N}");
        var path = Path.Combine(folder, "unanswered-questions.jsonl");
        try
        {
            var store = new FileUnansweredQuestionStore(path);
            var first = new DateTimeOffset(2027, 1, 1, 10, 0, 0, TimeSpan.Zero);
            store.Record(new("Alt", null, first));
            store.Record(new("Wie färbe ich die Wolken lila?", "kalender", new(2027, 3, 1, 10, 0, 0, TimeSpan.Zero)));
            store.Record(new("wie färbe ich die wolken lila?", "kalender", new(2027, 3, 2, 10, 0, 0, TimeSpan.Zero)));
            store.Record(new("Wie färbe ich die Wolken lila?", "kassa", new(2027, 3, 1, 10, 0, 0, TimeSpan.Zero)));

            // 1.1. + 90 Tage = 1.4.: am 2.4. ist „Alt“ weg; die neueste Frage steht oben.
            var list = new FileUnansweredQuestionStore(path).List(new(2027, 4, 2, 10, 0, 0, TimeSpan.Zero));
            Assert.Equal(
                [
                    new UnansweredQuestion("wie färbe ich die wolken lila?", "kalender", new(2027, 3, 2, 10, 0, 0, TimeSpan.Zero), 2),
                    new UnansweredQuestion("Wie färbe ich die Wolken lila?", "kassa", new(2027, 3, 1, 10, 0, 0, TimeSpan.Zero), 1),
                ],
                list);

            store.Remove(list[0]);
            Assert.Equal(["kassa"], store.List(new(2027, 4, 2, 10, 0, 0, TimeSpan.Zero)).Select(q => q.Page));
            Assert.DoesNotContain("Alt", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Fact]
    public void Eine_kaputte_Zeile_in_der_Datei_kostet_nur_sich_selbst()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"aispa-unanswered-{Guid.NewGuid():N}");
        var path = Path.Combine(folder, "unanswered-questions.jsonl");
        try
        {
            var store = new FileUnansweredQuestionStore(path);
            store.Record(new("Erste Frage", "kassa", Now));
            File.AppendAllText(path, "{\"question\": \"abgebrochen" + Environment.NewLine);

            store.Record(new("Zweite Frage", "kalender", Now.AddMinutes(1)));

            Assert.Equal(["Zweite Frage", "Erste Frage"], store.List(Now.AddMinutes(2)).Select(q => q.Question));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Theory]
    [InlineData(StaffRole.Owner, true)]
    [InlineData(StaffRole.GeneralManager, true)]
    [InlineData(StaffRole.Manager, true)]
    [InlineData(StaffRole.Reception, false)]
    [InlineData(StaffRole.Therapist, false)]
    [InlineData(StaffRole.Backoffice, false)]
    public void Die_Liste_sehen_nur_Inhaber_Manager_und_Standortleitung(StaffRole role, bool visible)
    {
        var store = new MemoryUnansweredQuestionStore();
        store.Record(new("Wie färbe ich die Wolken lila?", "kalender", DateTimeOffset.UtcNow));
        var help = new HelpViewModel(() => null, Path.GetTempPath(), store) { Role = role };

        help.EnsureShown();

        Assert.Equal(visible, help.CanReviewQuestions);
        Assert.Equal(visible ? 1 : 0, help.OpenQuestions.Count);
    }
}
