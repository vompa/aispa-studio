using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Kennzahl-Kachel der Übersicht; ein Klick springt in den Abschnitt, zu dem die Zahl gehört.</summary>
public sealed record OverviewTile(string Label, string Value, string? Hint, StatTileState State, IRelayCommand Command);

/// <summary>
/// Bereich „Übersicht“ der Personenkarte (ADR 0098, masken.md R15): Kennzahl-Kacheln aus <c>GET api/employees/{id}/overview</c>. Der Server rechnet und gibt jeden Teil nur mit dem
/// passenden Recht frei; ein Teil ohne Recht bekommt keine Kachel. Geladen wird beim ersten Zeigen und nach Änderungen an Dienstplan, Zeiten und Urlaubskonto.
/// </summary>
/// <param name="context">Zugang zur API und Zeitzone des Teams.</param>
/// <param name="employeeId">Die Person.</param>
/// <param name="jump">Sprung beim Klick auf eine Kachel: eine Abschnitts-Id der Karte oder <see cref="RequestsTarget"/>.</param>
public sealed partial class PersonOverviewViewModel(TeamContext context, Guid employeeId, Action<string> jump) : ViewModelBase
{
    /// <summary>Sprungziel der Kachel „Offene Anträge“: die Ansicht „Anträge“ des Teams.</summary>
    public const string RequestsTarget = TeamViewModel.RequestsMode;

    /// <summary>So weit sucht der Server den nächsten Dienst voraus.</summary>
    private const int ShiftLookaheadDays = 31;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private bool _loaded;
    private int _version;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLoading))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLoading))]
    public partial IReadOnlyList<OverviewTile> Tiles { get; private set; } = [];

    /// <summary>„Wird geladen …“ nur, solange noch keine Kachel steht.</summary>
    public bool ShowsLoading => IsLoading && Tiles.Count == 0;

    public Task EnsureLoadedAsync() => _loaded ? Task.CompletedTask : LoadAsync();

    /// <summary>Nach einer Änderung auf der Karte: neu laden, wenn die Übersicht schon einmal geladen war.</summary>
    public Task RefreshAsync() => _loaded ? LoadAsync() : Task.CompletedTask;

    /// <summary>Eine andere Ansicht hat etwas geändert; beim nächsten Zeigen lädt die Übersicht neu.</summary>
    public void MarkStale() => _loaded = false;

    /// <summary>Lädt die Kennzahlen (beim ersten Zeigen, nach Änderungen und über „Erneut laden“).</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        _loaded = true;
        var version = ++_version;
        IsLoading = true;
        LoadError = null;
        var result = await context.Api.GetAsync<EmployeeOverviewDto>($"api/employees/{employeeId}/overview").ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        IsLoading = false;
        if (result.Value is not { } overview)
        {
            Tiles = [];
            LoadError = result.Error ?? "Die Übersicht wurde nicht geladen.";
            return;
        }

        Tiles = [.. TilesOf(overview)];
    }

    private IEnumerable<OverviewTile> TilesOf(EmployeeOverviewDto overview)
    {
        // Dienst, Stunden und Urlaub kommen mit demselben Recht; die Stunden zeigen, ob „kein Dienst“ heißt, dass keiner geplant ist.
        if (overview.WeekPlannedHours is not null)
        {
            yield return overview.NextShift is { } shift
                ? Tile("Nächster Dienst", $"{DayText(shift.Date)}, {Time(shift.Start)}–{Time(shift.End)}", shift.LocationName, StatTileState.None, PersonCardViewModel.RosterTab)
                : Tile("Nächster Dienst", "Keiner geplant", $"in den nächsten {ShiftLookaheadDays} Tagen", StatTileState.None, PersonCardViewModel.RosterTab);
            yield return Tile("Diese Woche", $"{Hours(overview.WeekPlannedHours.Value)} h geplant", $"{Hours(overview.WeekActualHours ?? 0)} h erfasst", StatTileState.None,
                PersonCardViewModel.TimesTab);
        }

        if (overview.Leave is { } leave)
        {
            var planned = $"{LeaveAccountViewModel.Days(leave.Planned)} geplant";
            yield return leave.Remaining is { } rest
                ? Tile($"Resturlaub {leave.Year}", LeaveAccountViewModel.Days(rest), planned, rest < 0 ? StatTileState.Warn : StatTileState.None, PersonCardViewModel.RosterTab)
                : Tile($"Resturlaub {leave.Year}", "Kein Anspruch hinterlegt", planned, StatTileState.None, PersonCardViewModel.RosterTab);
        }

        if (overview.OpenAbsenceRequests is { } requests)
        {
            yield return Tile("Offene Anträge", requests.ToString(German), null, requests > 0 ? StatTileState.Warn : StatTileState.None, RequestsTarget);
        }

        if (overview.DueCertificates is { } due)
        {
            var expired = overview.ExpiredCertificates ?? 0;
            yield return due == 0
                ? Tile("Zertifikate", "Alle gültig", null, StatTileState.Ok, PersonCardViewModel.DutiesTab)
                : Tile("Zertifikate", $"{due} fällig", expired > 0 ? $"davon {expired} abgelaufen" : null, StatTileState.Warn, PersonCardViewModel.DutiesTab);
        }
    }

    private OverviewTile Tile(string label, string value, string? hint, StatTileState state, string target) =>
        new(label, value, hint, state, new RelayCommand(() => jump(target)));

    private string DayText(DateOnly date) => date == context.Today ? "Heute" : DayLayout.ShortDay(date);

    private static string Time(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Hours(decimal hours) => TimeDayEditorViewModel.Hours((double)hours);
}
