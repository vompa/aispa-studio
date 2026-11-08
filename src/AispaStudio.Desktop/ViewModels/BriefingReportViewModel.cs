using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Reiter „Tagesstart“ nach web/src/reports/BriefingView.tsx, als Übersicht (R07 Slice 144, Ä352): oben der Status, eine Zeile Werte, das Personal als Zeitleiste von Öffnung bis Schluss,
/// die Kassa mit Anfangsbestand. Die KI-Zusammenfassung schreibt der Server im Hintergrund; solange sie aussteht, fragt der Reiter nach.
/// </summary>
public sealed partial class BriefingReportViewModel(IWorkstationApi api, TimeProvider time) : ReportTabViewModel(api, time)
{
    private const int AiAttempts = 15;

    private static readonly Regex Span = new(@"(\d{1,2}):(\d{2})\s*[–-]\s*(\d{1,2}):(\d{2})", RegexOptions.CultureInvariant);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AiSummary), nameof(AiNotice), nameof(HasAiSummary), nameof(IsAiPending), nameof(Reminders), nameof(ServingTotals), nameof(HasServings))]
    public partial BriefingDto? Briefing { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<ReportSection> Sections { get; private set; } = [];

    /// <summary>Das Personal des Tages, je Person eine Zeile der Zeitleiste.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStaff))]
    public partial IReadOnlyList<StaffBarRow> StaffBars { get; private set; } = [];

    [ObservableProperty]
    public partial string OpensText { get; private set; } = "";

    [ObservableProperty]
    public partial string ClosesText { get; private set; } = "";

    /// <summary>Kassen mit Anfangsbestand laut letztem Abschluss, als Wert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRegister))]
    public partial IReadOnlyList<ReportValue> RegisterValues { get; private set; } = [];

    /// <summary>Hinweise der Kassa (Signatur, Abschluss): sie stehen als Warnband, nicht als Fließtext.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRegister))]
    public partial IReadOnlyList<string> RegisterWarnings { get; private set; } = [];

    public ObservableCollection<ReportCheckRow> Servings { get; } = [];

    public string? AiSummary => Briefing?.AiSummary;

    public string? AiNotice => Briefing?.AiNotice;

    public bool HasAiSummary => Briefing?.AiSummary is not null;

    public bool IsAiPending => Briefing?.AiSummaryPending == true;

    public bool HasStaff => StaffBars.Count > 0;

    public bool HasRegister => RegisterValues.Count > 0 || RegisterWarnings.Count > 0;

    public IReadOnlyList<string> Reminders => [.. (Briefing?.Reminders ?? []).Select(r => r.Area is { } area ? $"{r.Title} ({area}): {r.Text}" : $"{r.Title}: {r.Text}")];

    public IReadOnlyList<string> ServingTotals => [.. (Briefing?.Servings ?? []).Select(t => $"{t.Count} × {t.Text}: {string.Join(", ", t.DueTimes.Select(Clock))}")];

    public bool HasServings => Briefing?.Servings is { Count: > 0 } || Servings.Count > 0;

    protected override async Task ReloadAsync()
    {
        if (Location is not { } location)
        {
            return;
        }

        var version = NextVersion();
        LoadError = null;
        for (var attempt = 1; attempt <= AiAttempts; attempt++)
        {
            var result = await Api.GetAsync<BriefingDto>($"api/reports/locations/{location.Id}/briefing?date={Iso(Date)}").ConfigureAwait(true);
            if (version != Version)
            {
                return;
            }

            if (result.Value is not { } briefing)
            {
                LoadError = result.Error ?? "Der Tagesstart konnte nicht geladen werden.";
                Sections = [];
                StaffBars = [];
                RegisterValues = [];
                RegisterWarnings = [];
                ClearOverview();
                Briefing = null;
                Servings.Clear();
                OnPropertyChanged(nameof(HasServings));
                return;
            }

            Apply(briefing);
            if (attempt == 1)
            {
                await LoadServingsAsync(version).ConfigureAwait(true);
            }

            if (!briefing.AiSummaryPending)
            {
                return;
            }

            await Task.Delay(PollInterval, Time).ConfigureAwait(true);
            if (version != Version)
            {
                return;
            }
        }
    }

    private void Apply(BriefingDto briefing)
    {
        Sections =
        [
            .. new[]
            {
                ("Gäste", briefing.Guests),
                ("Räume und Geräte", briefing.Rooms),
            }.Where(s => s.Item2.Count > 0).Select(s => new ReportSection(s.Item1, [.. s.Item2.Select(i => i.Text)])),
        ];
        var points = briefing.ActionNeeded.Select(i => i.Text).ToList();
        var onDuty = briefing.Staff.Count(IsOnDuty);
        SetOverview(
            points.Count == 0 ? "Nichts Dringendes offen" : Plural(points.Count, "Punkt braucht Aufmerksamkeit", "Punkte brauchen Aufmerksamkeit"),
            points.Count == 0,
            points,
            [
                new("Behandlungen", briefing.Appointments.ToString(CultureInfo.InvariantCulture)),
                new("Auslastung", $"{briefing.Utilization} %"),
                new("Im Dienst", onDuty.ToString(CultureInfo.InvariantCulture)),
                new("Abwesend", (briefing.Staff.Count - onDuty).ToString(CultureInfo.InvariantCulture)),
            ]);
        ApplyStaff(briefing.Staff);
        ApplyRegister(briefing);
        Briefing = briefing;
        IsLoaded = true;
    }

    /// <summary>Im Dienst ist, wer an diesem Tag Zeiten hat; wer abwesend ist, hat statt Zeiten einen Zustand („Urlaub“).</summary>
    private static bool IsOnDuty(BriefingStaff person) => person.Times is not null;

    /// <summary>
    /// Die Leiste reicht von der Öffnung bis zum Schluss des Standorts an diesem Wochentag; ohne Öffnungszeiten vom ersten Dienstbeginn bis zum letzten Dienstende. Ein Dienst
    /// außerhalb der Öffnungszeit weitet sie, damit er sichtbar bleibt.
    /// </summary>
    private void ApplyStaff(IReadOnlyList<BriefingStaff> staff)
    {
        var shifts = staff.ToDictionary(p => p.EmployeeId, p => p.Times is null ? [] : SpansOf(p.Times));
        var all = shifts.Values.SelectMany(s => s).ToList();
        var hours = Location?.OpeningHours.Where(h => h.Day == Date.DayOfWeek).ToList() ?? [];
        var starts = hours.Select(h => h.Opens.ToTimeSpan().TotalMinutes).Concat(all.Select(s => s.From)).ToList();
        var ends = hours.Select(h => h.Closes.ToTimeSpan().TotalMinutes).Concat(all.Select(s => s.To)).ToList();
        if (starts.Count == 0 || ends.Count == 0 || starts.Min() >= ends.Max())
        {
            OpensText = ClosesText = "";
            StaffBars = [.. staff.Select(p => new StaffBarRow(p.Name, p.State, p.Times, !IsOnDuty(p), IsOnDuty(p) ? null : p.State, []))];
            return;
        }

        var (from, until) = (starts.Min(), ends.Max());
        var length = until - from;
        OpensText = Minutes(from);
        ClosesText = Minutes(until);
        StaffBars =
        [
            .. staff.Select(p => new StaffBarRow(p.Name, p.State, p.Times, !IsOnDuty(p), IsOnDuty(p) ? null : p.State,
                [.. shifts[p.EmployeeId].Select(s => new StaffSpan((s.From - from) / length, (s.To - from) / length))])),
        ];
    }

    private static List<(double From, double To)> SpansOf(string times) =>
        [.. Span.Matches(times).Select(m => (Minute(m.Groups[1].Value, m.Groups[2].Value), Minute(m.Groups[3].Value, m.Groups[4].Value)))];

    private static double Minute(string hour, string minute) => (int.Parse(hour, CultureInfo.InvariantCulture) * 60) + int.Parse(minute, CultureInfo.InvariantCulture);

    private static string Minutes(double minutes) => $"{(int)(minutes / 60):00}:{(int)(minutes % 60):00}";

    /// <summary>
    /// Der Anfangsbestand und die Hinweise je Kassa kommen als Felder der API (<c>Registers</c>); der Betrag steht in der Währung der Kasse. Ein älterer Server ohne dieses Feld liefert nur
    /// die Sätze in <c>Register</c>: sie stehen dann alle als Warnband, damit nichts verschwindet, und die App liest keinen Betrag aus ihrem Wortlaut.
    /// </summary>
    private void ApplyRegister(BriefingDto briefing)
    {
        if (briefing.Registers is not { } registers)
        {
            (RegisterValues, RegisterWarnings) = ([], [.. briefing.Register.Select(i => i.Text)]);
            return;
        }

        RegisterValues = [.. registers.Select(r => new ReportValue($"Kassa {r.Code}: Anfangsbestand", r.OpeningCash is { } cash ? Money.Format(cash, r.Currency) : "kein Abschluss"))];
        RegisterWarnings = [.. registers.SelectMany(r => r.Warnings.Select(w => $"Kassa {r.Code}: {w}"))];
    }

    private async Task LoadServingsAsync(int version)
    {
        var result = await Api.GetAsync<ServingDayDto>($"api/locations/{Location!.Id}/servings?date={Iso(Date)}").ConfigureAwait(true);
        if (version != Version || result.Value is not { } day)
        {
            return;
        }

        ReportRows.Apply(Servings, day.Items, r => (r.ParentId, r.Id), i => (i.AppointmentId, i.ExtraId), i =>
        {
            var row = new ReportCheckRow(i.ExtraId, row => ToggleServingAsync(row, i.AppointmentId), i.AppointmentId);
            Update(row, i);
            return row;
        }, Update);
        OnPropertyChanged(nameof(HasServings));
    }

    private void Update(ReportCheckRow row, ServingItemDto item)
    {
        row.Text = string.Join(" · ", new[]
        {
            $"{Clock(item.DueAt)} {item.Text}: {item.CustomerName}, {item.RoomName}",
            item.Wishes is { } wishes ? $"Wunsch: {wishes}" : null,
            item.AnamnesisOnFile ? "Anamnese beachten" : null,
        }.Where(part => part is not null));
        row.Done = item.Done;
        row.Detail = item.Done && item.DoneAt is { } doneAt ? $"({(item.DoneBy is { } by ? $"{by}, " : "")}{Clock(doneAt)})" : null;
    }

    private async Task ToggleServingAsync(ReportCheckRow row, Guid appointmentId)
    {
        Error = null;
        var result = await Api.PutAsync<object>($"api/appointments/{appointmentId}/extras/{row.Id}/check", new ServingCheck(row.Done)).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
        }

        await LoadServingsAsync(Version).ConfigureAwait(true);
    }
}

/// <summary>Ein Dienstabschnitt als Anteil der Leiste (0 bis 1, von Öffnung bis Schluss).</summary>
public sealed record StaffSpan(double Start, double End);

/// <summary>Eine Person in der Zeitleiste des Tagesstarts. Wer abwesend ist, hat keine Abschnitte, sondern den Chip mit dem Zustand („Urlaub“).</summary>
public sealed record StaffBarRow(string Name, string State, string? Times, bool IsAbsent, string? Chip, IReadOnlyList<StaffSpan> Spans)
{
    public bool HasTimes => Times is not null;

    public string SpokenName => IsAbsent ? $"{Name}: {State}" : $"{Name}, Dienst {Times}";
}
