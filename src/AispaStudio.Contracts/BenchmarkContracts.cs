namespace AispaStudio.Contracts;

// Ein-Klick-Vergleich mit anderen Spas: die eigenen Kennzahlen eines Standorts neben anonymisierten Werten vergleichbarer Spas
// (gleiches Land, ähnliche Größe). Andere Spas erscheinen nur als Verteilung (unteres Viertel, Mitte, oberes Viertel), nie einzeln.

public enum BenchmarkRating
{
    /// <summary>Besser als drei Viertel der Vergleichsgruppe.</summary>
    Top,

    /// <summary>Besser als die Mitte.</summary>
    AboveMedian,

    /// <summary>Schlechter als die Mitte.</summary>
    BelowMedian,

    /// <summary>Schlechter als drei Viertel der Vergleichsgruppe.</summary>
    Bottom,
}

/// <summary>
/// Eine Kennzahl im Vergleich. <c>Own</c> ist leer, wenn der Standort im Zeitraum dafür keine Daten hat; dann fehlt auch
/// <c>Rating</c>. <c>Median</c>: Mitte der Vergleichsgruppe; <c>P25</c>: Grenze zum schwächsten Viertel, <c>P75</c>: Grenze zum
/// stärksten Viertel. <c>HigherIsBetter</c> sagt, in welche Richtung „besser“ zeigt: bei der No-Show-Quote ist weniger besser,
/// dort ist <c>P75</c> also kleiner als <c>P25</c>. <c>Tip</c>: Hebel, wenn der Standort unter der Mitte liegt.
/// </summary>
public sealed record BenchmarkMetric(
    string Key,
    string Label,
    string Unit,
    decimal? Own,
    decimal P25,
    decimal Median,
    decimal P75,
    bool HigherIsBetter,
    BenchmarkRating? Rating,
    string? Tip);

/// <summary>
/// Vergleich eines Standorts für einen Zeitraum (Ortszeit, beide Tage eingeschlossen). <c>PeerGroup</c> beschreibt die
/// Vergleichsgruppe, <c>PeerCount</c> wie viele Spas darin sind. <c>IsSampleData</c>: die Vergleichswerte sind Beispieldaten
/// (Prototyp), noch keine echten Werte anderer Spas. <c>Notes</c>: Hinweise zur Aussagekraft.
/// </summary>
public sealed record BenchmarkReport(
    Guid LocationId,
    string LocationName,
    DateOnly From,
    DateOnly Until,
    string Currency,
    string PeerGroup,
    int PeerCount,
    bool IsSampleData,
    IReadOnlyList<BenchmarkMetric> Metrics,
    IReadOnlyList<string> Notes);
