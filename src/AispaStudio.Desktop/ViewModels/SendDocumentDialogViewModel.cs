using System.Net.Mail;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Rückfrage „… per Mail senden?“ (ADR 0091, Design-Brief 5.27): Adresse vorbelegt aus der Kundenkarte bzw. den Lieferantendaten, Betreff und Text vorbelegt von der API;
/// „Senden“ geht erst mit gültiger Adresse und Betreff. Enter in einem einzeiligen Feld sendet, Esc schließt.
/// </summary>
public sealed partial class SendDocumentDialogViewModel : ConfirmDialogViewModel
{
    public const string InvalidAddress = "Das ist keine gültige Mailadresse.";

    public SendDocumentDialogViewModel(string documentTitle, DocumentMailDraft draft, Action<ConfirmResult> answer)
        : base(new ConfirmRequest($"{documentTitle} per Mail senden?", "Der Empfänger bekommt das PDF als Anhang. Betreff und Text lassen sich vorher anpassen.", "Senden", IsDestructive: false), answer)
    {
        To = draft.To ?? "";
        Subject = draft.Subject;
        Text = draft.Text;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToError), nameof(CanSend), nameof(HasAddress))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string To { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Subject { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>Eine Adresse ist vorbelegt: der Anfangsfokus liegt dann auf „Senden“, sonst im Feld „An“.</summary>
    public bool HasAddress => To.Trim().Length > 0;

    /// <summary>Fehler am Feld (meldungen.md): erst wenn etwas drinsteht, das keine Adresse ist; ein leeres Feld sperrt nur den Knopf.</summary>
    public string? ToError => To.Trim().Length == 0 || IsAddress(To) ? null : InvalidAddress;

    public bool CanSend => IsAddress(To) && Subject.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private void Send() => Answer(new ConfirmResult(ConfirmChoice.Confirmed));

    internal static bool IsAddress(string value) => MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim();
}
