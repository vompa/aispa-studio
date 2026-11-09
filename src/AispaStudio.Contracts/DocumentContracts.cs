namespace AispaStudio.Contracts;

/// <summary>
/// Ein Dokument (PDF) per Mail senden: Adresse des Empfängers, optional eigener Betreff und Text; ohne Angabe nimmt die API die Vorbelegung aus
/// <see cref="DocumentMailDraft"/>. Die API legt die Mail mit dem PDF als Anhang in die Queue und antwortet 202.
/// </summary>
public sealed record SendDocumentRequest(string To, string? Subject = null, string? Text = null);

/// <summary>Vorbelegung für den Dialog „Per Mail senden“: Adresse aus der Kundenkarte bzw. den Lieferantendaten (null, wenn keine bekannt ist), fertiger Betreff und Text.</summary>
public sealed record DocumentMailDraft(string? To, string Subject, string Text);

/// <summary>Seitenformat eines Belegs als PDF: A4 (Standard) oder Bon-Format mit 80 mm Breite.</summary>
public enum DocumentPageFormat
{
    A4,
    Receipt,
}

/// <summary>Vermerk am Beleg oder an der Bestellung: wohin, wann und von wem (Vorname) das Dokument per Mail ging, damit die Rezeption Doppelsendungen sieht.</summary>
public sealed record SentDocumentDto(string To, DateTimeOffset SentAt, string? SentBy);
