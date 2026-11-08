using System.Globalization;
using System.Windows.Input;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine der letzten Behandlungen des Gastes in der Liste „Zuletzt“; ein Klick öffnet den Termin.</summary>
public sealed class RecentVisitRow
{
    public required Guid AppointmentId { get; init; }

    /// <summary>„3.10.2026“.</summary>
    public required string DateText { get; init; }

    public required string Treatment { get; init; }

    public required string Person { get; init; }

    public required ICommand OpenCommand { get; init; }

    public string Name => $"{DateText}, {Treatment} bei {Person}";
}

/// <summary>
/// Abschnitt „Kunde“ im Termin-Panel (Runde 5, Ä34), aufgebaut wie der Tagesüberblick: in der Kopfzeile vier Kennzahlen (Besuche gesamt, letzter Besuch,
/// nicht erschienen, nächster Termin) und, nur mit dem Recht „Berichte mit Beträgen“, eine Zeile mit offenem Betrag und Paketen; aufgeklappt die
/// Kundennotiz, der Stand der Anamnese (nie Inhalte), die Liste „Zuletzt“, klickbare Kontaktzeilen und der Weg zur Kundenkarte. Alles aus einem Aufruf
/// (<c>GET api/customers/{id}/summary</c>); er lädt beim ersten Zeigen (der Besitzer ruft <see cref="LoadAsync"/>) und ist zu, bis man ihn öffnet.
/// </summary>
public sealed partial class CustomerSectionViewModel : ViewModelBase
{
    /// <summary>Die Kundennotiz hat Platz für zwei Zeilen; ab so vielen Zeichen kürzt sie, dann steht der volle Text im Hinweis.</summary>
    private const int NoteTipFrom = 80;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private readonly IWorkstationApi _api;
    private readonly Guid _customerId;
    private readonly string _guestName;
    private readonly FiscalCountry _country;
    private readonly TimeZoneInfo _zone;
    private readonly TimeProvider _time;
    private readonly Func<Task>? _openCard;
    private readonly Action<Guid, DateTimeOffset>? _openAppointment;
    private readonly Action<string>? _toast;
    private readonly Func<IContactActions?> _actions;
    private int _version;

    public CustomerSectionViewModel(IWorkstationApi api, Guid customerId, string guestName, FiscalCountry country, TimeZoneInfo zone, TimeProvider time,
        Func<Task>? openCard, Action<Guid, DateTimeOffset>? openAppointment, Action<string>? toast, Func<IContactActions?> actions)
    {
        _api = api;
        _customerId = customerId;
        _guestName = guestName;
        _country = country;
        _zone = zone;
        _time = time;
        _openCard = openCard;
        _openAppointment = openAppointment;
        _toast = toast;
        _actions = actions;
    }

    /// <summary>Der Abschnitt ist aufgeklappt (Kopf gewählt).</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStats))]
    public partial bool IsLoading { get; private set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStats), nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>Die Kennzahlen stehen, sobald der Überblick geladen ist.</summary>
    public bool ShowStats => !IsLoading && Error is null;

    public bool CanOpenCard => _openCard is not null;

    // ---------- Kopfzeile: vier Kennzahlen ----------

    [ObservableProperty]
    public partial string VisitsText { get; private set; } = "–";

    [ObservableProperty]
    public partial string LastVisitText { get; private set; } = "–";

