using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Beispiel-Spa in der eigenen Datenbank (ADR 0100): Die Leiste über allen Seiten sagt, dass die Daten Beispieldaten sind, und löscht sie auf Wunsch.
/// Die echte Datenbank bleibt unberührt; die API löscht nur ihre Beispiel-Datenbank und beendet sich danach.
/// </summary>
public partial class ShellViewModel
{
    public const string DemoBarText = "Sie sehen Beispieldaten.";

    private readonly IConfirmDialogs? _demoDialogs;
    private readonly Action? _closeApp;
    private readonly bool _runsLocally;

    /// <summary>Die API läuft mit dem Beispiel-Spa (<c>SystemModules.Demo</c>).</summary>
    public bool IsDemo { get; }

    /// <summary>Die Leiste sehen nur Personen, die den Standort verwalten dürfen; nur sie dürfen die Beispieldaten löschen.</summary>
    public bool ShowDemoBar => IsDemo && User.Rights.Contains(AccessRight.ManageLocation);

    /// <summary>Vor dem Löschen: Folge zuerst; im Einzelplatz schließt sich die App danach, am Server beendet sich der Server.</summary>
    internal ConfirmRequest DeleteDemoRequest => new("Beispieldaten löschen?",
        "Das Beispiel-Spa mit allen Terminen, Gästen und Belegen ist danach gelöscht und lässt sich nicht wiederherstellen. "
        + (_runsLocally ? "Ihre eigenen Daten bleiben unberührt. Die App schließt sich danach."
            : "Die eigenen Daten des Servers bleiben unberührt. Der Server beendet sich danach."),
        "Löschen");

    /// <summary>Nach dem Löschen am Server: ohne Entfernen der Einstellung legt der nächste Start neue Beispieldaten an.</summary>
    internal static ConfirmRequest DemoDeletedOnServer { get; } = new("Beispieldaten gelöscht",
        "Der Server beendet sich jetzt. Vor dem Neustart AISPA_DEMO entfernen (bei Docker Compose auf false setzen), sonst legt er neue Beispieldaten an.",
        "", IsDestructive: false, CancelText: "Schließen");

    [RelayCommand]
    private async Task DeleteDemoAsync()
    {
        if (!ShowDemoBar || _demoDialogs is null)
        {
            return;
        }

        if (!(await _demoDialogs.AskAsync(DeleteDemoRequest).ConfigureAwait(true)).IsConfirmed)
        {
            return;
        }

        var deleted = await _api.DeleteAsync<object>("api/system/demo").ConfigureAwait(true);
        if (!deleted.Ok)
        {
            Toast = $"Die Beispieldaten ließen sich nicht löschen. {deleted.Error}";
            return;
        }

        if (_runsLocally)
        {
            _closeApp?.Invoke();
            return;
        }

        await _demoDialogs.AskAsync(DemoDeletedOnServer).ConfigureAwait(true);
    }
}
