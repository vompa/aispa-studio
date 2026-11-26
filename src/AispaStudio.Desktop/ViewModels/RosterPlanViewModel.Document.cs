using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Dienstplan-Aushang als PDF (PDF-Ausgaben F, ADR 0092; Menü „Ausgabe“ T-101) ----------
public sealed partial class RosterPlanViewModel
{
    /// <summary>
    /// Die Befehle „Drucken …“ und „Als PDF speichern“ des Aushangs für die gezeigte Woche am gewählten Standort (Menü „Ausgabe“, T-101); null auf der Personenkarte und ohne Standort. Der Pfad nennt Woche und Standort,
    /// deshalb folgt das Modell jedem Wechsel. Gedruckt wird der veröffentlichte Stand (Entwürfe stehen nicht auf dem Aushang); ohne Senden, der Plan hängt aus. Erfolge sagt der Toast der Shell, Fehler die Meldung „Nicht erledigt“.
    /// </summary>
    [ObservableProperty]
    public partial DocumentActionsViewModel? Document { get; private set; }

    /// <summary>Den Aushang einer ganzen Woche gibt es auf der Wochenansicht, nicht auf der Karte einer Person.</summary>
    public bool CanOfferDocument => ShowsNames;

    /// <summary>Die Gruppe „Aushang“ im Menü: auf der Wochenansicht und nur für die Standortleitung (die API verlangt dasselbe).</summary>
    public bool ShowsDocumentGroup => CanOfferDocument && _context.CanPlan;

    /// <summary>Der Knopf „Ausgabe“ steht, wenn die Person den Aushang oder die Listen sehen darf; sonst fehlt er und „Anträge“ und die Hilfe bleiben rechtsbündig.</summary>
    public bool HasOutput => ShowsDocumentGroup || Export is not null;

    partial void OnWeekStartChanged(DateOnly value) => UpdateDocument();

    /// <summary>Baut das Modell des Aushangs für die gezeigte Woche und den gewählten Standort neu.</summary>
    private void UpdateDocument()
    {
        if (!CanOfferDocument || SelectedLocation is not { Id: { } locationId })
        {
            Document = null;
            return;
        }

        var document = new DocumentActionsViewModel(DocumentSourceFor(locationId, WeekStart, _context.CanPlan), _context.Api, Dialogs) { ShowToast = text => _context.Toast.Show(text) };
        document.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DocumentActionsViewModel.Error) && document.Error is { } problem)
            {
                Error = problem;
            }
        };
        if (Files is not null)
        {
            document.Files = Files;
        }

        Document = document;
    }

    /// <summary>Pfad des Aushangs (PDF-Ausgaben F): die Woche, in der der Tag liegt; nur die Leitung druckt ihn (wie die API).</summary>
    internal static DocumentSource DocumentSourceFor(Guid locationId, DateOnly weekStart, bool isAllowed) =>
        new($"Dienstplan {weekStart.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}", $"api/locations/{locationId}/roster/pdf?week={weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            IsAllowed: isAllowed, DisabledHint: "Den Aushang druckt nur die Standortleitung.");
}
