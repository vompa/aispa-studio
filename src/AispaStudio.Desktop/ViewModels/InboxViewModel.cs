using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zeile im Posteingang.</summary>
public sealed record InboxRow(InboundMailSummary Mail, string TimeText)
{
    public string From => Mail.FromName ?? Mail.From;

    public string Subject => Mail.Subject;

    public string? Summary => Mail.Summary;

    public bool HasSummary => !string.IsNullOrEmpty(Mail.Summary);

    public string StatusText => InboxViewModel.StatusName(Mail.Status);

    /// <summary>„Prüfen“ und „Fehler“ rot, „Vorschlag“ gelb, „Eingetragen“ grün.</summary>
    public bool IsCritical => Mail.Status is InboundMailStatus.NeedsReview or InboundMailStatus.Failed;

    public bool IsWarn => Mail.Status == InboundMailStatus.Proposed;

    public bool IsOk => Mail.Status == InboundMailStatus.Booked;

    public string ChannelText => InboxViewModel.ChannelName(Mail.Channel);

    public bool ShowChannel => Mail.Channel != BookingChannel.Email;

    public bool IsOpen => InboxViewModel.NeedsAction(Mail.Status);

    /// <summary>Name für Screenreader: Status, Absender, Kanal (wie die Pille nur außer Mail), Uhrzeit, Betreff; die KI-Zusammenfassung liest das Detail.</summary>
    public string AccessibleName => AccessibleNames.Join(StatusText, From, ShowChannel ? ChannelText : null, TimeText, Subject);
}

