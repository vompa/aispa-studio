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
        nameof(HasSideChips), nameof(IsNewPrimary), nameof(IsBookPrimary),
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

    // ---------- Verlauf rechts (Variante C, Auftraggeber, 10.11.2026): nächster Termin als Kachel, „Zuletzt“ kurz mit der Stammperson einmal, Pakete, Gutscheine und Käufe als Chips ----------

    /// <summary>Der nächste Termin in der Kachel oben im Verlauf, sonst null.</summary>
    [ObservableProperty]
    public partial NextVisitEntry? NextVisit { get; private set; }

    /// <summary>Die zwei Termine nach dem nächsten, kurz unter der Kachel („Fr. 20.11. · 10:30“, „Aromaöl-Massage“).</summary>
    public ObservableCollection<TimelineEntry> LaterEntries { get; } = [];

    /// <summary>Die letzten drei Termine: Datum und Behandlung, die Person nur, wenn es nicht die Stammperson war.</summary>
    public ObservableCollection<TimelineEntry> RecentEntries { get; } = [];

    /// <summary>„meist bei Lena Huber“, wenn eine Person mehr als die Hälfte aller Besuche hatte, sonst null.</summary>
    [ObservableProperty]
    public partial string? MainPersonText { get; private set; }

    /// <summary>Offene Pakete, Gutscheine und der letzte Kauf als Chips; den vollen Wortlaut trägt der Tooltip. Gutscheine und Käufe nur mit dem Recht zu buchen (T-020).</summary>
    public ObservableCollection<PreferenceChip> SideChips { get; } = [];

    public bool HasSideChips => SideChips.Count > 0;

    /// <summary>Der Gast war schon da, hat aber keinen Termin vor sich: der Verlauf sagt „Kein Termin geplant.“ statt der Kachel.</summary>
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

    private string RecentText(AppointmentDto a, Guid? mainPerson) => a.EmployeeId == mainPerson ? a.TreatmentName : VisitText(a);

    private static string When(AppointmentDto a, TimeZoneInfo zone) => DayLayout.Local(a.Start, zone).ToString("ddd d.M. · HH:mm", GermanDates);

    private static DateOnly LocalDate(DateTimeOffset at, TimeZoneInfo zone) => DateOnly.FromDateTime(DayLayout.Local(at, zone));

    /// <summary>„19.10.“ im laufenden Jahr, sonst „21.9.2025“ (wie die Kennzahlenzeile).</summary>
    public static string ShortDate(DateOnly date, DateOnly today) => date.ToString(date.Year == today.Year ? "d.M." : "d.M.yyyy", CultureInfo.InvariantCulture);

    /// <summary>„heute“, „morgen“, „in 3 Tagen“, ab zwei Wochen „in 5 Wochen“.</summary>
    public static string RelativeDays(int days) => days switch
    {
        <= 0 => "heute",
        1 => "morgen",
        < 14 => $"in {days} Tagen",
        _ => $"in {days / 7} Wochen",
    };

    /// <summary>Die Person mit mehr als der Hälfte aller Besuche (der Server zählt sie in <see cref="CustomerInsights.Employees"/>), sonst null.</summary>
    private static CustomerEmployeeVisits? MainPerson(CustomerInsights? insights) =>
        insights?.Employees.MaxBy(e => e.Visits) is { } top && top.Visits * 2 > insights.Employees.Sum(e => e.Visits) ? top : null;

    /// <summary>Vergangene Termine tragen nur abweichende Zustände (nicht erschienen, storniert, noch eingecheckt) als Pille.</summary>
    private static string? PastStatus(AppointmentDto a) =>
        a.Status is AppointmentStatus.NoShow or AppointmentStatus.Cancelled or AppointmentStatus.CheckedIn ? StatusText(a.Status) : null;

    private void ShowTimeline(List<AppointmentDto> appointments, DateTimeOffset now, TimeZoneInfo zone)
    {
        LaterEntries.Clear();
        RecentEntries.Clear();
        var today = LocalDate(now, zone);
        var upcoming = appointments.Where(a => a.Start > now && a.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn).OrderBy(a => a.Start).ToList();
        NextVisit = upcoming.FirstOrDefault() is { } next
            ? new NextVisitEntry(When(next, zone), RelativeDays(LocalDate(next.Start, zone).DayNumber - today.DayNumber), VisitText(next))
            : null;
        foreach (var a in upcoming.Skip(1).Take(2))
        {
            LaterEntries.Add(new TimelineEntry(When(a, zone), a.TreatmentName));
        }

        // „Zuletzt“ zeigt drei Termine (Auftraggeber, 10.11.2026); ältere stehen unter „Alle Termine“ und auf der Seite „Verlauf“.
        var main = MainPerson(Insights);
        MainPersonText = main is null ? null : $"meist bei {main.Name}";
        foreach (var a in appointments.Where(a => a.Start <= now).OrderByDescending(a => a.Start).Take(3))
        {
            RecentEntries.Add(new TimelineEntry(
                ShortDate(LocalDate(a.Start, zone), today), RecentText(a, main?.EmployeeId), PastStatus(a), a.Status is AppointmentStatus.NoShow or AppointmentStatus.Cancelled));
        }

        NoUpcomingPlanned = upcoming.Count == 0 && appointments.Count > 0;
        CheckoutAppointment = DueToday(appointments, now, zone);
    }

    private void ShowPackages(CustomerInsights? insights)
    {
        SideChips.Clear();
        foreach (var package in insights?.Packages ?? [])
        {
            var text = $"{package.TreatmentName}: {package.UnitsLeft} von {package.Units} offen";
            SideChips.Add(new PreferenceChip(text, Tip: text + ValidUntil(package.ValidUntil)));
        }

        foreach (var voucher in insights?.Vouchers ?? [])
        {
            var balance = Money.Format(voucher.Balance, voucher.Currency);
            SideChips.Add(new PreferenceChip(
                voucher.TreatmentName is { } treatment ? $"Gutschein {treatment} {balance}" : $"Gutschein {balance}",
                Tip: $"{voucher.TreatmentName ?? "Wertgutschein"} …{voucher.CodeEnd}: {balance} offen{ValidUntil(voucher.ValidUntil)}"));
        }

        if (insights?.Purchases is [var last, ..] purchases)
        {
            SideChips.Add(new PreferenceChip(
                $"Letzter Kauf: {PurchaseText(last)}",
                Tip: string.Join("\n", purchases.Select(p => $"{p.Date.ToString("d.M.yyyy", CultureInfo.InvariantCulture)} · {PurchaseText(p)}"))));
        }

        RaiseSections();
    }

    private static string ValidUntil(DateOnly? until) => until is { } date ? $", gültig bis {date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}" : "";

    /// <summary>„Massageöl 2× · 24,00 €“.</summary>
    private static string PurchaseText(GuestPurchase purchase) =>
        purchase.Description + (purchase.Quantity == 1 ? "" : $" {purchase.Quantity}×") + $" · {Money.Format(purchase.Amount, purchase.Currency)}";
}
