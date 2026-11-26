using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Management-Bericht als PDF (T-095, Paket D2, ADR 0092) ----------
public sealed partial class ManagementReportViewModel
{
    /// <summary>
    /// Der Knopfblock „Drucken · PDF speichern · Per Mail senden“ für den geladenen Bericht (Standort, Zeitraum, Vergleich und, wenn sichtbar, der KI-Kommentar);
    /// null, solange kein Bericht geladen ist. Senden geht an die Adresse, die die Person im Dialog stehen lässt oder eintippt (vorbelegt ist ihre eigene).
    /// </summary>
    [ObservableProperty]
    public partial DocumentActionsViewModel? Document { get; private set; }

    /// <summary>Der Toast der Shell für „Gespeichert: …“, „Druckdialog geöffnet“, „Gesendet an …“.</summary>
    public Action<string>? ShowToast { get; init; }

    /// <summary>Drucken, PDF und Mail stehen im Menü „Ausgabe“, sobald ein Bericht geladen ist (T-104).</summary>
    public bool HasDocument => Document is not null;

    partial void OnDocumentChanged(DocumentActionsViewModel? value) => OnPropertyChanged(nameof(HasDocument));

    /// <summary>
    /// Baut die Befehle für Drucken, PDF und Mail für den gerade gezeigten Bericht neu. Sie stehen im Menü „Ausgabe“ (T-104); ihre Fehler zeigt der Bericht oben als Hinweis,
    /// den Speicherdialog bekommen sie vom Reiter.
    /// </summary>
    private void UpdateDocument(LocationDto location, DateOnly from, DateOnly until, CompareChoice comparison)
    {
        var document = new DocumentActionsViewModel(DocumentSourceFor(location, from, until, comparison, HasAiSummary, Analysis.AdoptedId), Api, Dialogs)
        {
            ShowToast = ShowToast,
            Files = Files,
        };
        document.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DocumentActionsViewModel.Error) && document.Error is { } problem)
            {
                Error = problem;
            }
        };
        Document = document;
    }

    /// <summary>
    /// Pfade von PDF und Versand; dieselben Abfrageparameter wie der Bericht, mit <c>ai=true</c>, wenn der Reiter einen KI-Kommentar zeigt, und mit <c>analysisId</c>,
    /// wenn eine KI-Auswertung in den Bericht übernommen ist.
    /// </summary>
    internal static DocumentSource DocumentSourceFor(LocationDto location, DateOnly from, DateOnly until, CompareChoice comparison, bool withAi, Guid? analysisId = null)
    {
        var query = $"from={Iso(from)}&until={Iso(until)}{(comparison.IsDefault ? "" : $"&compare={comparison.Key}")}{(withAi ? "&ai=true" : "")}{AnalysisQuery(analysisId)}";
        var root = $"api/reports/locations/{location.Id}/management";
        return new DocumentSource($"Management-Bericht {location.Name} {from:dd.MM.yyyy} bis {until:dd.MM.yyyy}", $"{root}/pdf?{query}", $"{root}/send?{query}");
    }

    private static string AnalysisQuery(Guid? analysisId) => analysisId is { } id ? $"&analysisId={id}" : "";
}
