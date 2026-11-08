using System.Text.Json;
using AispaStudio.Contracts;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.LocalAi;

/// <summary>Letzter bekannter Zustand des lokalen Modells. Die Oberfläche liest nur diesen Stand, sie fragt Ollama nie selbst.</summary>
internal sealed class LocalModelStatus(IOptions<LocalModelOptions> options, TimeProvider time)
{
    private readonly Lock _lock = new();
    private LocalAiState _state = options.Value.Enabled ? LocalAiState.Unreachable : LocalAiState.Off;
    private int? _percent;
    private string? _error;
    private DateTimeOffset? _checkedAt;

    public (LocalAiState Local, int? Percent, string? Error, DateTimeOffset? CheckedAt) Current
    {
        get
        {
            lock (_lock)
            {
                return (_state, _percent, _error, _checkedAt);
            }
        }
    }

    public string? Model => options.Value.Enabled ? options.Value.Model : null;

    public void Set(LocalAiState state, string? error, int? percent = null)
    {
        lock (_lock)
        {
            _state = state;
            _error = error;
            _percent = percent;
            _checkedAt = time.GetUtcNow();
        }
    }

    /// <summary>Während eines Downloads überschreibt die Prüfung den Fortschritt nicht.</summary>
    public bool Downloading
    {
        get
        {
            lock (_lock)
            {
                return _state == LocalAiState.Downloading;
            }
        }
    }
}

/// <summary>Fragt Ollama, ob es läuft und ob das Modell da ist (<c>GET /api/tags</c>).</summary>
internal sealed class LocalModelProbe(IHttpClientFactory http, IOptions<LocalModelOptions> options, LocalModelStatus status)
{
    public async Task CheckAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled || status.Downloading)
        {
            return;
        }

        try
        {
            using var client = http.CreateClient(LocalModelJson.HttpClientName);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var tags = await client.GetFromJsonAsync<TagsResponse>("api/tags", LocalModelJson.Wire, timeout.Token);
            var present = tags?.Models?.Any(m => IsModel(m.Name, options.Value.Model)) == true;
            if (!status.Downloading)
            {
                status.Set(present ? LocalAiState.Ready : LocalAiState.Missing, null);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            status.Set(LocalAiState.Unreachable, "Der Dienst für das lokale KI-Modell antwortet nicht.");
        }
    }

    /// <summary>Ollama hängt ohne Angabe „:latest“ an; „gemma3“ und „gemma3:latest“ sind dasselbe Modell.</summary>
    internal static bool IsModel(string? name, string wanted)
    {
        static string Full(string n) => n.Contains(':', StringComparison.Ordinal) ? n : $"{n}:latest";
        return name is not null && string.Equals(Full(name.Trim()), Full(wanted.Trim()), StringComparison.OrdinalIgnoreCase);
    }

    private sealed record TagsResponse(List<TagModel>? Models);

    private sealed record TagModel(string? Name);
}

/// <summary>Prüft regelmäßig das lokale Modell (<c>Ai:Local:CheckSeconds</c>), damit die Einstellungen den Stand zeigen.</summary>
internal sealed class LocalModelMonitor(LocalModelProbe probe, IOptions<LocalModelOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || options.Value.CheckSeconds <= 0)
        {
            return;
        }

        await probe.CheckAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(10, options.Value.CheckSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await probe.CheckAsync(stoppingToken);
        }
    }
}
