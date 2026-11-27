using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Stundenliste als PDF (PDF-Ausgaben F, ADR 0092; Menü „Ausgabe“ T-122) ----------
public sealed partial class TimesheetViewModel
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>
    /// Die Befehle „Drucken …“ und „Als PDF speichern“ der Stundenliste für den Monat der gezeigten Woche (Menü „Ausgabe“, T-122); null in der Übersicht aller. Der Pfad nennt Person und Monat,
    /// deshalb folgt das Modell jedem Wochenwechsel. Ohne Senden, sie wird unterschrieben und abgeheftet; die Zahlen rechnet die API. Erfolge sagt der Toast der Shell, Fehler die Meldung der Maske.
    /// </summary>
    [ObservableProperty]
    public partial DocumentActionsViewModel? Document { get; private set; }

    /// <summary>Die Stundenliste gehört zu einer Person: es gibt sie auf der Karte, nicht in der Übersicht aller.</summary>
    public bool CanOfferDocument => IsCard;

    /// <summary>Die Gruppe „Stundenliste“ im Menü: auf der Karte einer Person und nur, wer die Zeiten lesen darf (die API verlangt dasselbe).</summary>
    public bool ShowsDocumentGroup => CanOfferDocument && _context.CanViewTimes;

    /// <summary>Der Knopf „Ausgabe“ steht, wenn die Person die Stundenliste oder die Listen sehen darf; sonst fehlt er und das Fragezeichen bleibt am Rand.</summary>
    public bool HasOutput => ShowsDocumentGroup || HasExport;

    partial void OnWeekStartChanged(DateOnly value) => UpdateDocument();

    /// <summary>Baut das Modell der Stundenliste für den Monat der gezeigten Woche neu.</summary>
    private void UpdateDocument()
    {
        if (!CanOfferDocument || _employeeId is not { } employeeId)
        {
            Document = null;
            return;
        }

        var document = new DocumentActionsViewModel(DocumentSourceFor(employeeId, WeekStart, _context.CanViewTimes), _context.Api, Dialogs) { ShowToast = text => _context.Toast.Show(text) };
        document.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DocumentActionsViewModel.Error) && document.Error is { } problem)
            {
                LoadError = problem;
            }
        };
        if (Files is not null)
        {
            document.Files = Files;
        }

        Document = document;
    }

    /// <summary>Pfad der Stundenliste (PDF-Ausgaben F) für den Monat, in dem die Woche beginnt; die Leitung und das Backoffice drucken sie (wie die Zeiten).</summary>
    internal static DocumentSource DocumentSourceFor(Guid employeeId, DateOnly weekStart, bool isAllowed) =>
        new($"Stundenliste {weekStart.ToString("MMMM yyyy", German)}", $"api/employees/{employeeId}/timesheet/pdf?month={weekStart.ToString("yyyy-MM", CultureInfo.InvariantCulture)}",
            IsAllowed: isAllowed, DisabledHint: "Die Stundenliste druckt nur, wer die Zeiten lesen darf.");
}
