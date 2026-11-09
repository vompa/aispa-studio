using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Was die Rezeption mit einem Wunsch der Warteliste tun kann; der Kalender führt es aus.</summary>
public sealed record WaitlistActions(
    Func<WaitlistRow, Task> ShowFree,
    Func<WaitlistRow, Task> Book,
    Func<Guid, Task>? OpenCustomer,
    Func<WaitlistRow, WaitlistCloseReason, Task> Close);

/// <summary>
/// Ein Wunsch auf der Warteliste: Name (öffnet die Kundenkarte), Behandlung, Zeitraum, Stand als Pille, Telefonnummer zum Nachfragen und,
/// sobald etwas frei ist, „Frei: Do. 23.10. 17:00“. Aktionen: Freie Zeiten zeigen, Buchen, Anfrage abschließen mit Grund (gebucht, abgesagt, nicht erreichbar).
/// </summary>
public sealed partial class WaitlistRow(WaitlistStaffEntry entry, WaitlistActions actions) : ViewModelBase
{
    /// <summary>Gründe zum Schließen; die Wahl bestimmt, ob der Eintrag als gebucht oder als ausgetragen gilt.</summary>
    public static IReadOnlyList<WaitlistReason> Reasons { get; } =
    [
        new(WaitlistCloseReason.Arranged, "Gebucht", "Der Termin ist vereinbart, z. B. am Telefon."),
        new(WaitlistCloseReason.NoLongerWanted, "Abgesagt", "Der Gast braucht keinen Termin mehr."),
        new(WaitlistCloseReason.NoLongerWanted, "Nicht erreichbar", "Der Gast war nicht zu erreichen; die Anfrage endet ohne Termin."),
    ];

    /// <summary>Name des Knopfs, der den Grund erfragt.</summary>
    public const string CloseLabel = "Anfrage abschließen …";

    public WaitlistStaffEntry Entry { get; } = entry;

    public string Name => Entry.CustomerName;

    public string Treatment => Entry.TreatmentName;

    /// <summary>„Mi. 21.10. bis Fr. 30.10., vormittags“.</summary>
    public string Period => $"{Day(Entry.From)}{(Entry.Until != Entry.From ? $" bis {Day(Entry.Until)}" : "")}, {DayPartName(Entry.DayPart)}";

    /// <summary>Stand als Pille: „Wartet“ oder „2× freien Termin gemeldet“.</summary>
    public string State => Entry.OfferCount == 0 ? "Wartet" : $"{Entry.OfferCount}× freien Termin gemeldet";

    public bool IsOffered => Entry.OfferCount > 0;

    public string? Phone => string.IsNullOrWhiteSpace(Entry.Phone) ? null : Entry.Phone.Trim();

    public bool HasPhone => Phone is not null;

    /// <summary>Die erste freie Zeit im Wunschzeitraum und zur Wunschtageszeit; leer, solange sie gesucht wird oder nichts frei ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFree), nameof(FreeText))]
    [NotifyCanExecuteChangedFor(nameof(BookCommand))]
    public partial AvailableSlot? FreeSlot { get; set; }

    /// <summary>„Frei: Do. 23.10. 17:00 bei Lena Huber“, gesetzt vom Kalender.</summary>
    public string? FreeLabel { get; set; }

    public bool HasFree => FreeSlot is not null;

    public string? FreeText => FreeSlot is null ? null : $"Frei: {FreeLabel}";

    /// <summary>Das Schließen fragt nach dem Grund; solange es offen ist, stehen Auswahl und „Abschließen“ im Eintrag.</summary>
    [ObservableProperty]
    public partial bool IsClosing { get; private set; }

    [ObservableProperty]
    public partial WaitlistReason Reason { get; set; } = Reasons[0];

    /// <summary>Der Name öffnet die Kundenkarte.</summary>
    public bool CanOpenCustomer => actions.OpenCustomer is not null;

    [RelayCommand]
    private Task OpenCustomerAsync() => actions.OpenCustomer?.Invoke(Entry.CustomerId) ?? Task.CompletedTask;

    /// <summary>Öffnet „Freie Zeit suchen“ mit Behandlung, Gast, Zeitraum und Tageszeit des Wunsches.</summary>
    [RelayCommand]
    private Task ShowFreeAsync() => actions.ShowFree(this);

    /// <summary>Bucht die freie Zeit gleich für diesen Gast (der Klick ist die Bestätigung); der Eintrag verlässt dann die Liste.</summary>
    [RelayCommand(CanExecute = nameof(HasFree))]
    private Task BookAsync() => actions.Book(this);

    [RelayCommand]
    private void AskClose() => IsClosing = true;

    [RelayCommand]
    private void CancelClose() => IsClosing = false;

    /// <summary>„Abschließen“ mit dem gewählten Grund.</summary>
    [RelayCommand]
    private async Task CloseAsync()
    {
        await actions.Close(this, Reason.Value).ConfigureAwait(true);
        IsClosing = false;
    }

    private static string Day(DateOnly date) => DayLayout.ShortDay(date);

    public static string DayPartName(DayPart part) => part switch
    {
        DayPart.Morning => "vormittags",
        DayPart.Afternoon => "nachmittags",
        DayPart.Evening => "abends",
        _ => "ganztags",
    };
}

/// <summary>Ein Grund zum Schließen eines Wunsches, mit der Wirkung auf den Eintrag.</summary>
public sealed record WaitlistReason(WaitlistCloseReason Value, string Label, string Hint)
{
    public override string ToString() => Label;
}

/// <summary>
/// Offene Wünsche der Warteliste am Standort. Freie Termine meldet der Server selbst per Mail, sobald etwas storniert wird;
/// die Liste hilft der Rezeption, gezielt nachzufragen, Zeiten zu suchen und für den Gast gleich zu buchen.
/// </summary>
public sealed partial class WaitlistViewModel(string locationName, IReadOnlyList<WaitlistRow> rows, Action close, DocumentActionsViewModel? document = null, string? printHint = null)
{
    public string LocationName { get; } = locationName;

    /// <summary>Knopfblock „Drucken · PDF speichern“ für die Wartenden des gezeigten Tages (PDF-Ausgaben E, ADR 0091); null ohne Standort.</summary>
    public DocumentActionsViewModel? Document { get; } = document;

    /// <summary>Sagt, was gedruckt wird: „Druckt die offenen Wünsche, die am 01.03.2027 gelten.“</summary>
    public string? PrintHint { get; } = printHint;

    public IReadOnlyList<WaitlistRow> Rows { get; } = rows;

    public bool IsEmpty => Rows.Count == 0;

    [RelayCommand]
    private void Close() => close();
}
