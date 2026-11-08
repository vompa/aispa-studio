namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Maske der Verwaltung mit Speichern und Verwerfen (R05 Slice 93): was die Rückfrage beim Verlassen braucht.</summary>
public interface IEditableArea
{
    /// <summary>Name der Maske in der Rückfrage, z. B. „Firmendaten“.</summary>
    string Title { get; }

    bool HasUnsavedChanges { get; }

    /// <summary>Speichert; false, wenn es nicht ging (Fehler am Feld oder von der API), dann bleibt die Maske offen.</summary>
    Task<bool> SaveChangesAsync();

    /// <summary>Setzt alle Felder auf den gespeicherten Stand.</summary>
    void Discard();
}

/// <summary>
/// Rückfrage beim Verlassen einer Maske mit ungespeicherten Änderungen (Design-Brief 5.27, docs/gestaltung/meldungen.md): „Änderungen speichern?“ mit
/// Speichern (primär, Anfangsfokus), Verwerfen und Abbrechen. Gilt für jeden Weg aus der Maske: Navigation, Sprünge aus Kalender und Assistent, Bereichswechsel.
/// </summary>
public static class UnsavedChanges
{
    /// <summary>true, wenn die Maske verlassen werden darf: nichts geändert, gespeichert oder verworfen; false bei „Abbrechen“ oder wenn Speichern scheitert.</summary>
    public static async Task<bool> ConfirmLeaveAsync(IConfirmDialogs? dialogs, IEditableArea area)
    {
        if (!area.HasUnsavedChanges || dialogs is null)
        {
            return true;
        }

        var answer = await dialogs.AskAsync(new ConfirmRequest(
            "Änderungen speichern?", $"Ohne Speichern gehen die Änderungen in „{area.Title}“ verloren.", "Speichern",
            IsDestructive: false, AlternativeText: "Verwerfen")).ConfigureAwait(true);
        switch (answer.Choice)
        {
            case ConfirmChoice.Confirmed:
                return await area.SaveChangesAsync().ConfigureAwait(true);
            case ConfirmChoice.Alternative:
                area.Discard();
                return true;
            default:
                return false;
        }
    }
}
