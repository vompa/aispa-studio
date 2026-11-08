using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Eine Zeile im Protokoll der Absagen einer Schließzeit (<c>ClosurePage.tsx</c>): Gast, Termin oder Wunsch, Kanal mit Sprache, retournierte Anzahlung, Zustand und wer wann abgesagt hat.
/// Eine Nachricht, die noch auf ihre Wartezeit wartet (Ä95), zeigt „Nachricht an den Gast geht um 14:32 raus.“ mit „Jetzt senden“ und „Nicht senden“, wenn man buchen darf.
/// </summary>
public sealed partial class ClosureNoticeRow : ObservableObject
{
    private readonly bool _canAct;

    public ClosureNoticeRow(ClosureNoticeDto notice, TimeZoneInfo zone, string currency, DateTimeOffset now, bool canBook, Func<ClosureNoticeRow, string, Task> change)
    {
        Notice = notice;
        _canAct = canBook;
        State = notice.State;
        Guest = notice.CustomerName;
        WhenText = When(notice, zone);
        ChannelText = $"{ClosureAppointmentRow.ChannelName(notice.Channel)} · {ClosureLanguage.Code(notice.Language)}{(notice.AiWritten ? " · KI" : "")}";
        LanguageTip = ClosureLanguage.Name(notice.Language);
        RefundText = notice.RefundAmount is { } amount ? $"{Money.Format(amount, currency)} retourniert{(notice.RefundWay is { Length: > 0 } way ? $" ({way})" : "")}"
            : notice.RefundError is not null ? "nicht retourniert" : "";
        RefundTip = notice.RefundError;
        ErrorTip = notice.Error;
        ByText = $"{notice.SentBy}, {TimeZoneInfo.ConvertTime(notice.CreatedAt, zone).ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)}";
        IsWaiting = notice.State == ClosureNoticeState.Queued && notice.ScheduledFor is { } due && due > now;
        WaitingText = IsWaiting ? $"Nachricht an den Gast geht um {TimeZoneInfo.ConvertTime(notice.ScheduledFor!.Value, zone).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)} raus." : "";
        StateText = IsWaiting ? WaitingText : StateName(notice.State);
        SendNowCommand = new AsyncRelayCommand(() => change(this, "send-now"));
        CancelCommand = new AsyncRelayCommand(() => change(this, "cancel"));
    }

    public ClosureNoticeDto Notice { get; }

    public ClosureNoticeState State { get; }

    public string Guest { get; }

    /// <summary>„Mo. 1.3. 10:00, Massage“ oder „Warteliste, Wunsch Mo. 1.3. bis Do. 4.3., Massage“.</summary>
    public string WhenText { get; }

    /// <summary>„Mail · DE“, bei einer KI-Fassung mit „ · KI“.</summary>
    public string ChannelText { get; }

    public string LanguageTip { get; }

    public string RefundText { get; }

    public bool HasRefund => RefundText.Length > 0;

    public string? RefundTip { get; }

    public string? ErrorTip { get; }

    public string StateText { get; }

    public string ByText { get; }

    public bool IsWaiting { get; }

    public string WaitingText { get; }

    /// <summary>Die Knöpfe „Jetzt senden“ und „Nicht senden“ gibt es nur für wartende Nachrichten und nur, wer buchen darf.</summary>
    public bool CanAct => IsWaiting && _canAct;

    public string SendNowName => $"Nachricht an {Guest} jetzt senden";

    public string CancelName => $"Nachricht an {Guest} nicht senden";

    public string Name => $"{Guest}, {WhenText}, {ChannelText}, {StateText}";

    public IAsyncRelayCommand SendNowCommand { get; }

    public IAsyncRelayCommand CancelCommand { get; }

    private static string When(ClosureNoticeDto notice, TimeZoneInfo zone)
    {
        if (notice.AppointmentStart is { } start)
        {
            return $"{DayLayout.ShortDayTime(start, zone)}, {notice.TreatmentName}";
        }

        if (notice.WishFrom is { } from && notice.WishUntil is { } until)
        {
            return $"Warteliste, Wunsch {DayLayout.ShortDay(from)}{(from == until ? "" : $" bis {DayLayout.ShortDay(until)}")}, {notice.TreatmentName}";
        }

        return $"Warteliste, {notice.TreatmentName}";
    }

