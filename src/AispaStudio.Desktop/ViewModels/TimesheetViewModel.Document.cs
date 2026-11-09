using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Stundenliste als PDF (PDF-Ausgaben F, ADR 0091) ----------
public sealed partial class TimesheetViewModel
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private bool _documentOpen;

    /// <summary>
    /// Auf der Karte einer Person: der Knopfblock „Drucken · PDF speichern“ für den Monat der gezeigten Woche; null, solange er zu ist. Die Stundenliste (Tage, Kommen, Gehen, Pausen, Ist, Soll, Saldo,
    /// Monatssumme, Unterschriften) rechnet die API; ohne Senden, sie wird unterschrieben und abgeheftet.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDocumentOpen))]
    public partial DocumentActionsViewModel? Document { get; private set; }

    [ObservableProperty]
    public partial string? DocumentMessage { get; private set; }

    public bool IsDocumentOpen => Document is not null;

    /// <summary>Die Stundenliste gehört zu einer Person: es gibt sie auf der Karte, nicht in der Übersicht aller.</summary>
    public bool CanOfferDocument => IsCard;

    /// <summary>Sagt, was gedruckt wird: „Druckt die Stundenliste für März 2027.“</summary>
    public string DocumentHint => $"Druckt die Stundenliste für {WeekStart.ToString("MMMM yyyy", German)}.";

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

    private void UpdateDocument()
    {
        Document = _documentOpen && _employeeId is { } employeeId
            ? new DocumentActionsViewModel(DocumentSourceFor(employeeId, WeekStart, _context.CanViewTimes), _context.Api, Dialogs) { ShowToast = text => DocumentMessage = text }
            : null;
    }

    /// <summary>Pfad der Stundenliste (PDF-Ausgaben F) für den Monat, in dem die Woche beginnt; die Leitung und das Backoffice drucken sie (wie die Zeiten).</summary>
    internal static DocumentSource DocumentSourceFor(Guid employeeId, DateOnly weekStart, bool isAllowed) =>
        new($"Stundenliste {weekStart.ToString("MMMM yyyy", German)}", $"api/employees/{employeeId}/timesheet/pdf?month={weekStart.ToString("yyyy-MM", CultureInfo.InvariantCulture)}",
            IsAllowed: isAllowed, DisabledHint: "Die Stundenliste druckt nur, wer die Zeiten lesen darf.");
}
