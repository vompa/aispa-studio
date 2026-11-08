using System.Globalization;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Personalakte (R05 Slice 110): Erinnerung an fällige Folgegespräche bei der Leitung ----------
public partial class CalendarViewModel
{
    /// <summary>So oft holt der Kalender die fälligen Folgetermine nach, auch ohne neues Laden des Tages.</summary>
    private static readonly TimeSpan HrFollowUpsRefresh = TimeSpan.FromMinutes(5);

    private List<HrFollowUpDto> _hrFollowUps = [];
    private DateTimeOffset _hrFollowUpsLoadedAt = DateTimeOffset.MinValue;

    /// <summary>Öffnet die Personalakte einer Person; gesetzt von der Shell.</summary>
    public Action<Guid>? HrFileRequested { get; set; }

    private async Task LoadHrFollowUpsAsync()
    {
        _hrFollowUpsLoadedAt = _time.GetUtcNow();
        if (!_user.Rights.Contains(AccessRight.ManageHrFile))
        {
            return;
        }

        // Welcher Termin fällig ist, rechnet der Server; ohne Antwort bleibt der letzte Stand.
        var result = await _api.GetAsync<List<HrFollowUpDto>>("api/hr-file/follow-ups").ConfigureAwait(true);
        if (result.Value is { } followUps)
        {
            _hrFollowUps = followUps;
            RefreshMessages();
        }
    }

    /// <summary>Für den Takt des Kalenders: alle paar Minuten die Folgetermine neu holen; sie enden, sobald das Folgegespräch als geführt eingetragen oder der Termin geändert ist.</summary>
    private void RefreshHrFollowUpsIfDue()
    {
        if (_time.GetUtcNow() - _hrFollowUpsLoadedAt >= HrFollowUpsRefresh)
        {
            _ = LoadHrFollowUpsAsync();
        }
    }

    /// <summary>Ein Hinweis je Folgetermin: nur Person und Tag, nie Inhalte der Akte; die Seitenleiste sehen auch andere im Raum.</summary>
    private void AddHrFollowUps(MessageState state, List<MessageItem> all)
    {
        foreach (var followUp in _hrFollowUps)
        {
            var open = HrFileRequested is { } request ? () => request(followUp.EmployeeId) : (Action?)null;
            all.Add(Message(state, $"hr-follow-up:{followUp.EntryId}", MessageKind.HrFollowUp, MessageSeverity.Notice, followUp.Title,
                $"Fällig am {followUp.FollowUpOn.ToString("d.M.yyyy", CultureInfo.InvariantCulture)}.", followUp.Title,
                trigger: followUp.FollowUpOn.ToString("O", CultureInfo.InvariantCulture), actionLabel: "Zur Personalakte", action: open));
        }
    }
}
