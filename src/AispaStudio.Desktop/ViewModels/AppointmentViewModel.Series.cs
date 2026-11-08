using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Abstand der Wiederholung in der Auswahl.</summary>
public sealed record SeriesIntervalOption(int Weeks, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Ein Termin der Serien-Vorschau; Konflikte sind zunächst abgewählt.</summary>
public sealed partial class SeriesRowViewModel(SeriesOccurrence occurrence, TimeZoneInfo zone) : ObservableObject
{
    public SeriesOccurrence Occurrence { get; } = occurrence;

    public string DateText => $"{DayLayout.ShortDay(Occurrence.Date)}{Occurrence.Date.Year}";

    public string TimeText => Occurrence.State == SeriesOccurrenceState.Conflict ? "–" : $"{DayLayout.Local(Occurrence.Start, zone):HH:mm}";

    public string Person => Occurrence.EmployeeName ?? "";

    public bool CanTake => Occurrence.State != SeriesOccurrenceState.Conflict;

    public bool IsProblem => Occurrence.State != SeriesOccurrenceState.Free;

    /// <summary>„Andere Zeit: Anna: belegt“.</summary>
    public string StateText => string.Join(" · ", new[]
    {
        Occurrence.State switch
        {
            SeriesOccurrenceState.Free => "frei",
            SeriesOccurrenceState.OtherTime => "andere Uhrzeit",
            SeriesOccurrenceState.OtherEmployee => "andere Person",
            SeriesOccurrenceState.OtherTimeAndEmployee => "andere Uhrzeit und Person",
            _ => "nichts frei",
        },
        Occurrence.State == SeriesOccurrenceState.Free ? null : Occurrence.Problem,
    }.Where(p => p is not null));

    [ObservableProperty]
    public partial bool Take { get; set; } = occurrence.State != SeriesOccurrenceState.Conflict;
}

// ---------- Wiederholen: Serie ab diesem Termin, Vorschau mit Konflikten, ab hier stornieren ----------
public partial class AppointmentViewModel
{
    public static IReadOnlyList<SeriesIntervalOption> Intervals { get; } =
        [.. Enumerable.Range(1, 8).Select(w => new SeriesIntervalOption(w, w == 1 ? "jede Woche" : $"alle {w} Wochen"))];

    public bool IsSeries => Appointment.SeriesId is not null;

    public string? SeriesText => Series is { } s ? DayLayout.SeriesText(s, Appointment.Start, Zone) : null;

    public bool CanRepeat => MayBook && SeriesChanged is not null && !IsSeries && !IsGroup
        && Appointment.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn or AppointmentStatus.Completed;

    public bool CanCancelFollowing => IsSeries && CanCancel && SeriesChanged is not null;

    public string CancelOneText => IsSeries ? "Nur diesen stornieren" : "Stornieren";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRepeat), nameof(IsEditingCard))]
    public partial bool IsEditingSeries { get; private set; }

    public bool ShowRepeat => CanRepeat && !IsEditingSeries;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SeriesRuleHint))]
    public partial SeriesIntervalOption SeriesInterval { get; set; } = Intervals[3];

    /// <summary>„N Wochen nach dem letzten Besuch“ statt fester Wochentag.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SeriesRuleHint))]
    public partial bool AfterLastVisit { get; set; }

    /// <summary>Ende über die Anzahl; sonst über das Datum.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EndByDate))]
    public partial bool EndByCount { get; set; } = true;

    public bool EndByDate
    {
        get => !EndByCount;
        set => EndByCount = !value;
    }

    [ObservableProperty]
    public partial string SeriesCount { get; set; } = "6";

    [ObservableProperty]
    public partial string SeriesUntil { get; set; } = "";

    public string SeriesRuleHint => AfterLastVisit
        ? $"Gebucht ist immer nur der nächste Termin; ist er erledigt, bucht der Kalender den folgenden {SeriesInterval.Weeks} {(SeriesInterval.Weeks == 1 ? "Woche" : "Wochen")} danach."
        : $"Jeden {DayLayout.Local(Appointment.Start, Zone).ToString("dddd", CultureInfo.GetCultureInfo("de-AT"))} um {DayLayout.Local(Appointment.Start, Zone):HH:mm}, alle Termine werden gleich gebucht.";

    public ObservableCollection<SeriesRowViewModel> SeriesRows { get; } = [];

    public bool HasSeriesRows => SeriesRows.Count > 0;

    public string CreateSeriesText
    {
        get
        {
            var count = SeriesRows.Count(r => r.Take);
            return count == 1 ? "1 Termin buchen" : $"{count} Termine buchen";
        }
    }

    public bool CanCreateSeries => SeriesRows.Any(r => r.Take);

    [RelayCommand]
    private void EditSeries()
    {
        Error = null;
        SeriesRows.Clear();
        OnSeriesRowsChanged();
        SeriesUntil = DateOnly.FromDateTime(DayLayout.Local(Appointment.Start, Zone)).AddMonths(6).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        IsEditingSeries = true;
    }

    [RelayCommand]
    private void CancelSeries()
    {
        IsEditingSeries = false;
        SeriesRows.Clear();
        OnSeriesRowsChanged();
    }

    private SeriesInput? Rule()
    {
        int? count = null;
        DateOnly? until = null;
        if (EndByCount)
        {
            if (!int.TryParse(SeriesCount.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n is < 1 or > 52)
            {
                Error = "Bitte eine Anzahl zwischen 1 und 52 eingeben.";
                return null;
            }

            count = n;
        }
        else
        {
            var (endDate, dateError) = DateInputParser.ParseRequired(SeriesUntil, LocalToday);
            if (endDate is not { } d)
            {
                Error = dateError;
                return null;
            }

            until = d;
        }

        return new SeriesInput(SeriesInterval.Weeks, AfterLastVisit ? SeriesMode.AfterLastVisit : SeriesMode.Weekly, count, until);
    }

    [RelayCommand]
    private async Task PreviewSeriesAsync()
    {
        Error = null;
        if (Rule() is not { } rule)
        {
            return;
        }

        var result = await Api.PostAsync<List<SeriesOccurrence>>($"api/appointments/{Appointment.Id}/series/preview", rule).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        foreach (var row in SeriesRows)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        SeriesRows.Clear();
        foreach (var occurrence in result.Value)
        {
            var row = new SeriesRowViewModel(occurrence, Zone);
            row.PropertyChanged += OnRowChanged;
            SeriesRows.Add(row);
        }

        if (SeriesRows.Count == 0)
        {
            Error = "Bis zum Enddatum kommt kein weiterer Termin dazu.";
        }

        OnSeriesRowsChanged();
    }

    private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => OnSeriesRowsChanged();

    private void OnSeriesRowsChanged()
    {
        OnPropertyChanged(nameof(HasSeriesRows));
        OnPropertyChanged(nameof(CreateSeriesText));
        OnPropertyChanged(nameof(CanCreateSeries));
    }

    [RelayCommand]
    private async Task CreateSeriesAsync()
    {
        if (Rule() is not { } rule)
        {
            return;
        }

        var picks = SeriesRows.Where(r => r.Take && r.CanTake).Select(r => new SeriesPick(r.Occurrence.Start, r.Occurrence.EmployeeId)).ToList();
        var result = await Api.PostAsync<SeriesResult>($"api/appointments/{Appointment.Id}/series", new SeriesCreate(rule, picks)).ConfigureAwait(true);
        await SeriesDoneAsync(result).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CancelFollowingAsync()
    {
        var reason = string.IsNullOrWhiteSpace(CancelReason) ? "An der Rezeption storniert" : CancelReason.Trim();
        var result = await Api.PostAsync<SeriesResult>($"api/appointments/{Appointment.Id}/series/cancel-following",
            new CancelAppointmentRequest(reason)).ConfigureAwait(true);
        await SeriesDoneAsync(result).ConfigureAwait(true);
    }

    private async Task SeriesDoneAsync(ApiResult<SeriesResult> result)
    {
        if (result.Value is null)
        {
            Error = result.Error;
            return;
        }

        IsEditingSeries = false;
        await SeriesChanged!(result.Value).ConfigureAwait(true);
    }
}
