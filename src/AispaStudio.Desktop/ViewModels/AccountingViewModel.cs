using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Buchung der Vorschau, fertig für die Tabelle.</summary>
public sealed record PostingRow(string Date, string Document, string DebitAccount, string CreditAccount, string Amount, string Tax, string CostCenter, string Text)
{
    /// <summary>Was der Screenreader für die Zeile liest.</summary>
    public string SpokenName => $"{Date}, Beleg {Document}, Soll {DebitAccount}, Haben {CreditAccount}, {Amount}, {Tax}, {CostCenter}, {Text}";
}

/// <summary>Das Land, für das die Kontenzuordnung gilt; <c>null</c> ist der Standard für alle Kassen.</summary>
public sealed record AccountingCountryChoice(FiscalCountry? Value, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Ein Konto der Kontenzuordnung mit seiner Beschriftung.</summary>
public sealed partial class AccountMappingField(string label, Func<AccountingSettingsDto, string> read, Func<AccountingSettingsDto, string, AccountingSettingsDto> write) : ObservableObject
{
    public string Label { get; } = label;

    [ObservableProperty]
    public partial string Value { get; set; } = "";

    public void Load(AccountingSettingsDto settings) => Value = read(settings);

    public AccountingSettingsDto Store(AccountingSettingsDto settings) => write(settings, Value.Trim());
}

/// <summary>
/// Buchhaltung (R05 Slice 102), native Maske nach <c>AccountingPage.tsx</c>: Export der Kassenbuchungen für DATEV, BMD, RZL und die Schweiz, Erlöskonten und Kontenzuordnung. Die Datei kommt
/// unverändert von der API und wird Byte für Byte gespeichert; Buchungen und Dateiformat rechnet nur der Server. Vor dem DATEV-Export prüft die Maske wie das Web, ob jeder Steuersatz einen
/// BU-Schlüssel hat. Erlöskonten legt nur an, wer den Katalog pflegen darf (ManageCatalog), die Kontenzuordnung ändert nur, wer die Organisation verwaltet (ManageOrganization).
/// </summary>
public sealed partial class AccountingViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time) : ViewModelBase, INativeArea, IFocusTarget, ISavesFiles
{
    public const string FromField = "AccountingFromField";

    public const string UntilField = "AccountingUntilField";

    public const string AccountCodeField = "RevenueAccountCodeField";

    public const string AccountNameField = "RevenueAccountNameField";

    public const string AccountLengthField = "AccountLengthField";

    public const string FiscalYearField = "FiscalYearStartField";

    private const string SettingsPath = "api/accounting/settings";

    private static readonly NumberFormatInfo German = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    private static readonly string[] CsvPatterns = ["*.csv"];

    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private AccountingSettingsDto? _settings;
    private string? _savedSignature;

    private int _countryVersion;
    private Task? _loading;
    private bool _applying;
    private bool _reverting;

    public string Title => "Buchhaltung";

    public IFileSaver? Files { get; set; }

    public bool CanAddAccount => user.Rights.Contains(AccessRight.ManageCatalog);

    public bool CanEditSettings => user.Rights.Contains(AccessRight.ManageOrganization);

    public bool ShowReadOnlyHint => !CanEditSettings;

    public ObservableCollection<PostingRow> Postings { get; } = [];

    public ObservableCollection<RevenueAccountDto> Accounts { get; } = [];

    public IReadOnlyList<AccountingCountryChoice> Countries { get; } =
    [
        new(null, "Standard (alle Länder)"),
        new(FiscalCountry.AT, "Österreich"),
        new(FiscalCountry.DE, "Deutschland"),
        new(FiscalCountry.CH, "Schweiz"),
    ];

    public IReadOnlyList<AccountMappingField> MappingFields { get; } =
    [
        new("Verrechnungskonto Kassa", s => s.TransitAccount, (s, v) => s with { TransitAccount = v }),
        new("Kassa (Bar)", s => s.CashAccount, (s, v) => s with { CashAccount = v }),
        new("Karte (Verrechnung Terminal)", s => s.CardAccount, (s, v) => s with { CardAccount = v }),
        new("Bank (Sofortüberweisung)", s => s.InstantTransferAccount, (s, v) => s with { InstantTransferAccount = v }),
        new("Gutscheine (Verbindlichkeit)", s => s.VoucherAccount, (s, v) => s with { VoucherAccount = v }),
        new("Geldtransit (Einlage/Entnahme)", s => s.CashTransferAccount, (s, v) => s with { CashTransferAccount = v }),
        new("Digitaler Euro (Verrechnung)", s => s.DigitalEuroAccount, (s, v) => s with { DigitalEuroAccount = v }),
        new("Treatwell (Forderung, vorab bezahlt)", s => s.TreatwellAccount, (s, v) => s with { TreatwellAccount = v }),
        new("Kassenfehlbetrag (Tagesabschluss)", s => s.CashShortageAccount, (s, v) => s with { CashShortageAccount = v }),
        new("Kassenüberschuss (Tagesabschluss)", s => s.CashSurplusAccount, (s, v) => s with { CashSurplusAccount = v }),
    ];

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    // ---------- Export ----------

