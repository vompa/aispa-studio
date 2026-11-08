using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Vorschlag der Kassa (Slice 115): ein Verkaufsartikel, der zu einer Behandlung auf der Rechnung passt und am Verkaufsregal liegt. <c>Title</c> nennt den
/// Artikel („Passt dazu: Massageöl Lavendel 100 ml“), <c>Detail</c> Preis und Bestand am Regal; auf die Rechnung kommt er nur mit dem Knopf.
/// </summary>
public sealed record RecommendationHint(RecommendedArticleDto Article, string Title, string Detail)
{
    /// <summary>Name des Knopfes für Screenreader, damit nicht nur „Dazugeben“ vorgelesen wird.</summary>
    public string AddName => $"{Article.Name} dazugeben";
}

// ---------- „Passt dazu“ (Slice 115): die API schlägt Artikel zu den Behandlungen der Rechnung vor, der Klick legt eine Artikelzeile an ----------
public partial class PosViewModel
{
    public ObservableCollection<RecommendationHint> RecommendationHints { get; } = [];

    public bool HasRecommendationHints => RecommendationHints.Count > 0;

    /// <summary>Fragt die Vorschläge zur offenen Rechnung ab; ohne Antwort oder ohne Vorschlag gibt es keinen Hinweis.</summary>
    private async Task LoadRecommendationsAsync(BillDto? bill)
    {
        if (bill is not { Status: BillStatus.Open })
        {
            ShowRecommendations([]);
            return;
        }

        var result = await _api.GetAsync<List<RecommendedArticleDto>>($"api/pos/bills/{bill.Id}/recommendations").ConfigureAwait(true);
        if (Bill?.Id != bill.Id)
        {
            return;
        }

        ShowRecommendations([.. (result.Value ?? []).Select(Hint)]);
    }

    private RecommendationHint Hint(RecommendedArticleDto article) =>
        new(article, $"Passt dazu: {article.Name}",
            $"{Format(article.Price)} · {article.ShelfQuantity.ToString("0.###", CultureInfo.GetCultureInfo("de-AT"))} {article.Unit} am Regal");

    private void ShowRecommendations(List<RecommendationHint> hints)
    {
        RecommendationHints.Clear();
        foreach (var hint in hints)
        {
            RecommendationHints.Add(hint);
        }

        OnPropertyChanged(nameof(HasRecommendationHints));
    }

    /// <summary>Legt den vorgeschlagenen Artikel mit Menge 1 auf die Rechnung; danach schlägt die API ihn nicht mehr vor.</summary>
    [RelayCommand]
    private Task AddRecommendationAsync(RecommendationHint hint) => AddArticleByIdAsync(hint.Article.ArticleId);

    /// <summary>Ein Artikel per Id auf die Rechnung, z. B. „Mit kassieren“ aus dem Termin-Panel.</summary>
    public Task AddArticleByIdAsync(Guid articleId) =>
        AddLineAsync(new BillLineInput(BillLineKind.Article, null, articleId, 1, null, 0, null, null, null, null, null));
}
