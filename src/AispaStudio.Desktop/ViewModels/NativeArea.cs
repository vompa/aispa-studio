namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Wohin ein Sprung in die Verwaltung führt (Kalender, Assistent; R05 Slice 95): die Angaben, die <see cref="AdminPageViewModel.Show"/> bekommt, für die native Maske des Bereichs.
/// <c>Id</c> ist die Behandlung, der Raum oder was der Bereich sonst als Ziel kennt, <c>Location</c> der Standort, <c>From</c> und <c>Until</c> ein Zeitraum für ein neues Formular,
/// <c>Field</c> die Feldkennung aus <c>FieldMasks</c>, die den Fokus bekommt, <c>Section</c> der Reiter des Bereichs („management“ in den Berichten).
/// </summary>
public sealed record AdminTarget(Guid? Id = null, Guid? Location = null, DateOnly? From = null, DateOnly? Until = null, string? Field = null, string? Section = null);

/// <summary>Eine native Maske, die den Standort und einen Zeitraum des Sprungs kennt (<see cref="INativeArea.Open"/> kennt nur Eintrag, Abschnitt und Feld).</summary>
public interface ITargetedArea : INativeArea
{
    /// <summary>Ein Sprung aus Kalender oder Assistent: die Maske wählt Standort, Zeile oder Zeitraum und setzt den Fokus; steht sie noch nicht, gilt das Ziel nach dem Laden.</summary>
    void Open(AdminTarget target);
}
