using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Vorschlag der Kassa: eine laufende Aktion, die zu Positionen der offenen Rechnung passt. <c>Title</c> nennt Aktion und Rabatt
/// („Aktion „Herbst“ −10 %“), <c>Detail</c> die Wirkung auf die Rechnung; angewendet wird nur mit dem Knopf.
/// </summary>
public sealed record PromotionHint(PromotionOfferDto Offer, string Title, string Detail)
{
    /// <summary>Name des Knopfes für Screenreader, damit nicht nur „Anwenden“ vorgelesen wird.</summary>
    public string ApplyName => $"{Title} anwenden";
}

// ---------- Aktionen (ADR 0071): die Kassa schlägt die laufende Aktion vor, der Rabatt wird auf Klick gesetzt und vom Server gerechnet ----------
public partial class PosViewModel
{
    public ObservableCollection<PromotionHint> PromotionHints { get; } = [];

    public bool HasPromotionHints => PromotionHints.Count > 0;

    /// <summary>Fragt die Vorschläge zur offenen Rechnung ab; ohne Antwort oder ohne passende Aktion gibt es keinen Hinweis.</summary>
    private async Task LoadPromotionsAsync(BillDto? bill)
    {
        if (bill is not { Status: BillStatus.Open, Open: > 0 })
        {
            ShowPromotions([]);
            return;
        }

        var result = await _api.GetAsync<List<PromotionOfferDto>>($"api/pos/bills/{bill.Id}/promotions").ConfigureAwait(true);
        if (Bill?.Id != bill.Id)
        {
            return;
        }

        ShowPromotions([.. (result.Value ?? []).Select(Hint)]);
    }

    private PromotionHint Hint(PromotionOfferDto offer)
    {
        var positions = offer.Lines.Count == 1 ? "1 Position" : $"{offer.Lines.Count} Positionen";
        return new PromotionHint(offer, $"Aktion „{offer.Name}“ {offer.DiscountText}", $"{positions}, zusammen −{Format(offer.TotalDiscount)}");
    }

    private void ShowPromotions(List<PromotionHint> hints)
    {
        PromotionHints.Clear();
        foreach (var hint in hints)
        {
            PromotionHints.Add(hint);
        }

        OnPropertyChanged(nameof(HasPromotionHints));
    }

    /// <summary>Wendet die Aktion an: der Server setzt den Rabatt der passenden Positionen und nennt die Aktion auf dem Beleg (nie ohne Klick).</summary>
    [RelayCommand]
    private async Task ApplyPromotionAsync(PromotionHint hint)
    {
        if (Bill is null || CardBlocks())
        {
            return;
        }

        var result = await _api.PostAsync<BillDto>($"api/pos/bills/{Bill.Id}/promotions/{hint.Offer.PromotionId}/apply", null).ConfigureAwait(true);
        await ShowBillAsync(result).ConfigureAwait(true);
    }
}
