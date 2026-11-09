namespace AispaStudio.Api.Documents;

/// <summary>Seitenformat eines Dokuments: A4 hoch, A4 quer oder Bon mit 80 mm Breite, dessen Höhe mit dem Inhalt wächst (eine Seite).</summary>
public enum DocumentFormat
{
    A4Portrait,
    A4Landscape,
    Receipt80,
}

public enum DocumentAlign
{
    Left,
    Center,
    Right,
}

/// <summary>Spalte einer Tabelle: Titel, Gewicht als Anteil an der Breite und Ausrichtung; Beträge stehen rechtsbündig (dokumente.md).</summary>
public sealed record DocumentColumn(string Title, double Weight = 1, DocumentAlign Align = DocumentAlign.Left);

/// <summary>
/// Zeile einer Tabelle; <paramref name="Bold"/> für Summen, <paramref name="Muted"/> ganze Zeile grau hinterlegt, <paramref name="MutedCells"/> nur die genannten Zellen
/// (Nummern ab 0), z. B. die Pause einer Person im Tagesplan.
/// </summary>
public sealed record DocumentRow(IReadOnlyList<string> Cells, bool Bold = false, bool Muted = false, IReadOnlyList<int>? MutedCells = null)
{
    public static DocumentRow Of(params string[] cells) => new(cells);
}
