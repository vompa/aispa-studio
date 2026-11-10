using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zeile der Werte-Tabelle im Seitenpanel: Feld, vorher, nachher; leer steht als „–“.</summary>
public sealed record ActivityValueRow(string Label, string Old, string New)
{
    public static ActivityValueRow From(ActivityValue value) => new(value.Label, Shown(value.Old), Shown(value.New));

    private static string Shown(string? text) => string.IsNullOrWhiteSpace(text) ? "–" : text;
}

/// <summary>
/// Seitenpanel der Seite „Verlauf“ (Design-Brief 5.12, Vorbild Warteliste): der gewählte Eintrag mit Satz, Gast, Person, Kanal, Werten und den Sprüngen zu Termin, Kundenkarte und Beleg.
/// Was gilt, steht im Eintrag vom Server; das Panel lädt nur die Telefonnummer des Gastes und bei einer kurzfristigen Absage die Stornofrist nach.
/// </summary>
public sealed partial class ActivityViewModel
{
    private const string ReasonLabel = "Grund";

    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(5);

    private static readonly string[] PanelProperties =
    [
        nameof(HasPanel), nameof(PanelTitle), nameof(PanelSubtitle), nameof(PanelText), nameof(PanelGuest), nameof(HasPanelGuest), nameof(PanelActor), nameof(HasPanelActor),
        nameof(PanelChannel), nameof(PanelReason), nameof(HasPanelReason), nameof(PanelValues), nameof(HasPanelValues), nameof(ShowRestore), nameof(ShowReceipt), nameof(ShowAppointment),
        nameof(ShowCustomer), nameof(IsRestorePrimary), nameof(IsReceiptPrimary), nameof(IsAppointmentPrimary), nameof(IsCustomerPrimary), nameof(AppointmentTip),
        nameof(HasPanelPhone), nameof(HasLateNotice), nameof(ShowTreatment), nameof(ShowRoster), nameof(ShowAccess), nameof(IsTreatmentPrimary), nameof(IsRosterPrimary),
        nameof(IsAccessPrimary), nameof(ShowRequest), nameof(IsRequestPrimary),
    ];

    private int _panelVersion;
    private int _toastClock;

    /// <summary>„Kundenkarte öffnen“: die Shell wechselt zur Seite Kunden und wählt den Gast.</summary>
    public Func<Guid, Task>? CustomerRequested { get; set; }

    /// <summary>„Termin öffnen“: die Shell wechselt in den Kalender auf den Tag (Beginn des Termins) und wählt den Termin.</summary>
    public Action<Guid, DateTimeOffset?>? AppointmentRequested { get; set; }

    /// <summary>„Termin öffnen“ bei einem Storno: die Shell wechselt in den Kalender auf den Tag und öffnet das Panel des stornierten Termins (der Kalender zeigt ihn nicht als Karte).</summary>
    public Action<Guid, DateTimeOffset>? CancelledAppointmentRequested { get; set; }

    /// <summary>„Behandlung öffnen“: die Verwaltung zeigt die Behandlung in den Stammdaten.</summary>
    public Action<Guid>? TreatmentRequested { get; set; }

    /// <summary>„Dienstplan öffnen“: die Verwaltung zeigt den Wochenplan der Woche des Tages, mit der Person.</summary>
    public Action<DateOnly, Guid?>? RosterRequested { get; set; }

    /// <summary>„Zugang öffnen“: die Verwaltung zeigt den Zugang der Person.</summary>
    public Action<Guid?>? AccessRequested { get; set; }

    /// <summary>„Beleg anzeigen“: die Shell öffnet die Kassa auf dem Reiter Belege, am Tag des Belegs (Ausstellung), mit dem Beleg gewählt.</summary>
    public Func<Guid, DateTimeOffset, Task>? ReceiptRequested { get; set; }

    /// <summary>„Anfrage öffnen“: die Shell wechselt zu den Anfragen und wählt die Anfrage.</summary>
    public Func<Guid, Task>? RequestRequested { get; set; }

    /// <summary>Wird ausgelöst, wenn die Person das Panel schließt (Esc, Schließen-Knopf); die Ansicht gibt den Fokus an die Liste zurück.</summary>
    public event Action? PanelClosed;

    /// <summary>Der Eintrag im Panel; <c>null</c> heißt: das Panel ist zu.</summary>
    [ObservableProperty]
    public partial ActivityEntry? PanelEntry { get; private set; }

    [ObservableProperty]
    public partial string? PanelPhone { get; private set; }

