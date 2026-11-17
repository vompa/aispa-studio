namespace AispaStudio.Contracts;

/// <summary>Eine Art von Daten, die an einem Objekt hängt, mit Anzahl und fertigem deutschem Text („3 künftige Termine“).</summary>
public sealed record DependentCount(string Kind, int Count, string Label);

/// <summary>
/// Der Weg, der statt des Löschens bleibt (ADR 0099): <c>Kind</c> ist <c>deactivate</c> („Nicht mehr verwenden“); <c>Label</c> ist der Text des Knopfes.
/// </summary>
public sealed record DeletionAlternative(string Kind, string Label);

/// <summary>
/// Antwort von <c>GET …/{id}/deletion-check</c>: ob sich das Objekt löschen lässt, was daran hängt und was stattdessen geht. <c>Summary</c> ist der Satz für den Dialog;
/// <c>Dependents</c> zählt je Art, nur Arten mit mindestens einem Eintrag.
/// </summary>
public sealed record DeletionCheckDto(bool CanDelete, string Summary, IReadOnlyList<DependentCount> Dependents, DeletionAlternative? Alternative = null);
