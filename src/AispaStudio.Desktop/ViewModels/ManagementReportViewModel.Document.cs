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

    /// <summary>Baut den Knopfblock für den gerade gezeigten Bericht neu; Fehler zeigt der Knopfblock selbst darunter.</summary>
    private void UpdateDocument(LocationDto location, DateOnly from, DateOnly until, CompareChoice comparison) =>
        Document = new DocumentActionsViewModel(DocumentSourceFor(location, from, until, comparison, HasAiSummary), Api, Dialogs) { ShowToast = ShowToast };

    /// <summary>Pfade von PDF und Versand; dieselben Abfrageparameter wie der Bericht, mit <c>ai=true</c>, wenn der Reiter einen KI-Kommentar zeigt.</summary>
    internal static DocumentSource DocumentSourceFor(LocationDto location, DateOnly from, DateOnly until, CompareChoice comparison, bool withAi)
    {
        var query = $"from={Iso(from)}&until={Iso(until)}{(comparison.IsDefault ? "" : $"&compare={comparison.Key}")}{(withAi ? "&ai=true" : "")}";
        var root = $"api/reports/locations/{location.Id}/management";
        return new DocumentSource($"Management-Bericht {location.Name} {from:dd.MM.yyyy} bis {until:dd.MM.yyyy}", $"{root}/pdf?{query}", $"{root}/send?{query}");
    }
}
