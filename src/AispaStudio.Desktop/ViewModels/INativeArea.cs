namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Eine native Maske der Verwaltung (R05, ADR 0084): die Seite „Verwaltung“ lädt sie beim Öffnen des Bereichs und reicht Sprungziele aus Kalender und Assistent
/// an sie weiter (Behandlung, Abschnitt, Feld).
/// </summary>
public interface INativeArea : IEditableArea
{
    /// <summary>Lädt nur, wenn noch nichts geladen ist (beim Öffnen des Bereichs).</summary>
    Task EnsureLoadedAsync();

    /// <summary>Öffnet die Maske an einer Stelle: <paramref name="id"/> wählt einen Eintrag, <paramref name="section"/> einen Abschnitt, <paramref name="field"/> setzt den Fokus.</summary>
    void Open(Guid? id, string? section, string? field);
}