    [ObservableProperty]
    public partial DateOnly? From { get; set; } = new DateOnly(time.GetLocalNow().Year, time.GetLocalNow().Month, 1);

    [ObservableProperty]
    public partial DateOnly? Until { get; set; } = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    [ObservableProperty]
    public partial string? FromError { get; private set; }

    [ObservableProperty]
    public partial string? UntilError { get; private set; }

    /// <summary>Die Vorschau steht erst nach „Buchungen anzeigen“ oder der Prüfung vor dem DATEV-Export.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoPostings))]
    public partial bool HasPreview { get; private set; }

    public bool ShowNoPostings => HasPreview && Postings.Count == 0;

    public bool ShowPostings => HasPreview && Postings.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDatevHint))]
    public partial string? DatevHint { get; private set; }

    public bool HasDatevHint => DatevHint is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportError))]
    public partial string? ExportError { get; private set; }

    public bool HasExportError => ExportError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportMessage))]
    public partial string? ExportMessage { get; private set; }

    public bool HasExportMessage => ExportMessage is not null;

    // ---------- Erlöskonten ----------

    [ObservableProperty]
    public partial string AccountCode { get; set; } = "";

    [ObservableProperty]
    public partial string AccountName { get; set; } = "";

    [ObservableProperty]
    public partial string AccountCostCenter { get; set; } = "";

    [ObservableProperty]
    public partial string? AccountCodeError { get; private set; }

    [ObservableProperty]
    public partial string? AccountNameError { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccountError))]
    public partial string? AccountError { get; private set; }

    public bool HasAccountError => AccountError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccountMessage))]
    public partial string? AccountMessage { get; private set; }

    public bool HasAccountMessage => AccountMessage is not null;

    // ---------- Kontenzuordnung ----------

    [ObservableProperty]
    public partial AccountingCountryChoice? SelectedCountry { get; set; }

    public string CountryHint => SelectedCountry?.Value is null
        ? "Der Standard gilt für alle Kassen, deren Land keine eigene Zuordnung hat."
        : _settings?.Inherited == true
            ? "Dieses Land hat noch keine eigene Zuordnung, gezeigt wird der Standard. Speichern legt eine eigene an, z. B. für den Schweizer Kontenrahmen."
            : "Eigene Zuordnung für die Kassen dieses Landes.";

    public bool CanResetCountry => CanEditSettings && SelectedCountry?.Value is not null && _settings is { Inherited: false };

    [ObservableProperty]
    public partial string ConsultantNumber { get; set; } = "";

    [ObservableProperty]
    public partial string ClientNumber { get; set; } = "";

    [ObservableProperty]
    public partial decimal? AccountLength { get; set; }

    [ObservableProperty]
    public partial decimal? FiscalYearStart { get; set; }

    [ObservableProperty]
    public partial string TaxKeys { get; set; } = "";

    [ObservableProperty]
    public partial string SwissCodes { get; set; } = "";

    [ObservableProperty]
    public partial string RevenueCodes { get; set; } = "";

    [ObservableProperty]
    public partial string? AccountLengthError { get; private set; }

    [ObservableProperty]
    public partial string? FiscalYearError { get; private set; }

    /// <summary>Das Feld, das nach einem Fehler den Fokus bekommen soll; die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    /// <summary>„Gespeichert.“ in der Leiste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    public bool HasUnsavedChanges => _savedSignature is not null && CanEditSettings && Signature() != _savedSignature;

    private string CountryQuery => SelectedCountry?.Value is { } country ? $"?country={country}" : "";

    // ---------- Laden ----------

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : _loading ??= LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            LoadError = null;
            var accounts = api.GetAsync<List<RevenueAccountDto>>("api/pos/revenue-accounts");
            var settings = api.GetAsync<AccountingSettingsDto>(SettingsPath);
            await Task.WhenAll(accounts, settings).ConfigureAwait(true);
            if (accounts.Result.Value is not { } list || settings.Result.Value is not { } loaded)
            {
                LoadError = accounts.Result.Error ?? settings.Result.Error ?? "Die Buchhaltung konnte nicht geladen werden.";
                return;
            }

            Accounts.Clear();
            foreach (var account in list)
            {
                Accounts.Add(account);
            }

            _reverting = true;
            SelectedCountry = Countries[0];
            _reverting = false;
            Apply(loaded);
            foreach (var field in MappingFields)
            {
                field.PropertyChanged += OnMappingChanged;
            }

            IsLoaded = true;
        }
        finally
        {
            _loading = null;
        }
    }

    public void Open(Guid? id, string? section, string? field)
    {
    }

    private void OnMappingChanged(object? sender, PropertyChangedEventArgs e) => Touch();

    private void Apply(AccountingSettingsDto settings)
    {
        _applying = true;
        _settings = settings;
        foreach (var field in MappingFields)
        {
            field.Load(settings);
        }

        ConsultantNumber = settings.DatevConsultantNumber ?? "";
        ClientNumber = settings.DatevClientNumber ?? "";
        AccountLength = settings.AccountLength;
        FiscalYearStart = settings.FiscalYearStartMonth;
        TaxKeys = settings.DatevTaxKeys;
        SwissCodes = settings.SwissTaxCodes;
        RevenueCodes = settings.RevenueAccountCodes;
        _applying = false;
        _savedSignature = Signature();
        AccountLengthError = FiscalYearError = null;
        OnPropertyChanged(nameof(CountryHint));
        OnPropertyChanged(nameof(CanResetCountry));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private string Signature() => string.Join('\u001f', MappingFields.Select(f => f.Value).Concat([ConsultantNumber, ClientNumber, $"{AccountLength}", $"{FiscalYearStart}", TaxKeys, SwissCodes, RevenueCodes]));

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(From):
                FromError = UntilError = null;
                break;
            case nameof(Until):
                UntilError = null;
                break;
            case nameof(AccountCode):
                AccountCodeError = null;
                break;
            case nameof(AccountName):
                AccountNameError = null;
                break;
            case nameof(AccountLength):
                AccountLengthError = null;
                Touch();
                break;
            case nameof(FiscalYearStart):
                FiscalYearError = null;
                Touch();
                break;
            case nameof(ConsultantNumber) or nameof(ClientNumber) or nameof(TaxKeys) or nameof(SwissCodes) or nameof(RevenueCodes):
                Touch();
                break;
        }
    }

    private void Touch()
    {
        if (_applying)
        {
            return;
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    // ---------- Export ----------

    private bool ValidateRange()
    {
        FromError = From is null ? "Trage den Beginn ein." : null;
        UntilError = Until is null ? "Trage das Ende ein." : From is { } from && Until < from ? "Das Ende darf nicht vor dem Beginn liegen." : null;
        FocusTarget = FromError is not null ? FromField : UntilError is not null ? UntilField : null;
        return FromError is null && UntilError is null;
    }

    private string RangeQuery => $"from={From:yyyy-MM-dd}&until={Until:yyyy-MM-dd}";

    /// <summary>Holt die Buchungen des Zeitraums in die Vorschau; null, wenn es nicht ging (dann steht der Fehler über der Tabelle).</summary>
    private async Task<List<AccountingPosting>?> PreviewAsync()
    {
        var result = await api.GetAsync<List<AccountingPosting>>($"api/accounting/postings?{RangeQuery}").ConfigureAwait(true);
        if (result.Value is not { } postings)
        {
            HasPreview = false;
            Postings.Clear();
            OnPropertyChanged(nameof(ShowPostings));
            ExportError = result.Error ?? "Die Buchungen ließen sich nicht laden. Prüfen Sie die Verbindung und versuchen Sie es erneut.";
            return null;
        }

        Postings.Clear();
        foreach (var posting in postings)
        {
            Postings.Add(RowOf(posting));
        }

        HasPreview = true;
        OnPropertyChanged(nameof(ShowNoPostings));
        OnPropertyChanged(nameof(ShowPostings));
        DatevHint = MissingDatevKeys(postings);
        return postings;
    }

    private static PostingRow RowOf(AccountingPosting posting) => new(StockFormat.Date(posting.Date), posting.Document, posting.DebitAccount, posting.CreditAccount,
        Money.Format(posting.Amount, posting.Currency), posting.TaxPercent == 0 ? "" : $"{posting.TaxPercent.ToString("0.##", German)} %", posting.CostCenter ?? "", posting.Text);

    /// <summary>
    /// Steuersätze der Euro-Kassen, für die das Land der Kasse keinen DATEV-BU-Schlüssel kennt. Der Server verweigert dann den DATEV-Export; hier steht vorher, was fehlt und wo man es einträgt.
    /// </summary>
    public static string? MissingDatevKeys(IEnumerable<AccountingPosting> postings)
    {
        var missing = postings
            .Where(p => p is { Currency: "EUR", TaxKey: null } && p.TaxPercent != 0)
            .GroupBy(p => p.Country)
            .Select(g =>
            {
                var rates = string.Join(", ", g.Select(p => p.TaxPercent).Distinct().Order().Select(r => $"{r.ToString("0.##", German)} %"));
                return g.Key is { } country ? $"{rates} (Kassen in {CountryName(country)})" : rates;
            })
            .ToList();
        return missing.Count == 0
            ? null
            : $"Für {string.Join(" und ", missing)} fehlt der DATEV-BU-Schlüssel. Unter „Kontenzuordnung“ das Land wählen und die Schlüssel der Steuerberatung eintragen, z. B. „20=…“, oder „20=“ ohne Schlüssel für ein Automatikkonto. Für Österreich passen meist BMD oder RZL.";
    }

    private static string CountryName(FiscalCountry country) => country switch
    {
        FiscalCountry.AT => "Österreich",
        FiscalCountry.DE => "Deutschland",
        _ => "Schweiz",
    };

    [RelayCommand]
    private async Task ShowPostingsAsync()
    {
        ExportError = ExportMessage = null;
        if (ValidateRange())
        {
            await PreviewAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Vor dem DATEV-Export die Buchungen prüfen: fehlt ein BU-Schlüssel, kommt der Hinweis statt der Datei.</summary>
    [RelayCommand]
    private async Task ExportDatevAsync()
    {
        ExportError = ExportMessage = null;
        if (!ValidateRange() || await PreviewAsync().ConfigureAwait(true) is not { } postings)
        {
            return;
        }

        if (MissingDatevKeys(postings) is { } missing)
        {
            ExportError = missing;
            return;
        }

        await SaveExportAsync(AccountingFormat.Datev, "datev-buchungsstapel.csv").ConfigureAwait(true);
    }

    [RelayCommand]
    private Task ExportBmdAsync() => ExportAsync(AccountingFormat.Bmd, "bmd-import.csv");

    [RelayCommand]
    private Task ExportRzlAsync() => ExportAsync(AccountingFormat.Rzl, "rzl-import.csv");

    [RelayCommand]
    private Task ExportSwissAsync() => ExportAsync(AccountingFormat.Swiss, "schweiz-import.csv");

    private async Task ExportAsync(AccountingFormat format, string fileName)
    {
        ExportError = ExportMessage = null;
        if (ValidateRange())
        {
            await SaveExportAsync(format, fileName).ConfigureAwait(true);
        }
    }

    /// <summary>Holt die Datei und schreibt ihre Bytes unverändert; das Format (Zeichensatz, Zeilenenden) legt allein der Server fest.</summary>
    private async Task SaveExportAsync(AccountingFormat format, string fileName)
    {
        var result = await api.GetBytesAsync($"api/accounting/export?format={format}&{RangeQuery}").ConfigureAwait(true);
        if (result.Value is not { } content)
        {
            ExportError = result.Error ?? "Der Export konnte nicht erstellt werden.";
            return;
        }

        try
        {
            if (Files is not null && await Files.SaveAsync(fileName, "CSV-Datei", CsvPatterns, content).ConfigureAwait(true))
            {
                ExportMessage = $"{fileName} gespeichert.";
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ExportError = "Die Datei konnte nicht gespeichert werden. Wählen Sie einen anderen Ort.";
        }
    }

    // ---------- Erlöskonten ----------

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        AccountError = AccountMessage = null;
        AccountCodeError = AccountCode.Trim().Length == 0 ? "Trage die Kontonummer ein." : null;
        AccountNameError = AccountName.Trim().Length == 0 ? "Trage die Bezeichnung ein." : null;
        FocusTarget = AccountCodeError is not null ? AccountCodeField : AccountNameError is not null ? AccountNameField : null;
        if (FocusTarget is not null)
        {
            return;
        }

        var body = new RevenueAccountInput(AccountCode.Trim(), AccountName.Trim(), AccountCostCenter.Trim() is { Length: > 0 } center ? center : null);
        var result = await api.PostAsync<RevenueAccountDto>("api/pos/revenue-accounts", body).ConfigureAwait(true);
        if (result.Value is not { } created)
        {
            AccountError = result.Error ?? "Das Erlöskonto wurde nicht angelegt.";
            return;
        }

        Accounts.Add(created);
        AccountCode = AccountName = AccountCostCenter = "";
        AccountMessage = $"Erlöskonto {created.Code} angelegt.";
    }

    // ---------- Kontenzuordnung ----------

    partial void OnSelectedCountryChanged(AccountingCountryChoice? oldValue, AccountingCountryChoice? newValue)
    {
        if (_reverting || newValue is null)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Auswahl bleibt beim alten Land, bis die Antwort da ist.
            Revert(oldValue);
            _ = LeaveToCountryAsync(newValue);
            return;
        }

        _ = LoadCountryAsync();
    }

    private async Task LeaveToCountryAsync(AccountingCountryChoice target)
    {
        if (await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            SelectedCountry = target;
        }
    }

    private void Revert(AccountingCountryChoice? old)
    {
        void Restore()
        {
            _reverting = true;
            SelectedCountry = old;
            _reverting = false;
        }

        if (_ui is null)
        {
            Restore();
        }
        else
        {
            _ui.Post(_ => Restore(), null);
        }
    }

    private async Task LoadCountryAsync()
    {
        // Bis die Zuordnung des gewählten Landes da ist, darf nichts gespeichert werden: Die Felder zeigen noch das vorige Land.
        _settings = null;
        _savedSignature = null;
        var version = ++_countryVersion;
        var result = await api.GetAsync<AccountingSettingsDto>(SettingsPath + CountryQuery).ConfigureAwait(true);
        if (version != _countryVersion)
        {
            return;
        }

        if (result.Value is { } settings)
        {
            Error = null;
            StatusText = null;
            Apply(settings);
        }
        else
        {
            Error = result.Error ?? "Die Kontenzuordnung wurde nicht geladen.";
        }
    }

    private bool Validate()
    {
        AccountLengthError = AccountLength is >= 4 and <= 8 ? null : "Die Sachkontenlänge liegt zwischen 4 und 8.";
        FiscalYearError = FiscalYearStart is >= 1 and <= 12 ? null : "Der Monat liegt zwischen 1 und 12.";
        FocusTarget = AccountLengthError is not null ? AccountLengthField : FiscalYearError is not null ? FiscalYearField : null;
        return FocusTarget is null;
    }

    private bool CanSave => !IsSaving && CanEditSettings;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || !CanEditSettings || _settings is not { } current || !Validate())
        {
            return false;
        }

        Error = null;
        IsSaving = true;
        var body = MappingFields.Aggregate(current, (settings, field) => field.Store(settings)) with
        {
            DatevConsultantNumber = ConsultantNumber.Trim() is { Length: > 0 } consultant ? consultant : null,
            DatevClientNumber = ClientNumber.Trim() is { Length: > 0 } client ? client : null,
            AccountLength = (int)AccountLength!.Value,
            FiscalYearStartMonth = (int)FiscalYearStart!.Value,
            DatevTaxKeys = TaxKeys.Trim(),
            SwissTaxCodes = SwissCodes.Trim(),
            RevenueAccountCodes = RevenueCodes.Trim(),
        };
        var result = await api.PutAsync<AccountingSettingsDto>(SettingsPath + CountryQuery, body).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } saved)
        {
            Error = result.Error ?? "Die Kontenzuordnung wurde nicht gespeichert.";
            return false;
        }

        Apply(saved);
        StatusText = "Gespeichert.";
        return true;
    }

    [RelayCommand]
    public void Discard()
    {
        if (_settings is { } settings)
        {
            Apply(settings);
        }

        Error = null;
    }

    /// <summary>Die eigene Zuordnung des Landes geht verloren; deshalb mit Rückfrage.</summary>
    [RelayCommand]
    private async Task ResetCountryAsync()
    {
        if (SelectedCountry is not { Value: { } country } choice)
        {
            return;
        }

        var answer = await AskAsync(new ConfirmRequest("Auf Standard zurücksetzen?", $"Die eigene Zuordnung für {choice.Name} geht verloren. Danach gilt wieder der Standard.", "Zurücksetzen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await api.DeleteAsync<object>($"{SettingsPath}?country={country}").ConfigureAwait(true);
        if (result.Error is { } problem)
        {
            Error = problem;
            return;
        }

        await LoadCountryAsync().ConfigureAwait(true);
        StatusText = "Eigene Zuordnung entfernt, es gilt wieder der Standard.";
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    [RelayCommand]
    private void CloseExportError() => ExportError = null;

    [RelayCommand]
    private void CloseExportMessage() => ExportMessage = null;

    [RelayCommand]
    private void CloseAccountError() => AccountError = null;

    [RelayCommand]
    private void CloseAccountMessage() => AccountMessage = null;
}