    private static string StateName(ClosureNoticeState state) => state switch
    {
        ClosureNoticeState.Queued => "wird gesendet",
        ClosureNoticeState.Sent => "gesendet",
        ClosureNoticeState.Skipped => "nicht gesendet",
        ClosureNoticeState.Failed => "fehlgeschlagen",
        _ => "nicht gesendet (zurückgenommen)",
    };
}

/// <summary>
/// Schließzeiten (R05 Slice 95, ADR 0073): native Maske nach <c>ClosuresPage.tsx</c>. In einer Schließzeit gibt es keine freien Zeiten; bestehende Termine bleiben, bis jemand sie in der
/// Rückfrage „Termine absagen?“ absagt. Gleiche Felder und Regeln wie im Web: Zeitraum (mit Teil-Tagen), Art, Grund für das Team und Text für Gäste (je höchstens 200 Zeichen), dazu die
/// Wartezeit vor der Nachricht an den Gast je Standort und das Protokoll der Absagen. Verwalten darf, wer Standorte verwaltet; Absagen zusätzlich, wer buchen darf. Die Rückfrage zum Absagen
/// ist dieselbe wie im Kalender (<see cref="CancelClosureDialogViewModel"/>).
/// </summary>
public sealed partial class ClosuresViewModel : PeriodAreaViewModel
{
    public const int MaxText = 200;

    public const int MaxDelayMinutes = 60;

    private const string DelayError = "Bitte eine ganze Zahl von 0 bis 60 eingeben.";

    private static readonly (ClosureKind Kind, string Label, string Short)[] KindNames =
    [
        (ClosureKind.Vacation, "Betriebsurlaub", "Betriebsurlaub"),
        (ClosureKind.SpecialClosure, "Feiertag oder Sonderschließung", "Sonderschließung"),
        (ClosureKind.Outage, "Ausfall (Schaden, Krankheit, Behörde)", "Ausfall"),
    ];

    private List<ClosureDto> _closures = [];
    private ClosureDto? _current;
    private int? _savedDelay;
    private bool _noticesLoaded;

