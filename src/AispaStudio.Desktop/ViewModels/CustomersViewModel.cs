using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Auswahl, wofür der Kundenrabatt gilt.</summary>
public sealed record DiscountScopeOption(CustomerDiscountScope Scope, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Termin in der Kundenkarte.</summary>
public sealed record CustomerVisit(string Date, string Time, string Treatment, string Status, bool IsUpcoming, bool IsProblem, string Price);

/// <summary>
/// Kundenstamm an der Rezeption: suchen (Name, Telefon, E-Mail), Kundenkarte mit Kontakt, Notiz und allen Terminen,
/// neue Kundin oder neuen Kunden anlegen. Der Kundenstamm ist standortübergreifend, Termine zeigt der Server je Zugang.
/// </summary>
public partial class CustomersViewModel(IWorkstationApi api, TimeProvider time) : ViewModelBase
{
    private int _searchVersion;
    private int _detailVersion;
    private Dictionary<Guid, string>? _currencies;

    public ObservableCollection<CustomerDto> Results { get; } = [];

    public ObservableCollection<CustomerVisit> Visits { get; } = [];

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(SelectedName), nameof(SelectedInitials), nameof(SelectedBirthday), nameof(SelectedLanguage), nameof(BillingText), nameof(DiscountText), nameof(BookingText), nameof(CreatedFromText))]
    [NotifyPropertyChangedFor(nameof(SelectedSymbol), nameof(SelectedSymbolIcon), nameof(SelectedPronouns), nameof(SelectedAddressLine), nameof(SelectedPostalAddress), nameof(SelectedGreeting))]
    [NotifyPropertyChangedFor(nameof(IsAnonymized), nameof(AnonymizedNotice), nameof(CanBook), nameof(CanCheckout), nameof(MayAnonymize))]
    public partial CustomerDto? Selected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingAny))]
    public partial bool ShowNew { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand), nameof(CreateAndBookCommand))]
    public partial string NewFirstName { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand), nameof(CreateAndBookCommand))]
    public partial string NewLastName { get; set; } = "";

    [ObservableProperty]
    public partial string NewPhone { get; set; } = "";

    [ObservableProperty]
    public partial string NewEmail { get; set; } = "";

    [ObservableProperty]
    public partial string NewNote { get; set; } = "";

    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Der Abschnitt „Verlauf“ der ausgewählten Kundin (Ä184); lädt erst beim Aufklappen.</summary>
    [ObservableProperty]
    public partial HistorySectionViewModel? HistorySection { get; private set; }

    public bool HasSelection => Selected is not null;

    /// <summary>Kopf der Kundenkarte: der Name im Haus (Rufname statt Vorname); der gültige Name mit Titeln steht klein darunter.</summary>
    public string SelectedName => Selected?.InHouseName ?? "";

    public string SelectedInitials => DayLayout.Initials(SelectedName);

    public string? SelectedBirthday => Selected?.BirthDate is { } b
        ? b.ToString("d. MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("de-AT"))
        : null;

    /// <summary>„Spricht Italienisch“; bei Deutsch nichts.</summary>
    public string? SelectedLanguage => GuestLanguages.Name(Selected?.Language) is { } name ? $"Spricht {name}" : null;

    private Task? _initialization;

    /// <summary>Beim ersten Öffnen: die ersten Einträge des Kundenstamms und die Länder der Standorte für die Anrede.</summary>
    public Task InitializeAsync() => _initialization ??= Task.WhenAll(SearchAsync(Query), LoadCountriesAsync());

    partial void OnQueryChanged(string value) => _ = SearchAsync(value);

    partial void OnSelectedChanged(CustomerDto? value)
    {
        CloseEditors();
        if (value is not null)
        {
            ShowNew = false;
        }

        Suggestion = null;
        ResetCard();
        HistorySection = value is null
            ? null
            : new HistorySectionViewModel(api, $"api/customers/{value.Id}/history", time.LocalTimeZone, "Wer die Kundendaten wann geändert hat",
                "Änderungen", "Die Änderungen wurden nicht geladen.");
        _ = LoadVisitsAsync(value);
        _ = LoadEmailsAsync(value);
    }

    /// <summary>„Schon vorhanden?“ beim Anlegen einer neuen Kundin: Treffer der Kundensuche zu Nachname, Telefon und Mailadresse.</summary>
    public NewCustomerMatchesViewModel Matches => _matches ??= new NewCustomerMatchesViewModel(api, TakeMatch);

    private NewCustomerMatchesViewModel? _matches;

    partial void OnNewLastNameChanged(string value) => CheckMatches();

    partial void OnNewPhoneChanged(string value) => CheckMatches();

    partial void OnNewEmailChanged(string value)
    {
        NewEmailError = null;
        CheckMatches();
    }

    private void CheckMatches() => _ = Matches.CheckAsync(NewLastName, NewPhone, NewEmail);

    /// <summary>Land des Spas für die Vorwahl („AT“); erst beim ersten Verlassen des Telefonfelds geladen, gemerkt wird nur ein Erfolg.</summary>
    private string? _phoneCountry;

    /// <summary>
    /// Beim Verlassen des Telefonfelds: die Nummer im Format „+43 664 1234567“ zeigen, wie die API sie speichert (T-019). Ohne Land des Spas
    /// (keine Verbindung) und bei unlesbaren Nummern bleibt der Text wie getippt; wer während des Ladens weitertippt, wird nicht überschrieben.
    /// </summary>
    public async Task FormatNewPhoneAsync()
    {
        var typed = NewPhone;
        if (string.IsNullOrWhiteSpace(typed))
        {
            return;
        }

        _phoneCountry ??= (await api.GetAsync<TenantDto>("api/tenant").ConfigureAwait(true)).Value?.Country;
        if (_phoneCountry is { } country && NewPhone == typed && PhoneNumbers.Format(typed, country) is { Length: > 0 } formatted)
        {
            NewPhone = formatted;
        }
    }

    /// <summary>Zwischenablage des Fensters; die Ansicht setzt sie beim Anhängen, Tests setzen ein Doppel.</summary>
    public IContactActions? ContactActions { get; set; }

    /// <summary>Kurze Bestätigung neben Telefon und E-Mail im Kopf der Karte (T-024): „Kopiert.“ oder „Nicht kopiert.“.</summary>
    public CopyFeedback Copy => _copy ??= new(time);

    private CopyFeedback? _copy;

    [RelayCommand]
    private Task CopyPhoneAsync() => Copy.CopyAsync(ContactActions, Selected?.Phone);

    [RelayCommand]
    private Task CopyEmailAsync() => Copy.CopyAsync(ContactActions, Selected?.Email);

    /// <summary>„Diese Kundin nehmen“: die vorhandene Karte öffnen, statt neu anzulegen.</summary>
    private Task TakeMatch(CustomerDto customer)
    {
        Error = null;
        ShowNew = false;
        ClearNew();
        Matches.Clear();
        if (Results.All(c => c.Id != customer.Id))
        {
            Results.Insert(0, customer);
        }

        Selected = customer;
        return Task.CompletedTask;
    }

    // ---------- Stammdaten: Name, Telefon, E-Mail, Geburtstag, Sprache und Notiz nachträglich ändern ----------

    /// <summary>Das Formular für die Stammdaten (dasselbe wie im Seitenpanel des Kalenders); <c>null</c>, solange nichts bearbeitet wird.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingContact))]
    [NotifyPropertyChangedFor(nameof(IsEditingAny))]
    public partial CustomerContactFormViewModel? Contact { get; private set; }

    public bool IsEditingContact => Contact is not null;

    /// <summary>Etwas ist im Bearbeiten-Zustand (Stammdaten, eine Karte oder „Neue Kundin“); dann ist „Neue Kundin“ oben kein Primärknopf.</summary>
    public bool IsEditingAny => ShowNew || IsEditingContact || IsAddingFlag || IsEditingWishes || IsEditingReceptionNote
        || IsEditingBooking || IsEditingBilling || IsEditingDiscount || IsAddingEmail;

    /// <summary>Zählt jedes Schließen mit; ein Öffner, der auf den Server wartet, öffnet danach nur, wenn inzwischen nichts anderes geöffnet wurde.</summary>
    private int _editorGeneration;

    /// <summary>Höchstens eine Karte im Bearbeiten-Zustand: Öffnen einer Karte verwirft die Eingabe der offenen wie „Abbrechen“.</summary>
    private void CloseEditors()
    {
        _editorGeneration++;
        Contact = null;
        IsAddingFlag = false;
        IsEditingWishes = false;
        IsEditingReceptionNote = false;
        IsEditingBooking = false;
        IsEditingBilling = false;
        IsEditingDiscount = false;
        IsAddingEmail = false;
    }

    /// <summary>Esc in einer Karte oder in „Neue Kundin“: schließt sie wie ihr „Abbrechen“; false, wenn keine offen war (Stammdaten zählen nicht).</summary>
    public bool CancelCard()
    {
        if (ShowNew)
        {
            CancelNew();
            return true;
        }

        if (!(IsAddingFlag || IsEditingWishes || IsEditingReceptionNote || IsEditingBooking || IsEditingBilling || IsEditingDiscount || IsAddingEmail))
        {
            return false;
        }

        IsAddingFlag = false;
        IsEditingWishes = false;
        IsEditingReceptionNote = false;
        IsEditingBooking = false;
        IsEditingBilling = false;
        IsEditingDiscount = false;
        IsAddingEmail = false;
        return true;
    }

    [RelayCommand]
    private void EditContact()
    {
        if (Selected is not { } customer)
        {
            return;
        }

        CloseEditors();
        Error = null;
        Contact = new CustomerContactFormViewModel(api, customer, _countries,
            updated =>
            {
                Replace(customer, updated);
                return Task.CompletedTask;
            },
            () => Contact = null);
    }

    // ---------- Rechnungsempfänger (z. B. Firma der Kundin), belegt die Rechnung an der Kassa vor ----------

    public string BillingText => Selected?.Billing is { } b
        ? string.Join('\n', new[] { b.Name, b.Address, b.VatId is { } vat ? $"UID {vat}" : null }.Where(l => l is not null))
        : "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingAny))]
    public partial bool IsEditingBilling { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveBillingCommand))]
    public partial string BillingName { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveBillingCommand))]
    public partial string BillingAddress { get; set; } = "";

    [ObservableProperty]
    public partial string BillingVatId { get; set; } = "";

    [RelayCommand]
    private void EditBilling()
    {
        CloseEditors();
        BillingName = Selected?.Billing?.Name ?? "";
        BillingAddress = Selected?.Billing?.Address ?? "";
        BillingVatId = Selected?.Billing?.VatId ?? "";
        IsEditingBilling = true;
    }

    [RelayCommand]
    private void CancelBilling() => IsEditingBilling = false;

    private bool CanSaveBilling => BillingName.Trim().Length > 0 && BillingAddress.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanSaveBilling))]
    private Task SaveBillingAsync() => StoreBillingAsync(new CustomerBilling(BillingName.Trim(), BillingAddress.Trim(),
        BillingVatId.Trim().Length == 0 ? null : BillingVatId.Trim()));

    /// <summary>„Entfernen“ fragt nach (Rückfragedialog, Design-Brief 5.27).</summary>
    [RelayCommand]
    private async Task AskRemoveBillingAsync()
    {
        var answer = await AskAsync(new ConfirmRequest("Rechnungsanschrift entfernen?",
            "Name, Anschrift und UID stehen danach nicht mehr auf Rechnungen dieses Gastes.", "Entfernen")).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await RemoveBillingAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task RemoveBillingAsync() => StoreBillingAsync(new CustomerBilling("", ""));

    private async Task StoreBillingAsync(CustomerBilling billing)
    {
        if (Selected is not { } customer)
        {
            return;
        }

        var result = await api.PutAsync<CustomerDto>($"api/customers/{customer.Id}/billing", billing).ConfigureAwait(true);
        if (result.Value is not { } updated)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        Replace(customer, updated);
    }

    // ---------- Kundenrabatt: die Kassa zieht ihn bei neuen Positionen selbst ab und weist ihn auf dem Beleg aus ----------

    public static IReadOnlyList<DiscountScopeOption> DiscountScopes { get; } =
    [
        new(CustomerDiscountScope.All, "Auf alles außer Gutscheine"),
        new(CustomerDiscountScope.Treatments, "Nur auf Behandlungen"),
        new(CustomerDiscountScope.Products, "Nur auf Produkte"),
    ];

    public string DiscountText => Selected?.Discount is { } d
        ? $"{d.Percent.ToString("0.##", German)} % {d.Scope switch
        {
            CustomerDiscountScope.Treatments => "nur auf Behandlungen",
            CustomerDiscountScope.Products => "nur auf Produkte",
            _ => "auf alles außer Gutscheine",
        }}. Die Kassa zieht ihn selbst ab und weist ihn auf dem Beleg aus."
        : "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingAny))]
    public partial bool IsEditingDiscount { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDiscountCommand))]
    [NotifyPropertyChangedFor(nameof(NeedsApproval))]
    public partial string DiscountPercent { get; set; } = "";

    private PosSettingsDto? _settings;

    /// <summary>Über der Rabattgrenze gibt Inhaber oder Standortleitung am selben Platz mit der eigenen PIN frei.</summary>
    public bool NeedsApproval => _settings is { MayExceedLimit: false } s && ParsedPercent is { } p && p > s.DiscountLimitPercent;

    public ObservableCollection<QuickSwitchPerson> Approvers { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDiscountCommand))]
    public partial QuickSwitchPerson? Approver { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDiscountCommand))]
    public partial string ApprovalPin { get; set; } = "";

    [ObservableProperty]
    public partial DiscountScopeOption DiscountScope { get; set; } = DiscountScopes[0];

    /// <summary>Rabattgrenze des Spas: bis dahin darf die Rezeption, darüber nur die Standortleitung.</summary>
    [ObservableProperty]
    public partial string DiscountLimitHint { get; private set; } = "";

    [RelayCommand]
    private async Task EditDiscountAsync()
    {
        CloseEditors();
        var generation = _editorGeneration;
        _settings = (await api.GetAsync<PosSettingsDto>("api/pos/settings").ConfigureAwait(true)).Value;
        if (_settings is { MayExceedLimit: false } settings)
        {
            DiscountLimitHint = $"Bis {settings.DiscountLimitPercent.ToString("0.##", German)} % möglich, darüber gibt die Standortleitung mit ihrer PIN frei.";
            Approvers.Clear();
            foreach (var person in (await api.GetAsync<List<QuickSwitchPerson>>("api/pos/approvers").ConfigureAwait(true)).Value ?? [])
            {
                Approvers.Add(person);
            }

            Approver = Approvers.FirstOrDefault();
        }

        if (generation != _editorGeneration)
        {
            return;
        }

        ApprovalPin = "";

        DiscountPercent = Selected?.Discount?.Percent.ToString("0.##", German) ?? "";
        DiscountScope = DiscountScopes.First(o => o.Scope == (Selected?.Discount?.Scope ?? CustomerDiscountScope.All));
        IsEditingDiscount = true;
    }

    [RelayCommand]
    private void CancelDiscount() => IsEditingDiscount = false;

    private decimal? ParsedPercent =>
        decimal.TryParse(DiscountPercent.Replace("%", "", StringComparison.Ordinal).Trim(), System.Globalization.NumberStyles.Number, German, out var p)
        && p > 0 && p < 100 ? p : null;

    private bool CanSaveDiscount => ParsedPercent is not null && (!NeedsApproval || (Approver is not null && ApprovalPin.Trim().Length >= 4));

    [RelayCommand(CanExecute = nameof(CanSaveDiscount))]
    private Task SaveDiscountAsync() => StoreDiscountAsync(new CustomerDiscountInput(ParsedPercent!.Value, DiscountScope.Scope,
        NeedsApproval ? new SupervisorApproval(Approver!.UserId, ApprovalPin.Trim()) : null));

    /// <summary>„Entfernen“ fragt nach (Rückfragedialog, Design-Brief 5.27).</summary>
    [RelayCommand]
    private async Task AskRemoveDiscountAsync()
    {
        var answer = await AskAsync(new ConfirmRequest("Stammrabatt entfernen?", "Dieser Gast bekommt danach keinen Rabatt mehr auf seine Rechnungen.", "Entfernen")).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await RemoveDiscountAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task RemoveDiscountAsync() => StoreDiscountAsync(new CustomerDiscountInput(0));

    private async Task StoreDiscountAsync(CustomerDiscountInput discount)
    {
        if (Selected is not { } customer)
        {
            return;
        }

        var result = await api.PutAsync<CustomerDto>($"api/customers/{customer.Id}/discount", discount).ConfigureAwait(true);
        if (result.Value is not { } updated)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        ApprovalPin = "";
        Replace(customer, updated);
    }

    private void Replace(CustomerDto customer, CustomerDto updated)
    {
        var index = Results.IndexOf(customer);
        if (index >= 0)
        {
            Results[index] = updated;
        }

        Selected = updated;
    }

    private static readonly System.Globalization.CultureInfo German = System.Globalization.CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Nach einem Verbindungsausfall die Trefferliste neu laden; eine offene Kundenkarte bleibt offen (die Liste wird dann nicht angefasst).</summary>
    public Task ReloadListAsync() => _initialization is null || Selected is not null ? Task.CompletedTask : SearchAsync(Query);

    /// <summary>Eine ältere, langsamere Antwort überschreibt keine neuere.</summary>
    public async Task SearchAsync(string query)
    {
        var version = ++_searchVersion;
        var result = await api.GetAsync<List<CustomerDto>>($"api/customers?search={Uri.EscapeDataString(query.Trim())}").ConfigureAwait(true);
        if (version != _searchVersion)
        {
            return;
        }

        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        Error = null;
        Results.Clear();
        foreach (var customer in result.Value)
        {
            Results.Add(customer);
        }

        RefreshCreateOffer();
    }

    /// <summary>Öffnet eine bestimmte Kundenkarte, z. B. aus dem Kalender.</summary>
    public async Task ShowAsync(Guid customerId)
    {
        var result = await api.GetAsync<CustomerDto>($"api/customers/{customerId}").ConfigureAwait(true);
        if (result.Value is { } customer)
        {
            if (Results.All(c => c.Id != customer.Id))
            {
                Results.Insert(0, customer);
            }

            Selected = Results.First(c => c.Id == customer.Id);
        }
    }

    public async Task LoadVisitsAsync(CustomerDto? customer)
    {
        var version = ++_detailVersion;
        Visits.Clear();
        UpcomingEntries.Clear();
        RecentEntries.Clear();
        NextAppointmentText = "";
        CheckoutAppointment = null;
        if (customer is null)
        {
            return;
        }

        var suggestion = LoadSuggestionAsync(customer, version);
        var insights = LoadInsightsAsync(customer, version);
        var flags = LoadFlagsAsync(customer, version);
        var employees = LoadEmployeesAsync();
        var result = await api.GetAsync<List<AppointmentDto>>($"api/customers/{customer.Id}/appointments").ConfigureAwait(true);
        await suggestion.ConfigureAwait(true);
        await insights.ConfigureAwait(true);
        await flags.ConfigureAwait(true);
        await employees.ConfigureAwait(true);
        if (version != _detailVersion || result.Value is null)
        {
            return;
        }

        // Preise in der Währung des jeweiligen Standorts (Schweiz: CHF).
        _currencies ??= (await api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true)).Value?.ToDictionary(l => l.Id, l => l.Currency);
        if (version != _detailVersion)
        {
            return;
        }

        var now = time.GetUtcNow();
        var zone = time.LocalTimeZone;
        var appointments = result.Value;
        foreach (var a in appointments)
        {
            var local = DayLayout.Local(a.Start, zone);
            Visits.Add(new CustomerVisit(
                local.ToString("ddd d.M.yyyy", System.Globalization.CultureInfo.GetCultureInfo("de-AT")),
                DayLayout.TimeText(a.Start, a.End, zone),
                a.TreatmentName,
                StatusText(a.Status),
                a.Start > now && a.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn,
                a.Status is AppointmentStatus.NoShow or AppointmentStatus.Cancelled,
                Money.Format(a.Price, _currencies?.GetValueOrDefault(a.LocationId))));
        }

        ShowTimeline(appointments, now, zone);
    }

    [RelayCommand]
    private void StartNew()
    {
        Selected = null;
        ShowNew = true;
        NewMoreOpen = false;
        // Suchbegriff als Vorschlag für den Namen übernehmen: „Anna Kern“ → Vorname Anna, Nachname Kern; ein einzelnes Wort ist der Nachname.
        var parts = Query.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && parts.All(p => p.All(ch => char.IsLetter(ch) || ch == '-')))
        {
            (NewFirstName, NewLastName) = parts.Length > 1 ? (parts[0], parts[1]) : ("", parts[0]);
        }
    }

    [RelayCommand]
    private void CancelNew()
    {
        ShowNew = false;
        ClearNew();
        Matches.Clear();
    }

    /// <summary>Hinweis nach einer ohne Verbindung vorgemerkten Anlage.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    /// <summary>Pflicht ist nur ein Name (Vor- oder Nachname); Telefon, E-Mail und Notiz lassen sich in der Kundenkarte mit „Bearbeiten“ ergänzen.</summary>
    private bool CanCreate => NewFirstName.Trim().Length > 0 || NewLastName.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private Task CreateAsync() => CreateCustomerAsync(thenBook: false);

    /// <summary>„Anlegen und Termin buchen“: legt an und öffnet die Buchung mit dem Gast.</summary>
    [RelayCommand(CanExecute = nameof(CanCreate))]
    private Task CreateAndBookAsync() => CreateCustomerAsync(thenBook: true);

    private async Task CreateCustomerAsync(bool thenBook)
    {
        Error = null;
        if (!ValidateNewEmail(focusOnError: true))
        {
            return;
        }

        static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var result = await api.CreateCustomerAsync(new CustomerInput(NewFirstName.Trim(), NewLastName.Trim(),
            Blank(NewEmail), Blank(NewPhone), null, Blank(NewNote), Salutation: NewSalutation.Value, PreferredName: Blank(NewPreferredName),
            TitlesBefore: Blank(NewTitlesBefore))).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        Notice = result.Queued
            ? $"Ohne Verbindung vorgemerkt: {result.Value.InHouseName} wird beim Abgleich angelegt und ist schon jetzt buchbar."
            : null;

        ShowNew = false;
        ClearNew();
        Matches.Clear();
        Results.Insert(0, result.Value);
        Selected = result.Value;
        if (thenBook)
        {
            BookRequested?.Invoke(result.Value);
        }
    }

    private static string StatusText(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Booked => "Gebucht",
        AppointmentStatus.CheckedIn => "Eingecheckt",
        AppointmentStatus.Completed => "Erledigt",
        AppointmentStatus.NoShow => "Nicht erschienen",
        _ => "Storniert",
    };
}
