using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Seite „Netz“ im Server-Betrieb (ADR 0084, Punkt 5): zeigt, womit die App verbunden ist, und löst einen eingetragenen Server oder Haupt-PC
/// wieder. Die Umgebungsvariable <c>AISPA_API_URL</c> hat Vorrang und lässt sich hier nicht lösen.
/// </summary>
/// <param name="current">Die beim Start gewählte Verbindung.</param>
/// <param name="restart">Startet die App neu, damit sie ohne den Eintrag arbeitet.</param>
/// <param name="forgetSignIn">Widerruft den lokalen Zugang zum bisherigen Server.</param>
public partial class ServerLinkViewModel(ISettingsStore settings, ServerChoice.Choice current, Action restart, Action? forgetSignIn = null) : ViewModelBase
{
    internal ConnectionSettings Connection { get; init; } = new(settings, restart, forgetSignIn);

    [ObservableProperty]
    public partial string? Error { get; private set; }

    public string Text => current.Source switch
    {
        ServerSource.Server => $"Die App ist mit dem Server {current.Address} verbunden.",
        ServerSource.MainPc => $"Die App ist mit dem Haupt-PC {current.Address} im Netz verbunden.",
        ServerSource.Environment => $"Die App ist über die Umgebungsvariable AISPA_API_URL mit {current.Address} verbunden. Sie hat Vorrang vor einem eingetragenen Server und lässt sich hier nicht lösen.",
        _ => $"Es ist kein Server eingetragen. Die App sucht ihn unter {current.Address}.",
    };

    private bool CanRelease => current.Source is ServerSource.Server or ServerSource.MainPc;

    [RelayCommand(CanExecute = nameof(CanRelease))]
    private Task ReleaseAsync() => Connection.ChangeAsync(null, null, message => Error = message);
}

internal sealed class ConnectionSettings(ISettingsStore settings, Action restart, string? environmentUrl, Func<Task> endContext,
    Action<string, string>? showClosedError, Action<Exception>? log)
{
    public ConnectionSettings(ISettingsStore settings, Action restart, Action? forgetSignIn)
        : this(settings, restart, null, () => { forgetSignIn?.Invoke(); return Task.CompletedTask; }, null, null)
    {
    }

    private int _changing;

    public async Task ChangeAsync(Uri? address, string? code, Action<string?> showError)
    {
        if (Interlocked.Exchange(ref _changing, 1) != 0)
        {
            showError("Ein Verbindungswechsel läuft bereits.");
            return;
        }

        try
        {
            showError(null);
            var changed = false;
            var closing = false;
            var publishing = false;
            try
            {
                var store = settings as IPreparedSettingsStore ?? throw new IOException("Dieser Speicher kann eine Änderung der Verbindung nicht bestätigen.");
                using var write = store.Prepare(previous =>
                {
                    var updated = previous with { ServerAddress = address?.ToString(), ServerCode = code };
                    var before = ServerChoice.Resolve(previous, environmentUrl);
                    var after = ServerChoice.Resolve(updated, environmentUrl);
                    changed = before.Address != after.Address || !SamePin(before.Code, after.Code);
                    return changed ? updated with { AssistantHistory = null } : updated;
                });
                if (changed)
                {
                    closing = true;
                    await endContext().ConfigureAwait(true);
                }

                publishing = true;
                write.CommitAndVerify();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException or NotSupportedException
                || closing && error is AggregateException)
            {
                var message = publishing ? "Der Serverwechsel konnte nicht bestätigt werden. Die App wurde nicht neu gestartet."
                    : closing ? "Die bisherige Anmeldung konnte nicht vollständig entfernt werden. Die Serververbindung wurde nicht geändert."
                    : "Die Änderung der Serververbindung konnte nicht vorbereitet werden. Die App wurde nicht neu gestartet.";
                log?.Invoke(error);
                if (closing && showClosedError is not null)
                {
                    showClosedError("Serverwechsel unterbrochen", message);
                }
                else
                {
                    showError(message);
                }

                return;
            }

            restart();
        }
        finally
        {
            Volatile.Write(ref _changing, 0);
        }
    }

    private static bool SamePin(string? first, string? second) => first == second ||
        first is not null && second is not null && NetworkShare.NormalizeCode(first) is { } normalized && normalized == NetworkShare.NormalizeCode(second);
}
