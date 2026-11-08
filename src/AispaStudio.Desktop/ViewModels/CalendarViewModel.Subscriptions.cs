using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Kalender-Abos im Kalender (ADR 0075): der Feiertag im Datumstitel mit Quelle und Stand im Hinweis, Schulferien und Hinweise im Hinweis und im Tagesüberblick
/// („Heute: Nationalfeiertag · Herbstferien bis 2.11.“), der Chip „Kalender“ mit der Liste der Abos des Standorts und seinen zwei Schaltern („Zeigen“ je Zugang,
/// „Aktiv (für alle)“ für die Leitung), „Falsch melden“ und die Meldungen zu den Abos (Änderung, Fehler mit „Erneut versuchen“, Erinnerung im November).
/// Ausblenden wirkt nur auf die Anzeige; was der Server als Tagesfakten liefert, hat er schon nach den Abos dieses Zugangs gefiltert.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Für welchen Standort die Liste der Abos geladen ist; sie wird nur bei einem Wechsel des Standorts neu geholt (nicht bei jedem Tag), damit ein offenes Fenster ruhig bleibt.</summary>
    private Guid? _subscriptionsFor;

    private IReadOnlyList<SubscriptionNoticeDto> _subscriptionNotices = [];

    /// <summary>Die Abos des Standorts für das Fenster unter dem Chip „Kalender“.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSubscriptions), nameof(HasHiddenSubscriptions), nameof(SubscriptionsTip), nameof(SubscriptionsName))]
    public partial IReadOnlyList<SubscriptionRowViewModel> Subscriptions { get; private set; } = [];

    public bool HasSubscriptions => Subscriptions.Count > 0;

    /// <summary>Mindestens ein Abo ist für diesen Zugang ausgeblendet: der Chip trägt einen Punkt.</summary>
    public bool HasHiddenSubscriptions => Subscriptions.Any(s => !s.Show);

    public bool CanManageSubscriptions => _user.Rights.Contains(AccessRight.ManageLocation);

    public string SubscriptionsTip => ChipTip("Kalender-Abos", HasHiddenSubscriptions
        ? "Feiertage, Ferien und Hinweise; einige sind ausgeblendet"
        : "Feiertage, Ferien und Hinweise ein- oder ausblenden");

    /// <summary>Name für Screenreader: der Punkt allein sagt nicht, dass etwas ausgeblendet ist.</summary>
    public string SubscriptionsName => HasHiddenSubscriptions ? "Kalender-Abos, einige ausgeblendet" : "Kalender-Abos";

    // ---------- Tagesfakten ----------

    /// <summary>Feiertag, Ferien und Hinweise des angezeigten Tages; leer in der 7-Tage-Ansicht und solange der Tag noch geladen wird.</summary>
    public IReadOnlyList<CalendarDayFact> DayFacts => Filter is null && _day is { Facts: { } facts } day && day.Date == Date ? facts : [];

    /// <summary>
    /// Was im Datumstitel hinter dem Datum steht, mit „ · “ getrennt: die gesetzlichen Feiertage des Tages („Nationalfeiertag“) und eine Schließzeit des Standorts („Geschlossen: Betriebsurlaub“, ADR 0073). Schulferien, Hinweise und Sonstiges stehen nur
    /// im Hinweis und im Tagesüberblick, damit die Kopfzeile bei zwei Zeilen bleibt.
    /// </summary>
    public string DayFactsText => string.Join(" · ", TitleFacts());

    public bool HasDayFactsText => TitleFacts().Any();

    /// <summary>Hinweis am Datum: je Eintrag eine Zeile mit Quelle und Stand des Abrufs, zuletzt das Kürzel zum Datumsfenster.</summary>
    public string DayFactsTip
    {
        get
        {
            var lines = DayFacts.Select(f => $"{SubscriptionTexts.WithUntil(f, Date)} · {f.Source}{(f.AsOf is { } at ? $", {SubscriptionTexts.Stand(at, _time.GetUtcNow(), _zone)}" : "")}").ToList();
            lines.Add("Datum wählen (G)");
            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>Erste Zeile im Tagesüberblick: „Heute: Nationalfeiertag · Herbstferien bis 2.11.“; leer ohne Tagesfakten.</summary>
    public string OverviewDayLine => DayFacts.Count == 0
        ? ""
        : $"{(IsToday ? "Heute" : Date.ToString("d.M.", System.Globalization.CultureInfo.GetCultureInfo("de-AT")))}: {string.Join(" · ", DayFacts.Select(f => SubscriptionTexts.WithUntil(f, Date)))}";

    public bool HasOverviewDayLine => DayFacts.Count > 0;

    private IEnumerable<string> TitleFacts()
    {
        var holidays = DayFacts.Where(f => f.Kind == SubscriptionKind.PublicHoliday).Select(f => f.Title).Distinct(StringComparer.Ordinal);
        return ClosureChip is not null && _day?.Closures?.OrderBy(b => b.From).FirstOrDefault() is { } band ? holidays.Append($"Geschlossen: {band.Title}") : holidays;
    }

    private void NotifyDayFacts()
    {
        OnPropertyChanged(nameof(DayFacts));
        OnPropertyChanged(nameof(DayFactsText));
        OnPropertyChanged(nameof(HasDayFactsText));
        OnPropertyChanged(nameof(DayFactsTip));
        OnPropertyChanged(nameof(OverviewDayLine));
        OnPropertyChanged(nameof(HasOverviewDayLine));
    }

    // ---------- Laden ----------

    /// <summary>Holt die Abos des Standorts (bei einem Wechsel des Standorts) und die offenen Meldungen zu Abos; ein Fehler lässt den Kalender stehen, wie er ist.</summary>
    private async Task LoadSubscriptionsAsync(LocationDto location)
    {
        if (_subscriptionsFor != location.Id)
        {
            var list = await _api.GetAsync<List<CalendarSubscriptionDto>>($"api/calendar-subscriptions?locationId={location.Id}").ConfigureAwait(true);
            if (location.Id != Location?.Id)
            {
                return;
            }

            _subscriptionsFor = list.Ok ? location.Id : null;
            Subscriptions = [.. (list.Value ?? []).Select(NewRow)];
        }

        var notices = await _api.GetAsync<List<SubscriptionNoticeDto>>($"api/calendar-subscriptions/notices?locationId={location.Id}").ConfigureAwait(true);
        if (location.Id == Location?.Id)
        {
            _subscriptionNotices = notices.Value ?? [];
            RefreshMessages();
        }
    }

    private SubscriptionRowViewModel NewRow(CalendarSubscriptionDto dto) =>
        new(dto, SubscriptionTexts.Status(dto, _time.GetUtcNow(), _zone), CanManage(dto), SetShowAsync, SetActiveAsync, ReportSubscriptionAsync);

    /// <summary>Nur die Leitung schaltet um; ein Abo für alle Standorte schaltet nur, wer alle Standorte sieht (der Server prüft es ebenso).</summary>
    private bool CanManage(CalendarSubscriptionDto dto) => CanManageSubscriptions && (dto.LocationId is not null || _user.Access?.AllLocations == true);

    private void RefreshSubscriptionState()
    {
        OnPropertyChanged(nameof(HasHiddenSubscriptions));
        OnPropertyChanged(nameof(SubscriptionsTip));
        OnPropertyChanged(nameof(SubscriptionsName));
    }

    // ---------- Schalter ----------

    /// <summary>„Zeigen“: nur die Anzeige dieses Zugangs. Danach holt der Kalender den Tag neu, weil der Server die Tagesfakten nach den ausgeblendeten Abos filtert.</summary>
    private async Task<bool> SetShowAsync(SubscriptionRowViewModel row, bool show)
    {
        var result = await _api.PutAsync<CalendarSubscriptionDto>($"api/calendar-subscriptions/{row.Id}/show", new SubscriptionShowInput(show)).ConfigureAwait(true);
        if (result.Value is not { } dto)
        {
            ShowProblem("Abo nicht umgestellt", result.Error);
            return false;
        }

        row.Apply(dto, SubscriptionTexts.Status(dto, _time.GetUtcNow(), _zone));
        RefreshSubscriptionState();
        await LoadAsync().ConfigureAwait(true);
        return true;
    }

    /// <summary>„Aktiv (für alle)“, nur Leitung: gilt für alle Zugänge, Abruf und Wirkung. Danach holt der Kalender den Tag neu.</summary>
    private async Task<bool> SetActiveAsync(SubscriptionRowViewModel row, bool active)
    {
        var result = await _api.PostAsync<CalendarSubscriptionDto>($"api/calendar-subscriptions/{row.Id}/active", new SubscriptionActiveInput(active)).ConfigureAwait(true);
        if (result.Value is not { } dto)
        {
            ShowProblem("Abo nicht umgestellt", result.Error);
            return false;
        }

        row.Apply(dto, SubscriptionTexts.Status(dto, _time.GetUtcNow(), _zone));
        RefreshSubscriptionState();
        ShowToast(active ? $"{dto.Name}: eingeschaltet" : $"{dto.Name}: ausgeschaltet");
        await LoadAsync().ConfigureAwait(true);
        return true;
    }

    // ---------- Falsch melden ----------

    /// <summary>
    /// „Falsch melden“: Rückfrage mit Grund (Design-Brief 5.27, Feld „Grund“), dann eine Meldung an die Leitung mit dem angezeigten Tag und dem Grund. Das Abo ändert sich nicht.
    /// Ohne brauchbaren Grund (3 bis 300 Zeichen) bleibt es bei einem Hinweis mit „Grund eintragen“, der die Rückfrage noch einmal öffnet.
    /// </summary>
    private async Task ReportSubscriptionAsync(SubscriptionRowViewModel row)
    {
        var date = Date;
        var answer = await AskAsync(new ConfirmRequest("Falsch melden?",
            $"Die Leitung bekommt eine Meldung zu Abo ‚{row.Name}‘ für den {date:dd.MM.yyyy}, dem angezeigten Tag. Das Abo selbst ändert sich nicht.",
            "Melden", IsDestructive: false, InputLabel: "Grund")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var reason = (answer.Input ?? "").Trim();
        if (reason.Length is < 3 or > 300)
        {
            ShowProblem("Meldung nicht gesendet", "Bitte einen Grund angeben, 3 bis 300 Zeichen.", isError: false, actionText: "Grund eintragen", action: () => _ = ReportSubscriptionAsync(row));
            return;
        }

        var result = await _api.PostAsync<object>($"api/calendar-subscriptions/{row.Id}/report", new SubscriptionReportInput(date, reason, Location?.Id)).ConfigureAwait(true);
        if (!result.Ok)
        {
            ShowProblem("Meldung nicht gesendet", result.Error, action: () => _ = ReportSubscriptionAsync(row));
            return;
        }

        ShowToast("Gemeldet, die Leitung sieht Tag und Grund");
    }

    // ---------- Meldungen zu Abos ----------

    /// <summary>
    /// Meldungen der Seitenleiste zu Abos: ein Fehler beim Abruf ist eine Warnung mit „Erneut versuchen“ (nur Leitung), Änderung, Erinnerung und „Falsch gemeldet“ sind Hinweise mit „Erledigt“.
    /// Der Server liefert Meldungen für die Leitung nur der Leitung.
    /// </summary>
    private void AddSubscriptionNotices(MessageState state, List<MessageItem> all)
    {
        foreach (var notice in _subscriptionNotices)
        {
            var failed = notice.Kind == SubscriptionNoticeKind.Failed;
            var retry = failed && notice.CanRetry && notice.SubscriptionId is not null && CanManageSubscriptions;
            all.Add(Message(state, $"subscription:{notice.Id}", MessageKind.Subscription, failed ? MessageSeverity.Warning : MessageSeverity.Notice, notice.Title, notice.Text,
                notice.Title, trigger: notice.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture), since: notice.CreatedAt,
                actionLabel: retry ? "Erneut versuchen" : failed ? null : "Erledigt",
                action: retry ? () => _ = RetrySubscriptionAsync(notice) : failed ? null : () => _ = ResolveSubscriptionNoticeAsync(notice)));
        }
    }

    private async Task RetrySubscriptionAsync(SubscriptionNoticeDto notice)
    {
        var result = await _api.PostAsync<object>($"api/calendar-subscriptions/{notice.SubscriptionId}/fetch", null).ConfigureAwait(true);
        if (!result.Ok)
        {
            ShowProblem("Abruf nicht angestoßen", result.Error);
            return;
        }

        ShowToast("Der Abruf läuft im Hintergrund");
    }

    private async Task ResolveSubscriptionNoticeAsync(SubscriptionNoticeDto notice)
    {
        var result = await _api.PostAsync<object>($"api/calendar-subscriptions/notices/{notice.Id}/resolve", null).ConfigureAwait(true);
        if (!result.Ok)
        {
            ShowProblem("Meldung nicht erledigt", result.Error);
            return;
        }

        _subscriptionNotices = [.. _subscriptionNotices.Where(n => n.Id != notice.Id)];
        RefreshMessages();
    }
}
