using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Dienstplan-Aushang als PDF (PDF-Ausgaben F, ADR 0091) ----------
public sealed partial class RosterPlanViewModel
{
    private bool _documentOpen;

    /// <summary>
    /// Der Knopfblock „Drucken · PDF speichern“ für die gezeigte Woche am gewählten Standort; null, solange er zu ist. Der Pfad nennt Woche und Standort, deshalb folgt der Block jedem Wechsel.
    /// Gedruckt wird der veröffentlichte Stand (Entwürfe stehen nicht auf dem Aushang); ohne Senden, der Plan hängt aus.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDocumentOpen))]
    public partial DocumentActionsViewModel? Document { get; private set; }

    [ObservableProperty]
    public partial string? DocumentMessage { get; private set; }

    public bool IsDocumentOpen => Document is not null;

    /// <summary>Den Aushang einer ganzen Woche gibt es auf der Wochenansicht, nicht auf der Karte einer Person.</summary>
    public bool CanOfferDocument => ShowsNames;

    /// <summary>Sagt, was gedruckt wird: „Druckt den veröffentlichten Dienstplan der Woche 01.03. – 07.03.2027.“</summary>
    public string DocumentHint => $"Druckt den veröffentlichten Dienstplan der Woche {WeekStart.ToString("dd.MM.", CultureInfo.InvariantCulture)} – {WeekStart.AddDays(6).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}, ohne Entwürfe.";

    [RelayCommand]
    private void ToggleDocument()
    {
        _documentOpen = !_documentOpen;
        DocumentMessage = null;
        UpdateDocument();
    }

    partial void OnWeekStartChanged(DateOnly value)
    {
        OnPropertyChanged(nameof(DocumentHint));
        UpdateDocument();
    }

    /// <summary>Baut den Knopfblock für die gezeigte Woche und den gewählten Standort neu, solange er offen ist.</summary>
    private void UpdateDocument()
    {
        Document = _documentOpen && SelectedLocation is { Id: { } locationId }
            ? new DocumentActionsViewModel(DocumentSourceFor(locationId, WeekStart, _context.CanPlan), _context.Api, Dialogs) { ShowToast = text => DocumentMessage = text }
            : null;
    }

    /// <summary>Pfad des Aushangs (PDF-Ausgaben F): die Woche, in der der Tag liegt; nur die Leitung druckt ihn (wie die API).</summary>
    internal static DocumentSource DocumentSourceFor(Guid locationId, DateOnly weekStart, bool isAllowed) =>
        new($"Dienstplan {weekStart.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}", $"api/locations/{locationId}/roster/pdf?week={weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            IsAllowed: isAllowed, DisabledHint: "Den Aushang druckt nur die Standortleitung.");
}
