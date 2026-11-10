using System.Net.Mail;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Documents;

/// <summary>
/// Ein Dokument per Mail (ADR 0092): Adresse prüfen, Betreff und Text aus der Anfrage oder der Vorbelegung nehmen und die Mail mit dem PDF als
/// <see cref="SendSpaMail"/> in die Queue legen (Name des Spas als Absender, Postfach des Spas als Antwortadresse, wie bei den Absage-Nachrichten).
/// </summary>
internal static class DocumentMails
{
    public const string InvalidAddress = "Bitte eine gültige Mailadresse eingeben.";

    public static bool IsAddress(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 254 && MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim();

    /// <summary>Fehler für die Antwort 400, wenn die Adresse nicht passt; sonst null.</summary>
    public static Dictionary<string, string[]>? Validate(SendDocumentRequest request) =>
        IsAddress(request.To) ? null : new Dictionary<string, string[]> { ["to"] = [InvalidAddress] };

    /// <summary>Betreff und Text der Anfrage, leer gelassene Felder aus der Vorbelegung.</summary>
    public static (string Subject, string Text) Resolve(DocumentMailDraft draft, SendDocumentRequest request) =>
        (Or(request.Subject, draft.Subject), Or(request.Text, draft.Text));

    public static SendSpaMail Message(Guid tenantId, string to, string subject, string text, string fileName, byte[] pdf) =>
        new(tenantId, new OutgoingMail(to.Trim(), subject, text, Attachments: [new MailAttachment(fileName, DocumentFiles.PdfContentType, pdf)]));

    /// <summary>Der Gruß am Ende jedes vorbelegten Mailtexts.</summary>
    public static string Closing(string spaName) => $"\n\nFreundliche Grüße\n{spaName}";

    private static string Or(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