    [ObservableProperty]
    public partial string? LateNotice { get; private set; }

    /// <summary>Fehler im Panel (Sprung ohne Antwort, Rücknahme abgelehnt); steht als InfoBar „Fehler“ oben im Inhalt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanelError))]
    public partial string? PanelError { get; private set; }

    /// <summary>Meldung unten (meldungen.md M1): eine Zeile ohne Schlusspunkt, nach 5 Sekunden weg.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToast))]
    public partial string? Toast { get; private set; }

    public bool HasToast => Toast is not null;

    public bool HasPanelError => PanelError is not null;

    public bool HasPanel => PanelEntry is not null;

    public string PanelTitle => PanelEntry is { } entry ? KindTitle(entry.Kind) : "";

    /// <summary>„Mo. 9.11., 10:30“ in der Ortszeit des Standorts.</summary>
    public string PanelSubtitle
    {
        get
        {
            if (PanelEntry is not { } entry)
            {
                return "";
            }

            var local = DayLayout.Local(entry.At, Zone());
            return $"{DayLayout.ShortDay(DateOnly.FromDateTime(local))}, {local.ToString("HH:mm", CultureInfo.InvariantCulture)}";
        }
    }

    public string PanelText => PanelEntry?.Text ?? "";

    public string? PanelGuest => string.IsNullOrWhiteSpace(PanelEntry?.CustomerName) ? null : PanelEntry.CustomerName;

    public bool HasPanelGuest => PanelGuest is not null;

    public bool HasPanelPhone => !string.IsNullOrWhiteSpace(PanelPhone);

    public string? PanelActor => string.IsNullOrWhiteSpace(PanelEntry?.Actor) ? null : PanelEntry.Actor;

    public bool HasPanelActor => PanelActor is not null;

    public string PanelChannel => PanelEntry?.Channel switch
    {
        ActivityChannel.Online => "Online",
        ActivityChannel.Assistant => "Terminannahme",
        ActivityChannel.Background => "Hintergrund",
        _ => "Rezeption",
    };

    /// <summary>Der Grund einer Absage aus den Werten („Grund“); nur bei Stornos.</summary>
    public string? PanelReason => PanelEntry is { Kind: ActivityKind.Cancelled } entry && entry.Values.FirstOrDefault(v => v.Label == ReasonLabel) is { } reason
        ? reason.New ?? reason.Old
        : null;

    public bool HasPanelReason => !string.IsNullOrWhiteSpace(PanelReason);

    public bool HasLateNotice => LateNotice is not null;

    /// <summary>Die Werte alt → neu; der Grund einer Absage steht schon darüber und kommt nicht doppelt.</summary>
    public IReadOnlyList<ActivityValueRow> PanelValues => PanelEntry is { } entry
        ? [.. entry.Values.Where(v => !(entry.Kind == ActivityKind.Cancelled && v.Label == ReasonLabel)).Select(ActivityValueRow.From)]
        : [];

    public bool HasPanelValues => PanelValues.Count > 0;

    // ---------- Knöpfe ----------

    public bool ShowRestore => PanelEntry is { Kind: ActivityKind.Cancelled, AppointmentId: not null };

    public bool ShowReceipt => PanelEntry is { Kind: ActivityKind.Paid or ActivityKind.Deposit or ActivityKind.ReceiptVoided, ReceiptId: not null };

    public bool ShowRequest => PanelEntry is { Kind: ActivityKind.Request, RequestId: not null } && RequestRequested is not null;

    public bool ShowAppointment => PanelEntry is { AppointmentId: not null };

    public bool ShowCustomer => PanelEntry is { CustomerId: not null };

    public bool ShowTreatment => PanelEntry is { Kind: ActivityKind.TreatmentChanged, TreatmentId: not null };

    public bool ShowRoster => PanelEntry is { Kind: ActivityKind.RosterChanged };

    public bool ShowAccess => PanelEntry is { Kind: ActivityKind.AccessChanged };

    /// <summary>Genau ein Knopf ist primär (5.12 Punkt 6): Zurücknehmen, sonst Beleg, Anfrage, Termin, Kundenkarte, Behandlung, Dienstplan, Zugang.</summary>
    public bool IsRestorePrimary => ShowRestore;

    public bool IsReceiptPrimary => !ShowRestore && ShowReceipt;

    public bool IsRequestPrimary => !ShowRestore && !ShowReceipt && ShowRequest;

    public bool IsAppointmentPrimary => !ShowRestore && !ShowReceipt && !ShowRequest && ShowAppointment;

