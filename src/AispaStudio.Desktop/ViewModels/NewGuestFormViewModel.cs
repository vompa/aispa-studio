using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Kurze Neuanlage eines Gasts, wo es nur eine Suche gibt (Leiste „KI und Hilfe“): Vorname, Nachname, Telefon, E-Mail und „Gibt es schon?“
/// wie beim Buchen im Kalender. Was in der Suche getippt ist, steht als Name darin (letztes Wort Nachname, davor Vorname).
/// </summary>
public sealed partial class NewGuestFormViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly Action<CustomerDto> _chosen;
    private readonly Action _cancel;

    public NewGuestFormViewModel(IWorkstationApi api, string query, Action<CustomerDto> chosen, Action cancel)
    {
        _api = api;
        _chosen = chosen;
        _cancel = cancel;
        Duplicates = new DuplicateHintViewModel(api, AdoptAsync);
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length > 0 && !words.Any(w => w.Any(char.IsDigit) || w.Contains('@', StringComparison.Ordinal)))
        {
            LastName = words[^1];
            FirstName = string.Join(' ', words[..^1]);
        }
        else if (words.Length == 1 && words[0].Contains('@', StringComparison.Ordinal))
        {
            Email = words[0];
        }
        else if (words.Length > 0 && words.All(w => w.All(c => char.IsDigit(c) || c is '+' or '/' or '-')))
        {
            Phone = string.Join(' ', words);
        }
    }

    /// <summary>„Gibt es schon?“: ähnliche Kunden, übernehmen statt doppelt anlegen.</summary>
    public DuplicateHintViewModel Duplicates { get; }

    /// <summary>Name des Vornamen-Felds für <see cref="FocusRequested"/>: es bekommt den Fokus, wenn der Name fehlt.</summary>
    public const string NameField = "NewGuestFirstNameBox";

    /// <summary>Das erste ungültige Feld soll den Fokus bekommen (Name des Feldes, <see cref="NameField"/>).</summary>
    public event Action<string>? FocusRequested;

    [ObservableProperty]
    public partial string FirstName { get; set; } = "";

    [ObservableProperty]
    public partial string LastName { get; set; } = "";

    /// <summary>Feldfehler unter dem Vornamen, wenn weder Vor- noch Nachname steht; <see langword="null"/> ohne Fehler.</summary>
    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string Phone { get; set; } = "";

    [ObservableProperty]
    public partial string Email { get; set; } = "";

    [ObservableProperty]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial bool Busy { get; private set; }

    partial void OnFirstNameChanged(string value)
    {
        NameError = null;
        Check();
    }

    partial void OnLastNameChanged(string value)
    {
        NameError = null;
        Check();
    }

    partial void OnPhoneChanged(string value) => Check();

    partial void OnEmailChanged(string value) => Check();

    private void Check() => _ = Duplicates.CheckAsync(FirstName, LastName, Email, Phone);

    private bool CanCreate => !Busy;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        Error = null;
        if (FirstName.Trim().Length == 0 && LastName.Trim().Length == 0)
        {
            NameError = "Bitte einen Vor- oder Nachnamen eintragen.";
            FocusRequested?.Invoke(NameField);
            return;
        }

        Busy = true;
        var result = await _api.CreateCustomerAsync(new CustomerInput(FirstName.Trim(), LastName.Trim(),
            string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(), string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(), null, null)).ConfigureAwait(true);
        Busy = false;
        if (result.Value is not { } customer)
        {
            Error = result.Error;
            return;
        }

        _chosen(customer);
    }

    [RelayCommand]
    private void Cancel() => _cancel();

    private async Task AdoptAsync(Guid id)
    {
        var result = await _api.GetAsync<CustomerDto>($"api/customers/{id}").ConfigureAwait(true);
        if (result.Value is not { } customer)
        {
            Error = result.Error;
            return;
        }

        _chosen(customer);
    }
}