/// <summary>
/// Posteingang der KI-Terminannahme an der Rezeption: Mails, SMS, WhatsApp, Anrufe und Treatwell-Buchungen, die die Queue verarbeitet hat.
/// Neue Nachrichten kommen live; ohne Live-Verbindung fragt der Hintergrundabgleich der App regelmäßig nach.
/// </summary>
public partial class InboxViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly ILiveUpdates? _live;
    private readonly TimeProvider _time;
    private readonly TimeSpan _poll;
    private Dictionary<Guid, TimeZoneInfo> _zones = [];
    private List<LocationDto> _locations = [];
    private bool _initialized;
    private int _version;

    public InboxViewModel(IWorkstationApi api, ILiveUpdates? live, TimeProvider time, TimeSpan? poll = null)
    {
        _api = api;
        _live = live;
        _time = time;
        _poll = poll ?? TimeSpan.FromSeconds(1);
        if (live is not null)
        {
            live.InboxChanged += id => _ = OnInboxChangedAsync(id);
        }
    }

    public ObservableCollection<InboxRow> Mails { get; } = [];

    [ObservableProperty]
    public partial InboxRow? SelectedRow { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    public partial InboxDetailViewModel? Detail { get; private set; }

    public bool HasDetail => Detail is not null;

    /// <summary>Nur Nachrichten, bei denen die Rezeption entscheiden muss.</summary>
    [ObservableProperty]
    public partial bool OnlyOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveText), nameof(LiveTip))]
    public partial bool IsLive { get; private set; }

    public string LiveText => IsLive ? "Live" : "Nicht live";

    public string LiveTip => IsLive ? "Neue Nachrichten erscheinen sofort." : "Keine Live-Verbindung; die App fragt regelmäßig nach.";

    /// <summary>Offene Nachrichten (Prüfen, Vorschlag, Fehler) für die Zahl in der Navigation.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial int OpenCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool Loaded { get; private set; }

    public bool IsEmpty => Loaded && Mails.Count == 0;

    [ObservableProperty]
    public partial string? Error { get; private set; }

    public async Task InitializeAsync()
    {
        if (!_initialized)
        {
            _initialized = true;
            var locations = await _api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
            _locations = locations.Value ?? [];
            _zones = _locations.ToDictionary(l => l.Id, l => TimeZoneInfo.FindSystemTimeZoneById(l.TimeZone));
            if (_live is not null)
            {
                IsLive = await _live.WatchInboxAsync().ConfigureAwait(true);
            }
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    partial void OnOnlyOpenChanged(bool value) => _ = RefreshAsync();

    partial void OnSelectedRowChanged(InboxRow? value) => _ = OpenAsync(value?.Mail.Id);

    /// <summary>Liste neu laden; die Auswahl bleibt, wenn die Nachricht noch dabei ist.</summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        var version = ++_version;
        var result = await _api.GetAsync<List<InboundMailSummary>>(OnlyOpen ? "api/inbox?status=NeedsReview" : "api/inbox").ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        if (result.Value is null)
        {
            Error = result.Error;
            Loaded = true;
            return;
        }

        Error = null;
        var selected = SelectedRow?.Mail.Id;
        var rows = result.Value.Select(m => new InboxRow(m, TimeText(m.ReceivedAt))).ToList();
        Mails.Clear();
        foreach (var row in rows)
        {
            Mails.Add(row);
        }

        // Die Auswahl neu setzen, ohne die Einzelheiten neu zu laden.
        _keepDetail = true;
        SelectedRow = rows.FirstOrDefault(r => r.Mail.Id == selected);
        _keepDetail = false;
        if (!OnlyOpen)
        {
            OpenCount = rows.Count(r => r.IsOpen);
        }

        Loaded = true;
        OnPropertyChanged(nameof(IsEmpty));
    }

    private bool _keepDetail;

    private async Task OpenAsync(Guid? id)
    {
        if (_keepDetail)
        {
            return;
        }

        if (id is not { } mailId)
        {
            Detail = null;
            return;
        }

        var result = await _api.GetAsync<InboundMailDto>($"api/inbox/{mailId}").ConfigureAwait(true);
        if (SelectedRow?.Mail.Id != mailId)
        {
            return;
        }

        Detail = result.Value is { } mail ? NewDetail(mail) : null;
        Error = result.Error;
    }

    private InboxDetailViewModel NewDetail(InboundMailDto mail) =>
        new(_api, mail, Zone(mail.LocationId), _poll, () => RefreshAsync(), () => SelectedRow = null, _locations, _time)
        {
            Dialogs = Dialogs,
            CustomerRequested = CustomerRequested,
            CustomerEditRequested = CustomerEditRequested,
        };

    /// <summary>Kundenkarte öffnen (Shell); in der Kundenübersicht von Absender und Vorschlägen.</summary>
    public Func<Guid, Task>? CustomerRequested { get; set; }

    /// <summary>Kundenkarte öffnen und die Kundendaten bearbeiten (Shell).</summary>
    public Func<Guid, Task>? CustomerEditRequested { get; set; }

    /// <summary>Live-Meldung: Liste neu laden und die offene Nachricht auffrischen, ohne einen gerade bearbeiteten Entwurf zu überschreiben.</summary>
    private async Task OnInboxChangedAsync(Guid mailId)
    {
        if (!_initialized)
        {
            return;
        }

        IsLive = true;
        await RefreshAsync().ConfigureAwait(true);
        if (Detail is { } detail && (detail.Id == mailId || mailId == Guid.Empty))
        {
            await detail.ReloadAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Eine bestimmte Nachricht öffnen (z. B. aus einer Meldung des Kalenders); der Filter „Nur Prüfen“ fällt dafür weg.</summary>
    public async Task ShowMailAsync(Guid mailId)
    {
        await InitializeAsync().ConfigureAwait(true);
        if (OnlyOpen)
        {
            OnlyOpen = false;
            await RefreshAsync().ConfigureAwait(true);
        }

        SelectedRow = Mails.FirstOrDefault(r => r.Mail.Id == mailId);
    }

    /// <summary>Für den Hintergrundabgleich der App: nur ohne Live-Verbindung nachfragen.</summary>
    public Task CheckInAsync() => _initialized && !IsLive ? RefreshAsync() : Task.CompletedTask;

    /// <summary>Nach einem Verbindungsausfall neu laden, auch mit Live-Verbindung: was während der Unterbrechung kam, ist nicht live angekommen.</summary>
    public Task ReloadAfterOutageAsync() => _initialized ? RefreshAsync() : Task.CompletedTask;

    private TimeZoneInfo Zone(Guid? locationId) =>
        locationId is { } id && _zones.TryGetValue(id, out var zone) ? zone : _zones.Values.FirstOrDefault() ?? TimeZoneInfo.Local;

    private string TimeText(DateTimeOffset at)
    {
        var zone = _zones.Values.FirstOrDefault() ?? TimeZoneInfo.Local;
        var local = DayLayout.Local(at, zone);
        var today = DateOnly.FromDateTime(DayLayout.Local(_time.GetUtcNow(), zone));
        return DateOnly.FromDateTime(local) == today
            ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
            : DayLayout.ShortDayTime(at, zone);
    }

    public static bool NeedsAction(InboundMailStatus status) =>
        status is InboundMailStatus.NeedsReview or InboundMailStatus.Proposed or InboundMailStatus.Failed;

    public static string StatusName(InboundMailStatus status) => status switch
    {
        InboundMailStatus.Received => "In Arbeit",
        InboundMailStatus.Booked => "Eingetragen",
        InboundMailStatus.Proposed => "Vorschlag",
        InboundMailStatus.Cancelled => "Storniert",
        InboundMailStatus.NeedsReview => "Prüfen",
        InboundMailStatus.Ignored => "Erledigt",
        _ => "Fehler",
    };

    public static string ChannelName(BookingChannel channel) => channel switch
    {
        BookingChannel.Email => "Mail",
        BookingChannel.Treatwell => "Treatwell",
        BookingChannel.Sms => "SMS",
        BookingChannel.WhatsApp => "WhatsApp",
        _ => "Anruf",
    };
}

/// <summary>
/// Vorschlag, wer der Absender ist; „Zuordnen“ übernimmt ihn (mit der Rezeption als Entscheiderin, ADR 0061). Ein Klick auf den Namen zeigt die
/// Kundenübersicht wie im Kalender, damit die Rezeption vor dem Zuordnen sieht, wer es ist.
/// </summary>
public sealed partial class CandidateOption(CustomerCandidateDto candidate, Func<CustomerCandidateDto, Task> assign, CustomerOverviewViewModel overview)
{
    public string Name => candidate.Name;

    public CustomerOverviewViewModel Overview => overview;

    public string Reason => candidate.Reason;

    /// <summary>Wie sicher der Vorschlag ist, in Worten.</summary>
    public string StageText => candidate.Stage switch
    {
        1 => "Adresse oder Nummer gleich, aber mehrere Kunden teilen sie",
        2 => "Nummer aus der Nachricht passt",
        3 => "Name und ein weiteres Merkmal passen",
        _ => "Nur der Name ist ähnlich",
    };

    [RelayCommand]
    private Task AssignAsync() => assign(candidate);
}

/// <summary>
/// Geöffnete Nachricht, aus Sicht der Rezeption so einfach wie möglich (Auftraggeber, 29.10.2026): oben die Nachricht (fremdsprachig mit „DE“), rechts daneben
/// die freie Zeit; ein Klick merkt die Zeit für den Gast vor und schreibt den Vorschlag in den Entwurf (ADR 0083). Gesendet wird erst nach Freigabe
/// durch die Rezeption, immer mit dem Hinweis auf die KI (AI Act Art. 50), den der Server anhängt.
/// </summary>
[AiOutput(AiFunctions.InboxRead, AiFunctions.InboxDraft)]
public partial class InboxDetailViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly TimeZoneInfo _zone;
    private readonly TimeSpan _poll;
    private readonly Func<Task> _changed;
    private readonly Action _close;
    private readonly TimeProvider _time;

    public InboxDetailViewModel(IWorkstationApi api, InboundMailDto mail, TimeZoneInfo zone, TimeSpan poll, Func<Task> changed, Action close,
        IReadOnlyList<LocationDto>? locations = null, TimeProvider? time = null)
    {
        _api = api;
        _zone = zone;
        _poll = poll;
        _changed = changed;
        _close = close;
        _time = time ?? TimeProvider.System;
        FreeTimeLocations = locations ?? [];
        Mail = mail;
        ReplySubject = mail.ReplySubject ?? "";
        ReplyBody = mail.ReplyBody ?? "";
        _ = LoadAssignedNameAsync();
        SyncFreeTime();
    }

    // ---------- Wer ist das? Vorschläge für den Absender, Zuordnen, Neu anlegen, Lösen (ADR 0061) ----------

    /// <summary>Bis zu drei Vorschläge, solange der Absender keinem Kunden sicher oder bestätigt zugeordnet ist.</summary>
    public IReadOnlyList<CandidateOption> Candidates => !Mail.CustomerConfirmed && CanDecideCustomer && Mail.CustomerCandidates is { Count: > 0 } found
        ? [.. found.Select(c => new CandidateOption(c, AssignAsync, OverviewFor(c.CustomerId)))]
        : [];

    /// <summary>Kundenkarte öffnen (Shell: Bereich „Kunden“), wie in der Kundenübersicht des Kalenders.</summary>
    public Func<Guid, Task>? CustomerRequested { get; init; }

    /// <summary>Kundenkarte öffnen und gleich die Kundendaten bearbeiten.</summary>
    public Func<Guid, Task>? CustomerEditRequested { get; init; }

    private CustomerOverviewViewModel? _overview;

    /// <summary>Kundenübersicht des erkannten Absenders (Flyout an Initialen und Namen im Kopf), dieselbe wie im Terminkopf des Kalenders.</summary>
    public CustomerOverviewViewModel? Overview => Mail is { CustomerConfirmed: true, CustomerId: { } id }
        ? _overview is { CustomerId: var shown } current && shown == id ? current : _overview = OverviewFor(id)
        : null;

    /// <summary>Lädt die Kundenübersicht neu; beim Öffnen des Flyouts, damit der Stand nie veraltet ist.</summary>
    public void ReloadOverview() => _ = Overview?.LoadAsync();

    private CustomerOverviewViewModel OverviewFor(Guid customerId) => new(_api, customerId, _time,
        CustomerRequested is { } open ? () => open(customerId) : null,
        CustomerEditRequested is { } edit ? () => _ = edit(customerId) : null);

    /// <summary>Kopf der Nachricht: der Name des erkannten Kunden, sonst der Absender, wie er schreibt.</summary>
    public string HeaderName => HasAssigned && AssignedName is { } name ? name : Mail.FromName ?? Mail.From;

    /// <summary>Zeile unter dem Namen: beim erkannten Kunden der Absender, wie er schreibt; sonst die Mailadresse bzw. Nummer hinter dem Namen.</summary>
    public string? SenderLine => HasAssigned ? FromText : Mail.FromName is not null ? Mail.From : null;

    public string AssignedInitials => DayLayout.Initials(AssignedName ?? Mail.FromName ?? "");

    /// <summary>Hinweis am Namen im Kopf; nur wenn er die Kundenübersicht zeigt.</summary>
    public string? SenderTip => HasAssigned ? "Kundenübersicht" : null;

    public bool HasCandidates => Candidates.Count > 0;

    /// <summary>Wie auf dem Server: nur solange die Rezeption noch entscheidet.</summary>
    private bool CanDecideCustomer =>
        (Mail.Status is InboundMailStatus.NeedsReview or InboundMailStatus.Proposed
            && (Mail.AppointmentId is null || Mail.Kind == InboundMailKind.Cancellation)
            || AssignmentOpen)
        && !Mail.Reproposing && Mail.ReplyStatus != ReplyStatus.Sending;

    /// <summary>Treatwell-Buchung, die schon im Kalender steht, deren Gast aber noch vorläufig angelegt ist (ADR 0062).</summary>
    public bool AssignmentOpen => Mail.CustomerAssignmentOpen;

    public string CandidatesHint => AssignmentOpen
        ? "Der Termin steht schon im Kalender, der Gast ist vorläufig angelegt. Mit „Zuordnen“ wandert der Termin auf den Kunden; den vorläufigen Gast gibt es danach nicht mehr."
        : "Der Absender ist keinem Kunden sicher zuzuordnen. Erst nach Ihrer Entscheidung nutzen Antwort und Buchung Daten aus der Kundenkarte.";

    public string CreateCustomerText => AssignmentOpen ? "Als neuen Kunden bestätigen" : "Neu anlegen";

    /// <summary>
    /// „KI-Vorschlag aus der Mail: Frau · Dr.“; nur ein Hinweis, übernommen wird nichts von selbst (ADR 0064). Die Rezeption trägt es in der
    /// Kundenkarte ein, wenn es stimmt.
    /// </summary>
    public string? SalutationSuggestionText => new[] { Mail.SuggestedSalutation is { } s ? PersonNames.Label(s) : null, Mail.SuggestedTitlesBefore }
        .Where(p => !string.IsNullOrEmpty(p)).ToList() is { Count: > 0 } parts
            ? $"KI-Vorschlag aus der Mail: {string.Join(" · ", parts)} (bitte in der Kundenkarte prüfen und eintragen)"
            : null;

    /// <summary>„Vorab über Treatwell bezahlt: 35,00 €“ oder „Zahlt vor Ort“.</summary>
    public string? PaymentText => PlatformText.Payment(Mail.PlatformPayment, Mail.PrepaidAmount);

    public bool HasPaymentText => PaymentText is not null;

    /// <summary>Die Buchung steht im Kalender, ist in Treatwell aber noch anzunehmen, bis die Rezeption „Erledigt“ sagt.</summary>
    public bool ShowAcceptPending => Mail.AcceptInPlatformPending;

    public string AcceptPendingText => $"Die Buchung {Mail.PlatformReference} steht im Kalender, gilt bei Treatwell aber erst, wenn Sie sie dort annehmen. Danach hier „Erledigt“.";

    [RelayCommand(CanExecute = nameof(CanConfirmAccepted))]
    private Task PlatformAcceptedAsync() => ActAsync(
        () => _api.PostAsync<InboundMailDto>($"api/inbox/{Id}/platform-accepted", null), "Als in Treatwell angenommen vermerkt.");

    private bool CanConfirmAccepted => ShowAcceptPending && !Busy;

    /// <summary>Häkchen: Mailadresse bzw. Nummer des Absenders in der Kundenkarte merken (nächste Nachricht ist dann sicher zugeordnet).</summary>
    [ObservableProperty]
    public partial bool RememberContact { get; set; }

    public bool HasAssigned => Mail is { CustomerConfirmed: true, CustomerId: not null };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AssignedText), nameof(HeaderName), nameof(AssignedInitials))]
    public partial string? AssignedName { get; private set; }

    public string AssignedText => AssignedName is { } name ? $"Zugeordnet: {name}" : "Einem Kunden zugeordnet";

    public bool CanReleaseCustomer => HasAssigned && CanDecideCustomer && !Busy;

    private Guid? _assignedLoaded;

    private async Task LoadAssignedNameAsync()
    {
        if (Mail is not { CustomerConfirmed: true, CustomerId: { } id } || _assignedLoaded == id)
        {
            return;
        }

        _assignedLoaded = id;
        var result = await _api.GetAsync<CustomerDto>($"api/customers/{id}").ConfigureAwait(true);
        if (Mail.CustomerId == id)
        {
            AssignedName = result.Value?.InHouseName;
        }
    }

    private Task AssignAsync(CustomerCandidateDto candidate) => ActAsync(
        () => _api.PostAsync<InboundMailDto>($"api/inbox/{Id}/customer/assign", new AssignCustomerInput(candidate.CustomerId, RememberContact)),
        $"Zugeordnet: {candidate.Name}.");

    private bool CanCreateCustomer => HasCandidates && !Busy;

    /// <summary>Der Absender ist niemand von den Vorschlägen: Kunde aus Name, Telefon und Mailadresse der Nachricht anlegen.</summary>
    [RelayCommand(CanExecute = nameof(CanCreateCustomer))]
    private Task CreateCustomerAsync() => ActAsync(
        () => _api.PostAsync<InboundMailDto>($"api/inbox/{Id}/customer/new", new NewCustomerFromMailInput()),
        "Kunde neu angelegt und zugeordnet.");

    [RelayCommand(CanExecute = nameof(CanReleaseCustomer))]
    private Task ReleaseCustomerAsync() => ActAsync(
        () => _api.PostAsync<InboundMailDto>($"api/inbox/{Id}/customer/release", null), "Zuordnung gelöst.");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subject), nameof(FromText), nameof(StatusText), nameof(IsCritical), nameof(IsWarn), nameof(IsOk),
        nameof(Details), nameof(Summary), nameof(HasSummary), nameof(Note), nameof(HasNote), nameof(Body),
        nameof(ShowReply), nameof(ReplyLabel), nameof(IsShortMessage), nameof(IsDraft), nameof(ReplyState), nameof(DraftHint),
        nameof(CanRetry), nameof(CanDismiss), nameof(IsWorking), nameof(HasTranslation), nameof(ShowsTranslation), nameof(MessageText),
        nameof(MessageSubject), nameof(HasMessageSubject), nameof(TranslationNote), nameof(HasTranslationNote), nameof(ReplyTranslation), nameof(HasReplyTranslation),
        nameof(CanRevise), nameof(IsRevising), nameof(ReplyNotice), nameof(HasReplyNotice), nameof(LanguageButton), nameof(LanguageButtonTip),
        nameof(CanCancelAppointment), nameof(Candidates), nameof(HasCandidates), nameof(HasAssigned),
        nameof(AssignedText), nameof(CanReleaseCustomer), nameof(AssignmentOpen), nameof(CandidatesHint), nameof(CreateCustomerText),
        nameof(PaymentText), nameof(HasPaymentText), nameof(ShowAcceptPending), nameof(AcceptPendingText), nameof(SalutationSuggestionText),
        nameof(ShowFreeTime), nameof(HasHold), nameof(HoldText), nameof(HoldDetail), nameof(HeaderName), nameof(Overview), nameof(AssignedInitials), nameof(SenderTip), nameof(SenderLine))]
    [NotifyCanExecuteChangedFor(nameof(SaveDraftCommand), nameof(SendCommand), nameof(ReviseCommand), nameof(ReviseCustomCommand),
        nameof(UndoRevisionCommand), nameof(ReleaseCustomerCommand), nameof(PlatformAcceptedCommand), nameof(ConfirmHoldCommand), nameof(ReleaseHoldCommand))]
    public partial InboundMailDto Mail { get; private set; }

    public Guid Id => Mail.Id;

    public string Subject => Mail.Subject;

    public string FromText => Mail.FromName is { } name ? $"{name} <{Mail.From}>" : Mail.From;

    public string StatusText => InboxViewModel.StatusName(Mail.Status);

    public bool IsCritical => Mail.Status is InboundMailStatus.NeedsReview or InboundMailStatus.Failed;

    public bool IsWarn => Mail.Status == InboundMailStatus.Proposed;

    public bool IsOk => Mail.Status == InboundMailStatus.Booked;

    /// <summary>Kanal, Eingang und erkannte Sprache.</summary>
    public string Details => string.Join(" · ", new[]
    {
        InboxViewModel.ChannelName(Mail.Channel),
        DayLayout.ShortDayTime(Mail.ReceivedAt, _zone),
        Mail.Language is { } language ? $"Sprache {language}" : null,
    }.Where(s => s is not null));

    public string? Summary => Mail.Summary;

    public bool HasSummary => !string.IsNullOrEmpty(Mail.Summary);

    public string? Note => Mail.Note;

    public bool HasNote => !string.IsNullOrEmpty(Mail.Note);

    public string Body => Mail.Body;

    public bool IsForeign => Mail.Language is { } language && language != "de";

    /// <summary>Es gibt eine Übersetzung ins Deutsche (bei abweichender Zahl nur auf Wunsch).</summary>
    public bool HasTranslation => Mail.Translation is MessageTranslationState.Done or MessageTranslationState.Mismatch && Mail.TranslatedBody is not null;

    /// <summary>„DE“ (Taste O): die deutsche Übersetzung statt des Originals; die Nachricht steht zuerst so da, wie der Gast sie geschrieben hat (Auftraggeber, 29.10.2026).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTranslation), nameof(MessageText), nameof(MessageSubject), nameof(HasMessageSubject), nameof(TranslationNote),
        nameof(HasTranslationNote), nameof(LanguageButton), nameof(LanguageButtonTip))]
    public partial bool GermanChosen { get; set; }

    public bool ShowsTranslation => HasTranslation && GermanChosen;

    public string MessageText => ShowsTranslation ? Mail.TranslatedBody! : Mail.Body;

    public string MessageSubject => ShowsTranslation && Mail.TranslatedSubject is { } subject ? subject : Mail.Subject;

    /// <summary>Der Betreff steht schon im Kopf; nur ein übersetzter Betreff steht zusätzlich über der Nachricht.</summary>
    public bool HasMessageSubject => ShowsTranslation && Mail.TranslatedSubject is not null;

    /// <summary>„DE“ zeigt die Übersetzung, „Original“ wieder den Text des Gastes.</summary>
    public string LanguageButton => ShowsTranslation ? "Original" : "DE";

    public string LanguageButtonTip => ShowsTranslation ? "Original zeigen (O)" : "Auf Deutsch zeigen (O)";

    /// <summary>Höchstens eine kurze Zeile: übersetzt (mit Warnung bei abweichender Zahl) oder läuft noch.</summary>
    public string? TranslationNote => Mail.Translation switch
    {
        MessageTranslationState.Pending when IsForeign => "Übersetzung läuft …",
        MessageTranslationState.Mismatch when ShowsTranslation => "Achtung: eine Zahl weicht vom Original ab.",
        _ => null,
    };

    public bool HasTranslationNote => TranslationNote is not null;

    [RelayCommand]
    public void ToggleOriginal()
    {
        if (HasTranslation)
        {
            GermanChosen = !GermanChosen;
        }
    }

    public static string LanguageName(string? language) => language switch
    {
        "en" => "Englischen",
        "it" => "Italienischen",
        "fr" => "Französischen",
        _ => "Fremdsprachigen",
    };

    /// <summary>Rückübersetzung des Entwurfs, solange er nicht geändert ist.</summary>
    public string? ReplyTranslation => DraftChanged ? null : Mail.ReplyTranslation;

    public bool HasReplyTranslation => ReplyTranslation is not null && IsForeign;

    public bool CanRevise => Mail.CanRevise && IsDraft;

    public bool IsRevising => Mail.ReplyRevising;

    public string? ReplyNotice => Mail.ReplyNotice;

    public bool HasReplyNotice => ReplyNotice is not null;

    /// <summary>Eigener Hinweis zum Überarbeiten, z. B. „bitte Parkplatz erwähnen“.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReviseCustomCommand))]
    public partial string ReviseInstruction { get; set; } = "";

    public bool IsShortMessage => Mail.Channel is BookingChannel.Sms or BookingChannel.WhatsApp or BookingChannel.Phone;

    public bool ShowReply => Mail.ReplyTo is not null && Mail.ReplyStatus != ReplyStatus.None;

    public string ReplyLabel => IsShortMessage
        ? $"Antwort per {(Mail.Channel == BookingChannel.WhatsApp ? "WhatsApp" : "SMS")} an {Mail.ReplyTo}"
        : $"Antwort an {Mail.ReplyTo}";

    public bool IsDraft => Mail.ReplyStatus == ReplyStatus.Draft;

    public string ReplyState => Mail.ReplyStatus switch
    {
        ReplyStatus.Sent => "Gesendet.",
        ReplyStatus.Sending => "Wird gesendet …",
        _ => "",
    };

    /// <summary>Eine Zeile unter dem Entwurf: was beim Senden angehängt wird (AI Act Art. 50, Text vom Server).</summary>
    public string DraftHint => $"Beim Senden angehängt: „{Mail.AiDisclosure}“";

    public bool CanRetry => Mail.Status == InboundMailStatus.Failed;

    public bool CanDismiss => InboxViewModel.NeedsAction(Mail.Status);

    /// <summary>Die Queue arbeitet gerade (Verarbeitung, neuer Entwurf oder Senden).</summary>
    public bool IsWorking => Mail.Status == InboundMailStatus.Received || Mail.ReplyStatus == ReplyStatus.Sending || Mail.ReplyRevising
        || Mail.Reproposing || Mail.Translation == MessageTranslationState.Pending;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDraftCommand), nameof(SendCommand))]
    public partial string ReplySubject { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDraftCommand), nameof(SendCommand))]
    public partial string ReplyBody { get; set; }

    /// <summary>Entwurf weicht vom gespeicherten Stand ab; vor dem Senden speichern.</summary>
    public bool DraftChanged => ReplySubject != (Mail.ReplySubject ?? "") || ReplyBody != (Mail.ReplyBody ?? "");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; private set; }

    public bool HasNotice => Notice is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoticeSeverity))]
    public partial bool NoticeIsError { get; private set; }

    /// <summary>Fehler als Fehler-Meldung, Fortschritt und Erfolg neutral.</summary>
    public InfoBarSeverity NoticeSeverity => NoticeIsError ? InfoBarSeverity.Error : InfoBarSeverity.Info;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDraftCommand), nameof(SendCommand), nameof(RetryCommand), nameof(DismissCommand),
        nameof(CancelAppointmentCommand), nameof(CreateCustomerCommand), nameof(ReleaseCustomerCommand), nameof(ConfirmHoldCommand), nameof(ReleaseHoldCommand))]
    public partial bool Busy { get; private set; }

    partial void OnReplySubjectChanged(string value) => OnPropertyChanged(nameof(DraftChanged));

    partial void OnReplyBodyChanged(string value)
    {
        OnPropertyChanged(nameof(DraftChanged));
        OnPropertyChanged(nameof(ReplyTranslation));
        OnPropertyChanged(nameof(HasReplyTranslation));
    }

    /// <summary>Neu laden (Live-Meldung); ein gerade bearbeiteter Entwurf bleibt stehen.</summary>
    public async Task ReloadAsync()
    {
        var result = await _api.GetAsync<InboundMailDto>($"api/inbox/{Id}").ConfigureAwait(true);
        if (result.Value is { } mail)
        {
            Show(mail, keepDraft: DraftChanged);
        }
    }

    private void Show(InboundMailDto mail, bool keepDraft = false)
    {
        Mail = mail;
        if (!keepDraft)
        {
            ReplySubject = mail.ReplySubject ?? "";
            ReplyBody = mail.ReplyBody ?? "";
        }

        OnPropertyChanged(nameof(DraftChanged));
        if (!mail.CustomerConfirmed || mail.CustomerId is null)
        {
            _assignedLoaded = null;
            AssignedName = null;
        }

        _ = LoadAssignedNameAsync();
        SyncFreeTime();
    }

    // ---------- Freie Zeit rechts neben der Nachricht und Vormerkung (ADR 0083) ----------

    /// <summary>Text des Hauptknopfs der freien Zeit im Posteingang.</summary>
    public const string HoldLabel = "Vormerken und vorschlagen";

    /// <summary>Terminanfrage ohne Termin (wie der Server sie sieht): rechts steht die freie Zeit.</summary>
    public bool ShowFreeTime => Mail.CanRepropose && Mail.Channel != BookingChannel.Treatwell;

    public IReadOnlyList<LocationDto> FreeTimeLocations { get; }

    /// <summary>Mehr als ein Standort: die Rezeption wählt ihn, vorbelegt mit dem erkannten.</summary>
    public bool HasLocationChoice => FreeTimeLocations.Count > 1;

    [ObservableProperty]
    public partial LocationDto? FreeTimeLocation { get; set; }

    [ObservableProperty]
    public partial FreeTimeViewModel? FreeTime { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFreeTimeError))]
    public partial string? FreeTimeError { get; private set; }

    public bool HasFreeTimeError => FreeTimeError is not null;

    private int _freeTimeVersion;

    partial void OnFreeTimeLocationChanged(LocationDto? value) => _ = LoadFreeTimeAsync();

    /// <summary>Die freie Zeit erscheint, sobald die Anfrage eine offene Terminanfrage ist, und verschwindet mit der Vormerkung.</summary>
    private void SyncFreeTime()
    {
        if (!ShowFreeTime)
        {
            FreeTime = null;
            return;
        }

        if (FreeTimeLocation is null)
        {
            FreeTimeLocation = FreeTimeLocations.FirstOrDefault(l => l.Id == Mail.LocationId)
                ?? (FreeTimeLocations.Count == 1 ? FreeTimeLocations[0] : null);
        }
        else if (FreeTime is null)
        {
            _ = LoadFreeTimeAsync();
        }
    }

    private async Task LoadFreeTimeAsync()
    {
        var version = ++_freeTimeVersion;
        FreeTime = null;
        FreeTimeError = null;
        if (FreeTimeLocation is not { } location || !ShowFreeTime)
        {
            return;
        }

        var resources = await _api.GetAsync<LocationResources>($"api/locations/{location.Id}/resources").ConfigureAwait(true);
        if (version != _freeTimeVersion)
        {
            return;
        }

        if (resources.Value is not { } loaded)
        {
            FreeTimeError = _api.IsOffline ? "Ohne Verbindung lässt sich keine freie Zeit suchen." : resources.Error ?? "Die freie Zeit ließ sich nicht laden.";
            return;
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone);
        var now = _time.GetUtcNow();
        var today = DateOnly.FromDateTime(DayLayout.Local(now, zone));
        // Die Suche beginnt am Tag des ersten Vorschlags der KI, sonst heute; nie in der Vergangenheit.
        var day = Mail.Proposals.Count > 0 ? DateOnly.FromDateTime(DayLayout.Local(Mail.Proposals.Min(p => p.Start), zone)) : today;
        FreeTime = new FreeTimeViewModel(_api, loaded, day < today ? today : day, zone, now, HoldAsync, () => { },
            prefill: new FreeTimePrefill(Mail.TreatmentId, BookLabel: HoldLabel));
    }

    private Task HoldAsync(FreeTimeBooking booking) => FreeTimeLocation is { } location
        ? ActAsync(() => _api.PostAsync<InboundMailDto>($"api/inbox/{Id}/hold",
                new HoldTimeRequest(location.Id, booking.Treatment.Id, booking.Slot.Start, booking.Slot.EmployeeId, booking.Slot.RoomId, booking.Slot.EquipmentId)),
            "Vorgemerkt. Der Vorschlag steht im Entwurf: prüfen und senden.")
        : Task.CompletedTask;

    /// <summary>Die Zeit ist für den Gast vorgemerkt, bis die Rezeption bestätigt oder freigibt.</summary>
    public bool HasHold => Mail is { HeldStart: not null, AppointmentId: not null };

    public string? HoldText => Mail.HeldStart is { } start ? $"Vorgemerkt: {DayLayout.ShortDayTime(start, _zone)}" : null;

    /// <summary>Was zu tun ist: auf die Antwort des Gastes warten oder, wenn sie da ist, entscheiden.</summary>
    public string HoldDetail => Mail.PreviousMailId is not null
        ? "Der Gast hat geantwortet. Passt es, bestätigen; sonst freigeben."
        : "Die Zeit bleibt frei, bis Sie bestätigen oder freigeben.";

    private bool CanDecideHold => HasHold && !Busy;

    [RelayCommand(CanExecute = nameof(CanDecideHold))]
    private Task ConfirmHoldAsync() => HoldActionAsync("confirm", "Fest gebucht. Die Bestätigung an den Gast wird entworfen.");

    [RelayCommand(CanExecute = nameof(CanDecideHold))]
    private Task ReleaseHoldAsync() => HoldActionAsync("release", "Freigegeben. Die Zeit ist wieder frei.");

    /// <summary>Bestätigen oder freigeben gilt dem Termin; danach steht die Anfrage neu geladen da.</summary>
    private Task HoldActionAsync(string action, string success) => ActAsync(async () =>
    {
        var result = await _api.PostAsync<AppointmentDto>($"api/appointments/{Mail.AppointmentId}/hold/{action}", null).ConfigureAwait(true);
        return result.Value is null
            ? new ApiResult<InboundMailDto>(default, result.Error, result.Status)
            : await _api.GetAsync<InboundMailDto>($"api/inbox/{Id}").ConfigureAwait(true);
    }, success);

    private bool CanSave => IsDraft && DraftChanged && !Busy && ReplyBody.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveDraftAsync() => ActAsync(PutDraftAsync, "Entwurf gespeichert.");

    private Task<ApiResult<InboundMailDto>> PutDraftAsync() =>
        _api.PutAsync<InboundMailDto>($"api/inbox/{Id}/reply", new ReplyDraftInput(IsShortMessage && ReplySubject.Trim().Length == 0 ? Mail.Subject : ReplySubject, ReplyBody));

    private bool CanSend => IsDraft && !DraftChanged && !Busy;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendAsync() => ActAsync(() => _api.PostAsync<InboundMailDto>($"api/inbox/{Id}/send", null), "Antwort wird gesendet.");

    private bool CanReviseNow => CanRevise && !Busy && !IsRevising;

    /// <summary>Knöpfe unter dem Entwurf. Ein geänderter Entwurf wird vorher gespeichert, damit die KI den aktuellen Text überarbeitet.</summary>
    [RelayCommand(CanExecute = nameof(CanReviseNow))]
    private Task ReviseAsync(ReplyRevisionKind kind) => RunRevisionAsync(new ReplyRevisionInput(kind));

    private bool CanReviseCustom => CanReviseNow && ReviseInstruction.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanReviseCustom))]
    private async Task ReviseCustomAsync()
    {
        await RunRevisionAsync(new ReplyRevisionInput(ReplyRevisionKind.Custom, ReviseInstruction.Trim())).ConfigureAwait(true);
        if (!NoticeIsError)
        {
            ReviseInstruction = "";
        }
    }

    private async Task RunRevisionAsync(ReplyRevisionInput input)
    {
        if (DraftChanged)
        {
            var saved = await PutDraftAsync().ConfigureAwait(true);
            if (saved.Value is null)
            {
                Notice = saved.Error ?? "Der Entwurf ließ sich nicht speichern.";
                NoticeIsError = true;
                return;
            }

            Show(saved.Value);
        }

        await ActAsync(() => _api.PostAsync<InboundMailDto>($"api/inbox/{Id}/reply/revise", input), "Die KI überarbeitet den Entwurf …").ConfigureAwait(true);
    }

    private bool CanUndoRevision => Mail.CanUndoReply && !Busy;

    /// <summary>Strg+Z außerhalb des Textfelds: Fassung vor der letzten KI-Überarbeitung.</summary>
    [RelayCommand(CanExecute = nameof(CanUndoRevision))]
    public Task UndoRevisionAsync() =>
        ActAsync(() => _api.PostAsync<InboundMailDto>($"api/inbox/{Id}/reply/undo", null), "Vorige Fassung wiederhergestellt.");

    private bool CanAct => !Busy;

    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task RetryAsync() => ActAsync(() => _api.PostAsync<InboundMailDto>($"api/inbox/{Id}/retry", null), "Wird erneut verarbeitet.");

    /// <summary>Absage mit gefundenem Termin: die Rezeption storniert ihn mit einem Klick.</summary>
    public bool CanCancelAppointment => Mail.CanCancelAppointment;

    /// <summary>„Termin stornieren“ fragt nach (Rückfragedialog, Design-Brief 5.27).</summary>
    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task AskCancelAppointmentAsync()
    {
        var answer = await AskAsync(new ConfirmRequest("Termin stornieren?",
            "Der gefundene Termin ist danach storniert; die Antwort an den Gast wird neu entworfen.", "Stornieren")).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await CancelAppointmentAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task CancelAppointmentAsync() => ActAsync(
        () => _api.PostAsync<InboundMailDto>($"api/inbox/{Id}/cancel-appointment", null), "Termin storniert. Die Antwort wird neu entworfen.");

    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task DismissAsync() => ActAsync(() => _api.PostAsync<InboundMailDto>($"api/inbox/{Id}/dismiss", null), "Als erledigt markiert.");

    [RelayCommand]
    private void Close() => _close();

    private async Task ActAsync(Func<Task<ApiResult<InboundMailDto>>> action, string success)
    {
        Busy = true;
        var result = await action().ConfigureAwait(true);
        Busy = false;
        if (result.Value is null)
        {
            // Posteingang-Aktionen brauchen den Server (anders als Buchungen im Kalender wird hier nichts vorgemerkt).
            Notice = result.Status == 0
                ? "Ohne Verbindung zum Server nicht möglich. Zuordnen, Antworten und Buchen gehen nur mit Verbindung; bitte später noch einmal versuchen."
                : result.Error ?? "Das hat nicht geklappt.";
            NoticeIsError = true;
            return;
        }

        Show(result.Value);
        Notice = success;
        NoticeIsError = false;
        await _changed().ConfigureAwait(true);
        await FollowAsync().ConfigureAwait(true);
    }

    /// <summary>Solange die Queue arbeitet, kurz nachfragen (höchstens 30-mal), falls keine Live-Meldung kommt.</summary>
    private async Task FollowAsync()
    {
        var polled = false;
        for (var i = 0; i < 30 && IsWorking; i++)
        {
            await Task.Delay(_poll).ConfigureAwait(true);
            await ReloadAsync().ConfigureAwait(true);
            polled = true;
        }

        if (polled)
        {
            await _changed().ConfigureAwait(true);
        }
    }
}
