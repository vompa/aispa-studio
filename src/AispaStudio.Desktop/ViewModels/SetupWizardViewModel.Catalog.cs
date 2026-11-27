using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Paket des Startkatalogs mit Häkchen; die Zeilen darunter nennen Behandlung und Dauer, schon Vorhandenes ist gekennzeichnet.</summary>
public sealed partial class SetupPackageChoice : ObservableObject
{
    public SetupPackageChoice(SetupCatalogPackageDto package, bool selected)
    {
        Key = package.Key;
        Title = package.Title;
        Hint = package.Hint;
        Lines = [.. package.Treatments.Select(t => $"{t.Name} · {t.DurationMinutes} Min.{(t.Exists ? " · vorhanden" : "")}")];
        IsSelected = selected;
    }

    public string Key { get; }

    public string Title { get; }

    public string Hint { get; }

    public IReadOnlyList<string> Lines { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Ein Behandlungsraum mit Häkchen: die Auswahl gilt für alle gewählten Pakete.</summary>
public sealed partial class SetupRoomChoice(Guid id, string name, bool selected) : ObservableObject
{
    public Guid Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = selected;
}

/// <summary>Schritt „Behandlungen“: Startkatalog über <c>GET api/setup/catalog</c> und <c>POST api/setup/catalog/apply</c>.</summary>
public sealed partial class SetupWizardViewModel
{
    public const string TreatmentsKey = "treatments";
    public const string NoRoomHint = "Für Behandlungen braucht es einen Behandlungsraum. Lege ihn im Schritt Räume an.";

    public ObservableCollection<SetupPackageChoice> Packages { get; } = [];

    public ObservableCollection<SetupRoomChoice> RoomChoices { get; } = [];

    /// <summary>Ergebnis des letzten Anlegens („3 Behandlungen angelegt, 1 übersprungen.“); wechselt mit dem Schritt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCatalogMessage))]
    public partial string? CatalogMessage { get; private set; }

    public bool HasCatalogMessage => CatalogMessage is not null;

    public bool IsTreatmentsStep => Current?.Key == TreatmentsKey && Packages.Count > 0;

    public bool HasTreatmentRoom => RoomChoices.Count > 0;

    public bool ShowNoRoomHint => IsTreatmentsStep && !HasTreatmentRoom;

    public bool HasPackageSelection => Packages.Any(p => p.IsSelected);

    private bool CanApplyCatalog => !IsBusy && IsTreatmentsStep && HasPackageSelection && RoomChoices.Any(r => r.IsSelected);

    [RelayCommand]
    private void ShowRoomsStep()
    {
        var rooms = _steps.ToList().FindIndex(s => s.Key == RoomsKey);
        if (rooms >= 0 && !IsBusy)
        {
            SetIndex(rooms);
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyCatalog))]
    private async Task ApplyCatalogAsync()
    {
        if (!CanApplyCatalog)
        {
            return;
        }

        var roomIds = RoomChoices.Where(r => r.IsSelected).Select(r => r.Id).ToList();
        var chosen = Packages.Where(p => p.IsSelected).ToList();
        Error = null;
        CatalogMessage = null;
        IsBusy = true;
        try
        {
            var input = new SetupCatalogApplyInput([.. chosen.Select(p => new SetupCatalogChoice(p.Key, roomIds))]);
            var result = await _api.PostAsync<SetupCatalogApplyResult>("api/setup/catalog/apply", input).ConfigureAwait(true);
            if (result.Value is not { } applied)
            {
                Error = result.Error ?? "Die Behandlungen wurden nicht angelegt.";
                return;
            }

            var created = applied.Packages.Sum(p => p.Created);
            var skipped = applied.Packages.Sum(p => p.Skipped);
            foreach (var package in chosen)
            {
                package.IsSelected = false;
            }

            CatalogMessage = $"{created} {(created == 1 ? "Behandlung" : "Behandlungen")} angelegt, {skipped} übersprungen.";
            if (!await RefreshAsync().ConfigureAwait(true))
            {
                Error = NotLoaded;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