    public bool IsCustomerPrimary => !ShowRestore && !ShowReceipt && !ShowRequest && !ShowAppointment && ShowCustomer;

    public bool IsTreatmentPrimary => !ShowRestore && !ShowReceipt && !ShowAppointment && !ShowCustomer && ShowTreatment;

    public bool IsRosterPrimary => !ShowRestore && !ShowReceipt && !ShowAppointment && !ShowCustomer && ShowRoster;

    public bool IsAccessPrimary => !ShowRestore && !ShowReceipt && !ShowAppointment && !ShowCustomer && ShowAccess;

    public string AppointmentTip => PanelEntry?.Kind == ActivityKind.Cancelled
        ? "Öffnet den stornierten Termin im Kalender."
        : "Zeigt den Termin im Kalender.";

    public bool CanRestore => _user.Rights.Contains(AccessRight.Book);

    public bool CanOpenTreatment => _user.Rights.Contains(AccessRight.ManageCatalog);

    public bool CanOpenTeam => _user.Rights.Contains(AccessRight.ManageStaff);

    /// <summary>Warum „Behandlung öffnen“ gesperrt ist; leer, solange es geht.</summary>
    public string TreatmentHint => CanOpenTreatment ? "" : "Dafür braucht der Zugang das Recht, Behandlungen zu verwalten.";

    /// <summary>Warum „Dienstplan öffnen“ und „Zugang öffnen“ gesperrt sind; leer, solange es geht.</summary>
    public string TeamHint => CanOpenTeam ? "" : "Dafür braucht der Zugang das Recht, das Team zu verwalten.";

    /// <summary>Warum „Stornierung zurücknehmen“ gesperrt ist; leer, solange es geht.</summary>
    public string RestoreHint => CanRestore ? "" : "Dafür braucht der Zugang das Recht, Termine zu buchen.";

    // ---------- Öffnen und Schließen ----------

    /// <summary>Zeigt den Eintrag im Panel und lädt Telefonnummer und Stornofrist nach.</summary>
    private void ShowPanel(ActivityEntry entry)
    {
        var version = ++_panelVersion;
        PanelEntry = entry;
        PanelPhone = null;
        PanelError = null;
        LateNotice = entry.Kind == ActivityKind.Cancelled && entry.LateCancellation == true ? "Kurzfristig abgesagt: innerhalb der Stornofrist des Standorts." : null;
        NotifyPanel();
        _ = LoadPanelDetailsAsync(entry, version);
    }

    /// <summary>Schließt das Panel, ohne dass die Person es verlangt hat (neue Liste); der Fokus bleibt, wo er ist.</summary>
    private void DropPanel()
    {
        _panelVersion++;
        if (PanelEntry is not null)
        {
            PanelEntry = null;
            NotifyPanel();
        }
    }

    /// <summary>Esc oder Schließen-Kreuz: das Panel zu, der Fokus zurück auf die Liste.</summary>
    [RelayCommand]
    public void ClosePanel()
    {
        if (PanelEntry is null)
        {
            return;
        }

        DropPanel();
        PanelClosed?.Invoke();
    }

    private async Task LoadPanelDetailsAsync(ActivityEntry entry, int version)
    {
        if (entry.CustomerId is { } customerId)
        {
            var customer = await _api.GetAsync<CustomerDto>($"api/customers/{customerId}").ConfigureAwait(true);
            if (version == _panelVersion && customer.Value is { } guest)
            {
                PanelPhone = string.IsNullOrWhiteSpace(guest.Phone) ? null : guest.Phone.Trim();
            }
        }

        if (entry.Kind == ActivityKind.Cancelled && entry.LateCancellation == true && LocationSource?.Invoke() is { } location)
        {
            var rules = await _api.GetAsync<LocationOnlineRules>($"api/locations/{location.Id}/online-rules").ConfigureAwait(true);
            if (version == _panelVersion && rules.Value is { } value)
            {
                LateNotice = LateNoticeText(value);
            }
        }
    }

    /// <summary>„Kurzfristig abgesagt: innerhalb der Stornofrist von 24 Stunden. Laut Regel fallen 50 % Ausfallgebühr an.“</summary>
    internal static string LateNoticeText(LocationOnlineRules rules)
    {
        var hours = rules.CancellationDeadlineHours == 1 ? "1 Stunde" : $"{rules.CancellationDeadlineHours} Stunden";
        var text = $"Kurzfristig abgesagt: innerhalb der Stornofrist von {hours}.";
        return rules.NoShowFeePercent > 0 ? $"{text} Laut Regel fallen {rules.NoShowFeePercent} % Ausfallgebühr an." : text;
    }

