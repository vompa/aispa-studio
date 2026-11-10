using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Aufbau der Kundenkarte (R07 Slice 141, Ä349): ruhiger Kopf, „Gut zu wissen“ links, Verlauf rechts; Leeres kostet keinen Platz ----------
public partial class CustomersViewModel
{
    private static readonly CultureInfo GermanDates = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Zustände, von denen abhängt, welche Abschnitte die Karte zeigt.</summary>
    private static readonly HashSet<string> SectionStates =
    [
        nameof(Selected), nameof(Insights), nameof(Wishes), nameof(IsAddingFlag), nameof(IsEditingWishes), nameof(IsEditingReceptionNote),
        nameof(IsEditingBooking), nameof(IsEditingBilling), nameof(IsEditingDiscount), nameof(IsAddingEmail), nameof(IsEditingAny), nameof(IsEditingConsent), nameof(Consent),
    ];

    private static readonly string[] SectionProperties =
    [
        nameof(WishesSectionVisible), nameof(FlagsSectionVisible), nameof(ReceptionNoteSectionVisible), nameof(BookingSectionVisible),
        nameof(BillingSectionVisible), nameof(DiscountSectionVisible), nameof(NoteSectionVisible), nameof(EmailsSectionVisible), nameof(ConsentSectionVisible),
        nameof(MissingWishes), nameof(MissingFlags), nameof(MissingReceptionNote), nameof(MissingBooking), nameof(HasMissing),
        nameof(HasPackages), nameof(HasVouchers), nameof(HasPurchases), nameof(HasNameChanges), nameof(IsNewPrimary), nameof(IsBookPrimary),
    ];

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is { } name && SectionStates.Contains(name))
        {
            RaiseSections();
        }
    }

    private void RaiseSections()
    {
        foreach (var name in SectionProperties)
        {
            base.OnPropertyChanged(new PropertyChangedEventArgs(name));
        }
    }

    // ---------- „Gut zu wissen“: ein Abschnitt erscheint nur, wenn etwas gesetzt ist oder gerade bearbeitet wird ----------

    public bool WishesSectionVisible => WishChips.Count > 0 || IsEditingWishes;

    public bool FlagsSectionVisible => Flags.Count > 0 || IsAddingFlag;

    public bool ReceptionNoteSectionVisible => MaySeeReceptionNote && (Insights?.ReceptionNote is not null || IsEditingReceptionNote);

    public bool BookingSectionVisible => Selected?.Booking is not null || IsEditingBooking;

    public bool BillingSectionVisible => Selected?.Billing is not null || IsEditingBilling;

    public bool DiscountSectionVisible => Selected?.Discount is not null || IsEditingDiscount;

    public bool NoteSectionVisible => !string.IsNullOrWhiteSpace(Selected?.Note);

    public bool EmailsSectionVisible => OtherEmails.Count > 0 || IsAddingEmail;

    // ---------- „Noch nicht erfasst: + Wünsche · + Kennzeichen · + Rezeptionsvermerk · + Termine am liebsten“ ----------

    /// <summary>Erst nach dem Laden: vorher wüsste die Karte nicht, was fehlt.</summary>
    private bool IsCardLoaded => Insights is not null;

    public bool MissingWishes => IsCardLoaded && !WishesSectionVisible;

    public bool MissingFlags => IsCardLoaded && !FlagsSectionVisible;

    public bool MissingReceptionNote => IsCardLoaded && MaySeeReceptionNote && !ReceptionNoteSectionVisible;

    public bool MissingBooking => IsCardLoaded && Selected is not null && !BookingSectionVisible;

    public bool HasMissing => !IsAnonymized && (MissingWishes || MissingFlags || MissingReceptionNote || MissingBooking);

    // ---------- Ein Hauptknopf je Ansicht: ohne Karte „Neue Kundin“, mit Karte „Termin buchen“, beim Bearbeiten der Speichern-Knopf ----------

    public bool IsNewPrimary => !HasSelection && !IsEditingAny;

    public bool IsBookPrimary => !IsEditingAny;

    // ---------- Kopf: „Termin buchen“ (Hauptknopf), „Kassieren“, wenn heute etwas offen ist, und das Menü „…“ ----------

    /// <summary>Buchen, Kassieren und die Stammdaten verlangen das Recht „Termine buchen“; ohne Recht fehlen die Knöpfe, die API prüft trotzdem. Bei einem anonymisierten Gast fehlen sie immer.</summary>
    public bool CanBook => User?.Rights.Contains(AccessRight.Book) == true && !IsAnonymized;

    /// <summary>Verwaltungsaktionen der Karte (Kennzeichen „Leitung informieren“ und „Nicht mehr buchen“) nur für die Leitung.</summary>
    public bool MayManage => User?.Rights.Contains(AccessRight.ManageLocation) == true;

    [RelayCommand]
    private void Book()
    {
        if (Selected is { } customer)
        {
            BookRequested?.Invoke(customer);
        }
    }

    /// <summary>„Kassieren“ setzt den Termin auf die Rechnung (wie „K“ im Kalender).</summary>
    public Func<AppointmentDto, Task>? CheckoutRequested { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheckout))]
    public partial AppointmentDto? CheckoutAppointment { get; private set; }

    public bool CanCheckout => CanBook && CheckoutRequested is not null && CheckoutAppointment is not null;

    [RelayCommand]
    private Task CheckoutAsync() => CheckoutRequested is { } checkout && CheckoutAppointment is { } appointment ? checkout(appointment) : Task.CompletedTask;

    /// <summary>Heute eingecheckt oder erledigt: dieselbe Regel wie „Kassieren“ im Kalender; ob schon bezahlt ist, weiß erst die Kassa.</summary>
    private static AppointmentDto? DueToday(IEnumerable<AppointmentDto> appointments, DateTimeOffset now, TimeZoneInfo zone)
    {
        var today = DayLayout.Local(now, zone).Date;
        return appointments
            .Where(a => a.Status is AppointmentStatus.CheckedIn or AppointmentStatus.Completed && DayLayout.Local(a.Start, zone).Date == today)
            .MaxBy(a => a.Start);
    }

    /// <summary>„Leitung informieren …“ und „Nicht mehr buchen …“ im Menü „…“: öffnet „Kennzeichen setzen“ mit dieser Art.</summary>
    [RelayCommand]
    private void AddManagementFlag(CustomerFlagKind kind)
    {
        AddFlag();
        NewFlag = FlagChoices.FirstOrDefault(c => c.Kind == kind) ?? NewFlag;
    }

    // ---------- Weitere E-Mail-Adresse (Menü „…“) ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingAny))]
    public partial bool IsAddingEmail { get; private set; }

    [RelayCommand]
    private void StartAddEmail()
    {
        CloseEditors();
        NewOtherEmail = "";
        IsAddingEmail = true;
    }

    [RelayCommand]
    private void CancelAddEmail() => IsAddingEmail = false;

    // ---------- Verlauf rechts: nächste und letzte Termine mit Behandlung und Person, offene Pakete, Namensänderungen ----------

    public ObservableCollection<TimelineEntry> UpcomingEntries { get; } = [];

    public ObservableCollection<TimelineEntry> RecentEntries { get; } = [];

    public ObservableCollection<string> PackageLines { get; } = [];

    public ObservableCollection<string> NameChangeLines { get; } = [];

    public bool HasPackages => PackageLines.Count > 0;

    /// <summary>Offene Wertgutscheine, z. B. „Wertgutschein …4F2K: 35,00 € offen, gültig bis 31.12.2026“ (nur mit dem Recht zu buchen, T-020).</summary>
    public ObservableCollection<string> VoucherLines { get; } = [];

    /// <summary>Letzte Käufe ohne Termin, z. B. „3.11.2026 · Massageöl 2× · 24,00 €“ (nur mit dem Recht zu buchen, T-020).</summary>
    public ObservableCollection<string> PurchaseLines { get; } = [];

    public bool HasVouchers => VoucherLines.Count > 0;

    public bool HasPurchases => PurchaseLines.Count > 0;

    public bool HasNameChanges => NameChangeLines.Count > 0;

    /// <summary>Der Gast war schon da, hat aber keinen Termin vor sich: der Verlauf sagt unter „Demnächst“ „Kein Termin geplant.“ (statt einer eigenen Zeile „Nächster Termin“).</summary>
    [ObservableProperty]
    public partial bool NoUpcomingPlanned { get; private set; }

    /// <summary>Alle Termine (höchstens die letzten 100) unter dem Verlauf, aufgeklappt mit „Alle Termine“.</summary>
    [ObservableProperty]
    public partial bool ShowAllVisits { get; set; }

    [RelayCommand]
    private void ToggleAllVisits() => ShowAllVisits = !ShowAllVisits;

    private string? EmployeeName(Guid id) =>
        _employeeNames?.GetValueOrDefault(id) ?? Insights?.Employees.FirstOrDefault(e => e.EmployeeId == id)?.Name;

    private string VisitText(AppointmentDto a) => EmployeeName(a.EmployeeId) is { } person ? $"{a.TreatmentName} bei {person}" : a.TreatmentName;

    private static string When(AppointmentDto a, TimeZoneInfo zone) => DayLayout.Local(a.Start, zone).ToString("ddd d.M. HH:mm", GermanDates);

    /// <summary>Vergangene Termine tragen nur abweichende Zustände (nicht erschienen, storniert, noch eingecheckt) als Pille.</summary>
    private static string? PastStatus(AppointmentDto a) =>
        a.Status is AppointmentStatus.NoShow or AppointmentStatus.Cancelled or AppointmentStatus.CheckedIn ? StatusText(a.Status) : null;

    private void ShowTimeline(List<AppointmentDto> appointments, DateTimeOffset now, TimeZoneInfo zone)
    {
        UpcomingEntries.Clear();
        RecentEntries.Clear();
        var upcoming = appointments.Where(a => a.Start > now && a.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn).OrderBy(a => a.Start).ToList();
        foreach (var a in upcoming.Take(3))
        {
            UpcomingEntries.Add(new TimelineEntry(When(a, zone), VisitText(a)));
        }

        foreach (var a in appointments.Where(a => a.Start <= now).OrderByDescending(a => a.Start).Take(5))
        {
            RecentEntries.Add(new TimelineEntry(When(a, zone), VisitText(a), PastStatus(a), a.Status is AppointmentStatus.NoShow or AppointmentStatus.Cancelled));
        }

        NoUpcomingPlanned = upcoming.Count == 0 && appointments.Count > 0;
        CheckoutAppointment = DueToday(appointments, now, zone);
    }

    private void ShowPackages(CustomerInsights? insights)
    {
        PackageLines.Clear();
        NameChangeLines.Clear();
        foreach (var package in insights?.Packages ?? [])
        {
            PackageLines.Add($"{package.TreatmentName}: {package.UnitsLeft} von {package.Units} offen"
                + (package.ValidUntil is { } until ? $", gültig bis {until.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}" : ""));
        }

        VoucherLines.Clear();
        foreach (var voucher in insights?.Vouchers ?? [])
        {
            VoucherLines.Add($"{voucher.TreatmentName ?? "Wertgutschein"} …{voucher.CodeEnd}: {Money.Format(voucher.Balance, voucher.Currency)} offen"
                + (voucher.ValidUntil is { } until ? $", gültig bis {until.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}" : ""));
        }

        PurchaseLines.Clear();
        foreach (var purchase in insights?.Purchases ?? [])
        {
            PurchaseLines.Add($"{purchase.Date.ToString("d.M.yyyy", CultureInfo.InvariantCulture)} · {purchase.Description}"
                + (purchase.Quantity == 1 ? "" : $" {purchase.Quantity}×") + $" · {Money.Format(purchase.Amount, purchase.Currency)}");
        }

        // Der gültige Name steht auf Belegen: jede Änderung bleibt nachvollziehbar.
        foreach (var change in insights?.NameChanges ?? [])
        {
            NameChangeLines.Add($"{change.At.ToLocalTime().ToString("d.M.yyyy", CultureInfo.InvariantCulture)}: {change.OldName} → {change.NewName}"
                + (change.ChangedBy is { } by ? $" ({by})" : ""));
        }

        RaiseSections();
    }
}
