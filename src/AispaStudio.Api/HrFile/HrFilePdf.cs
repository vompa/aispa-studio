using System.Globalization;
using AispaStudio.Contracts;

namespace AispaStudio.Api.HrFile;

/// <summary>Die Einträge einer Person als PDF für die Auskunft nach Art. 15 DSGVO (R05 Slice 110).</summary>
internal static class HrFilePdf
{
    public static byte[] Build(string personName, IEnumerable<HrEntryDto> entries, DateOnly createdOn)
    {
        var pdf = new SimplePdf();
        pdf.Heading($"Personalakte: {personName}");
        pdf.Text($"Auskunft nach Art. 15 DSGVO, erstellt am {Day(createdOn)}");
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

        return pdf.ToBytes();
    }

    private static void WriteBody(SimplePdf pdf, HrEntryDto entry)
    {
        if (entry.Participants is { } participants)
        {
            pdf.Text($"Teilnehmende: {participants}");
        }

        pdf.Text(entry.Kind == HrEntryKind.Warning ? $"Grund: {entry.Text}" : entry.Text);
        foreach (var agreement in entry.Agreements)
        {
            pdf.Text($"[{(agreement.IsDone ? "x" : " ")}] {agreement.Text}");
        }

        if (entry.FollowUpOn is { } followUp)
        {
            pdf.Text($"Folgetermin: {Day(followUp)}{(entry.FollowUpDone ? " (geführt)" : "")}");
        }

        if (entry.Kind == HrEntryKind.Warning)
        {
            pdf.Text($"Ausgesprochen von: {entry.IssuedBy}");
            pdf.Text($"Schriftlich übergeben: {(entry.HandedOverInWriting ? "ja" : "nein")}");
            pdf.Text($"Gültig bis: {(entry.ExpiresOn is { } expires ? Day(expires) : "–")}");
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
