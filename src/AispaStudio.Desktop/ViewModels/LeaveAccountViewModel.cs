using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Urlaubskonto einer Person auf ihrer Karte (ADR 0097): Anspruch und Übertrag eines Kalenderjahres trägt die Leitung ein,
/// genommene und geplante Tage zählt der Server aus dem veröffentlichten Dienstplan. Ohne Anspruch nennt das Konto keinen Rest.
/// </summary>
public sealed partial class LeaveAccountViewModel : ViewModelBase, IEditableArea
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");
    private readonly TeamContext _context;
    private readonly Guid _employeeId;
    private LeaveAccountDto? _account;
    private bool _loaded;
    private int _version;

    public LeaveAccountViewModel(TeamContext context, Guid employeeId)
    {
        _context = context;
        _employeeId = employeeId;
        Year = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(context.Time.GetUtcNow(), context.Zone).DateTime).Year;
    }

    public string Title => "Urlaubskonto";

    /// <summary>Anspruch und Übertrag ändert, wer Standorte verwaltet; wer nur Personalunterlagen liest, sieht das Konto.</summary>
    public bool IsEditable => _context.CanPlan;

    public bool IsReadOnly => !IsEditable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Heading))]
    public partial int Year { get; private set; }

    public string Heading => $"Urlaubskonto {Year}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string EntitlementText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string CarriedOverText { get; set; } = "";

    [ObservableProperty]
    public partial string TakenText { get; private set; } = "";

    [ObservableProperty]
    public partial string PlannedText { get; private set; } = "";

    [ObservableProperty]
    public partial string RemainingText { get; private set; } = "";

    /// <summary>Der Rest ist negativ: mehr Urlaub eingetragen, als Anspruch und Übertrag hergeben.</summary>
    [ObservableProperty]
    public partial bool IsOverdrawn { get; private set; }

    /// <summary>Wie gezählt wird, und was nicht zählt.</summary>
    [ObservableProperty]
    public partial string Hint { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    public bool HasUnsavedChanges => IsEditable && _account is { } account
        && (EntitlementText.Trim() != Format(account.Entitlement) || CarriedOverText.Trim() != FormatCarried(account.CarriedOver));

    public Task EnsureLoadedAsync() => _loaded ? Task.CompletedTask : LoadAsync();

    /// <summary>Nach einer Änderung im Dienstplan: Zahlen neu holen, eine angefangene Eingabe bleibt stehen.</summary>
    public Task RefreshAsync() => _loaded ? LoadAsync(keepInput: HasUnsavedChanges) : Task.CompletedTask;

    [RelayCommand]
    private Task PreviousYearAsync() => GoToAsync(Year - 1);

    [RelayCommand]
    private Task NextYearAsync() => GoToAsync(Year + 1);

    private async Task GoToAsync(int year)
    {
        if (year is < 2000 or > 2100)
        {
            return;
        }

        if (HasUnsavedChanges && !await SaveChangesAsync().ConfigureAwait(true))
        {
            return;
        }

        Year = year;
        await LoadAsync().ConfigureAwait(true);
    }

    public async Task LoadAsync(bool keepInput = false)
    {
        _loaded = true;
        var version = ++_version;
        var result = await _context.Api.GetAsync<LeaveAccountDto>($"api/employees/{_employeeId}/leave-account/{Year}").ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        if (!result.Ok || result.Value is null)
        {
            Error = result.Error ?? "Das Urlaubskonto ließ sich nicht laden.";
            IsLoaded = _account is not null;
            return;
        }

        Error = null;
        Show(result.Value, keepInput);
    }

    private bool CanSave() => HasUnsavedChanges;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (!HasUnsavedChanges)
        {
            return true;
        }

        var entitlementText = EntitlementText.Trim();
        decimal? entitlement = null;
        if (entitlementText.Length > 0)
        {
            if (!TryParse(entitlementText, out var parsed))
            {
                Error = "Bitte den Anspruch als Zahl eintragen, zum Beispiel 25 oder 12,5.";
                return false;
            }

            entitlement = parsed;
        }

        var carried = 0m;
        if (CarriedOverText.Trim().Length > 0 && !TryParse(CarriedOverText.Trim(), out carried))
        {
            Error = "Bitte den Übertrag als Zahl eintragen, zum Beispiel 3 oder 2,5.";
            return false;
        }

        var result = await _context.Api.PutAsync<LeaveAccountDto>($"api/employees/{_employeeId}/leave-account/{Year}", new LeaveEntitlementInput(entitlement, carried)).ConfigureAwait(true);
        if (!result.Ok || result.Value is null)
        {
            Error = result.Error ?? "Das Urlaubskonto ließ sich nicht speichern.";
            return false;
        }

        Error = null;
        Show(result.Value, keepInput: false);
        StatusText = "Gespeichert.";
        return true;
    }

    public void Discard()
    {
        if (_account is { } account)
        {
            Show(account, keepInput: false);
        }

        Error = null;
    }

    private void Show(LeaveAccountDto account, bool keepInput)
    {
        _account = account;
        if (!keepInput)
        {
            EntitlementText = Format(account.Entitlement);
            CarriedOverText = FormatCarried(account.CarriedOver);
        }

        StatusText = null;
        TakenText = Days(account.Taken);
        PlannedText = Days(account.Planned);
        RemainingText = account.Remaining is { } rest ? Days(rest) : "–";
        IsOverdrawn = account.Remaining < 0;
        Hint = HintFor(account);
        IsLoaded = true;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        SaveCommand.NotifyCanExecuteChanged();
    }

    internal static string HintFor(LeaveAccountDto account)
    {
        var days = string.Join(", ", account.Workdays.Select(d => German.DateTimeFormat.GetAbbreviatedDayName(d).TrimEnd('.')));
        var counted = account.WorkdaysAssumed
            ? "Keine Arbeitszeiten hinterlegt: gezählt wird Urlaub von Montag bis Freitag."
            : $"Gezählt wird Urlaub an den Arbeitstagen laut Stammdaten ({days}).";
        var none = account.Entitlement is null ? $" Für {account.Year} ist kein Anspruch eingetragen, darum steht kein Rest da." : "";
        var uncounted = account.UncountedDays switch
        {
            0 => "",
            1 => " 1 Urlaubseintrag an einem anderen Wochentag zählt nicht.",
            var n => $" {n} Urlaubseinträge an anderen Wochentagen zählen nicht.",
        };
        return $"{counted} Ein Urlaubstag mit Dienst zählt halb. Es zählt der veröffentlichte Dienstplan.{uncounted}{none}";
    }

    private static string Days(decimal value) => value == 1 ? "1 Tag" : $"{value.ToString("0.#", German)} Tage";

    private static string Format(decimal? value) => value?.ToString("0.#", German) ?? "";

    private static string FormatCarried(decimal value) => value == 0 ? "" : value.ToString("0.#", German);

    private static bool TryParse(string text, out decimal value) =>
        decimal.TryParse(text.Replace('.', ','), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, German, out value);
}