    [ObservableProperty]
    public partial string? LastVisitTip { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoShows))]
    public partial string NoShowsText { get; private set; } = "–";

    public bool HasNoShows => NoShowsText is not ("0" or "–");

    [ObservableProperty]
    public partial string NextText { get; private set; } = "–";

    [ObservableProperty]
    public partial string? NextTip { get; private set; }

    /// <summary>„Offen 45,00 €“; leer ohne offenen Betrag oder ohne das Recht.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpen))]
    public partial string? OpenText { get; private set; }

    /// <summary>„Paket Massage: 3 von 10 offen“, mehrere mit „ · “.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpen))]
    public partial string? PackagesText { get; private set; }

    public bool HasOpen => OpenText is not null || PackagesText is not null;

    /// <summary>Die Zeile unter den Kennzahlen: „Offen 45,00 € · Paket Massage: 3 von 10 offen“.</summary>
    public string OpenLine => string.Join(" · ", new[] { OpenText, PackagesText }.OfType<string>());

    // ---------- Aufgeklappt ----------

    /// <summary>Kundennotiz aus „Weitere Angaben“, auf zwei Zeilen gekürzt; der volle Text steht im Hinweis.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote), nameof(NoteTip))]
    public partial string? Note { get; private set; }

    public bool HasNote => Note is not null;

    /// <summary>Voller Text der Kundennotiz, wenn sie in zwei Zeilen nicht mehr passt; sonst kein Hinweis (er wiederholte nur den sichtbaren Text).</summary>
    public string? NoteTip => Note is { Length: >= NoteTipFrom } ? Note : null;

    /// <summary>„Anamnese vorhanden“ oder „Anamnese fehlt“; mehr nicht.</summary>
    [ObservableProperty]
    public partial string AnamnesisText { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecent))]
    public partial IReadOnlyList<RecentVisitRow> Recent { get; private set; } = [];

    public bool HasRecent => Recent.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhone), nameof(HasNoContact))]
    public partial string? Phone { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmail), nameof(HasNoContact))]
    public partial string? Email { get; private set; }

    public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);

    public bool HasEmail => !string.IsNullOrWhiteSpace(Email);

    public bool HasNoContact => !HasPhone && !HasEmail;

    /// <summary>Mit einer Telefonnummer stehen Anrufen und WhatsApp da; ohne sie keiner von beiden.</summary>
    public Uri? PhoneUri => PhoneLinks.Tel(Phone);

    public Uri? WhatsAppUri => PhoneLinks.WhatsApp(Phone, _country);

    public Uri? MailUri => PhoneLinks.Mail(Email, _guestName);

    /// <summary>Lädt den Überblick neu (beim ersten Zeigen und über „Erneut laden“).</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        var version = ++_version;
        IsLoading = true;
        Error = null;
        var result = await _api.GetAsync<CustomerSummary>($"api/customers/{_customerId}/summary").ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        if (result.Value is not { } summary)
        {
            Error = result.Error ?? "Der Überblick konnte nicht geladen werden.";
            IsLoading = false;
            return;
        }

        Show(summary);
        IsLoading = false;
    }

    private void Show(CustomerSummary s)
    {
        var today = DateOnly.FromDateTime(DayLayout.Local(_time.GetUtcNow(), _zone));
        VisitsText = s.Visits.ToString(German);
        LastVisitText = s.LastVisit is { } last ? ShortDate(last, today) : "–";
        LastVisitTip = s.LastVisit is { } lastDay ? $"Letzter Besuch: {lastDay.ToString("dddd, d. MMMM yyyy", German)}" : "Noch kein Besuch";
        NoShowsText = s.NoShows.ToString(German);
        NextText = s.Next is { } next ? ShortDate(DateOnly.FromDateTime(DayLayout.Local(next.Start, _zone)), today) : "–";
        NextTip = s.Next is { } nextAppointment ? $"{DayLayout.ShortDayTime(nextAppointment.Start, _zone)} · {nextAppointment.TreatmentName}" : "Kein Termin gebucht";
        OpenText = s.Open is { OpenAmount: > 0 } open ? $"Offen {Money.Format(open.OpenAmount, open.Currency)}" : null;
        PackagesText = s.Open is { Packages.Count: > 0 } packages
            ? string.Join(" · ", packages.Packages.Select(p => $"Paket {p.TreatmentName}: {p.UnitsLeft} von {p.Units} offen"))
            : null;
        OnPropertyChanged(nameof(OpenLine));
        Note = string.IsNullOrWhiteSpace(s.Note) ? null : s.Note.Trim();
        AnamnesisText = s.AnamnesisOnFile ? "Anamnese vorhanden" : "Anamnese fehlt";
        Recent = [.. s.Recent.Select(r => new RecentVisitRow
        {
            AppointmentId = r.AppointmentId,
            DateText = DayLayout.Local(r.Start, _zone).ToString("d.M.yyyy", CultureInfo.InvariantCulture),
            Treatment = r.TreatmentName,
            Person = r.EmployeeName,
            OpenCommand = new RelayCommand(() => _openAppointment?.Invoke(r.AppointmentId, r.Start)),
        })];
        Phone = s.Phone;
        Email = s.Email;
        OnPropertyChanged(nameof(PhoneUri));
        OnPropertyChanged(nameof(WhatsAppUri));
        OnPropertyChanged(nameof(MailUri));
    }

    /// <summary>„3.10.“ im laufenden Jahr, sonst „3.10.25“; der volle Tag steht im Hinweis.</summary>
    private static string ShortDate(DateOnly date, DateOnly today) =>
        date.ToString(date.Year == today.Year ? "d.M." : "d.M.yy", CultureInfo.InvariantCulture);

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;

    [RelayCommand]
    private Task OpenCardAsync() => _openCard?.Invoke() ?? Task.CompletedTask;

    [RelayCommand]
    private Task CallAsync() => LaunchAsync(PhoneUri);

    [RelayCommand]
    private Task MailAsync() => LaunchAsync(MailUri);

    [RelayCommand]
    private Task WhatsAppAsync() => LaunchAsync(WhatsAppUri);

    private Task LaunchAsync(Uri? uri) => uri is null || _actions() is not { } actions ? Task.CompletedTask : actions.LaunchAsync(uri);

    [RelayCommand]
    private Task CopyPhoneAsync() => CopyAsync(Phone);

    [RelayCommand]
    private Task CopyEmailAsync() => CopyAsync(Email);

    /// <summary>Legt Telefonnummer oder Adresse in die Zwischenablage und bestätigt mit dem Toast „Kopiert“ (unter 60 Zeichen, ohne Punkt).</summary>
    private async Task CopyAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || _actions() is not { } actions)
        {
            return;
        }

        await actions.CopyAsync(text.Trim()).ConfigureAwait(true);
        _toast?.Invoke("Kopiert");
    }
}
