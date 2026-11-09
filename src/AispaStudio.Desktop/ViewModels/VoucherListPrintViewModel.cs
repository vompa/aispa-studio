using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Wahl im Feld „Status“ der Gutscheinliste: Beschriftung und Wert der Abfrage (<c>null</c> heißt alle).</summary>
public sealed record VoucherStatusChoice(string Label, string? Query);

/// <summary>
/// „Gutscheinliste …“ (PDF-Ausgaben, Zusatz; ADR 0091): wählt Zeitraum der Ausstellung, Status und die Schalter „läuft in 30 Tagen ab“ und „noch nicht versendet“ und trägt den Knopfblock
/// „Drucken · PDF speichern“ für diese Wahl. Die Liste rechnet die API (<c>GET /api/pos/locations/{id}/vouchers/pdf</c>); die App kennt nur den Pfad. Ohne Senden: die Liste geht zur Buchhaltung.
/// </summary>
public sealed partial class VoucherListPrintViewModel : ObservableObject
{
    public static IReadOnlyList<VoucherStatusChoice> StatusChoices { get; } =
    [
        new("Alle", null), new("aktiv", "active"), new("aufgebraucht", "redeemed"), new("abgelaufen", "expired"), new("storniert", "voided"),
    ];

    private readonly Guid _locationId;
    private readonly bool _isAllowed;
    private readonly IAispaApi _api;
    private readonly IConfirmDialogs? _dialogs;
    private readonly Action<string> _showToast;
    private readonly Action _close;

    public VoucherListPrintViewModel(Guid locationId, bool isAllowed, IAispaApi api, IConfirmDialogs? dialogs, Action<string> showToast, Action close)
    {
        _locationId = locationId;
        _isAllowed = isAllowed;
        _api = api;
        _dialogs = dialogs;
        _showToast = showToast;
        _close = close;
        SelectedStatus = StatusChoices[0];
        Document = DocumentFor();
    }

    /// <summary>Erster Tag der Ausstellung; leer heißt ohne Beginn.</summary>
    [ObservableProperty]
    public partial DateOnly? From { get; set; }

    /// <summary>Letzter Tag der Ausstellung; leer heißt ohne Ende.</summary>
    [ObservableProperty]
    public partial DateOnly? Until { get; set; }

    [ObservableProperty]
    public partial VoucherStatusChoice SelectedStatus { get; set; }

    /// <summary>Nur aktive Gutscheine, die in den nächsten 30 Tagen ablaufen.</summary>
    [ObservableProperty]
    public partial bool Expiring { get; set; }

    /// <summary>Nur Gutscheine ohne Versandvermerk (weder Mail noch Druck noch Übergabe).</summary>
    [ObservableProperty]
    public partial bool NotSent { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRangeError))]
    public partial string? RangeError { get; private set; }

    public bool HasRangeError => RangeError is not null;

    /// <summary>Der Knopfblock für die gewählte Auswahl; jede Änderung ersetzt ihn samt Pfad.</summary>
    [ObservableProperty]
    public partial DocumentActionsViewModel Document { get; private set; }

    /// <summary>Pfad und Abfrage der Liste für die Wahl; nur gesetzte Felder stehen darin.</summary>
    public string Path
    {
        get
        {
            var query = new List<string>();
            if (From is { } from)
            {
                query.Add($"from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
            }

            if (Until is { } until)
            {
                query.Add($"until={until.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
            }

            if (SelectedStatus.Query is { } status)
            {
                query.Add($"status={status}");
            }

            if (Expiring)
            {
                query.Add("expiring=true");
            }

            if (NotSent)
            {
                query.Add("notSent=true");
            }

            return $"api/pos/locations/{_locationId}/vouchers/pdf{(query.Count == 0 ? "" : $"?{string.Join('&', query)}")}";
        }
    }

    partial void OnFromChanged(DateOnly? value) => Refresh();

    partial void OnUntilChanged(DateOnly? value) => Refresh();

    partial void OnSelectedStatusChanged(VoucherStatusChoice value) => Refresh();

    partial void OnExpiringChanged(bool value) => Refresh();

    partial void OnNotSentChanged(bool value) => Refresh();

    [RelayCommand]
    private void Close() => _close();

    private void Refresh()
    {
        RangeError = From is { } from && Until is { } until && until < from ? "Das Ende darf nicht vor dem Beginn liegen." : null;
        Document = DocumentFor();
    }

    private DocumentActionsViewModel DocumentFor() =>
        new(new DocumentSource("Gutscheinliste", Path, IsAllowed: _isAllowed && RangeError is null,
            DisabledHint: RangeError ?? "Die Gutscheinliste druckt nur die Kassenaufsicht oder wer „Berichte mit Beträgen“ sehen darf."),
            _api, _dialogs)
        { ShowToast = _showToast };
}
