using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Was die Person im Lösch-Dialog gewählt hat (ADR 0099).</summary>
public enum DeletionChoice
{
    /// <summary>„Abbrechen“ oder „Schließen“: es geschieht nichts.</summary>
    Cancelled,

    /// <summary>Das Objekt ist frei und die Person bestätigt „Löschen“.</summary>
    Delete,

    /// <summary>Das Objekt lässt sich nicht löschen und die Person wählt den Weg, den die API nennt („Nicht mehr verwenden“).</summary>
    Alternative,
}

/// <summary>Antwort von <see cref="DeletionDialog.AskAsync"/>: die Wahl oder, wenn die Prüfung scheiterte, die Meldung der API.</summary>
public sealed record DeletionOutcome(DeletionChoice Choice, string? Error = null);

/// <summary>
/// Der gemeinsame Dialog „Löschen oder nicht“ für alle Objekte, die sich anlegen lassen (ADR 0099). Die API prüft (<c>GET …/deletion-check</c>), die App zeigt nur an:
/// Hängt nichts am Objekt, fragt der Dialog „„X“ löschen?“ mit „Löschen“ (rot). Hängt etwas daran, steht „„X“ lässt sich nicht löschen“ mit der Liste, was daran hängt,
/// und – wenn die API einen kennt – dem Knopf für den Weg statt des Löschens („Nicht mehr verwenden“). Texte und Zählung kommen aus der API.
/// </summary>
public static class DeletionDialog
{
    /// <summary>Der Dialog zu einer Prüfung der API.</summary>
    public static ConfirmRequest RequestFor(string name, DeletionCheckDto check)
    {
        if (check.CanDelete)
        {
            return new ConfirmRequest($"„{name}“ löschen?", $"Danach ist „{name}“ gelöscht. Das lässt sich nicht rückgängig machen.", "Löschen");
        }

        var message = check.Dependents.Count == 0
            ? check.Summary
            : $"{check.Summary}\n{string.Join('\n', check.Dependents.Select(d => $"• {d.Label}"))}";
        return check.Alternative is { } alternative
            ? new ConfirmRequest($"„{name}“ lässt sich nicht löschen", message, alternative.Label, IsDestructive: false)
            : new ConfirmRequest($"„{name}“ lässt sich nicht löschen", message, "", IsDestructive: false, CancelText: "Schließen");
    }

    /// <summary>Fragt die API, zeigt den Dialog und gibt die Wahl zurück; ohne Dialogfenster (Tests ohne Fenster) gilt „Abbrechen“.</summary>
    public static async Task<DeletionOutcome> AskAsync(IAispaApi api, IConfirmDialogs? dialogs, string checkPath, string name)
    {
        var result = await api.GetAsync<DeletionCheckDto>(checkPath).ConfigureAwait(true);
        if (result.Value is not { } check)
        {
            return new DeletionOutcome(DeletionChoice.Cancelled, result.Error ?? "Die Prüfung vor dem Löschen ist gescheitert.");
        }

        if (dialogs is null)
        {
            return new DeletionOutcome(DeletionChoice.Cancelled);
        }

        var answer = await dialogs.AskAsync(RequestFor(name, check)).ConfigureAwait(true);
        return new DeletionOutcome(answer.Choice switch
        {
            ConfirmChoice.Confirmed when check.CanDelete => DeletionChoice.Delete,
            ConfirmChoice.Confirmed => DeletionChoice.Alternative,
            _ => DeletionChoice.Cancelled,
        });
    }
}
