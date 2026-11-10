namespace AispaStudio.Api.Documents;

/// <summary>
/// Kopf und Fuß jedes Dokuments (dokumente.md): Spa-Name groß, Firmendaten klein rechts, Standort darunter; unten „Erstellt am … von …“ mit dem Vornamen der Person,
/// die das Dokument erzeugt. Zeiten stehen in der Zeitzone des Standorts.
/// </summary>
public sealed record DocumentHeader(
    string SpaName,
    IReadOnlyList<string> CompanyLines,
    IReadOnlyList<string> LocationLines,
    DateTimeOffset CreatedAt,
    TimeZoneInfo Zone,
    string? CreatedBy)
{
    public string CreatedText => $"Erstellt am {DocumentBuilder.DateTime(CreatedAt, Zone)}{(CreatedBy is null ? "" : $" von {CreatedBy}")}";
}
