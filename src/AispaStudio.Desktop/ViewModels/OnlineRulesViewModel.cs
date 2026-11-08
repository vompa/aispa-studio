using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Raster der angebotenen Startzeiten in der Auswahl (<c>STEPS</c> in <c>OnlineRulesSection.tsx</c>).</summary>
public sealed record SlotStepOption(int Minutes)
{
    public override string ToString() => $"{Minutes} Minuten";
}

/// <summary>
/// Abschnitt „Online-Buchung“ eines Standorts (Standorte &amp; Räume, R05 Slice 95) wie <c>OnlineRulesSection.tsx</c>: die Regeln der Online-Buchung je Standort mit eigenem
/// Speichern („Regeln speichern“) und „Vorgaben wiederherstellen“. Prüfen macht die API. Ohne Recht (ManageLocation) sind die Felder gesperrt.
/// </summary>
public sealed partial class OnlineRulesViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly Guid _locationId;

    public OnlineRulesViewModel(IWorkstationApi api, Guid locationId, bool canEdit)
    {
        _api = api;
        _locationId = locationId;
        CanEdit = canEdit;
    }

    public static IReadOnlyList<SlotStepOption> Steps { get; } = [.. new[] { 5, 10, 15, 20, 30, 60 }.Select(m => new SlotStepOption(m))];

    public bool CanEdit { get; }

    /// <summary>Die Regeln wurden gespeichert oder auf die Vorgaben zurückgesetzt.</summary>
    public event Action? Saved;

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    /// <summary>Fehler beim Laden oder Speichern der Regeln.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>„Gespeichert.“ bzw. „Vorgaben gelten wieder.“ neben den Knöpfen.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>Es gelten die Vorgaben für alle Standorte, nicht eigene Regeln.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReset))]
    public partial bool IsDefault { get; private set; }

    public bool CanReset => CanEdit && !IsDefault;

    [ObservableProperty]
    public partial bool BookingEnabled { get; set; }

    [ObservableProperty]
    public partial decimal? MinLeadMinutes { get; set; }

    [ObservableProperty]
    public partial decimal? MaxDaysAhead { get; set; }

    [ObservableProperty]
    public partial decimal? CancellationDeadlineHours { get; set; }

    [ObservableProperty]
    public partial decimal? NoShowFeePercent { get; set; }

    [ObservableProperty]
    public partial decimal? DepositPercent { get; set; }

    [ObservableProperty]
    public partial decimal? CardHoldHours { get; set; }

    [ObservableProperty]
    public partial SlotStepOption SlotStep { get; set; } = Steps[0];

    [ObservableProperty]
    public partial bool NewCustomersOnly { get; set; }

    [ObservableProperty]
    public partial string GuestNote { get; set; } = "";

    [ObservableProperty]
    public partial string CancellationPolicy { get; set; } = "";

    public async Task LoadAsync()
    {
        var result = await _api.GetAsync<LocationOnlineRules>($"api/locations/{_locationId}/online-rules").ConfigureAwait(true);
        if (result.Value is not { } rules)
        {
            Error = result.Error ?? "Die Regeln der Online-Buchung wurden nicht geladen.";
            return;
        }

        Error = null;
        Apply(rules);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        StatusText = null;
        var result = await _api.PutAsync<LocationOnlineRules>($"api/locations/{_locationId}/online-rules", Input()).ConfigureAwait(true);
        if (result.Value is not { } rules)
        {
            Error = result.Error ?? "Die Regeln wurden nicht gespeichert.";
            return;
        }

        Error = null;
        Apply(rules);
        StatusText = "Gespeichert.";
        Saved?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanReset))]
    private async Task ResetAsync()
    {
        StatusText = null;
        var result = await _api.DeleteAsync<LocationOnlineRules>($"api/locations/{_locationId}/online-rules").ConfigureAwait(true);
        if (result.Value is not { } rules)
        {
            Error = result.Error ?? "Die Vorgaben wurden nicht wiederhergestellt.";
            return;
        }

        Error = null;
        Apply(rules);
        StatusText = "Vorgaben gelten wieder.";
        Saved?.Invoke();
    }

    private void Apply(LocationOnlineRules rules)
    {
        IsDefault = rules.IsDefault;
        BookingEnabled = rules.BookingEnabled;
        MinLeadMinutes = rules.MinLeadMinutes;
        MaxDaysAhead = rules.MaxDaysAhead;
        CancellationDeadlineHours = rules.CancellationDeadlineHours;
        NoShowFeePercent = rules.NoShowFeePercent;
        DepositPercent = rules.DepositPercent;
        CardHoldHours = rules.CardHoldHours;
        SlotStep = Steps.FirstOrDefault(s => s.Minutes == rules.SlotStepMinutes) ?? new SlotStepOption(rules.SlotStepMinutes);
        NewCustomersOnly = rules.NoShowFeeNewCustomersOnly;
        GuestNote = rules.GuestNote ?? "";
        CancellationPolicy = rules.CancellationPolicy ?? "";
        IsLoaded = true;
        ResetCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Was gespeichert würde; leere Zahlenfelder gelten als 0 und leere Texte als „keine Angabe“ wie im Web.</summary>
    private LocationOnlineRules Input() => new(
        BookingEnabled, Whole(MinLeadMinutes), Whole(MaxDaysAhead), Whole(CancellationDeadlineHours), SlotStep.Minutes, Text(GuestNote), IsDefault,
        Whole(NoShowFeePercent), Whole(CardHoldHours), NewCustomersOnly, Whole(DepositPercent), Text(CancellationPolicy));

    private static int Whole(decimal? value) => (int)(value ?? 0);

    private static string? Text(string value) => value.Length == 0 ? null : value;
}
