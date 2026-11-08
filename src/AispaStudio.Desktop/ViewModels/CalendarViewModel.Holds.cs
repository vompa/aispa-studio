using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Vormerkungen ohne Antwort des Gastes (ADR 0083): Meldung „Gast hat sich nicht gemeldet“ ----------
public partial class CalendarViewModel
{
    /// <summary>So oft holt der Kalender die offenen Vormerkungen nach, auch ohne neues Laden des Tages.</summary>
    private static readonly TimeSpan HoldsRefresh = TimeSpan.FromMinutes(5);

    private List<AppointmentHoldDto> _holds = [];
    private DateTimeOffset _holdsLoadedAt = DateTimeOffset.MinValue;

    /// <summary>Öffnet die Anfrage zur Vormerkung im Posteingang; gesetzt von der Shell.</summary>
    public Func<Guid, Task>? InboxRequested { get; set; }

    private async Task LoadHoldsAsync(LocationDto location)
    {
        _holdsLoadedAt = _time.GetUtcNow();
        if (!CanBook)
        {
            _holds = [];
            return;
        }

        var result = await _api.GetAsync<List<AppointmentHoldDto>>($"api/locations/{location.Id}/appointment-holds").ConfigureAwait(true);
        if (location.Id == Location?.Id && result.Value is { } holds)
        {
            _holds = holds;
            RefreshMessages();
        }
    }

    /// <summary>Für den Takt des Kalenders: alle paar Minuten die Vormerkungen neu holen (eine neue Vormerkung kommt aus dem Posteingang).</summary>
    private void RefreshHoldsIfDue()
    {
        if (Location is { } location && _time.GetUtcNow() - _holdsLoadedAt >= HoldsRefresh)
        {
            _ = LoadHoldsAsync(location);
        }
    }

    /// <summary>Ab dem Zeitpunkt, den der Server rechnet, erinnert eine Warnung je Vormerkung; „Zur Anfrage“ führt zu Bestätigen und Freigeben.</summary>
    private void AddHolds(MessageState state, List<MessageItem> all)
    {
        var now = _time.GetUtcNow();
        foreach (var hold in _holds.Where(h => h.RemindAt <= now))
        {
            var when = DayLayout.ShortDayTime(hold.Start, _zone);
            var open = hold.MailId is { } mailId && InboxRequested is { } request ? () => _ = request(mailId) : (Action?)null;
            all.Add(Message(state, $"hold:{hold.AppointmentId}", MessageKind.Hold, MessageSeverity.Warning, "Gast hat sich nicht gemeldet",
                $"{hold.CustomerName}: {hold.TreatmentName}, {when} ist vorgemerkt. Bestätigen oder freigeben.", $"{hold.CustomerName} hat sich nicht gemeldet",
                trigger: hold.RemindAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture), since: hold.RemindAt,
                actionLabel: "Zur Anfrage", action: open));
        }
    }
}
