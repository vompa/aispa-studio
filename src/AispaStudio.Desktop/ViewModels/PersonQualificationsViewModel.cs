using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Reiter „Qualifikationen“ der Personenkarte (R05 Slice 98): oben die Kästchen, welche Qualifikationen die Person hat (gespeichert mit der Leiste der Karte,
/// zusammen mit den Stammdaten), darunter „Qualifikationen verwalten“ mit Umbenennen und Hinzufügen, die wie <c>web/src/admin/QualificationsTab.tsx</c> sofort wirken.
/// </summary>
public sealed partial class PersonQualificationsViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly Action _changed;
    private IReadOnlyList<Guid> _saved;
    private bool _applying;

    public PersonQualificationsViewModel(IWorkstationApi api, IReadOnlyList<QualificationDto> all, IReadOnlyList<Guid> selected, Action changed)
    {
        _api = api;
        _changed = changed;
        _saved = selected;
        Fill(all, selected);
    }

    /// <summary>Kästchen je Qualifikation, angehakt, was die Person hat.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNone))]
    public partial IReadOnlyList<CheckOption> Options { get; private set; } = [];

    public bool HasNone => Options.Count == 0;

    /// <summary>„Qualifikationen verwalten“: je Qualifikation ein Namensfeld mit „Umbenennen“.</summary>
    public ObservableCollection<QualificationRow> Rows { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string NewName { get; set; } = "";

    /// <summary>Fehler der API beim Umbenennen oder Hinzufügen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasManageError))]
    public partial string? ManageError { get; private set; }

    public bool HasManageError => ManageError is not null;

    /// <summary>Die Liste aller Qualifikationen hat sich geändert (umbenannt, hinzugefügt).</summary>
    public event Action<IReadOnlyList<QualificationDto>>? ListChanged;

    public IReadOnlyList<Guid> Selected => [.. Options.Where(o => o.IsChecked).Select(o => o.Id)];

    public bool HasUnsavedChanges => !Selected.OrderBy(id => id).SequenceEqual(_saved.OrderBy(id => id));

    /// <summary>Nach dem Speichern: der gesendete Stand gilt als gespeichert.</summary>
    public void Saved(IReadOnlyList<Guid> selected) => _saved = selected;

    public void Discard()
    {
        _applying = true;
        foreach (var option in Options)
        {
            option.IsChecked = _saved.Contains(option.Id);
        }

        _applying = false;
    }

    private bool CanAdd => NewName.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        var result = await _api.PostAsync<QualificationDto>("api/qualifications", new QualificationInput(NewName.Trim())).ConfigureAwait(true);
        if (await ReloadAsync(result.Error).ConfigureAwait(true))
        {
            NewName = "";
        }
    }

    private async Task RenameAsync(QualificationRow row)
    {
        var result = await _api.PutAsync<QualificationDto>($"api/qualifications/{row.Id}", new QualificationInput(row.Name.Trim())).ConfigureAwait(true);
        await ReloadAsync(result.Error).ConfigureAwait(true);
    }

    /// <summary>Nach Umbenennen oder Hinzufügen: die Liste neu laden; die Kästchen behalten, was angehakt ist.</summary>
    private async Task<bool> ReloadAsync(string? error)
    {
        ManageError = error;
        if (error is not null)
        {
            return false;
        }

        var all = await _api.GetAsync<List<QualificationDto>>("api/qualifications").ConfigureAwait(true);
        if (all.Value is null)
        {
            ManageError = all.Error;
            return false;
        }

        Fill(all.Value, Selected);
        ListChanged?.Invoke(all.Value);
        return true;
    }

    /// <summary>Baut Kästchen und Namensfelder neu; angehakte Kästchen bleiben angehakt.</summary>
    private void Fill(IReadOnlyList<QualificationDto> all, IReadOnlyList<Guid> selected)
    {
        _applying = true;
        Options = [.. all.Select(q => new CheckOption(q.Id, q.Name, selected.Contains(q.Id), Changed))];
        Rows.Clear();
        foreach (var qualification in all)
        {
            Rows.Add(new QualificationRow(qualification, RenameAsync));
        }

        _applying = false;
    }

    private void Changed()
    {
        if (!_applying)
        {
            _changed();
        }
    }
}
