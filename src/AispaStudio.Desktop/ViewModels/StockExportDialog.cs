using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine der fünf Listen im Dialog „Lager exportieren“: Titel, ein Satz dazu und, wenn es sie noch nicht gibt, der Grund (die API nennt ihn).</summary>
public sealed partial class StockExportChoice(string list, string title, string description, bool isAvailable, string? reason) : ObservableObject
{
    public string List { get; } = list;

    public string Title { get; } = title;

    public string Description { get; } = description;

    public bool IsAvailable { get; } = isAvailable;

    /// <summary>Der Grund, warum die Liste gesperrt ist, sonst leer.</summary>
    public string? Reason { get; } = reason;

    public bool HasReason => !IsAvailable && !string.IsNullOrEmpty(Reason);

    /// <summary>Was ein Screenreader beim Optionsfeld liest: Titel, Satz und gegebenenfalls der Grund.</summary>
    public string AutomationName => HasReason ? $"{Title}, gesperrt: {Reason}" : $"{Title}, {Description}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Dialog „Lager exportieren“ (P7, Entwurf Abschnitt 7): ein Inhalt aus fünf Listen als Optionsfelder mit kurzer Erklärung, das Format Excel oder CSV als Segment und bei den
/// Lagerbewegungen der Zeitraum (vorbelegt mit den letzten 30 Tagen). Was es an diesem Standort noch nicht gibt, ist gesperrt und nennt den Grund aus der API. „Exportieren“ prüft den
/// Zeitraum und schließt erst dann; die Maske holt danach die Datei.
/// </summary>
public sealed partial class StockExportDialogViewModel : ConfirmDialogViewModel
{
    public const string DialogTitle = "Lager exportieren";
    public const string Movements = "movements";

    private static readonly (string List, string Title, string Description)[] Lists =
    [
        ("levels", "Bestand je Lagerort", "Mengen und Mindestbestand von heute"),
        (Movements, "Lagerbewegungen", "Zugänge, Verbrauch und Korrekturen im Zeitraum"),
        ("orders", "Bestellungen", "Alle Bestellungen mit Status und Summe"),
        ("expiry", "Ablaufliste", "Chargen nach Ablaufdatum"),
        ("count", "Inventur", "Soll, gezählt und Differenz der letzten Zählung"),
    ];

    private bool _selecting;

    /// <param name="locationName">Der Standort, dessen Lager exportiert wird.</param>
    /// <param name="availability">Was die API je Liste meldet; eine Liste, die fehlt, gilt als verfügbar.</param>
    /// <param name="preselected">Die Liste, die zum offenen Bereich passt; ist sie gesperrt, steht die erste freie vorn.</param>
    /// <param name="excel">Das zuletzt gewählte Format: Excel (true) oder CSV.</param>
    public StockExportDialogViewModel(string locationName, IReadOnlyList<StockExportListAvailabilityDto> availability, string preselected, bool excel, DateOnly from, DateOnly until,
        Action<ConfirmResult> answer)
        : base(new ConfirmRequest(DialogTitle, $"Eine Liste von {locationName} als Datei speichern.", "Exportieren", IsDestructive: false), answer)
    {
        Choices =
        [
            .. Lists.Select(l =>
            {
                var info = availability.FirstOrDefault(a => a.List == l.List);
                return new StockExportChoice(l.List, l.Title, l.Description, info?.Available ?? true, info?.Reason);
            }),
        ];
        foreach (var choice in Choices)
        {
            choice.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(StockExportChoice.IsSelected) && choice.IsSelected)
                {
                    Select(choice);
                }
            };
        }

        IsExcel = excel;
        From = from;
        Until = until;
        Select(Choices.FirstOrDefault(c => c.List == preselected && c.IsAvailable) ?? Choices.FirstOrDefault(c => c.IsAvailable));
    }

    public IReadOnlyList<StockExportChoice> Choices { get; }

    /// <summary>Die gewählte Liste (<c>levels</c>, <c>movements</c>, …) oder null, wenn der Standort noch nichts zum Exportieren hat.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRange), nameof(CanExport))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial string? Selected { get; private set; }

    public bool CanExport => Selected is not null;

    /// <summary>Der Zeitraum gilt nur für die Lagerbewegungen.</summary>
    public bool ShowRange => Selected == Movements;

    [ObservableProperty]
    public partial DateOnly? From { get; set; }

    [ObservableProperty]
    public partial DateOnly? Until { get; set; }

    /// <summary>Fehler am Feld „Bis“; Ändern eines Datums nimmt ihn weg.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCsv), nameof(Format))]
    public partial bool IsExcel { get; set; }

    public bool IsCsv => !IsExcel;

    /// <summary>„xlsx“ oder „csv“, wie die API es als <c>format</c> annimmt.</summary>
    public string Format => IsExcel ? "xlsx" : "csv";

    partial void OnFromChanged(DateOnly? value) => Error = null;

    partial void OnUntilChanged(DateOnly? value) => Error = null;

    [RelayCommand]
    private void ChooseExcel() => IsExcel = true;

    [RelayCommand]
    private void ChooseCsv() => IsExcel = false;

    private void Select(StockExportChoice? choice)
    {
        if (_selecting)
        {
            return;
        }

        _selecting = true;
        foreach (var other in Choices)
        {
            other.IsSelected = other == choice;
        }

        _selecting = false;
        Selected = choice?.List;
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export()
    {
        Error = ShowRange ? AppointmentListDialogViewModel.RangeError(From, Until) : null;
        if (Error is null)
        {
            Answer(new ConfirmResult(ConfirmChoice.Confirmed));
        }
    }
}
