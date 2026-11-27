using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein noch offener Schritt auf der Abschlussseite: Titel, was fehlt, und der Index für den Sprung dorthin.</summary>
public sealed record SetupOpenStep(int Index, string Title, string? Missing)
{
    public string JumpName => $"Zum Schritt {Title}";
}

/// <summary>
/// Abschlussseite „Fertig“ (ADR 0104, Entscheidung 6): der letzte Eintrag des Assistenten, hinter dem letzten Schritt, ohne Zählung in „Schritt n von m“.
/// Sie fragt <c>GET api/setup/progress?readiness=true</c> und zeigt nur, was die API sagt: den ersten freien Platz oder den Grund, dazu die offenen Schritte.
/// </summary>
public sealed partial class SetupWizardViewModel
{
    public const string ReadinessNotLoaded = "Die Bereitschaft wurde nicht geladen.";

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("de-AT");

    private IReadOnlyList<LocationDto> _allLocations = [];
    private SetupReadinessDto? _readiness;
    private bool _readinessLoading;
    private bool _closed;

    /// <summary>„Probetermin im Kalender“: die Shell öffnet den Kalender an Tag und Standort des ersten freien Platzes.</summary>
    public event Action<SetupFirstSlotDto>? CalendarRequested;

    public ObservableCollection<SetupOpenStep> OpenSteps { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadinessError))]
    public partial string? ReadinessError { get; private set; }

    public bool HasReadinessError => ReadinessError is not null;

    public bool IsFinishStep => _steps.Count > 0 && Index == _steps.Count;

    public bool ReadinessLoaded => IsFinishStep && _readiness is not null && ReadinessError is null;

    public bool IsReady => _readiness is { Ready: true, FirstSlot: not null };

    public bool IsNotReady => ReadinessLoaded && !IsReady;

    public bool HasOpenSteps => OpenSteps.Count > 0;

    /// <summary>Der Grund aus der API, wenn sich nichts buchen lässt.</summary>
    public string? NotReadyReason => _readiness is { Ready: false } readiness ? readiness.Reason : null;

    public string? FirstSlotTreatment => _readiness?.FirstSlot?.TreatmentName;

    /// <summary>Tag und Uhrzeit des ersten freien Platzes in der Zeitzone des Standorts, in der Sprache der Oberfläche; der Platz selbst kommt aus der API.</summary>
    public string? FirstSlotWhen => _readiness?.FirstSlot is { } slot
        ? string.Create(Culture, $"{TimeZoneInfo.ConvertTime(slot.Start, ZoneOf(slot.LocationId)):dddd, d. MMMM yyyy 'um' HH:mm} Uhr · {slot.LocationName}")
        : null;

    /// <summary>Der Tag des Platzes in der Zeitzone seines Standorts, so wie ihn der Kalender zeigt.</summary>
    public DateOnly DayOf(SetupFirstSlotDto slot) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(slot.Start, ZoneOf(slot.LocationId)).DateTime);

    private bool CanOpenCalendar => !IsBusy && IsReady;

    private TimeZoneInfo ZoneOf(Guid locationId)
    {
        var zoneId = _allLocations.FirstOrDefault(l => l.Id == locationId)?.TimeZone;
        try
        {
            return zoneId is null ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    public async Task LoadReadinessAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ReadinessError = null;
        _readiness = null;
        OpenSteps.Clear();
        RaiseStateChanged();
        _readinessLoading = true;
        IsBusy = true;
        try
        {
            var result = (await _api.GetAsync<SetupProgressDto>("api/setup/progress?readiness=true").ConfigureAwait(true)).Value;
            if (_closed)
            {
                return;
            }

            if (result?.Readiness is not { } readiness)
            {
                ReadinessError = ReadinessNotLoaded;
                RaiseStateChanged();
                OnPropertyChanged(nameof(ReadinessLoaded));
                return;
            }

            _steps = result.Steps;
            _readiness = readiness;
            Index = _steps.Count;
            OpenSteps.Clear();
            for (var i = 0; i < _steps.Count; i++)
            {
                if (!_steps[i].Done || _steps[i].Note is not null)
                {
                    OpenSteps.Add(new SetupOpenStep(i, _steps[i].Title, _steps[i].Missing ?? _steps[i].Note));
                }
            }

            RaiseStateChanged();
            OnPropertyChanged(nameof(NotReadyReason));
            OnPropertyChanged(nameof(FirstSlotTreatment));
            OnPropertyChanged(nameof(FirstSlotWhen));
            OnPropertyChanged(nameof(ReadinessLoaded));
        }
        finally
        {
            _readinessLoading = false;
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenCalendar))]
    private void OpenCalendar()
    {
        if (_readiness?.FirstSlot is { } slot && !IsBusy)
        {
            CalendarRequested?.Invoke(slot);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void GoToStep(SetupOpenStep? step)
    {
        if (step is not null && !IsBusy)
        {
            SetIndex(step.Index);
        }
    }
}
