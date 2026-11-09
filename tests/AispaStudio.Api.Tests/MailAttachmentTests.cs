using AispaStudio.Api.Mail;
using MimeKit;

namespace AispaStudio.Api.Tests;

/// <summary>Mail-Anhänge (ADR 0091): das PDF hängt an der MIME-Nachricht, der Text bleibt; mehr als 5 MB je Mail lehnt die Nachricht ab.</summary>
public sealed class MailAttachmentTests
{
    private static readonly MailboxAddress From = new("Day-Spa Wien", "noreply@aispa.local");

    [Fact]
    public void Ein_Anhang_steht_mit_Name_Typ_und_Inhalt_in_der_MIME_Nachricht()
    {
        byte[] pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34];
        var mail = new OutgoingMail("eva@example.at", "Ihre Rechnung Nr. K1-142", "Guten Tag,\n\nanbei Ihre Rechnung.",
            ReplyTo: "spa@example.at", Attachments: [new MailAttachment("Rechnung-K1-142.pdf", "application/pdf", pdf)]);

        var message = MailMessages.Build(mail, From);

        var part = Assert.IsType<MimePart>(Assert.Single(message.Attachments));
        Assert.Equal("Rechnung-K1-142.pdf", part.FileName);
        Assert.Equal("application/pdf", part.ContentType.MimeType);
        using var content = new MemoryStream();
        part.Content!.DecodeTo(content, TestContext.Current.CancellationToken);
        Assert.Equal(pdf, content.ToArray());
        Assert.Equal("Guten Tag,\n\nanbei Ihre Rechnung.", message.TextBody?.ReplaceLineEndings("\n"));
        Assert.Equal("spa@example.at", Assert.Single(message.ReplyTo.Mailboxes).Address);
    }

    [Fact]
    public void Ohne_Anhang_bleibt_die_Nachricht_wie_bisher()
    {
        var message = MailMessages.Build(new OutgoingMail("eva@example.at", "Betreff", "Text"), From);

        Assert.Empty(message.Attachments);
        Assert.Equal("Text", message.TextBody);
        Assert.Equal(0, new OutgoingMail("eva@example.at", "Betreff", "Text").AttachmentBytes);
    }

    [Fact]
    public void Mehr_als_5_MB_Anhaenge_je_Mail_sind_ein_Fehler()
    {
        var mail = new OutgoingMail("eva@example.at", "Betreff", "Text",
            Attachments: [new MailAttachment("a.pdf", "application/pdf", new byte[3 * 1024 * 1024]), new MailAttachment("b.pdf", "application/pdf", new byte[(2 * 1024 * 1024) + 1])]);

        Assert.True(mail.AttachmentsTooLarge);
        var error = Assert.Throws<InvalidOperationException>(() => MailMessages.Build(mail, From));
        Assert.Contains("5 MB", error.Message, StringComparison.Ordinal);
        Assert.False(new OutgoingMail("eva@example.at", "Betreff", "Text", Attachments: [new MailAttachment("a.pdf", "application/pdf", new byte[5 * 1024 * 1024])]).AttachmentsTooLarge);
    }
}
