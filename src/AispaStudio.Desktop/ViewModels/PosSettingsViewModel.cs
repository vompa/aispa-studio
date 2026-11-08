using System.ComponentModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Kassa-Einstellungen des Spas als native Maske der Verwaltung (R05 Slice 96): dieselben Felder und Hilfen wie <c>web/src/admin/PosTab.tsx</c>, Rabattgrenze und
/// Barzahlungsrabatt (<c>api/pos/settings</c>). An der Kassa-Logik, an RKSV und TSE ändert die Maske nichts (ADR 0010); was gilt, rechnet die API. Ohne Änderung
/// schickt „Speichern“ nichts.
/// </summary>
public sealed partial class PosSettingsViewModel(IWorkstationApi api) : ViewModelBase, INativeArea
{
    /// <summary>Feldkennung der Rabattgrenze, für den Fokus nach einem Fehler.</summary>
    public const string LimitField = "limit";

    private PosSettingsDto? _saved;
    private bool _applying;

    public string Title => "Kassa";

    /// <summary>Die Einstellungen wurden gespeichert; die Leiste „KI und Hilfe“ bietet dann an, eine Frage zu wiederholen.</summary>
    public event Action? Saved;

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    /// <summary>Rabatt ohne Freigabe bis (%).</summary>
    [ObservableProperty]
    public partial decimal? DiscountLimit { get; set; }

    /// <summary>Barzahlungsrabatt (%, 0 = aus); leer heißt aus.</summary>
    [ObservableProperty]
    public partial decimal? CashDiscount { get; set; }

    /// <summary>Fehler am Feld der Rabattgrenze (Pflichtfeld).</summary>
    [ObservableProperty]
    public partial string? LimitError { get; private set; }

    /// <summary>Fehler der API beim Speichern, über der Leiste.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>„Gespeichert.“ in der Leiste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>Das Feld, das den Fokus bekommen soll; die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    public bool HasUnsavedChanges => _saved is not null && (DiscountLimit != _saved.DiscountLimitPercent || (CashDiscount ?? 0) != _saved.CashDiscountPercent);

    /// <summary>Lädt die Einstellungen, beim ersten Öffnen und über „Erneut laden“.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var result = await api.GetAsync<PosSettingsDto>("api/pos/settings").ConfigureAwait(true);
        if (result.Value is not { } settings)
        {
            LoadError = result.Error ?? "Die Kassa-Einstellungen wurden nicht geladen.";
            return;
        }

        Apply(settings);
        IsLoaded = true;
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    private bool CanSave => !IsSaving;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || _saved is null)
        {
            return false;
        }

        if (!HasUnsavedChanges)
        {
            StatusText = "Gespeichert.";
            return true;
        }

        if (DiscountLimit is not { } limit)
        {
            LimitError = "Die Rabattgrenze fehlt. Trage einen Wert von 0 bis 100 ein, 0 heißt: jeder Rabatt braucht die Freigabe.";
            FocusTarget = LimitField;
            return false;
        }

        Error = null;
        IsSaving = true;
        var result = await api.PutAsync<PosSettingsDto>("api/pos/settings", new PosSettingsInput(limit, CashDiscount ?? 0)).ConfigureAwait(true);
        IsSaving = false;
        if (result.Value is not { } settings)
        {
            Error = result.Error ?? "Die Kassa-Einstellungen wurden nicht gespeichert.";
            return false;
        }

        Apply(settings);
        StatusText = "Gespeichert.";
        Saved?.Invoke();
        return true;
    }

    [RelayCommand]
    public void Discard()
    {
        if (_saved is not null)
        {
            Apply(_saved);
        }

        LimitError = null;
        Error = null;
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    /// <summary>Die Kassa-Einstellungen haben einen Eintrag und keine Abschnitte; es zählt nur das Feld.</summary>
    public void Open(Guid? id, string? section, string? field) => FocusTarget = field;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_applying || e.PropertyName is not (nameof(DiscountLimit) or nameof(CashDiscount)))
        {
            return;
        }

        if (e.PropertyName == nameof(DiscountLimit))
        {
            LimitError = null;
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Apply(PosSettingsDto settings)
    {
        _applying = true;
        _saved = settings;
        DiscountLimit = settings.DiscountLimitPercent;
        CashDiscount = settings.CashDiscountPercent;
        _applying = false;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }
}
