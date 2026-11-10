using System.Globalization;
using AispaStudio.Api.Documents;
using AispaStudio.Contracts;

namespace AispaStudio.Api.HrFile;

/// <summary>Die Einträge einer Person als PDF für die Auskunft nach Art. 15 DSGVO (R05 Slice 110); seit ADR 0092 über den Dokument-Dienst, Inhalt unverändert.</summary>
internal static class HrFilePdf
{
    public static byte[] Build(DocumentHeader header, string personName, IEnumerable<HrEntryDto> entries, DateOnly createdOn)
    {
        var pdf = new DocumentBuilder(PaperFormat.A4Portrait, header);
        pdf.Title($"Personalakte: {personName}");
        pdf.Paragraph($"Auskunft nach Art. 15 DSGVO, erstellt am {Day(createdOn)}", muted: true);
        var any = false;
        foreach (var entry in entries)
        {
            any = true;
            pdf.Heading(entry.Kind switch
            {
                HrEntryKind.Conversation => $"Mitarbeitergespräch ({ConversationLabel(entry.ConversationKind)}), {Day(entry.Date)}",
                HrEntryKind.Warning => $"Verwarnung, {Day(entry.Date)}",
                _ => $"Anmerkung, {Day(entry.Date)}",
            });
            WriteBody(pdf, entry);
        }

        if (!any)
        {
            pdf.Heading("Keine Einträge");
        }

        return pdf.Build();
    }

    private static void WriteBody(DocumentBuilder pdf, HrEntryDto entry)
    {
        if (entry.Participants is { } participants)
        {
            pdf.Paragraph($"Teilnehmende: {participants}");
        }

        pdf.Paragraph(entry.Kind == HrEntryKind.Warning ? $"Grund: {entry.Text}" : entry.Text);
        foreach (var agreement in entry.Agreements)
        {
            pdf.Paragraph($"[{(agreement.IsDone ? "x" : " ")}] {agreement.Text}");
        }

        if (entry.FollowUpOn is { } followUp)
        {
            pdf.Paragraph($"Folgetermin: {Day(followUp)}{(entry.FollowUpDone ? " (geführt)" : "")}");
        }

        if (entry.Kind == HrEntryKind.Warning)
        {
            pdf.Paragraph($"Ausgesprochen von: {entry.IssuedBy}");
            pdf.Paragraph($"Schriftlich übergeben: {(entry.HandedOverInWriting ? "ja" : "nein")}");
            pdf.Paragraph($"Gültig bis: {(entry.ExpiresOn is { } expires ? Day(expires) : "–")}");
        }
    }

    private static string ConversationLabel(HrConversationKind? kind) => kind switch
    {
        HrConversationKind.Annual => "Jahresgespräch",
        HrConversationKind.Feedback => "Feedback",
        HrConversationKind.Return => "Rückkehr",
        _ => "Anlass",
    };

    private static string Day(DateOnly day) => day.ToString("d.M.yyyy", CultureInfo.InvariantCulture);
}
