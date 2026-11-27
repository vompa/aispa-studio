using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Qualifikation mit Häkchen im Formular „Person hinzufügen“.</summary>
public sealed partial class SetupQualificationChoice(Guid id, string name, bool selected) : ObservableObject
{
    public Guid Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = selected;
}

/// <summary>
/// Schritt „Team“: legt über <c>POST api/employees</c> eine Person ohne Zugang an (Name, Qualifikationen, Arbeitszeiten); Zugänge und Feineres bleiben in der Verwaltung.
/// </summary>
public sealed partial class SetupWizardViewModel
{
    public const string PeopleKey = "people";
    public const string PersonNameMissing = "Vorname und Nachname fehlen. Trage beide ein, damit die Person im Kalender erscheint.";
    public const string NoStoredHours = "Der Standort hat noch keine gespeicherten Öffnungszeiten. Speichere sie im Schritt Öffnungszeiten.";

    /// <summary>Die Personen im Team (Anzeigename).</summary>
    public ObservableCollection<string> People { get; } = [];

    public ObservableCollection<SetupQualificationChoice> QualificationChoices { get; } = [];

    [ObservableProperty]
    public partial string FirstName { get; set; } = "";

    [ObservableProperty]
    public partial string LastName { get; set; } = "";

    /// <summary>Die Person arbeitet zu den Öffnungszeiten des Standorts; Feineres steht in der Verwaltung.</summary>
    [ObservableProperty]
    public partial bool WorksAtOpeningHours { get; set; } = true;

    public bool HasQualifications => QualificationChoices.Count > 0;

    public bool IsPeopleStep => Current?.Key == PeopleKey && _location is not null && !_teamForbidden;

    public bool HasPersonInput => FirstName.Trim().Length > 0 || LastName.Trim().Length > 0;

    private bool CanAddPerson => !IsBusy && FirstName.Trim().Length > 0 && LastName.Trim().Length > 0;

    partial void OnFirstNameChanged(string value) => RaiseStateChanged();

    partial void OnLastNameChanged(string value) => RaiseStateChanged();

    [RelayCommand(CanExecute = nameof(CanAddPerson))]
    private async Task AddPersonAsync()
    {
        if (IsBusy || _location is not { } location || !IsPeopleStep)
        {
            return;
        }

        var first = FirstName.Trim();
        var last = LastName.Trim();
        if (first.Length == 0 || last.Length == 0)
        {
            Error = PersonNameMissing;
            return;
        }

        if (WorksAtOpeningHours && location.OpeningHours.Count == 0)
        {
            Error = NoStoredHours;
            return;
        }

        Error = null;
        IsBusy = true;
        try
        {
            var name = $"{first} {last}";
            IReadOnlyList<WorkingHours> hours = WorksAtOpeningHours ? [.. location.OpeningHours.Select(h => new WorkingHours(location.Id, h.Day, h.Opens, h.Closes))] : [];
            var qualifications = QualificationChoices.Where(q => q.IsSelected).Select(q => q.Id).ToList();
            var result = await _api.PostAsync<EmployeeDto>("api/employees", new EmployeeInput(name, PersonDetailsViewModel.DefaultColor, qualifications, hours)).ConfigureAwait(true);
            if (result.Value is null)
            {
                Error = result.Error ?? "Die Person wurde nicht gespeichert.";
                return;
            }

            People.Add(name);
            FirstName = "";
            LastName = "";
            foreach (var choice in QualificationChoices)
            {
                choice.IsSelected = true;
            }

            _toast.Show("Person hinzugefügt");
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
