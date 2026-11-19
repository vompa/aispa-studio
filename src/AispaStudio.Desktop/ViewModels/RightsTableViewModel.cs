using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Person in einer Zeile der Tabelle, als Kennzeichen mit „✕“ zum Entfernen; abgelaufene stehen ausgegraut.</summary>
public sealed record RightsHolder(string Text, string Tip, bool IsExpired, IAsyncRelayCommand RemoveCommand)
{
    public string RemoveName => $"{Text} entfernen";
}

/// <summary>Eine Person, die in der Zeile noch fehlt („+ Person“).</summary>
public sealed record RightsCandidate(string Name, IAsyncRelayCommand AddCommand);

/// <summary>Eine Zeile der Tabelle: eine Vollmacht oder eine Zuständigkeit mit den eingetragenen Personen.</summary>
public sealed record RightsRow(
    string Name,
    string? Hint,
    string Effect,
    bool FreesSomething,
    bool IsUnstaffed,
    IReadOnlyList<RightsHolder> Holders,
    IReadOnlyList<RightsCandidate> Candidates)
{
    public bool HasHint => Hint is not null;

    public bool HasCandidates => Candidates.Count > 0;

    public string AddName => $"Person für {Name} eintragen";

    /// <summary>Was die Wirkung bedeutet (Tooltip an der Pille); leer bei einer Wirkung, die die API neu liefert und die hier noch keinen Text hat.</summary>
    public string? EffectTip => Effect switch
    {
        "Schaltet frei" => "Die App prüft diese Vollmacht zusätzlich zur Rolle.",
        "Nur Aushang" => "Der Eintrag zeigt nur, wer zuständig ist. Die App prüft nichts.",
        "Warnt im Dienstplan" => "Beim Speichern eines Dienstplantags warnt die App, wenn zu wenige Ersthelfer im Dienst sind.",
        "Erinnert an Bestellungen und Ablauf" => "Die App erinnert die Zuständigen an Bestellungen und ablaufende Ware.",
        _ => null,
    };
}

/// <summary>Ein Thema der Tabelle mit seinen Zeilen.</summary>
public sealed record RightsGroup(string Title, IReadOnlyList<RightsRow> Rows);

/// <summary>Ein Filter über der Tabelle mit der Zahl der Zeilen („Unbesetzt 4“).</summary>
public sealed partial class RightsFilter(string id, string title, Action<string> select) : ObservableObject
{
    public string Id => id;

    [ObservableProperty]
    public partial string Label { get; set; } = title;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [RelayCommand]
    private void Select() => select(id);
}

/// <summary>Ein Standort in der Auswahl im Kopf; <c>Id</c> ist bei „Alle Standorte“ leer.</summary>
public sealed record RightsLocation(Guid? Id, string Name);

/// <summary>
/// Tabelle „Wer darf was, wer ist zuständig?“ im Bereich Team (Masken-Überarbeitung P11, ADR 0098 Nachtrag): Vollmachten und Zuständigkeiten in einer Tabelle nach Themen, je Zeile
/// die eingetragenen Personen als Kennzeichen, „+ Person“ und „✕“ in der Zeile. Pflegt dieselben Einträge wie die Personenkarte (<c>…/regulations/powers</c> und <c>…/duties</c>);
/// Themen, Hinweise, Wirkung und „Unbesetzt“ liefert die API, die App zeigt an und filtert nur nach den gelieferten Kennzeichen. Nur mit dem Recht auf die Personalakte.
/// </summary>
public sealed partial class RightsTableViewModel : ViewModelBase
{
    private const string AllId = "alle";
    private const string UnstaffedId = "unbesetzt";
    private const string FreesId = "frei";

    private readonly TeamContext _context;
    private List<RightsGroup> _all = [];
    private string _filter = AllId;
    private bool _syncing;

    public RightsTableViewModel(TeamContext context)
    {
        _context = context;
        Locations = [new RightsLocation(null, "Alle Standorte")];
        SelectedLocation = Locations[0];
        Filters = [new RightsFilter(AllId, "Alle", SelectFilter), new RightsFilter(UnstaffedId, "Unbesetzt", SelectFilter), new RightsFilter(FreesId, "Schaltet etwas frei", SelectFilter)];
        Filters[0].IsOn = true;
    }

