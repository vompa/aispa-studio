using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 97 (Ä191): native Maske „KI“ nach AiTab.tsx: Stand von Claude und lokalem Modell, Download mit Nachfrage; es gibt keine Schlüsseleingabe.</summary>
public sealed class AdminAiTests
{
    private const string StatusRoute = "/api/system/ai";
    private const string DownloadRoute = "/api/system/ai/local/download";

    private static AiStatus Status(bool claude = true, LocalAiState local = LocalAiState.Missing, string? model = "gemma3:4b", int? percent = null, string? error = null) =>
        new(claude, local, model, percent, error, null);

    private static async Task<AiViewModel> Open(StubApi api)
    {
        var ai = new AiViewModel(api.Client(), TimeSpan.FromMilliseconds(1));
        await ai.EnsureLoadedAsync();
        return ai;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition(), "Die Bedingung wurde nicht erreicht.");
    }

    [Theory]
    [InlineData(true, "Eingeschaltet.")]
    [InlineData(false, "Aus: auf diesem Server ist kein API-Schlüssel gesetzt.")]
    public async Task Given_Claude_mit_und_ohne_Schluessel_Then_Text_Eingeschaltet_oder_Aus(bool claude, string expected)
    {
        using var ai = await Open(new StubApi().On($"GET {StatusRoute}", Status(claude)));

        Assert.True(ai.IsLoaded);
        Assert.Equal(expected, ai.ClaudeText);
    }

    [Theory]
    [InlineData(LocalAiState.Off, "Nicht eingerichtet. Ohne Internet arbeitet die Terminannahme dann nach festen Regeln.")]
    [InlineData(LocalAiState.Unreachable, "Eingerichtet, aber der Dienst für das lokale Modell antwortet nicht.")]
    [InlineData(LocalAiState.Missing, "Der Dienst läuft, das Modell ist noch nicht geladen.")]
    [InlineData(LocalAiState.Downloading, "Das Modell wird heruntergeladen.")]
    [InlineData(LocalAiState.Ready, "Bereit. Ohne Internet liest das lokale Modell Terminwünsche und Fragen ans Team.")]
    public async Task Given_ein_Zustand_des_lokalen_Modells_Then_sagt_die_Maske_was_er_bedeutet(LocalAiState state, string expected)
    {
        using var ai = await Open(new StubApi().On($"GET {StatusRoute}", Status(local: state, percent: 40)));

        Assert.Equal(expected, ai.LocalText);
        Assert.Equal(state == LocalAiState.Missing, ai.CanDownload);
        Assert.Equal(state == LocalAiState.Downloading, ai.IsDownloading);
    }

    [Fact]
    public async Task Given_ein_Modell_im_Download_Then_nennt_die_Maske_Modell_und_Prozent()
    {
        using var ai = await Open(new StubApi().On($"GET {StatusRoute}", Status(local: LocalAiState.Downloading, percent: 40)));

        Assert.Equal("Modell: gemma3:4b · 40 %", ai.ModelText);
        Assert.Equal(40, ai.DownloadPercent);
    }

    [Fact]
    public async Task Given_ein_fertiges_Modell_Then_steht_nur_der_Name_ohne_Prozent_da()
    {
        using var ai = await Open(new StubApi().On($"GET {StatusRoute}", Status(local: LocalAiState.Ready, percent: 100)));

        Assert.Equal("Modell: gemma3:4b", ai.ModelText);
    }

    [Fact]
    public async Task Given_kein_Modell_eingerichtet_Then_gibt_es_keine_Modellzeile()
    {
        using var ai = await Open(new StubApi().On($"GET {StatusRoute}", Status(local: LocalAiState.Off, model: null)));

        Assert.False(ai.HasModelText);
    }

    [Fact]
    public async Task Given_ein_Fehler_des_lokalen_Modells_Then_steht_er_in_der_Maske()
    {
        using var ai = await Open(new StubApi().On($"GET {StatusRoute}", Status(local: LocalAiState.Unreachable, error: "Der Dienst antwortet nicht auf Port 11434.")));

        Assert.True(ai.HasLocalError);
        Assert.Equal("Der Dienst antwortet nicht auf Port 11434.", ai.LocalError);
    }

    [Fact]
    public async Task Given_der_Status_laesst_sich_nicht_laden_Then_steht_ein_Fehler_mit_Erneut_laden_da()
    {
        var api = new StubApi().On($"GET {StatusRoute}", new { title = "Nicht erlaubt." }, HttpStatusCode.Forbidden);
        using var ai = new AiViewModel(api.Client());

        await ai.EnsureLoadedAsync();

        Assert.False(ai.IsLoaded);
        Assert.Equal("Nicht erlaubt.", ai.LoadError);
    }

    [Fact]
    public async Task Given_KI_Status_Missing_When_Modell_herunterladen_Then_POST_download_und_Stand_wird_nachgefragt()
    {
        var current = Status(local: LocalAiState.Missing);
        var api = new StubApi()
            .OnJson($"GET {StatusRoute}", _ => current)
            .OnJson($"POST {DownloadRoute}", _ =>
            {
                current = Status(local: LocalAiState.Downloading, percent: 5);
                return new { };
            }, HttpStatusCode.Accepted);
        using var ai = await Open(api);
        Assert.True(ai.DownloadCommand.CanExecute(null));

        await ai.DownloadCommand.ExecuteAsync(null);

        Assert.Equal(1, api.Calls.Count(c => c.Route == $"POST {DownloadRoute}"));
        Assert.Equal("Download gestartet. Er läuft im Hintergrund weiter, auch beim Verlassen der Seite.", ai.Message);
        Assert.True(ai.IsDownloading);
        Assert.Equal("Modell: gemma3:4b · 5 %", ai.ModelText);
        Assert.False(ai.CanDownload);
        current = Status(local: LocalAiState.Ready);
        await WaitUntil(() => !ai.IsDownloading);
    }

    [Fact]
    public async Task Given_Downloading_Then_Nachfrage_bis_Ready()
    {
        var polls = 0;
        var api = new StubApi().OnJson($"GET {StatusRoute}", _ =>
            ++polls switch
            {
                1 or 2 => Status(local: LocalAiState.Downloading, percent: 10 * polls),
                _ => Status(local: LocalAiState.Ready, percent: 100),
            });

        using var ai = await Open(api);
        Assert.True(ai.IsDownloading);
        await WaitUntil(() => ai.LocalText.StartsWith("Bereit.", StringComparison.Ordinal));

        Assert.False(ai.IsDownloading);
        var gets = api.Calls.Count(c => c.Route == $"GET {StatusRoute}");
        Assert.Equal(3, gets);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(gets, api.Calls.Count(c => c.Route == $"GET {StatusRoute}"));
    }

    [Fact]
    public async Task Given_die_Nachfrage_scheitert_im_Download_When_die_Maske_wieder_geoeffnet_wird_Then_laedt_sie_neu()
    {
        var current = Status(local: LocalAiState.Downloading, percent: 10);
        var api = new StubApi().OnJson($"GET {StatusRoute}", _ => current);
        using var ai = await Open(api);

        api.Down = true;
        await Task.Delay(100, TestContext.Current.CancellationToken);
        api.Down = false;
        current = Status(local: LocalAiState.Ready, percent: 100);
        await ai.EnsureLoadedAsync();

        Assert.False(ai.IsDownloading);
        Assert.StartsWith("Bereit.", ai.LocalText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_der_Download_wird_abgelehnt_When_Modell_herunterladen_Then_nennt_die_Meldung_den_Grund()
    {
        var api = new StubApi()
            .On($"GET {StatusRoute}", Status(local: LocalAiState.Missing))
            .On($"POST {DownloadRoute}", new { title = "Es läuft schon ein Download." }, HttpStatusCode.Conflict);
        using var ai = await Open(api);

        await ai.DownloadCommand.ExecuteAsync(null);

        Assert.Equal("Es läuft schon ein Download.", ai.Message);
        Assert.Equal(Controls.InfoBarSeverity.Error, ai.MessageSeverity);
    }

    [Fact]
    public async Task Given_die_KI_Maske_Then_gibt_es_nichts_zu_speichern()
    {
        using var ai = await Open(new StubApi().On($"GET {StatusRoute}", Status()));

        Assert.False(ai.HasUnsavedChanges);
        Assert.True(await ai.SaveChangesAsync());
    }
}
