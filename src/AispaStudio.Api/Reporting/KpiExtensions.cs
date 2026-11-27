using AispaStudio.Contracts;

namespace AispaStudio.Api.Reporting;

internal static class KpiExtensions
{
    /// <summary>Name der Kennzahl für Ausgaben ohne eigene Hinweiszeile (PDF, Excel, Word, Mail): mit Zusatz in Klammern, wenn die Kachel einen hat.</summary>
    public static string LabelWithNote(this Kpi kpi) => kpi.Note is { Length: > 0 } note ? $"{kpi.Label} ({note})" : kpi.Label;
}