    /// <summary>Etwas wurde eingetragen oder entfernt: Kennzeichen der Liste und offene Karte neu holen.</summary>
    public event Func<Task>? Changed;

    [ObservableProperty]
    public partial IReadOnlyList<RightsLocation> Locations { get; private set; }

    [ObservableProperty]
    public partial RightsLocation? SelectedLocation { get; set; }

    /// <summary>Die Auswahl steht nur, wenn es mehr als einen Standort gibt.</summary>
    public bool ShowsLocations => Locations.Count > 2;

    public IReadOnlyList<RightsFilter> Filters { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoGroups))]
    public partial IReadOnlyList<RightsGroup> Groups { get; private set; } = [];

    public bool HasNoGroups => Groups.Count == 0 && IsLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoGroups))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_context.Now, _context.Zone).DateTime);

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        BuildLocations();
        var query = SelectedLocation?.Id is { } id ? $"?locationId={id}" : "";
        var powers = await _context.Api.GetAsync<List<PowerOverviewRowDto>>($"api/staff-regulations/powers{query}").ConfigureAwait(true);
        var duties = await _context.Api.GetAsync<List<DutyOverviewRowDto>>($"api/staff-duties/overview{query}").ConfigureAwait(true);
        if (powers.Value is null || duties.Value is null)
        {
            LoadError = powers.Error ?? duties.Error ?? "Die Übersicht wurde nicht geladen.";
            return;
        }

        var people = Candidates();
        var rows = powers.Value.Select(p => (Order: p.GroupOrder ?? 4, Group: p.Group ?? "Weiteres", Row: ToRow(p, people)))
            .Concat(duties.Value.Select(d => (Order: d.GroupOrder ?? 4, Group: d.Group ?? "Weiteres", Row: ToRow(d, people))));
        _all = [.. rows.GroupBy(r => (r.Order, r.Group)).OrderBy(g => g.Key.Order).Select(g => new RightsGroup(g.Key.Group, [.. g.Select(r => r.Row)]))];
        IsLoaded = true;
        ApplyFilter();
        UpdateFilterLabels();
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    partial void OnSelectedLocationChanged(RightsLocation? value)
    {
        if (IsLoaded && !_syncing && value is not null)
        {
            _ = LoadAsync();
        }
    }

    private void BuildLocations()
    {
        var selected = SelectedLocation?.Id;
        var all = _context.Data.Locations;
        Locations = [new RightsLocation(null, "Alle Standorte"), .. all.Select(l => new RightsLocation(l.Id, l.Name))];
        OnPropertyChanged(nameof(ShowsLocations));
        var keep = Locations.FirstOrDefault(l => l.Id == selected) ?? Locations[0];
        if (!ReferenceEquals(SelectedLocation, keep))
        {
            // Ohne neuen Aufruf: die Auswahl wird nur an die neue Liste angeglichen.
            _syncing = true;
            SelectedLocation = keep;
            _syncing = false;
        }
    }

    /// <summary>Aktive Personen, die am gewählten Standort arbeiten (wie die API), nach Name.</summary>
    private List<EmployeeDto> Candidates()
    {
        var location = SelectedLocation?.Id;
        return [.. _context.Data.Employees.Where(e => location is null || e.WorkingHours.Any(h => h.LocationId == location)).OrderBy(e => e.DisplayName, StringComparer.CurrentCulture)];
    }

    private RightsRow ToRow(PowerOverviewRowDto row, List<EmployeeDto> people)
    {
        var holding = row.Holders.Where(h => !h.IsExpired).Select(h => h.EmployeeId).ToHashSet();
        return new RightsRow(
            row.Label,
            row.Hint,
            row.Effect ?? (row.IsChecked ? "Schaltet frei" : "Nur Aushang"),
            row.IsChecked,
            row.IsUnstaffed ?? row.Holders.All(h => h.IsExpired),
            [.. row.Holders.Select(h => Holder(h.Name, Period(h.ValidFrom, h.ValidUntil, h.IsExpired), $"{Period(h.ValidFrom, h.ValidUntil, h.IsExpired)}, erteilt von {h.GrantedBy}", h.IsExpired,
                () => RemoveAsync(row.Label, h.Name, $"api/employees/{h.EmployeeId}/regulations/powers/{h.EntryId}", h.EntryId is not null)))],
            [.. people.Where(e => !holding.Contains(e.Id)).Select(e => new RightsCandidate(e.DisplayName,
                new AsyncRelayCommand(() => AddAsync($"api/employees/{e.Id}/regulations/powers", new StaffPowerInput(row.Kind, Today)))))]);
    }

    private RightsRow ToRow(DutyOverviewRowDto row, List<EmployeeDto> people)
    {
        var holding = row.Holders.Where(h => !h.IsExpired).Select(h => h.EmployeeId).ToHashSet();
        return new RightsRow(
            row.Name,
            row.Hint ?? (row.RequiredCertificate is { } required ? $"Verlangt: {required}" : null),
            row.Effect ?? "Nur Aushang",
            false,
            row.IsUnstaffed ?? row.Holders.All(h => h.IsExpired),
            [.. row.Holders.Select(h => Holder(h.Name, DutyPeriod(h), $"{DutyPeriod(h)}{(h.DeputyName is { } deputy ? $", Vertretung {deputy}" : "")}", h.IsExpired,
                () => RemoveAsync(row.Name, h.Name, $"api/employees/{h.EmployeeId}/duties/{h.DutyId}", true)))],
            [.. people.Where(e => !holding.Contains(e.Id)).Select(e => new RightsCandidate(e.DisplayName,
                new AsyncRelayCommand(() => AddAsync($"api/employees/{e.Id}/duties", new StaffDutyInput(row.DutyTypeId, Today)))))]);
    }

    private static RightsHolder Holder(string name, string period, string tip, bool expired, Func<Task> remove) =>
        new(expired ? $"{name} ({period})" : name, tip, expired, new AsyncRelayCommand(remove));

    private async Task AddAsync(string path, object input)
    {
        Error = StatusText = null;
        var result = await _context.Api.PostAsync<object>(path, input).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return;
        }

        await AfterChangeAsync("Eingetragen.").ConfigureAwait(true);
    }

    private async Task RemoveAsync(string subject, string person, string path, bool canRemove)
    {
        if (!canRemove || _context.Dialogs is not { } dialogs)
        {
            return;
        }

        Error = StatusText = null;
        var answer = await dialogs.AskAsync(new ConfirmRequest($"{person} bei „{subject}“ entfernen?", "Der Eintrag verschwindet auch auf der Personenkarte. Im Verlauf bleibt nur, dass er entfernt wurde.", "Entfernen", IsDestructive: true))
            .ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _context.Api.DeleteAsync<object>(path).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return;
        }

        await AfterChangeAsync("Entfernt.").ConfigureAwait(true);
    }

    private async Task AfterChangeAsync(string status)
    {
        await LoadAsync().ConfigureAwait(true);
        StatusText = status;
        if (Changed is { } changed)
        {
            await changed().ConfigureAwait(true);
        }
    }

    private void SelectFilter(string id)
    {
        _filter = id;
        foreach (var chip in Filters)
        {
            chip.IsOn = chip.Id == id;
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Groups = [.. _all.Select(g => g with { Rows = [.. g.Rows.Where(Matches)] }).Where(g => g.Rows.Count > 0)];
    }

    private bool Matches(RightsRow row) => _filter switch
    {
        UnstaffedId => row.IsUnstaffed,
        FreesId => row.FreesSomething,
        _ => true,
    };

    /// <summary>Die Zahlen an den Filtern zählen die gelieferten Kennzeichen der Zeilen.</summary>
    private void UpdateFilterLabels()
    {
        var rows = _all.SelectMany(g => g.Rows).ToList();
        Filters[0].Label = $"Alle {rows.Count}";
        Filters[1].Label = $"Unbesetzt {rows.Count(r => r.IsUnstaffed)}";
        Filters[2].Label = $"Schaltet etwas frei {rows.Count(r => r.FreesSomething)}";
    }

    private static string Day(DateOnly day) => day.ToString("d.M.yyyy", CultureInfo.InvariantCulture);

    private static string Period(DateOnly from, DateOnly? until, bool expired) => (expired, until) switch
    {
        (true, { } end) => $"abgelaufen am {Day(end)}",
        (false, { } end) => $"bis {Day(end)}",
        _ => $"ab {Day(from)}",
    };

    private static string DutyPeriod(DutyHolderDto holder) => Period(holder.ValidFrom, holder.ValidUntil, holder.IsExpired);
}
