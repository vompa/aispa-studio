using System.Globalization;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Zuständigkeiten (R05 Slice 117, Ä274): Zertifikate vor dem Ablauf und Lageraufgaben als Meldung ----------
public partial class CalendarViewModel
{
    /// <summary>So oft holt der Kalender die Erinnerungen nach, auch ohne neues Laden des Tages (wie die Folgetermine der Personalakte).</summary>
    private static readonly TimeSpan DutyRemindersRefresh = TimeSpan.FromMinutes(5);

    private List<DutyReminderDto> _dutyReminders = [];
    private DateTimeOffset _dutyRemindersLoadedAt = DateTimeOffset.MinValue;

    /// <summary>Öffnet den Reiter „Zuständigkeiten“ der Personenkarte; gesetzt von der Shell.</summary>
    public Action<Guid>? PersonDutiesRequested { get; set; }

    /// <summary>Zur Personenkarte nur, wer sie öffnen und den Reiter sehen darf (Stammdaten oder Zugänge und Personalakte); die Erinnerung selbst gibt keine Rechte.</summary>
    private bool CanOpenPersonDuties => PersonDutiesRequested is not null && _user.Rights.Contains(AccessRight.ManageHrFile)
        && (_user.Rights.Contains(AccessRight.ManageCatalog) || _user.Rights.Contains(AccessRight.ManageStaff));

    /// <summary>Zum Lager nur, wer das Lager öffnen darf; die Zuständigkeit Lager gibt keine Rechte.</summary>
    private bool CanOpenStock => AdminRequested is not null && _user.Rights.Contains(AccessRight.Book);

    private async Task LoadDutyRemindersAsync(Guid locationId)
    {
        _dutyRemindersLoadedAt = _time.GetUtcNow();

        // Wer was bekommt und ab wann, rechnet der Server; ohne Antwort bleibt der letzte Stand.
        var result = await _api.GetAsync<List<DutyReminderDto>>($"api/staff-duties/reminders?locationId={locationId}").ConfigureAwait(true);
        if (result.Value is { } reminders)
        {
            _dutyReminders = reminders;
            RefreshMessages();
        }
    }

    /// <summary>Für den Takt des Kalenders: alle paar Minuten neu holen; eine Erinnerung endet, sobald das Zertifikat erneuert oder das Lager erledigt ist.</summary>
    private void RefreshDutyRemindersIfDue()
    {
        if (Location is { } location && _time.GetUtcNow() - _dutyRemindersLoadedAt >= DutyRemindersRefresh)
        {
            _ = LoadDutyRemindersAsync(location.Id);
        }
    }

    /// <summary>Eine Meldung je Erinnerung; abgelaufene Zertifikate als Warnung, alles andere als Hinweis.</summary>
    private void AddDutyReminders(MessageState state, List<MessageItem> all)
    {
        foreach (var reminder in _dutyReminders)
        {
            var date = reminder.Date.ToString("O", CultureInfo.InvariantCulture);
            if (reminder.Kind is DutyReminderKind.StockReorder or DutyReminderKind.StockExpiry)
            {
                var open = CanOpenStock ? () => AdminRequested!("stock", null, null) : (Action?)null;
                all.Add(Message(state, $"duty-stock:{reminder.Kind}:{reminder.LocationId}", MessageKind.StockDuty, MessageSeverity.Notice, reminder.Title, reminder.Text, reminder.Text,
                    trigger: $"{date}:{reminder.Text}", actionLabel: "Zum Lager", action: open));
                continue;
            }

            var person = reminder.EmployeeId is { } id && CanOpenPersonDuties ? () => PersonDutiesRequested!(id) : (Action?)null;
            var severity = reminder.Kind == DutyReminderKind.CertificateExpired ? MessageSeverity.Warning : MessageSeverity.Notice;
            all.Add(Message(state, $"duty-certificate:{reminder.EmployeeId}:{reminder.Text}", MessageKind.Certificate, severity, reminder.Title, reminder.Text, reminder.Text,
                trigger: $"{reminder.Kind}:{date}", actionLabel: "Zur Personenkarte", action: person));
        }
    }
}
