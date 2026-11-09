using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Ausgewählter Termin: Einzelheiten, Einchecken, Erledigt, Nicht erschienen, Stornieren. Bei einer gemeinsamen Buchung (Paar, Spa-Tag)
/// stehen die anderen Termine dabei, und die ganze Buchung lässt sich stornieren oder verschieben.
/// </summary>
public partial class AppointmentViewModel(
    IWorkstationApi api,
    AppointmentDto appointment,
    string employeeName,
    string roomName,
    TimeZoneInfo zone,
    bool canChangeStatus,
    bool canCancel,
    Func<AppointmentDto, Task> changed,
    Action close,
    Func<AppointmentDto, Task>? checkout = null,
    Func<Guid, Task>? showCustomer = null,
    IReadOnlyList<AppointmentDto>? group = null,
    Func<GroupBookingResult, Task>? groupChanged = null,
    string? currency = null,
    SeriesInfo? series = null,
    Func<SeriesResult, Task>? seriesChanged = null,
    Func<DateTimeOffset, Task<string?>>? moveTo = null) : ViewModelBase
{
    private IWorkstationApi Api => api;

    private Func<AppointmentDto, Task> Changed => changed;

    private TimeZoneInfo Zone => zone;

    private bool MayBook => canCancel;

    private SeriesInfo? Series => series;

    private Func<SeriesResult, Task>? SeriesChanged => seriesChanged;

    public AppointmentDto Appointment { get; } = appointment;

    public string Title => GuestName(Appointment);

    public string TimeText => DayLayout.TimeText(Appointment.Start, Appointment.End, zone);

    public string Details => $"{Appointment.TreatmentName} · {employeeName} · {roomName}";

    public string Initials => Appointment.PlaceholderGuest is { } number ? $"G{number}" : DayLayout.Initials(Appointment.CustomerName);

    public string Treatment => Appointment.TreatmentName;

    public string EmployeeName => employeeName;

    public string RoomName => roomName;

    public string DateText => DayLayout.DateText(DateOnly.FromDateTime(DayLayout.Local(Appointment.Start, zone)));

    /// <summary>Kurzes Datum für Rückfragen („Do. 12.11.“, design.md 5.27).</summary>
    private string ShortDateText => DayLayout.ShortDay(DateOnly.FromDateTime(DayLayout.Local(Appointment.Start, zone)));

    public string Duration => $"{(Appointment.End - Appointment.Start).TotalMinutes:0} Min.";

    /// <summary>Die Behandlung im Katalog: weichen Vor- oder Nachbereitung des Termins davon ab, steht „(geändert)“ dahinter.</summary>
    public TreatmentDto? CatalogTreatment { get; init; }

    /// <summary>Meldet einen abgelehnten Aufruf (Titel, Begründung) als Meldung oben im Kalender (InfoBar); ohne sie steht der Fehler im Panel.</summary>
    public Action<string, string?>? ReportProblem { get; init; }

    /// <summary>
    /// Ä281: Der Server hat die Änderung abgelehnt, weil der Termin inzwischen anderswo geändert wurde (aktueller Termin, Meldung). Der Kalender
    /// zeigt dann den aktuellen Stand mit der Meldung oben im Panel; gespeichert wird nichts ein zweites Mal.
    /// </summary>
    public Action<AppointmentDto, string>? Stale { get; init; }

    /// <summary>Gibt eine Ablehnung wegen eines veralteten Stands an <see cref="Stale"/> weiter; <c>true</c>, wenn sie damit erledigt ist.</summary>
    private bool ReportedStale<T>(ApiResult<T> result)
    {
        if (result.Rejection is not { Current: { } current } rejection || Stale is not { } stale)
        {
            return false;
        }

        stale(current, rejection.Message);
        return true;
    }

    /// <summary>
    /// Die Zeit, die der Termin bindet: „Gebunden 10:25–12:10 · Vorb. 5 · Nachb. 10 · Puffer 10 Min.“; Teile mit 0 Minuten fehlen.
    /// Weichen die Zeiten von der Behandlung ab, steht „(geändert)“ dahinter. Mit Einwirkzeit folgt in einer zweiten Zeile der ganze Ablauf
    /// („Vorb. 5 · Hände 20 · Einwirken 20 · Ausarbeiten 20 · Nachb. 10 Min.“).
    /// </summary>
    public string BoundText
    {
        get
        {
            var parts = DayLayout.TimeParts(Appointment).ToList();
            var flow = DayLayout.ProcessingOf(Appointment).Processing > 0 ? $"\n{string.Join(" · ", DayLayout.FlowParts(Appointment))} Min." : "";
            if (parts.Count == 0)
            {
                return $"Keine Vor- und Nachbereitung{flow}";
            }

            var changed = DayLayout.IsTimesChanged(Appointment, CatalogTreatment) ? " (geändert)" : "";
            return $"Gebunden {DayLayout.TimeText(Appointment.BlockedFrom, Appointment.BlockedUntil, zone)} · {string.Join(" · ", parts)} Min.{changed}{flow}";
        }
    }

    public bool IsBooked => Appointment.Status == AppointmentStatus.Booked;

    public bool IsCheckedIn => Appointment.Status == AppointmentStatus.CheckedIn;

    public bool IsDone => Appointment.Status == AppointmentStatus.Completed;

    public bool IsProblem => Appointment.Status is AppointmentStatus.NoShow or AppointmentStatus.Cancelled;

    /// <summary>Einchecken nur vor der Behandlung und nur am Tag des Termins (L1); Erledigt und Nicht erschienen erst ab dem Tag des Termins, wie der Server es prüft.</summary>
    public bool ShowCheckIn => CanChangeStatus && IsBooked && IsOnToday;

    public bool ShowComplete => HasStatusActions && !IsOnFutureDay;

    public bool ShowNoShow => CanChangeStatus && IsBooked && !IsOnFutureDay;

    /// <summary>Grund am gesperrten Knopf hinter „Mehr“.</summary>
    public string CompleteHint => IsOnFutureDay ? FutureDayHint : "Verfügbar bei gebuchten und eingecheckten Terminen.";

    public string NoShowHint => IsOnFutureDay ? FutureDayHint : "Verfügbar bei gebuchten Terminen.";

    private const string FutureDayHint = "Verfügbar ab dem Tag des Termins.";

    private DateOnly AppointmentDay => DateOnly.FromDateTime(DayLayout.Local(Appointment.Start, zone));

    /// <summary>Der Termin liegt am heutigen Tag des Standorts.</summary>
    public bool IsOnToday => AppointmentDay == LocalToday;

    private bool IsOnFutureDay => AppointmentDay > LocalToday;

    /// <summary>Erledigt oder Nicht erschienen stehen hinter „Mehr“, an künftigen Tagen gesperrt mit Grund.</summary>
    private bool HasStatusActions => CanChangeStatus && Appointment.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn;

    /// <summary>
    /// Hinter „Mehr“ steht etwas: Erledigt, Nicht erschienen, Wiederholen, Zeiten ändern oder die Kalenderdatei. Stornieren und Einchecken zurücknehmen stehen im
    /// Aktionsblock (Ä81).
    /// </summary>
    public bool HasMore => HasStatusActions || CanRepeat || CanChangeTimes || CanSaveCalendarFile;

    /// <summary>„Mehr“ › „Kalenderdatei …“ (Export/Import, Aufgabe C): der Termin als iCal für den Gast, bei einer Buchung mit mehreren Teilen alle Teile; nicht bei Stornierten.</summary>
    public bool CanSaveCalendarFile => Appointment.Status != AppointmentStatus.Cancelled;

    public ExportOption CalendarFileOption => ExportOption.For(ExportFormat.Ics, () => $"api/appointments/{Appointment.Id}/ics", "Kalenderdatei …");

    /// <summary>Holt die Kalenderdatei und speichert sie über den Dialog des Fensters (<see cref="FileSaver"/>); danach „Gespeichert: …“ unten.</summary>
    public ExportViewModel CalendarFile => _calendarFile ??= new ExportViewModel(api, [CalendarFileOption])
    {
        Files = FileSaver,
        Saved = text => ShowToast?.Invoke(text),
        Failed = text => ReportProblem?.Invoke("Kalenderdatei nicht gespeichert", text),
    };

    /// <summary>Der Speicherdialog des Fensters; der Kalender reicht ihn beim Öffnen des Panels weiter.</summary>
    public IFileSaver? FileSaver { get; init; }

    private ExportViewModel? _calendarFile;

    public string Price => Money.Format(Appointment.Price, currency);

    public string? Note => Appointment.Note;

    /// <summary>Treatwell-Buchung: „Vorab über Treatwell bezahlt: 35,00 €“ oder „Zahlt vor Ort“.</summary>
    public string? PaymentText => PlatformText.Payment(Appointment.PlatformPayment, Appointment.PrepaidAmount, currency);

    public bool HasPaymentText => PaymentText is not null;

    /// <summary>Die Buchung ist in Treatwell noch anzunehmen (bis die Rezeption „Erledigt“ sagt).</summary>
    public bool ShowAcceptPending => Appointment is { AcceptInPlatformPending: true, Status: not AppointmentStatus.Cancelled };

    public bool ShowAssignmentOpen => Appointment is { CustomerAssignmentOpen: true, Status: not AppointmentStatus.Cancelled };

    public bool CanConfirmAccepted => MayBook;

    [RelayCommand]
    private async Task PlatformAcceptedAsync()
    {
        var result = await api.PostAsync<AppointmentDto>($"api/appointments/{Appointment.Id}/platform-accepted", null).ConfigureAwait(true);
        await DoneAsync(result).ConfigureAwait(true);
    }

    public string StatusText => Appointment.Status switch
    {
        AppointmentStatus.Booked when Appointment.HeldSince is not null => CalendarItem.HeldName,
        AppointmentStatus.Booked => "Gebucht",
        AppointmentStatus.CheckedIn when Appointment.StartedAt is not null => CalendarItem.RunningName,
        AppointmentStatus.CheckedIn => "Eingecheckt",
        AppointmentStatus.Completed => "Erledigt",
        AppointmentStatus.NoShow => "Nicht erschienen",
        _ => "Storniert",
    };

    public bool CanChangeStatus { get; } = canChangeStatus && appointment.Status != AppointmentStatus.Cancelled;

    public bool CanCancel { get; } = canCancel && appointment.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorOnTop), nameof(ErrorInMove))]
    public partial string? Error { get; set; }

    /// <summary>Die neue Zeit ist nicht frei (Zeiten ändern, Buchung verschieben): Liste der Belegungen in Ortszeit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConflictOnTop), nameof(ConflictInMove))]
    public partial ConflictNotice? Conflict { get; private set; }

    // Ortsregel (meldungen.md, M7): die Meldung steht direkt über dem Knopf der Aktion; bei offener Verschieben-Karte also in dieser Karte, sonst oben im Panel.
    public bool ErrorOnTop => Error is not null && !IsMovingToDay;

    public bool ErrorInMove => Error is not null && IsMovingToDay;

    public bool ConflictOnTop => Conflict is not null && !IsMovingToDay;

    public bool ConflictInMove => Conflict is not null && IsMovingToDay;

    /// <summary>Was der Kalender der Konfliktmeldung mitgibt: Termin markieren, „Zum Konflikt“, Gegenstück von Person und Raum.</summary>
    public ConflictLinks? Links { get; init; }

    private ConflictNotice? ConflictOf<T>(ApiResult<T> result) =>
        ConflictNotice.From(result.Rejection, zone, DateOnly.FromDateTime(DayLayout.Local(Appointment.Start, zone)), Links ?? new ConflictLinks(null));

    [RelayCommand]
    private Task CheckInAsync() => SetStatusAsync(AppointmentStatus.CheckedIn);

    [RelayCommand]
    private Task CompleteAsync() => SetStatusAsync(AppointmentStatus.Completed);

    [RelayCommand]
    private Task NoShowAsync() => SetStatusAsync(AppointmentStatus.NoShow);

    private const string DefaultCancelReason = "An der Rezeption storniert";

    /// <summary>Vorbelegung des Grunds bei einer Absage innerhalb der Stornofrist (L3), damit der Verlauf es zeigt.</summary>
    private const string LateCancelReason = "Kurzfristig an der Rezeption storniert";

    [ObservableProperty]
    public partial string CancelReason { get; set; } = DefaultCancelReason;

    /// <summary>
    /// Hinweis in der Storno-Rückfrage (L3, meldungen.md M3): Liegt die Absage innerhalb der Stornofrist, nennt er die Frist, die Gebühr laut Regel
    /// und wer sie belasten kann; außerhalb der Frist oder ohne Regel keiner. Frist und Gebühr kommen vom Server (<see cref="CancellationDeadlineAt"/>).
    /// </summary>
    public string? LateCancelNotice
    {
        get
        {
            if (CancellationDeadlineAt is not { } deadline || Time.GetUtcNow() <= deadline)
            {
                return null;
            }

            var until = $"Absage innerhalb der Stornofrist (bis {DayLayout.Local(deadline, zone):dd.MM. HH:mm}).";
            if (LateCancelFeePercent <= 0)
            {
                return until;
            }

            var charge = MayChargeFee ? ". Belasten unter „Ausfallgebühr belasten …“ nach dem Stornieren." : "; belasten kann die Standortleitung.";
            return $"{until} Laut Regel fallen {LateCancelFeePercent} % Ausfallgebühr an{charge}";
        }
    }

    /// <summary>
    /// Stornieren fragt nach (Rückfragedialog, Design-Brief 5.27): die Frage als Titel, zuerst die Folge, ein Feld für den Grund und die Aktion;
    /// bei einer Serie zusätzlich „Diesen und alle folgenden“. „Abbrechen“ lässt den Termin, wie er ist.
    /// </summary>
    [RelayCommand]
    public Task AskCancelAsync() => AskCancelWithFeeAsync(null);

    /// <summary>Hinweis oben im Panel, wenn ein Auftrag aus „KI und Hilfe“ den Termin geöffnet hat (Ä319).</summary>
    [ObservableProperty]
    public partial string? AssistantNote { get; set; }

    /// <summary>Wie <see cref="AskCancelAsync()"/>; <paramref name="fee"/> ist der Satz zur Gebühr laut Stornoregel (Auftrag „sag … ab“, Ä319) und steht in der Rückfrage.</summary>
    public async Task AskCancelWithFeeAsync(string? fee)
    {
        if (!CanCancel)
        {
            return;
        }

        var notice = LateCancelNotice;
        var reason = notice is not null && CancelReason == DefaultCancelReason ? LateCancelReason : CancelReason;
        var answer = await AskAsync(new ConfirmRequest("Termin stornieren?",
            $"Der Termin von {Title} am {ShortDateText}, {TimeText}, ist danach storniert und blockiert keine Zeit mehr.{(fee is null ? "" : $" {fee}")}", CancelOneText,
            AlternativeText: CanCancelFollowing ? "Diesen und alle folgenden" : null, InputLabel: "Grund", InputText: reason, Notice: notice)).ConfigureAwait(true);
        if (answer.Choice == ConfirmChoice.Cancelled)
        {
            return;
        }

        CancelReason = answer.Input ?? CancelReason;
        await (answer.Choice == ConfirmChoice.Confirmed ? CancelAppointmentAsync() : CancelFollowingAsync()).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CancelAppointmentAsync()
    {
        var reason = string.IsNullOrWhiteSpace(CancelReason) ? "An der Rezeption storniert" : CancelReason.Trim();
        var result = await api.PostOrQueueAsync($"api/appointments/{Appointment.Id}/cancel",
            new CancelAppointmentRequest(reason, Appointment.Version), $"{Describe()}: stornieren",
            Appointment with { Status = AppointmentStatus.Cancelled }).ConfigureAwait(true);
        await DoneAsync(result).ConfigureAwait(true);
    }

    // ---------- Stornierung und Einchecken zurücknehmen (Ä95, Ä97) ----------

    /// <summary>„Stornierung zurücknehmen“ im Panel eines stornierten Termins.</summary>
    public bool ShowRestore => MayBook && Appointment.Status == AppointmentStatus.Cancelled;

    /// <summary>„Einchecken zurücknehmen“ im Aktionsblock, nur solange nicht kassiert ist (ein erledigter Termin hat den Eintrag nicht; eine Rechnung der Kassa lehnt der Server ab).</summary>
    public bool ShowUndoCheckIn => CanChangeStatus && IsCheckedIn && !IsBilled;

    /// <summary>Der Termin steht auf einer Rechnung der Kassa (auch einer offenen): kassiert, das Einchecken lässt sich nicht mehr zurücknehmen.</summary>
    public bool IsBilled { get; init; }

    [RelayCommand]
    private Task UndoCheckInAsync() => SetStatusAsync(AppointmentStatus.Booked);

    /// <summary>
    /// Fragt vor dem Zurücknehmen nach: wartet die Absage-Nachricht an den Gast noch, geht sie nie raus; ist sie schon raus, entscheidet das Team über eine Korrektur.
    /// Ist die Zeit inzwischen belegt, nennt die zweite Rückfrage den Konflikt und bietet „Verschieben“ an.
    /// </summary>
    [RelayCommand]
    public async Task AskRestoreAsync()
    {
        if (!ShowRestore)
        {
            return;
        }

        Error = null;
        Conflict = null;
        var info = await api.GetAsync<AppointmentRestoration>($"api/appointments/{Appointment.Id}/restoration").ConfigureAwait(true);
        if (info.Value is not { } restoration)
        {
            Error = info.Error;
            return;
        }

        var sent = restoration.Notice == CancellationNoticeState.Sent;
        var answer = await AskAsync(new ConfirmRequest("Stornierung zurücknehmen?", RestoreText(restoration), sent ? "Mit Korrektur zurücknehmen" : "Zurücknehmen",
            IsDestructive: false, AlternativeText: sent ? "Ohne Korrektur" : null)).ConfigureAwait(true);
        if (answer.Choice != ConfirmChoice.Cancelled)
        {
            _restoreCorrection = sent && answer.Choice == ConfirmChoice.Confirmed;
            Error = await RestoreAsync(null, _restoreCorrection).ConfigureAwait(true);
        }
    }

    /// <summary>Die Wahl aus der Rückfrage gilt auch, wenn das Team danach zu einer anderen Zeit zurücknimmt.</summary>
    private bool _restoreCorrection;

    private string RestoreText(AppointmentRestoration restoration)
    {
        var text = $"Der Termin von {Title} am {ShortDateText}, {TimeText}, ist danach wieder gebucht. Das Programm prüft zuerst, ob Zeit, Person und Raum noch frei sind.";
        text += restoration.Notice switch
        {
            CancellationNoticeState.Waiting => " Die Absage an den Gast ist noch nicht raus und wird nicht mehr gesendet.",
            CancellationNoticeState.Sent => " Der Gast hat die Absage schon bekommen. Soll er eine Korrektur erhalten?",
            _ => "",
        };
        return restoration.DepositReturned ? text + " Die Anzahlung ging mit der Absage zurück; sie ist an der Kassa neu zu verlangen." : text;
    }

    /// <returns>Der Fehler, wenn es nicht ging, sonst <c>null</c>.</returns>
    private async Task<string?> RestoreAsync(DateTimeOffset? start, bool sendCorrection)
    {
        var result = await api.PostAsync<AppointmentDto>($"api/appointments/{Appointment.Id}/restore", new RestoreAppointmentRequest(start, sendCorrection, Appointment.Version))
            .ConfigureAwait(true);
        if (result.Value is { } restored)
        {
            IsMovingToDay = false;
            await changed(restored).ConfigureAwait(true);
            return null;
        }

        if (ReportedStale(result))
        {
            return null;
        }

        if (result.Rejection is null)
        {
            return result.Error;
        }

        Conflict = ConflictOf(result);
        var reason = Conflict?.Summary ?? result.Error ?? result.Rejection.Message;
        var answer = await AskAsync(new ConfirmRequest("Zu anderer Zeit zurücknehmen?",
            $"{reason} Die Stornierung lässt sich nur zu einer anderen Zeit zurücknehmen.", "Verschieben …", IsDestructive: false)).ConfigureAwait(true);
        IsMovingToDay = answer.IsConfirmed;
        return null;
    }

    // ---------- Vor- und Nachbereitung dieses Termins ändern ----------

    /// <summary>Wer buchen darf, ändert Vor- und Nachbereitung eines gebuchten oder eingecheckten Termins.</summary>
    public bool CanChangeTimes => MayBook && Appointment.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingCard))]
    public partial bool IsEditingTimes { get; private set; }

    /// <summary>Eine Karte zum Bearbeiten ist offen (Zeiten, Notiz, Wiederholen, Verschieben): dann ist ihr Bestätigen der eine Primärknopf, nicht Einchecken oder Kassieren (W53).</summary>
    public bool IsEditingCard => IsEditingTimes || IsEditingNote || IsEditingSeries || IsMovingToDay;

    /// <summary>Vorbereitung in Minuten (0 bis 120), vorbelegt mit der des Termins.</summary>
    [ObservableProperty]
    public partial int TimesPreparation { get; set; }

    /// <summary>Nachbereitung in Minuten (0 bis 120), vorbelegt mit der des Termins.</summary>
    [ObservableProperty]
    public partial int TimesFollowUp { get; set; }

    /// <summary>Die Zeiten des Termins vor dem Ändern; was die Zahlenfelder davon abweicht, ist eine ungespeicherte Änderung.</summary>
    private int PreparationAtOpen => DayLayout.TimesOf(Appointment).Item1;

    private int FollowUpAtOpen => DayLayout.TimesOf(Appointment).Item2;

    [RelayCommand]
    public void BeginEditTimes()
    {
        if (!CanChangeTimes || IsEditingNote)
        {
            return;
        }

        var (preparation, followUp, _) = DayLayout.TimesOf(Appointment);
        TimesPreparation = preparation;
        TimesFollowUp = followUp;
        IsEditingTimes = true;
    }

    [RelayCommand]
    public void CancelEditTimes() => IsEditingTimes = false;

    [RelayCommand]
    private async Task ApplyTimesAsync()
    {
        Conflict = null;
        var result = await api.PutAsync<AppointmentDto>($"api/appointments/{Appointment.Id}/times",
            new AppointmentTimesRequest(TimesPreparation, TimesFollowUp, Appointment.Version)).ConfigureAwait(true);
        if (result.Value is null)
        {
            if (ReportedStale(result))
            {
                return;
            }

            if (ConflictOf(result) is { } conflict)
            {
                Conflict = conflict;
            }
            else if (ReportProblem is { } report)
            {
                report("Zeiten nicht geändert", result.Error);
            }
            else
            {
                Error = result.Error;
            }

            return;
        }

        IsEditingTimes = false;
        await changed(result.Value).ConfigureAwait(true);
    }

    /// <summary>Kassieren direkt aus dem Termin, sobald die Kundin da ist.</summary>
    public bool CanCheckout => checkout is not null && Appointment.Status is AppointmentStatus.CheckedIn or AppointmentStatus.Completed;

    public bool CanShowCustomer => showCustomer is not null;

    [RelayCommand]
    private Task CheckoutAsync() => checkout?.Invoke(Appointment) ?? Task.CompletedTask;

    [RelayCommand]
    private Task ShowCustomerAsync() => showCustomer?.Invoke(Appointment.CustomerId) ?? Task.CompletedTask;

    [RelayCommand]
    private void Close() => close();

    // ---------- Auf einen anderen Tag oder eine andere Uhrzeit verschieben ----------

    /// <summary>Einzeltermin, der noch nicht begonnen hat; gemeinsame Buchungen ziehen nur als Ganzes um.</summary>
    public bool CanMoveToDay => moveTo is not null && IsBooked && !IsGroup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingCard), nameof(ErrorOnTop), nameof(ErrorInMove), nameof(ConflictOnTop), nameof(ConflictInMove))]
    public partial bool IsMovingToDay { get; private set; }

    /// <summary>Neuer Tag als „TT.MM.JJJJ“, vorbelegt mit dem bisherigen.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveToDayCommand), nameof(MoveDaysCommand))]
    public partial string MoveDateText { get; set; } = $"{DayLayout.Local(appointment.Start, zone):dd.MM.yyyy}";

    /// <summary>Untergrenze des Tag-Felds: Verschoben wird nur auf heute oder später (L2), wie der Server es prüft.</summary>
    public DateOnly Today => LocalToday;

    /// <summary>Der eingegebene Tag liegt nicht vor heute; ein unlesbarer Tag bleibt erlaubt, damit „Verschieben“ ihn meldet statt still zu sperren. Den Grund am gesperrten Knopf nennt die Ansicht (DisabledHint).</summary>
    public bool MoveDateIsAllowed => ParseMoveDate() is not { } date || date >= LocalToday;

    [ObservableProperty]
    public partial string MoveTimeText { get; set; } = $"{DayLayout.Local(appointment.Start, zone):HH:mm}";

    [RelayCommand]
    private void AskMoveToDay() => IsMovingToDay = CanMoveToDay;

    /// <summary>Auftrag „verschiebe …“ (Ä319): „Verschieben“ mit dem genannten Tag und der Uhrzeit öffnen; was der Auftrag nicht nennt, bleibt leer.</summary>
    internal void ProposeMove(DateOnly? day, DateTimeOffset? start)
    {
        if (!CanMoveToDay)
        {
            return;
        }

        var local = start is { } at ? DayLayout.Local(at, zone) : (DateTime?)null;
        MoveDateText = local is { } l ? $"{l:dd.MM.yyyy}" : day is { } d ? $"{d:dd.MM.yyyy}" : "";
        MoveTimeText = local is { } t ? $"{t:HH:mm}" : "";
        IsMovingToDay = true;
    }

    [RelayCommand]
    private void CancelMoveToDay() => IsMovingToDay = false;

    [RelayCommand(CanExecute = nameof(CanMoveDays))]
    private void MoveDays(string? days)
    {
        if (int.TryParse(days, System.Globalization.CultureInfo.InvariantCulture, out var offset) && ParseMoveDate() is { } date)
        {
            MoveDateText = $"{date.AddDays(offset):dd.MM.yyyy}";
        }
    }

    /// <summary>„−1 Tag“ ist gesperrt, sobald das Ziel vor heute läge.</summary>
    private bool CanMoveDays(string? days) =>
        !(int.TryParse(days, System.Globalization.CultureInfo.InvariantCulture, out var offset) && ParseMoveDate() is { } date) || date.AddDays(offset) >= LocalToday;

    /// <summary>Der heutige Tag in der Ortszeit des Standorts, für Datumsangaben ohne Jahr und zweistellige Jahre.</summary>
    private DateOnly LocalToday => DateOnly.FromDateTime(DayLayout.Local(Time.GetUtcNow(), zone));

    private DateOnly? ParseMoveDate() => DateInputParser.TryParse(MoveDateText, LocalToday);

    [RelayCommand(CanExecute = nameof(MoveDateIsAllowed))]
    private async Task MoveToDayAsync()
    {
        var (moveDate, dateError) = DateInputParser.ParseRequired(MoveDateText, LocalToday);
        if (moveDate is not { } date)
        {
            Error = dateError;
            return;
        }

        var (moveTime, timeError) = TimeInputParser.ParseRequired(MoveTimeText);
        if (moveTime is not { } time)
        {
            Error = timeError;
            return;
        }

        var start = DayLayout.LocalToUtc(date, time, zone);
        Error = ShowRestore ? await RestoreAsync(start, _restoreCorrection).ConfigureAwait(true) : await moveTo!(start).ConfigureAwait(true);
    }

    // ---------- Gemeinsame Buchung ----------

    private readonly IReadOnlyList<AppointmentDto> _group = group ?? [];

    public bool IsGroup => Appointment.GroupId is not null;

    /// <summary>Alle Teile der gemeinsamen Buchung nach Beginn, auch dieser: Zeit, Gast, Behandlung, Person, Raum und Status.</summary>
    public IReadOnlyList<GroupPartRow> GroupParts { get; init; } = [];

    public bool HasGroupParts => GroupParts.Count > 0;

    /// <summary>„Paar“, „Spa-Tag“ oder „Gruppe · 3 Gäste“ über der Liste der Teile.</summary>
    public string GroupTitle => GroupParts.Where(p => !p.IsCancelled).Select(p => p.Appointment.CustomerId).Distinct().Count() switch
    {
        <= 1 => "Spa-Tag",
        2 => "Paar",
        var guests => $"Gruppe · {guests} Gäste",
    };

    /// <summary>Einen anderen Teil der Buchung öffnen (Klick auf die Zeile).</summary>
    public Action<AppointmentDto>? OpenPart { get; init; }

    /// <summary>„Alle kassieren“: die Teile einer Buchung landen gemeinsam auf einer Rechnung der Kassa.</summary>
    public Func<IReadOnlyList<AppointmentDto>, Task>? CheckoutGroupRequested { get; init; }

    [RelayCommand]
    private void OpenGroupPart(GroupPartRow row)
    {
        if (!row.IsCurrent)
        {
            OpenPart?.Invoke(row.Appointment);
        }
    }

    /// <summary>Die Teile der Buchung ohne stornierte, in der Reihenfolge der Liste im Panel.</summary>
    private IEnumerable<AppointmentDto> GroupAppointments => _group.Append(Appointment).Where(a => a.Status != AppointmentStatus.Cancelled)
        .OrderBy(a => a.Start).ThenBy(a => a.CustomerName, StringComparer.Ordinal).ThenBy(a => a.Id);

    /// <summary>Gemeinsam einchecken: alle Teile, die noch gebucht sind. Nur mit Buchungsrecht, damit niemand fremde Termine einchecken kann.</summary>
    public bool CanCheckInGroup => IsGroup && MayBook && CanChangeStatus && IsOnToday && groupChanged is not null && GroupAppointments.Any(a => a.Status == AppointmentStatus.Booked);

    public string CheckInGroupText => $"Alle einchecken ({GroupAppointments.Count(a => a.Status == AppointmentStatus.Booked)})";

    [RelayCommand]
    private async Task CheckInGroupAsync()
    {
        var done = new List<AppointmentDto>();
        foreach (var part in GroupAppointments.Where(a => a.Status == AppointmentStatus.Booked).ToList())
        {
            var result = await api.PostOrQueueAsync($"api/appointments/{part.Id}/status", new AppointmentStatusChange(AppointmentStatus.CheckedIn, part.Version),
                $"{part.CustomerName}, {DateText} {DayLayout.TimeText(part.Start, part.End, zone)}: {CalendarItem.StatusName(AppointmentStatus.CheckedIn)}",
                part with { Status = AppointmentStatus.CheckedIn }).ConfigureAwait(true);
            if (result.Value is null)
            {
                if (!ReportedStale(result))
                {
                    Error = result.Error;
                }

                break;
            }

            done.Add(result.Value);
        }

        if (done.Count > 0)
        {
            await groupChanged!(new GroupBookingResult(Appointment.GroupId!.Value, done)).ConfigureAwait(true);
        }
    }

    /// <summary>Gemeinsam kassieren: alle eingecheckten oder erledigten Teile auf eine Rechnung.</summary>
    public bool CanCheckoutGroup => IsGroup && CheckoutGroupRequested is not null && GroupAppointments.Count(a => a.Status is AppointmentStatus.CheckedIn or AppointmentStatus.Completed) > 1;

    [RelayCommand]
    private Task CheckoutGroupAsync() => CheckoutGroupRequested?.Invoke([.. GroupAppointments
        .Where(a => a.Status is AppointmentStatus.CheckedIn or AppointmentStatus.Completed)]) ?? Task.CompletedTask;

    public bool CanCancelGroup => IsGroup && CanCancel && groupChanged is not null;

    /// <summary>Gast ohne Namen: hier lässt sich der Name nachtragen.</summary>
    public bool CanNameGuest => IsPlaceholder && CanCancel && groupChanged is not null && Appointment.Status != AppointmentStatus.Cancelled;

    /// <summary>Verschieben geht nur, solange noch niemand eingecheckt ist; dann bleibt der ganze Ablauf erhalten.</summary>
    public bool CanMoveGroup => CanCancelGroup && _group.Count > 0
        && Appointment.Status == AppointmentStatus.Booked && _group.All(a => a.Status is AppointmentStatus.Booked or AppointmentStatus.Cancelled);

    /// <summary>Neuer Beginn der ganzen Buchung, vorbelegt mit dem bisherigen ersten Termin.</summary>
    [ObservableProperty]
    public partial string GroupStartText { get; set; } =
        $"{DayLayout.Local(group?.Where(a => a.Status != AppointmentStatus.Cancelled).Append(appointment).Min(a => a.Start) ?? appointment.Start, zone):HH:mm}";

    /// <summary>„Ganze Buchung stornieren“ fragt nach (Rückfragedialog, Design-Brief 5.27).</summary>
    [RelayCommand]
    private async Task AskCancelGroupAsync()
    {
        var count = GroupAppointments.Count();
        var answer = await AskAsync(new ConfirmRequest("Ganze Buchung stornieren?",
            $"Alle {count} Termine dieser Buchung sind danach storniert und blockieren keine Zeit mehr.", "Stornieren")).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await CancelGroupAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task CancelGroupAsync()
    {
        var result = await api.PostAsync<GroupBookingResult>($"api/locations/{Appointment.LocationId}/appointment-groups/{Appointment.GroupId}/cancel",
            new CancelAppointmentRequest("An der Rezeption storniert (ganze Buchung)")).ConfigureAwait(true);
        await GroupDoneAsync(result).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task MoveGroupAsync()
    {
        var (groupStart, startError) = TimeInputParser.ParseRequired(GroupStartText);
        if (groupStart is not { } time)
        {
            Error = startError;
            return;
        }

        var date = DateOnly.FromDateTime(DayLayout.Local(Appointment.Start, zone));
        var result = await api.PostAsync<GroupBookingResult>($"api/locations/{Appointment.LocationId}/appointment-groups/{Appointment.GroupId}/move",
            new MoveGroupRequest(DayLayout.LocalToUtc(date, time, zone))).ConfigureAwait(true);
        await GroupDoneAsync(result).ConfigureAwait(true);
    }

    private async Task GroupDoneAsync(ApiResult<GroupBookingResult> result)
    {
        if (result.Value is null)
        {
            Conflict = ConflictOf(result);
            Error = Conflict is null ? result.Error : null;
            return;
        }

        await groupChanged!(result.Value).ConfigureAwait(true);
    }

    private async Task SetStatusAsync(AppointmentStatus status)
    {
        var result = await api.PostOrQueueAsync($"api/appointments/{Appointment.Id}/status", new AppointmentStatusChange(status, Appointment.Version),
            $"{Describe()}: {CalendarItem.StatusName(status)}", Appointment with { Status = status }).ConfigureAwait(true);
        await DoneAsync(result).ConfigureAwait(true);
    }

    private string Describe() => $"{Appointment.CustomerName}, {DateText} {TimeText}";

    private async Task DoneAsync(ApiResult<AppointmentDto> result)
    {
        if (result.Value is null)
        {
            if (!ReportedStale(result))
            {
                Error = result.Error;
            }

            return;
        }

        await changed(result.Value).ConfigureAwait(true);
    }
}
