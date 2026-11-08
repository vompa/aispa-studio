using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Steuert ein Kartenterminal im Netz am Empfang (Anbieter „Local“, ADR 0034): holt Zahlungen, Stornos und Gutschriften beim Server ab,
/// führt sie am Gerät aus und meldet das Ergebnis. Läuft nur auf dem PC, der in den Geräte-Einstellungen dafür gewählt ist,
/// damit nie zwei PCs denselben Auftrag ans Gerät schicken.
/// </summary>
public sealed partial class TerminalAgent(IWorkstationApi api, Guid terminalId, ICardTerminalDriver driver, TimeSpan? interval = null) : ObservableObject, IAsyncDisposable
{
    private readonly HashSet<Guid> _done = [];
    private readonly Dictionary<Guid, CardTerminalResult> _unreported = [];
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;

    public Guid TerminalId => terminalId;

    /// <summary>Zuletzt Erlebtes für die Anzeige, z. B. „Zahlung 89,00 € genehmigt“.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Wartet auf Zahlungen.";

    /// <summary>Ausgang ungewiss (Verbindung während der Zahlung abgebrochen): an der Kasse prüfen, nicht automatisch melden.</summary>
    [ObservableProperty]
    public partial bool NeedsAttention { get; private set; }

    /// <summary>Auf dem Oberflächen-Thread starten; gewartet wird asynchron, damit Status und Hinweis direkt angezeigt werden können.</summary>
    public void Start() => _loop ??= RunAsync(_stop.Token);

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(ct).ConfigureAwait(true);
                await Task.Delay(interval ?? TimeSpan.FromSeconds(2), ct).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>Ein Durchgang: alle offenen Aufträge nacheinander (ein Gerät kann nur eine Zahlung zugleich).</summary>
    public async Task PollOnceAsync(CancellationToken ct)
    {
        // Ergebnisse, die der Server noch nicht bekommen hat (z. B. Netz weg), zuerst nachmelden.
        foreach (var (id, open) in _unreported.ToList())
        {
            if ((await api.PostAsync<CardPaymentDto>($"api/pos/card-payments/{id}/result", open, ct).ConfigureAwait(true)).Ok)
            {
                _unreported.Remove(id);
            }
        }

        var jobs = await api.GetAsync<List<CardPaymentDto>>($"api/pos/terminals/{terminalId}/jobs", ct).ConfigureAwait(true);
        foreach (var job in jobs.Value ?? [])
        {
            // Jeder Auftrag geht höchstens einmal ans Gerät; ein zweites Mal könnte doppelt abbuchen.
            if (!_done.Add(job.Id))
            {
                continue;
            }

            var (kind, genitive, done) = job.Status switch
            {
                CardPaymentStatus.ReversalPending => ("Storno", "des Stornos", "storniert"),
                CardPaymentStatus.RefundPending => ("Gutschrift", "der Gutschrift", "gutgeschrieben"),
                _ => ("Zahlung", "der Zahlung", "genehmigt"),
            };
            CardTerminalResult result;
            try
            {
                Status = job.Status == CardPaymentStatus.RefundPending
                    ? $"Gutschrift {Money(job)} läuft am Terminal: bitte die Karte des Gastes vorlegen …"
                    : $"{kind} {Money(job)} läuft am Terminal …";
                result = job.Status switch
                {
                    CardPaymentStatus.ReversalPending => await driver.ReverseAsync(job.Reference, ct).ConfigureAwait(true),
                    CardPaymentStatus.RefundPending => await driver.RefundAsync(job.Amount, ct).ConfigureAwait(true),
                    _ => await driver.PayAsync(job.Amount, ct).ConfigureAwait(true),
                };
            }
            catch (TerminalUnreachableException ex)
            {
                // Nichts ans Gerät geschickt: sicher als nicht genehmigt melden.
                result = new(false, ex.Message, null, null, null, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                NeedsAttention = true;
                Status = $"Verbindung zum Terminal während {genitive} über {Money(job)} abgebrochen. " +
                    (job.Status == CardPaymentStatus.RefundPending
                        ? "Bitte am Terminal den letzten Beleg prüfen und die Gutschrift falls nötig direkt am Terminal ausführen."
                        : "Bitte am Terminal den letzten Beleg prüfen und die Zahlung an der Kassa abbrechen oder stornieren.");
                continue;
            }

            var reported = await api.PostAsync<CardPaymentDto>($"api/pos/card-payments/{job.Id}/result", result, ct).ConfigureAwait(true);
            if (!reported.Ok && reported.Status == 0)
            {
                _unreported[job.Id] = result;
            }

            Status = reported.Ok
                ? $"{kind} {Money(job)} {(result.Approved ? done : "nicht " + done)}."
                : $"Ergebnis konnte nicht gemeldet werden: {reported.Error}";
        }
    }

    private static string Money(CardPaymentDto job) => ViewModels.Money.Format(job.Amount);

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(true);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _stop.Dispose();
    }
}
