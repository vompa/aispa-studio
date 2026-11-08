using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Kundenübersicht als Flyout im Termin-Panel (Klick auf Initialen oder Namen im Kopf): wer ist der Gast, wie erreicht man ihn, wie oft war er da,
/// was kommt als Nächstes, was ist zu beachten. Alles aus vorhandenen Endpunkten (Kunde, Kennzahlen, Kennzeichen, Wünsche, Termine); die Kundenkarte
/// und „Bearbeiten“ sind eine Handbreit entfernt. Gesundheitsdaten stehen hier nie.
/// </summary>
public sealed partial class CustomerOverviewViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly Guid _customerId;
    private readonly TimeProvider _time;
    private readonly Func<Task>? _openCard;
    private readonly Action? _edit;
    private int _version;

    public CustomerOverviewViewModel(IWorkstationApi api, Guid customerId, TimeProvider time, Func<Task>? openCard, Action? edit)
    {
        _api = api;
        _customerId = customerId;
        _time = time;
        _openCard = openCard;
        _edit = edit;
        Copy = new CopyFeedback(time);
    }

    public Guid CustomerId => _customerId;

    /// <summary>Zwischenablage der gerade offenen Ansicht; sie setzt sich beim Anhängen selbst ein (T-031), Tests setzen ein Doppel.</summary>
    public IContactActions? ContactActions { get; set; }

    /// <summary>Kurze Bestätigung neben Telefon und E-Mail (T-031): „Kopiert.“ oder „Nicht kopiert.“.</summary>
    public CopyFeedback Copy { get; }

    [RelayCommand]
    private Task CopyPhoneAsync() => Copy.CopyAsync(ContactActions, Phone);

    [RelayCommand]
    private Task CopyEmailAsync() => Copy.CopyAsync(ContactActions, Email);

    public bool CanOpenCard => _openCard is not null;

    public bool CanEdit => _edit is not null;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; } = true;

    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>Name im Haus (Rufname statt Vorname).</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>♀/♂ aus der Anrede; null ohne Anrede oder bei „divers“.</summary>
    [ObservableProperty]
    public partial string? SymbolIcon { get; private set; }

    [ObservableProperty]
    public partial string? Pronouns { get; private set; }

    /// <summary>Gültiger Name mit Anrede und Titeln, wie auf Belegen: „Frau Mag. Stefanie Huber, MSc“.</summary>
    [ObservableProperty]
    public partial string? AddressLine { get; private set; }

    /// <summary>„Rufname Steffi“, nur wenn einer eingetragen ist.</summary>
    [ObservableProperty]
    public partial string? PreferredNameText { get; private set; }

    [ObservableProperty]
    public partial string? PostalAddress { get; private set; }

    [ObservableProperty]
    public partial string? Phone { get; private set; }

    [ObservableProperty]
    public partial string? Email { get; private set; }

    /// <summary>„3 Besuche in zwölf Monaten“.</summary>
    [ObservableProperty]
    public partial string? VisitsText { get; private set; }

    /// <summary>„Letzter Besuch 3.10.2026“ bzw. „Noch kein Besuch“.</summary>
    [ObservableProperty]
    public partial string? LastVisitText { get; private set; }

    [ObservableProperty]
    public partial string? Note { get; private set; }

    /// <summary>Wünsche des Gastes in einer Zeile („Druck: kräftig · Duft: Lavendel“).</summary>
    [ObservableProperty]
    public partial string? WishesText { get; private set; }

    /// <summary>Die nächsten drei Termine: „Do. 22.10. 10:00 · Hot-Stone-Massage“.</summary>
    public ObservableCollection<string> Next { get; } = [];

    /// <summary>Kennzeichen wie „Braucht mehr Zeit“, „Nicht bei Lena Huber“.</summary>
    public ObservableCollection<string> Flags { get; } = [];

    /// <summary>Offene Pakete: „Massage 10er-Block: 7 von 10 offen, gültig bis 31.12.2026“.</summary>
    public ObservableCollection<string> Packages { get; } = [];

    public bool HasNext => Next.Count > 0;

    public bool HasFlags => Flags.Count > 0;

    public bool HasPackages => Packages.Count > 0;

    public bool HasPhone => !string.IsNullOrEmpty(Phone);

    public bool HasEmail => !string.IsNullOrEmpty(Email);

    /// <summary>Lädt den Stand neu; beim Öffnen des Flyouts, damit er nie veraltet ist.</summary>
    public async Task LoadAsync()
    {
        var version = ++_version;
        IsLoading = true;
        Error = null;
        var customer = _api.GetAsync<CustomerDto>($"api/customers/{_customerId}");
        var insights = _api.GetAsync<CustomerInsights>($"api/customers/{_customerId}/insights");
        var flags = _api.GetAsync<List<CustomerFlagDto>>($"api/customers/{_customerId}/flags");
        var wishes = _api.GetAsync<CustomerPreferences>($"api/reports/customers/{_customerId}/preferences");
        var appointments = _api.GetAsync<List<AppointmentDto>>($"api/customers/{_customerId}/appointments");
        await Task.WhenAll(customer, insights, flags, wishes, appointments).ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        if (customer.Result.Value is not { } c)
        {
            Error = customer.Result.Error ?? "Der Gast konnte nicht geladen werden.";
            IsLoading = false;
            return;
        }

        Show(c, insights.Result.Value, flags.Result.Value ?? [], wishes.Result.Value, appointments.Result.Value ?? []);
        IsLoading = false;
    }

    private void Show(CustomerDto customer, CustomerInsights? insights, IReadOnlyList<CustomerFlagDto> flags, CustomerPreferences? wishes, IReadOnlyList<AppointmentDto> appointments)
    {
        Name = customer.InHouseName;
        SymbolIcon = Controls.Icons.Salutation(PersonNames.Symbol(customer.Salutation));
        Pronouns = string.IsNullOrWhiteSpace(customer.Pronouns) ? null : $"({customer.Pronouns.Trim()})";
        AddressLine = PersonNames.AddressLine(customer.Salutation, customer.TitlesBefore, customer.FirstName, customer.LastName, customer.TitlesAfter);
        PreferredNameText = string.IsNullOrWhiteSpace(customer.PreferredName) ? null : $"Rufname {customer.PreferredName.Trim()}";
        PostalAddress = customer.Address is { } address ? CustomersViewModel.PostalText(address) : null;
        Phone = customer.Phone;
        Email = customer.Email;
        Note = string.IsNullOrWhiteSpace(customer.Note) ? null : customer.Note.Trim();
        OnPropertyChanged(nameof(HasPhone));
        OnPropertyChanged(nameof(HasEmail));

        VisitsText = insights is null ? null : insights.VisitsLastYear == 1 ? "1 Besuch in zwölf Monaten" : $"{insights.VisitsLastYear} Besuche in zwölf Monaten";
        LastVisitText = insights is null ? null
            : insights.LastVisit is { } last ? $"Letzter Besuch {last.ToString("d.M.yyyy", CultureInfo.InvariantCulture)}" : "Noch kein Besuch";
        WishesText = wishes is null ? null : WishLine(wishes);

        var zone = _time.LocalTimeZone;
        var now = _time.GetUtcNow();
        Replace(Next, appointments
            .Where(a => a.Start > now && a.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn)
            .OrderBy(a => a.Start).Take(3)
            .Select(a => $"{DayLayout.ShortDayTime(a.Start, zone)} · {a.TreatmentName}"));
        Replace(Flags, flags.Select(CustomersViewModel.Title));
        Replace(Packages, (insights?.Packages ?? []).Where(p => p.UnitsLeft > 0).Select(p =>
            $"{p.TreatmentName}: {p.UnitsLeft} von {p.Units} offen" + (p.ValidUntil is { } until ? $", gültig bis {until.ToString("d.M.yyyy", CultureInfo.InvariantCulture)}" : "")));
        OnPropertyChanged(nameof(HasNext));
        OnPropertyChanged(nameof(HasFlags));
        OnPropertyChanged(nameof(HasPackages));
    }

    private static void Replace(ObservableCollection<string> target, IEnumerable<string> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    internal static string? WishLine(CustomerPreferences p)
    {
        var parts = new[]
        {
            p.Pressure is { } pressure ? $"Druck: {pressure}" : null,
            p.Scent is { } scent ? $"Duft: {scent}" : null,
            p.RoomTemperature is { } temperature ? $"Raum: {temperature}" : null,
            p.Music is { } music ? $"Musik: {music}" : null,
            p.PrefersQuiet == true ? "wenig Gespräch" : null,
            p.FocusAreas is { } focus ? $"Fokus: {focus}" : null,
            p.AvoidAreas is { } avoid ? $"nicht behandeln: {avoid}" : null,
            p.Other,
        }.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    [RelayCommand]
    private Task OpenCardAsync() => _openCard?.Invoke() ?? Task.CompletedTask;

    [RelayCommand]
    private void Edit() => _edit?.Invoke();
}
