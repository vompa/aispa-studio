using System.Net.Mail;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Rückfrage „… per Mail senden?“ (ADR 0092, Design-Brief 5.27): Adresse vorbelegt aus der Kundenkarte bzw. den Lieferantendaten, Betreff und Text vorbelegt von der API;
/// „Senden“ geht erst mit gültiger Adresse und Betreff. Enter in einem einzeiligen Feld sendet, Esc schließt.
/// </summary>
public sealed partial class SendDocumentDialogViewModel : ConfirmDialogViewModel
{
    public const string InvalidAddress = "Das ist keine gültige Mailadresse.";

    public const string MissingAddress = "Bitte eine Mailadresse eintragen.";

    public const string MissingSubject = "Bitte einen Betreff eintragen.";

    /// <summary>Namen der Felder (<c>x:Name</c>): das erste ungültige Feld bekommt nach „Senden“ den Fokus.</summary>
    public const string ToField = "SendTo";

    public const string SubjectField = "SendSubject";

    public SendDocumentDialogViewModel(string documentTitle, DocumentMailDraft draft, Action<ConfirmResult> answer)
        : base(new ConfirmRequest($"{documentTitle} per Mail senden?", "Der Empfänger bekommt das PDF als Anhang. Betreff und Text lassen sich vorher anpassen.", "Senden", IsDestructive: false), answer)
    {
        To = draft.To ?? "";
        Subject = draft.Subject;
        Text = draft.Text;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAddress))]
    public partial string To { get; set; }

    [ObservableProperty]
    public partial string Subject { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>Eine Adresse ist vorbelegt: der Anfangsfokus liegt dann auf „Senden“, sonst im Feld „An“.</summary>
    public bool HasAddress => To.Trim().Length > 0;

    /// <summary>Fehler am Feld (meldungen.md): beim Tippen, sobald etwas drinsteht, das keine Adresse ist; ein leeres Feld meldet erst „Senden“.</summary>
    [ObservableProperty]
    public partial string? ToError { get; private set; }

    [ObservableProperty]
    public partial string? SubjectError { get; private set; }

    partial void OnToChanged(string value) => ToError = value.Trim().Length == 0 || IsAddress(value) ? null : InvalidAddress;

    partial void OnSubjectChanged(string value) => SubjectError = null;

    /// <summary>„Senden“ ist aktiv und prüft Adresse und Betreff; nichts geht hinaus, solange ein Feld einen Fehler zeigt.</summary>
    [RelayCommand]
    private void Send()
    {
        ToError = IsAddress(To) ? null : To.Trim().Length == 0 ? MissingAddress : InvalidAddress;
        SubjectError = Subject.Trim().Length == 0 ? MissingSubject : null;
        if (ToError is not null || SubjectError is not null)
        {
            RequestFocus(ToError is not null ? ToField : SubjectField);
            return;
        }

        Answer(new ConfirmResult(ConfirmChoice.Confirmed));
    }

    internal static bool IsAddress(string value) => MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim();
}
