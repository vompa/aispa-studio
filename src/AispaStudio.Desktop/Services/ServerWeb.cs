using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Web-Adresse des Servers für die Hilfe (ADR 0079) und die Seite „Anmelden“ (Slice 103, Annahme 8): zuerst die vorgegebene Adresse (<c>AISPA_TEAM_WEB_URL</c>,
/// setzt der Aspire-AppHost), sonst die Adresse, die der Server unter <c>/api/system/team-web</c> nennt, sonst die Adresse des Servers selbst.
/// Ob dort wirklich eine Hilfe-Seite liegt, prüft <see cref="HelpPageCheck"/> vor dem Laden.
/// </summary>
/// <param name="configured">Vorgegebene Adresse; ohne Angabe <c>AISPA_TEAM_WEB_URL</c>.</param>
public sealed class ServerWeb(IWorkstationApi api, Uri? configured = null)
{
    private readonly Uri? _configured = configured
        ?? (Uri.TryCreate(Environment.GetEnvironmentVariable("AISPA_TEAM_WEB_URL"), UriKind.Absolute, out var url) ? url : null);

    private Uri? _named;
    private Task? _lookup;

    /// <summary>Wurzel der Web-Adresse; die Hilfe liegt darunter unter <c>help/</c>. Vor <see cref="LookUpAsync"/> ohne die Antwort des Servers.</summary>
    public Uri Address => new(_configured ?? _named ?? api.BaseAddress, "/");

    /// <summary>Fragt einmal den Server nach seiner Web-Adresse; mit vorgegebener Adresse nie. Weitere Aufrufe warten auf dieselbe Antwort.</summary>
    public Task LookUpAsync() => _configured is not null ? Task.CompletedTask : _lookup ??= AskServerAsync();

    private async Task AskServerAsync()
    {
        var info = await api.GetAsync<TeamWebInfo>("api/system/team-web").ConfigureAwait(true);
        if (Uri.TryCreate(info.Value?.Url, UriKind.Absolute, out var named))
        {
            _named = named;
        }
    }

    /// <summary>
    /// Die Seite „Anmelden“ (<c>/anmelden/</c>), auf der es den Code für die Windows-App gibt. Unter der Adresse des Servers nur, wenn er die Seite selbst ausliefert
    /// (Einzelplatz, Server mit Web-Ordner); sonst <c>null</c>, weil die Adresse der API dort ins Leere führt.
    /// </summary>
    public async Task<Uri?> SignInPageAsync()
    {
        await LookUpAsync().ConfigureAwait(true);
        if ((_configured ?? _named) is { } web)
        {
            return new Uri(web, "/anmelden/");
        }

        var own = new Uri(api.BaseAddress, "/anmelden/");
        return await api.IsPageAsync(own).ConfigureAwait(true) ? own : null;
    }
}
