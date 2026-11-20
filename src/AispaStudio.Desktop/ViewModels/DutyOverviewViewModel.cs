using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Zertifikat in „Was läuft bald ab?“; abgelaufene mit Warnung als Text und Symbol.</summary>
public sealed record ExpiringRow(string Text, bool IsExpired, string PersonName = "", ICommand? OpenCommand = null)
{
    /// <summary>Der Weg zur Person: ihre Karte öffnet bei den Zuständigkeiten (nur, wenn die Maske ihn anbietet).</summary>
    public bool CanOpen => OpenCommand is not null;

    public string OpenName => $"Karte von {PersonName} öffnen";

    /// <summary>Für den Screenreader: die Warnung gehört zum Namen, nicht nur zum Symbol.</summary>
    public string AutomationName => IsExpired ? $"Warnung: {Text}" : Text;
}

/// <summary>Ein Eintrag der Liste der Zuständigkeiten zum Bearbeiten; die Mindestzahl im Dienst gibt es nur beim Ersthelfer.</summary>
public sealed partial class DutyTypeRow(Guid? id, DutyTypeKey? key, Action changed) : ObservableObject
{
    public Guid? Id => id;

    public bool ShowsMinimum => key == DutyTypeKey.FirstAid;

    /// <summary>Vorgaben lassen sich nicht löschen, nur ausblenden.</summary>
    public bool IsNew => id is null;

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string RequiredCertificate { get; set; } = "";

    [ObservableProperty]
    public partial decimal? MinimumOnDuty { get; set; }

    [ObservableProperty]
    public partial bool IsHidden { get; set; }

    [ObservableProperty]
    public partial string? NameError { get; set; }

    [ObservableProperty]
    public partial string? MinimumError { get; set; }

    public string State => string.Join('|', Name, RequiredCertificate, MinimumOnDuty, IsHidden);

    public DutyTypeInput Input() => new(
        Name.Trim(), string.IsNullOrWhiteSpace(RequiredCertificate) ? null : RequiredCertificate.Trim(), ShowsMinimum ? (int?)MinimumOnDuty : null, !IsHidden);

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Name) or nameof(RequiredCertificate) or nameof(MinimumOnDuty) or nameof(IsHidden))
        {
            changed();
        }
    }
}

/// <summary>
/// Abschnitt „Zertifikate und Liste der Zuständigkeiten“ im Bereich Team (R05 Slice 117, Ä274, E14): „Was läuft bald ab?“ (Zertifikate in der Frist
/// und abgelaufene) und die pflegbare „Liste der Zuständigkeiten“ mit verlangtem Zertifikat und der Mindestzahl Ersthelferinnen im Dienst. Nur mit <c>ManageHrFile</c>; wer wofür zuständig ist, steht in der Tabelle
/// <see cref="RightsTableViewModel"/>.
/// </summary>
public sealed partial class DutyOverviewViewModel(IWorkstationApi api, Action<Guid>? openPerson = null, ToastState? toast = null) : ViewModelBase, IEditableArea
{
    private const string TypesPath = "api/staff-duties/types";
    private string _savedState = "";
    private bool _applying;

    public string Title => "Zuständigkeiten";

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoExpiring))]
    public partial IReadOnlyList<ExpiringRow> Expiring { get; private set; } = [];

    public bool HasNoExpiring => Expiring.Count == 0;

    public ObservableCollection<DutyTypeRow> Types { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>Der Toast der Shell: „Gespeichert“ nach dem Speichern der Zuständigkeiten.</summary>
    public ToastState Toast { get; } = toast ?? new ToastState(TimeProvider.System);

    public bool HasUnsavedChanges => IsLoaded && TypesState() != _savedState;

    public Task EnsureLoadedAsync() => HasUnsavedChanges ? Task.CompletedTask : LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var expiring = await api.GetAsync<List<ExpiringCertificateDto>>("api/staff-duties/expiring").ConfigureAwait(true);
        var types = await api.GetAsync<List<DutyTypeDto>>(TypesPath).ConfigureAwait(true);
        if (expiring.Value is null || types.Value is null)
        {
            LoadError = expiring.Error ?? types.Error ?? "Die Zuständigkeiten wurden nicht geladen.";
            return;
        }

        Expiring = [.. expiring.Value.Select(c => new ExpiringRow(
            c.IsExpired ? $"{c.EmployeeName}: {c.Name}, abgelaufen am {Day(c.ValidUntil)}" : $"{c.EmployeeName}: {c.Name}, läuft am {Day(c.ValidUntil)} ab", c.IsExpired,
            c.EmployeeName, openPerson is null ? null : new RelayCommand(() => openPerson(c.EmployeeId))))];
        _applying = true;
        Types.Clear();
        foreach (var type in types.Value)
        {
            Types.Add(new DutyTypeRow(type.Id, type.Key, TypesChanged)
            {
                Name = type.Name,
                RequiredCertificate = type.RequiredCertificate ?? "",
                MinimumOnDuty = type.MinimumOnDuty,
                IsHidden = !type.IsActive,
            });
        }

        _applying = false;
        _savedState = TypesState();
        IsLoaded = true;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    [RelayCommand]
    private void AddType()
    {
        Types.Add(new DutyTypeRow(null, null, TypesChanged));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    [RelayCommand]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    [RelayCommand]
    private void DiscardChanges() => Discard();

    [RelayCommand]
    private void CloseError() => Error = null;

    /// <summary>Speichert die geänderten und neuen Einträge der Liste; prüft nur, was offensichtlich fehlt, den Rest prüft die API.</summary>
    public async Task<bool> SaveChangesAsync()
    {
        if (!HasUnsavedChanges)
        {
            return true;
        }

        foreach (var row in Types)
        {
            row.NameError = string.IsNullOrWhiteSpace(row.Name) ? "Bitte einen Namen eingeben." : null;
            row.MinimumError = row.ShowsMinimum && row.MinimumOnDuty is not (null or (>= 0 and <= 20)) ? "Bitte 0 bis 20 eingeben." : null;
        }

        if (Types.Any(t => t.NameError is not null || t.MinimumError is not null))
        {
            return false;
        }

        Error = null;
        var saved = _savedState.Split('\n');
        for (var i = 0; i < Types.Count; i++)
        {
            var row = Types[i];
            if (i < saved.Length && saved[i] == row.State && !row.IsNew)
            {
                continue;
            }

            var result = row.Id is { } id ? await api.PutAsync<object>($"{TypesPath}/{id}", row.Input()).ConfigureAwait(true) : await api.PostAsync<object>(TypesPath, row.Input()).ConfigureAwait(true);
            if (result.Error is { } error)
            {
                Error = error;
                return false;
            }
        }

        await LoadAsync().ConfigureAwait(true);
        Toast.Show("Gespeichert");
        return true;
    }

    public void Discard()
    {
        Error = null;
        _ = LoadAsync();
    }

    private static string Day(DateOnly day) => day.ToString("d.M.yyyy", CultureInfo.InvariantCulture);

    private string TypesState() => string.Join('\n', Types.Select(t => t.State));

    private void TypesChanged()
    {
        if (_applying)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }
}
