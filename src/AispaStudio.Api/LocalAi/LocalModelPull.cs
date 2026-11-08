using System.Text.Json;
using AispaStudio.Contracts;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.LocalAi;

/// <summary>Queue: das lokale Modell herunterladen. Der Download dauert Minuten und läuft deshalb nie im HTTP-Aufruf.</summary>
public sealed record LocalModelPullRequested(Guid RequestId);

public static class LocalModelPullRequestedHandler
{
    public static async Task Handle(LocalModelPullRequested message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LocalModelDownloader>().PullAsync(ct);
    }
}

/// <summary>Lädt das Modell über Ollama (<c>POST /api/pull</c>) und schreibt den Fortschritt in <see cref="LocalModelStatus"/>.</summary>
internal sealed class LocalModelDownloader(
    IHttpClientFactory http,
    IOptions<LocalModelOptions> options,
    LocalModelStatus status,
    LocalModelProbe probe)
{
    public async Task PullAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        status.Set(LocalAiState.Downloading, null, 0);
        try
        {
            // Eigener Client ohne Zeitlimit: ein paar Gigabyte brauchen je nach Leitung länger als jede Anfrage.
            using var client = http.CreateClient(LocalModelJson.HttpClientName + "-pull");
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/pull")
            {
                Content = JsonContent.Create(new { model = options.Value.Model, stream = true }),
            };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var progress = JsonSerializer.Deserialize<PullProgress>(line, LocalModelJson.Wire);
                if (progress?.Error is { } error)
                {
                    status.Set(LocalAiState.Missing, $"Herunterladen fehlgeschlagen: {error}");
                    return;
                }

                if (progress?.Total is > 0 and var total && progress.Completed is { } done)
                {
                    status.Set(LocalAiState.Downloading, null, (int)Math.Clamp(done * 100 / total, 0, 100));
                }
            }

            status.Set(LocalAiState.Missing, null);
            await probe.CheckAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException && !ct.IsCancellationRequested)
        {
            status.Set(LocalAiState.Unreachable, "Herunterladen fehlgeschlagen: Der Dienst für das lokale KI-Modell antwortet nicht.");
        }
    }

    private sealed record PullProgress(string? Status, long? Total, long? Completed, string? Error);
}