    public ClosuresViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time)
        : base(api, user, time, "Schließzeiten", "Bitte den ersten geschlossenen Tag wählen.", "Bitte den letzten geschlossenen Tag wählen.")
    {
        CanBook = user.Rights.Contains(AccessRight.Book);
        Kinds = [.. KindNames.Select(k => new KindOption(k.Kind, k.Label, () => Kind, value => Kind = (ClosureKind)value))];
    }

    public override string NewText => "Neue Schließzeit";

    public override string HintText => "In einer Schließzeit gibt es keine freien Zeiten. Bestehende Termine bleiben, bis man sie absagt.";

    public override string HelpTopic => "schliesszeiten";

    public override string HelpSection => "schliesszeiten";

    public override string SelectHint => "Links eine Schließzeit wählen oder mit „Neue Schließzeit“ eine anlegen.";

    public override string LoadFailedText => "Die Schließzeiten konnten nicht geladen werden.";

    public override string NoLocationText => "Es gibt noch keinen Standort.";

    protected override string EmptyCurrentText => "Es ist keine Schließzeit eingetragen.";

    protected override string EmptyEndedText => "Keine beendete Schließzeit.";

    protected override string SaveFailedText => "Die Schließzeit konnte nicht gespeichert werden. Bitte die markierten Felder prüfen.";

    /// <summary>Absagen brauchen zusätzlich das Recht zu buchen.</summary>
    public bool CanBook { get; }

    public IReadOnlyList<KindOption> Kinds { get; }

    /// <summary>Das Protokoll der Absagen; leer, solange wegen der Schließzeit nichts abgesagt wurde.</summary>
    public ObservableCollection<ClosureNoticeRow> Notices { get; } = [];

    public bool HasProtocol => _current is { CancelledAppointments: > 0 };

    /// <summary>Das Protokoll der Absagen ist aufgeklappt; zu ist es bei jeder Schließzeit, die man neu öffnet.</summary>
    [ObservableProperty]
    public partial bool IsProtocolOpen { get; set; }

    public string ProtocolHeader => $"Protokoll der Absagen ({_current?.CancelledAppointments ?? 0})";

    public bool IsProtocolLoading => HasProtocol && !_noticesLoaded;

    /// <summary>„Wurden wegen dieser Schließzeit Termine abgesagt, …“: solange noch nichts abgesagt ist, lässt sich die Schließzeit löschen.</summary>
    public string? NoDeleteHint => _current is { CancelledAppointments: 0 }
        ? "Wurden wegen dieser Schließzeit Termine abgesagt, bleibt sie als Verlauf stehen und lässt sich nicht löschen."
        : null;

    public bool HasNoDeleteHint => NoDeleteHint is not null;

    /// <summary>Der Knopf „Termine absagen …“ ist da, solange offene Termine oder Wartelisten-Einträge in der Schließzeit liegen und man buchen darf.</summary>
    public bool CanCancelAppointments => CanBook && _current is { } current && (current.OpenAppointments > 0 || current.OpenWaitlist > 0);

    /// <summary>Ohne das Recht zu buchen steht statt des Knopfes ein Hinweis.</summary>
    public string? CancelHint => !CanBook && HasOpenNotice ? "Absagen darf, wer buchen darf." : null;

    public bool HasCancelHint => CancelHint is not null;

    protected override bool CanDeleteItem => _current is { CancelledAppointments: 0 };

    [ObservableProperty]
    public partial ClosureKind Kind { get; set; }

    /// <summary>Grund für das Team, höchstens 200 Zeichen; sehen nur Mitarbeitende.</summary>
    [ObservableProperty]
    public partial string InternalReason { get; set; } = "";

    /// <summary>Kurzer Text für Gäste, höchstens 200 Zeichen; leer: ein Standardsatz der Art.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuestTextHint))]
    public partial string GuestText { get; set; } = "";

    [ObservableProperty]
    public partial string? InternalReasonError { get; private set; }

    [ObservableProperty]
    public partial string? GuestTextError { get; private set; }

    /// <summary>Hinweis unter dem Text für Gäste: nennt den Standardsatz der gewählten Art.</summary>
    public string GuestTextHint => $"Kurz; steht im Kundenbereich, beim Assistenten und in der Absage. Leer: „{KindNames.First(k => k.Kind == Kind).Short}“ in einem Standardsatz.";

    // ---------- Wartezeit vor der Nachricht an den Gast ----------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveGuestDelayCommand))]
    public partial decimal? GuestDelayMinutes { get; set; }

    /// <summary>Die Wartezeit des Standorts ist geladen; sonst fehlt der Abschnitt.</summary>
    [ObservableProperty]
    public partial bool HasGuestDelay { get; private set; }

    [ObservableProperty]
    public partial string? GuestDelayError { get; private set; }

    [ObservableProperty]
    public partial string? GuestDelayStatus { get; private set; }

    public const string GuestDelayHint = "Sagt das Team ab, geht die Nachricht erst danach raus. Beginnt der Termin in weniger als 2 Stunden, geht sie sofort. 0 sendet immer sofort.";

    // ---------- Laden und Anzeigen ----------

    protected override async Task<string?> FetchAsync(Guid locationId)
    {
        var closures = await Api.GetAsync<List<ClosureDto>>($"api/locations/{locationId}/closures").ConfigureAwait(true);
        if (closures.Value is not { } list)
        {
            return closures.Error ?? LoadFailedText;
        }

        _closures = list;
        return null;
    }

    protected override IReadOnlyList<PeriodRow> BuildRows() =>
    [
        .. _closures.Select(c => new PeriodRow(c.Id, TabOf(c.Phase), KindShort(c.Kind), PeriodLabel(c),
            c.OpenAppointments > 0 || c.OpenWaitlist > 0 ? $"{CalendarViewModel.ClosureWhat(c.OpenAppointments, c.OpenWaitlist)} offen" : c.InternalReason ?? c.GuestText ?? "keine offenen Termine")),
    ];

    protected override async Task OnLocationReadyAsync(Guid locationId)
    {
        var delay = await Api.GetAsync<GuestMessageDelayDto>($"api/locations/{locationId}/guest-message-delay").ConfigureAwait(true);
        HasGuestDelay = delay.Value is not null;
        _savedDelay = delay.Value?.Minutes;
        GuestDelayMinutes = delay.Value?.Minutes;
        GuestDelayError = null;
        GuestDelayStatus = null;
        SaveGuestDelayCommand.NotifyCanExecuteChanged();
    }

    protected override void ApplyItem(Guid id)
    {
        _current = _closures.First(c => c.Id == id);
        Period.Set(_current.FromDate, _current.FromTime, _current.UntilDate, _current.UntilTime);
        Kind = _current.Kind;
        InternalReason = _current.InternalReason ?? "";
        GuestText = _current.GuestText ?? "";
        ClearErrors();
        _noticesLoaded = false;
        IsProtocolOpen = false;
        Notices.Clear();
        SetInfo($"{KindShort(_current.Kind)}, {PeriodLabel(_current)}", PhaseName(_current.Phase), OpenText(_current), $"Angelegt von {_current.CreatedBy} am {Stamp(_current.CreatedAt)}.");
        RaiseItemState();
    }

    protected override void ApplyBlank(AdminTarget? target)
    {
        _current = null;
        Kind = ClosureKind.Vacation;
        InternalReason = "";
        GuestText = "";
        ClearErrors();
        _noticesLoaded = false;
        Notices.Clear();
        RaiseItemState();
    }

    protected override string FormSignature() => string.Join('|', Kind, InternalReason, GuestText);

    protected override string? ValidateFields()
    {
        InternalReasonError = InternalReason.Trim().Length > MaxText ? $"Höchstens {MaxText} Zeichen." : null;
        GuestTextError = GuestText.Trim().Length > MaxText ? $"Höchstens {MaxText} Zeichen." : null;
        return InternalReasonError is not null ? "InternalReasonField" : GuestTextError is not null ? "GuestTextField" : null;
    }

    protected override async Task<(Guid? Id, PeriodTab Tab, string? Error)> SendAsync(Guid locationId, Guid? id, PeriodValue period)
    {
        var body = new ClosureInput(period.From, period.FromTime, period.Until, period.UntilTime, Kind, Trimmed(InternalReason), Trimmed(GuestText));
        var result = id is { } existing
            ? await Api.PutAsync<ClosureDto>($"api/locations/{locationId}/closures/{existing}", body).ConfigureAwait(true)
            : await Api.PostAsync<ClosureDto>($"api/locations/{locationId}/closures", body).ConfigureAwait(true);
        return result.Value is { } saved ? (saved.Id, TabOf(saved.Phase), null) : (null, PeriodTab.Current, result.Error);
    }

    protected override async Task<string?> DeleteItemAsync(Guid locationId, Guid id)
    {
        var result = await Api.DeleteAsync<object>($"api/locations/{locationId}/closures/{id}").ConfigureAwait(true);
        return result.Ok ? null : result.Error ?? "Die Schließzeit wurde nicht gelöscht.";
    }

    protected override ConfirmRequest DeleteRequest(Guid id) => new("Schließzeit löschen?",
        $"Die Schließzeit „{HeadingText}“ ist danach weg. Termine werden nicht angefasst; ab dann gibt es in der Zeit wieder freie Zeiten.", "Löschen");

    protected override async Task OnItemShownAsync(Guid id)
    {
        if (_current is not { CancelledAppointments: > 0 } || SelectedLocation?.Id is not { } locationId)
        {
            return;
        }

        var notices = await Api.GetAsync<List<ClosureNoticeDto>>($"api/locations/{locationId}/closures/{id}/notices").ConfigureAwait(true);
        if (CurrentId != id)
        {
            return;
        }

        Notices.Clear();
        foreach (var notice in notices.Value ?? [])
        {
            Notices.Add(NoticeRow(notice));
        }

        _noticesLoaded = true;
        OnPropertyChanged(nameof(IsProtocolLoading));
    }

    partial void OnKindChanged(ClosureKind value)
    {
        foreach (var option in Kinds)
        {
            option.Refresh();
        }

        OnPropertyChanged(nameof(GuestTextHint));
        Touch();
    }

    partial void OnInternalReasonChanged(string value)
    {
        InternalReasonError = null;
        Touch();
    }

    partial void OnGuestTextChanged(string value)
    {
        GuestTextError = null;
        Touch();
    }

    partial void OnGuestDelayMinutesChanged(decimal? value)
    {
        GuestDelayError = null;
        GuestDelayStatus = null;
    }

    private static string PeriodLabel(ClosureDto c) => PeriodFormViewModel.Label(c.FromDate, c.FromTime, c.UntilDate, c.UntilTime);

    private static string KindShort(ClosureKind kind) => KindNames.First(k => k.Kind == kind).Short;

    private void ClearErrors()
    {
        InternalReasonError = null;
        GuestTextError = null;
    }

    private void RaiseItemState()
    {
        OnPropertyChanged(nameof(HasProtocol));
        OnPropertyChanged(nameof(ProtocolHeader));
        OnPropertyChanged(nameof(IsProtocolLoading));
        OnPropertyChanged(nameof(NoDeleteHint));
        OnPropertyChanged(nameof(HasNoDeleteHint));
        OnPropertyChanged(nameof(CanCancelAppointments));
        OnPropertyChanged(nameof(CancelHint));
        OnPropertyChanged(nameof(HasCancelHint));
        CancelAppointmentsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>„2 Termine liegen in der Schließzeit und bleiben gebucht, bis sie abgesagt werden. 1 Eintrag der Warteliste hat den Wunsch …“</summary>
    private static string? OpenText(ClosureDto closure)
    {
        var parts = new List<string>();
        var open = closure.OpenAppointments;
        if (open > 0)
        {
            parts.Add($"{(open == 1 ? "1 Termin liegt" : $"{open} Termine liegen")} in der Schließzeit und {(open == 1 ? "bleibt" : "bleiben")} gebucht, bis {(open == 1 ? "er" : "sie")} abgesagt {(open == 1 ? "wird" : "werden")}.");
        }

        var waiting = closure.OpenWaitlist;
        if (waiting > 0)
        {
            parts.Add($"{(waiting == 1 ? "1 Eintrag der Warteliste hat" : $"{waiting} Einträge der Warteliste haben")} den Wunsch in der Schließzeit und {(waiting == 1 ? "wartet" : "warten")} auf eine Nachricht.");
        }

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    private ClosureNoticeRow NoticeRow(ClosureNoticeDto notice) =>
        new(notice, Zone, CurrentLocation?.Currency ?? "EUR", UtcNow, CanBook, ChangeNoticeAsync);

    // ---------- Aktionen ----------

    /// <summary>
    /// „Termine absagen …“: holt die offenen Termine und den Textvorschlag, fragt in derselben Rückfrage wie der Kalender nach (Auswahl, Text, Zahl der Nachrichten) und sagt erst nach dem
    /// Klick auf den roten Knopf ab. Danach lädt die Maske neu, damit Zahlen, Protokoll und Hinweis stimmen.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancelAppointments))]
    private async Task CancelAppointmentsAsync()
    {
        if (_current is not { } closure || SelectedLocation?.Id is not { } locationId || Dialogs is not { } dialogs)
        {
            return;
        }

        var path = $"api/locations/{locationId}/closures/{closure.Id}";
        var zone = Zone;
        var currency = CurrentLocation?.Currency;
        var preview = await Api.GetAsync<ClosureCancellationPreview>($"{path}/cancellation").ConfigureAwait(true);
        if (preview.Value is null)
        {
            Notify($"Die Termine wurden nicht geladen. {preview.Error}".TrimEnd(), InfoBarSeverity.Error);
            return;
        }

        CancelClosureDialogViewModel? dialog = null;
        var answer = await dialogs.ShowAsync(done => dialog = new CancelClosureDialogViewModel(preview.Value, zone, done, async (kind, text) =>
        {
            // Der bearbeitbare Text ist die deutsche Vorlage: die KI behält Deutsch (Ä46).
            var revised = await Api.PostAsync<ReviseClosureTextResult>($"{path}/cancellation/revise", new ReviseClosureTextRequest(text, kind, "de")).ConfigureAwait(true);
            return (revised.Value?.Text, revised.Value is null ? revised.Error : null);
        }, currency)).ConfigureAwait(true);
        if (!answer.IsConfirmed || dialog is null)
        {
            return;
        }

        var request = new CancelClosureAppointmentsRequest([.. dialog.Selected.Select(r => r.Appointment.AppointmentId)], dialog.Subject.Trim(), dialog.Text.Trim(),
            dialog.OfferRebooking, dialog.AiWritten, [.. dialog.SelectedWaitlist.Select(r => r.Entry.EntryId)]);
        var result = await Api.PostAsync<CancelClosureAppointmentsResult>($"{path}/cancellation", request).ConfigureAwait(true);
        if (result.Value is not { } done)
        {
            Notify($"Die Termine wurden nicht abgesagt. {result.Error}".TrimEnd(), InfoBarSeverity.Error);
            return;
        }

        await ReloadAsync(closure.Id).ConfigureAwait(true);
        var problems = new List<string>();
        if (done.Failed > 0)
        {
            problems.Add($"{done.Failed} nicht möglich");
        }

        if (done.RefundsFailed > 0)
        {
            problems.Add($"{(done.RefundsFailed == 1 ? "1 Anzahlung" : $"{done.RefundsFailed} Anzahlungen")} nicht retourniert (an der Kassa erstatten)");
        }

        Notify(string.Join(", ", new[] { CalendarViewModel.CancelledText(done) }.Concat(problems).Where(p => p.Length > 0)), problems.Count > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
        AnnounceSaved();
    }

    /// <summary>„Jetzt senden“ oder „Nicht senden“ bei einer Nachricht, die noch auf ihre Wartezeit wartet; die Antwort ersetzt die Zeile.</summary>
    private async Task ChangeNoticeAsync(ClosureNoticeRow row, string change)
    {
        if (_current is not { } closure || SelectedLocation?.Id is not { } locationId)
        {
            return;
        }

        var result = await Api.PostAsync<ClosureNoticeDto>($"api/locations/{locationId}/closures/{closure.Id}/notices/{row.Notice.Id}/{change}", null).ConfigureAwait(true);
        if (result.Value is not { } updated)
        {
            Notify(result.Status == System.Net.HttpStatusCode.Conflict ? "Die Nachricht ist schon gesendet oder zurückgenommen." : result.Error ?? "Die Nachricht ließ sich nicht ändern.", InfoBarSeverity.Error);
            return;
        }

        var index = Notices.IndexOf(row);
        if (index >= 0)
        {
            Notices[index] = NoticeRow(updated);
        }
    }

    private bool CanSaveGuestDelay => HasGuestDelay && GuestDelayMinutes != _savedDelay;

    /// <summary>„Wartezeit speichern“: eine ganze Zahl von 0 bis 60; Gäste, die selbst auslösen, bekommen ihre Nachricht immer sofort.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveGuestDelay))]
    private async Task SaveGuestDelayAsync()
    {
        if (SelectedLocation?.Id is not { } locationId)
        {
            return;
        }

        if (GuestDelayMinutes is not { } minutes || minutes != decimal.Truncate(minutes) || minutes is < 0 or > MaxDelayMinutes)
        {
            GuestDelayError = DelayError;
            return;
        }

        var result = await Api.PutAsync<GuestMessageDelayDto>($"api/locations/{locationId}/guest-message-delay", new GuestMessageDelayDto((int)minutes)).ConfigureAwait(true);
        if (result.Value is not { } saved)
        {
            Notify(result.Error ?? "Die Wartezeit wurde nicht gespeichert.", InfoBarSeverity.Error);
            return;
        }

        _savedDelay = saved.Minutes;
        GuestDelayError = null;
        GuestDelayMinutes = saved.Minutes;
        GuestDelayStatus = "Gespeichert.";
        SaveGuestDelayCommand.NotifyCanExecuteChanged();
    }
}
