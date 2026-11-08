using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Reiter „Stammdaten“ der Personenkarte (R05 Slice 98): dieselben Felder und Texte wie <c>EmployeeForm</c> in <c>web/src/admin/TeamTab.tsx</c> ohne die
/// Qualifikationen (eigener Reiter): vollständiger Name, Name im Haus, Farbe im Kalender, „Für Gäste mit Wunsch“, Sprachen und Wochenarbeitszeiten.
/// Gespeichert wird mit der Leiste der Karte; ausgeschiedene Personen sind nur lesbar.
/// </summary>
public sealed partial class PersonDetailsViewModel : ViewModelBase
{
    public const string NameField = "name";

    /// <summary>Vorgabe einer neuen Person wie im Web.</summary>
    public const string DefaultColor = "#2f6f5e";

    /// <summary>Beispiel im leeren Farbfeld; ein Text, keine Farbe der Oberfläche.</summary>
    public const string ColorExample = "z. B. #2f6f5e";

    private static readonly string[] Inputs = [nameof(Name), nameof(PreferredName), nameof(Color), nameof(Gender)];

    private readonly IReadOnlyList<Choice> _locations;
    private readonly Action _changed;
    private bool _applying;

    public PersonDetailsViewModel(EmployeeDto? employee, IReadOnlyList<LocationDto> locations, Action changed, bool isReadOnly = false)
    {
        _locations = [.. locations.Select(l => new Choice(l.Id, l.Name))];
        _changed = changed;
        IsReadOnly = isReadOnly;
        Apply(employee);
    }

    public bool IsReadOnly { get; }

    public bool IsEditable => !IsReadOnly;

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string PreferredName { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorSwatch))]
    public partial string Color { get; set; } = DefaultColor;

    [ObservableProperty]
    public partial PersonGenderChoice Gender { get; set; } = PersonGenderChoice.All[0];

    [ObservableProperty]
    public partial IReadOnlyList<LanguageOption> Languages { get; private set; } = [];

    public ObservableCollection<WorkingHoursRow> WorkingHours { get; } = [];

    /// <summary>Ohne Standort lässt sich keine Arbeitszeit erfassen (wie im Web).</summary>
    public bool CanAddHours => IsEditable && _locations.Count > 0;

    /// <summary>Die Fläche neben dem Farbfeld: die eingetragene Farbe, solange sie lesbar ist (#rrggbb), sonst die gespeicherte.</summary>
    public string ColorSwatch => HexColor().IsMatch(Color.Trim()) ? Color.Trim() : _savedColor;

    private string _savedColor = DefaultColor;

    /// <summary>Das Feld, das den Fokus bekommen soll; die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    /// <summary>Pflichtfeld vor dem Speichern; die übrigen Regeln prüft die API.</summary>
    public bool Check()
    {
        if (Name.Trim().Length > 0)
        {
            return true;
        }

        NameError = "Der Name der Person fehlt. Trage den vollständigen Namen ein.";
        FocusTarget = NameField;
        return false;
    }

    /// <summary>Was zusammen mit den Qualifikationen gespeichert wird.</summary>
    public EmployeeInput ToInput(IReadOnlyList<Guid> qualificationIds) => new(
        Name.Trim(), Color.Trim(), qualificationIds, [.. WorkingHours.Select(h => h.ToDto())],
        [.. Languages.Where(l => l.IsChecked).Select(l => l.Code)], Gender.Gender, PreferredName.Trim().Length == 0 ? null : PreferredName.Trim());

    public string State() => JsonSerializer.Serialize(ToInput([]), ApiJson.Options);

    /// <summary>Setzt alle Felder auf <paramref name="employee"/> (leer: Vorgaben einer neuen Person).</summary>
    public void Apply(EmployeeDto? employee)
    {
        _applying = true;
        Name = employee?.DisplayName ?? "";
        PreferredName = employee?.PreferredName ?? "";
        _savedColor = employee?.Color ?? DefaultColor;
        Color = _savedColor;
        Gender = PersonGenderChoice.All.First(g => g.Gender == employee?.Gender);
        var languages = employee?.Languages ?? [];
        Languages = [.. GuestLanguages.All.Select(l => new LanguageOption(l.Code, l.Name, languages.Contains(l.Code), Changed))];
        WorkingHours.Clear();
        foreach (var hours in employee?.WorkingHours ?? [])
        {
            WorkingHours.Add(new WorkingHoursRow(hours, _locations, Changed, Remove));
        }

        NameError = null;
        _applying = false;
    }

    [RelayCommand]
    private void AddHours()
    {
        var last = WorkingHours.Count > 0 ? WorkingHours[^1] : null;
        var taken = WorkingHours.Select(h => h.Day.Day).ToHashSet();
        var start = last is null ? 0 : WorkDayChoice.All.ToList().FindIndex(d => d.Day == last.Day.Day) + 1;
        var day = Enumerable.Range(start, WorkDayChoice.All.Count).Select(i => WorkDayChoice.All[i % WorkDayChoice.All.Count]).FirstOrDefault(d => !taken.Contains(d.Day))
            ?? WorkDayChoice.All[0];
        var location = last?.Location?.Id ?? _locations[0].Id ?? Guid.Empty;
        WorkingHours.Add(new WorkingHoursRow(new WorkingHours(location, day.Day, last?.Start ?? new(9, 0), last?.End ?? new(17, 0)), _locations, Changed, Remove));
        Changed();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_applying || e.PropertyName is not { } name || !Inputs.Contains(name))
        {
            return;
        }

        if (name == nameof(Name))
        {
            NameError = null;
        }

        Changed();
    }

    private void Remove(WorkingHoursRow row)
    {
        WorkingHours.Remove(row);
        Changed();
    }

    private void Changed()
    {
        if (!_applying)
        {
            _changed();
        }
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
