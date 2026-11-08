using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Vorschlag im Termin-Panel (Slice 115): „Massageöl Lavendel 100 ml · 24,90 € · 4 am Regal“ mit dem Knopf „Mit kassieren“.</summary>
public sealed record PanelRecommendation(RecommendedArticleDto Article, string Text)
{
    /// <summary>Name des Knopfes für Screenreader, damit nicht nur „Mit kassieren“ vorgelesen wird.</summary>
    public string CheckoutName => $"{Article.Name} mit kassieren";
}

// ---------- „Passt dazu“ (Slice 115): nach dem Abschließen schlägt das Panel Artikel mit Bestand am Verkaufsregal vor ----------
public partial class AppointmentViewModel
{
    private bool _recommendationsLoaded;

    /// <summary>„Mit kassieren“: öffnet die Kassa mit dem Termin und legt den Artikel dazu; ohne Recht zu kassieren leer.</summary>
    public Func<AppointmentDto, Guid, Task>? CheckoutWithArticleRequested { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecommendations))]
    public partial IReadOnlyList<PanelRecommendation> Recommendations { get; private set; } = [];

    public bool HasRecommendations => Recommendations.Count > 0;

    /// <summary>
    /// Lädt die Vorschläge für einen erledigten Termin. Baut ein Live-Echo das Panel desselben Termins neu auf (<paramref name="previous"/>), übernimmt es die
    /// geladenen Vorschläge, damit der Hinweis nicht verschwindet und wiederkommt.
    /// </summary>
    public async Task LoadRecommendationsAsync(AppointmentViewModel? previous)
    {
        if (!IsDone || CheckoutWithArticleRequested is null)
        {
            return;
        }

        if (previous is { IsDone: true, _recommendationsLoaded: true } && previous.Appointment.Id == Appointment.Id
            && previous.Appointment.TreatmentId == Appointment.TreatmentId)
        {
            Recommendations = previous.Recommendations;
            _recommendationsLoaded = true;
            return;
        }

        var result = await Api.GetAsync<List<RecommendedArticleDto>>($"api/appointments/{Appointment.Id}/recommendations").ConfigureAwait(true);
        _recommendationsLoaded = result.Value is not null;
        Recommendations = [.. (result.Value ?? []).Select(a => new PanelRecommendation(a,
            $"{a.Name} · {Money.Format(a.Price, currency)} · {a.ShelfQuantity.ToString("0.###", CultureInfo.GetCultureInfo("de-AT"))} am Regal"))];
    }

    [RelayCommand]
    private Task CheckoutWithArticleAsync(PanelRecommendation recommendation) =>
        CheckoutWithArticleRequested?.Invoke(Appointment, recommendation.Article.ArticleId) ?? Task.CompletedTask;
}