    private void NotifyPanel()
    {
        foreach (var name in PanelProperties)
        {
            OnPropertyChanged(name);
        }
    }

    partial void OnPanelPhoneChanged(string? value) => OnPropertyChanged(nameof(HasPanelPhone));

    partial void OnLateNoticeChanged(string? value) => OnPropertyChanged(nameof(HasLateNotice));

    private static string KindTitle(ActivityKind kind) => kind switch
    {
        ActivityKind.Booked => "Buchung",
        ActivityKind.Moved => "Verschoben",
        ActivityKind.Cancelled => "Storno",
        ActivityKind.Restored => "Storno zurückgenommen",
        ActivityKind.StatusChanged => "Status",
        ActivityKind.AppointmentChanged => "Termin geändert",
        ActivityKind.Paid => "Zahlung",
        ActivityKind.Deposit => "Anzahlung",
        ActivityKind.ReceiptVoided => "Beleg storniert",
        ActivityKind.CustomerCreated => "Neuer Kunde",
        ActivityKind.CustomerChanged => "Kundendaten",
        ActivityKind.TreatmentChanged => "Behandlung",
        ActivityKind.RosterChanged => "Dienstplan",
        ActivityKind.AccessChanged => "Zugang",
        ActivityKind.Request => "Anfrage",
        _ => "Firmendaten",
    };

    // ---------- Sprünge ----------

    /// <summary>„Termin öffnen“: der Kalender zeigt den Tag und wählt den Termin; ein stornierter Termin öffnet mit seinem Panel (dort steht „Stornierung zurücknehmen“).</summary>
    [RelayCommand]
    private async Task OpenAppointmentAsync()
    {
        if (PanelEntry is not { AppointmentId: { } id } entry)
        {
            return;
        }

        PanelError = null;
        var appointment = await _api.GetAsync<AppointmentDto>($"api/appointments/{id}").ConfigureAwait(true);
        if (appointment.Value is not { } found)
        {
            PanelError = appointment.Error ?? "Der Termin konnte nicht geladen werden.";
            return;
        }

        if (entry.Kind == ActivityKind.Cancelled)
        {
            CancelledAppointmentRequested?.Invoke(id, found.Start);
            return;
        }

        AppointmentRequested?.Invoke(id, found.Start);
    }

    [RelayCommand]
    private async Task OpenCustomerAsync()
    {
        if (PanelEntry?.CustomerId is { } id && CustomerRequested is { } open)
        {
            await open(id).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void OpenTreatment()
    {
        if (PanelEntry?.TreatmentId is { } id && CanOpenTreatment)
        {
            TreatmentRequested?.Invoke(id);
        }
    }

    [RelayCommand]
    private void OpenRoster()
    {
        if (PanelEntry is { } entry && CanOpenTeam)
        {
            RosterRequested?.Invoke(DateOnly.FromDateTime(DayLayout.Local(entry.At, Zone())), entry.EmployeeId);
        }
    }

    [RelayCommand]
    private void OpenAccess()
    {
        if (PanelEntry is { } entry && CanOpenTeam)
        {
            AccessRequested?.Invoke(entry.EmployeeId);
        }
    }

    [RelayCommand]
    private async Task OpenRequestAsync()
    {
        if (PanelEntry is { RequestId: { } id } && RequestRequested is { } open)
        {
            await open(id).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ShowReceiptAsync()
    {
        if (PanelEntry is { ReceiptId: { } id } entry && ReceiptRequested is { } open)
        {
            await open(id, entry.At).ConfigureAwait(true);
        }
    }

    // ---------- Meldung unten ----------

    /// <summary>Zeigt eine Rückmeldung ohne Schlusspunkt, höchstens 60 Zeichen, und blendet sie nach 5 Sekunden aus.</summary>
    private void ShowToast(string text)
    {
        Toast = CalendarViewModel.FitToast(text);
        var clock = ++_toastClock;
        _ = ExpireToastAsync(clock);
    }

    private async Task ExpireToastAsync(int clock)
    {
        await Task.Delay(ToastDuration, _time).ConfigureAwait(true);
        if (clock == _toastClock)
        {
            Toast = null;
        }
    }

    [RelayCommand]
    private void CloseToast()
    {
        _toastClock++;
        Toast = null;
    }
}
