using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Reiter „Mein Tag“ nach web/src/reports/DayPlanView.tsx: Tagesplan einer Person mit einer Behandlungskarte je Termin.</summary>
public sealed partial class DayPlanReportViewModel(IWorkstationApi api, TimeProvider time, Guid? ownEmployeeId) : ReportTabViewModel(api, time)
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChoosePerson))]
    public partial IReadOnlyList<ReportPersonChoice> People { get; private set; } = [];

    [ObservableProperty]
    public partial ReportPersonChoice? SelectedPerson { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoPerson))]
    public partial Guid? EmployeeId { get; private set; } = ownEmployeeId;

    [ObservableProperty]
    public partial string Heading { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoCards))]
    public partial IReadOnlyList<TreatmentCardRow> Cards { get; private set; } = [];

    public bool CanChoosePerson => People.Count > 0;

    public bool HasNoPerson => EmployeeId is null;

    public bool NoCards => IsLoaded && Cards.Count == 0;

    public bool HasUnsavedNotes => Cards.Any(c => c.HasUnsavedNote);

    /// <summary>Personenwahl nur mit dem Recht „Buchen“; sonst sieht jede Person ihren eigenen Tag.</summary>
    public void SetPeople(IReadOnlyList<EmployeeDto> employees)
    {
        People = [.. employees.Select(e => new ReportPersonChoice(e.Id, e.InHouseName()))];
        EmployeeId ??= People.Count > 0 ? People[0].Id : null;
        SelectedPerson = People.FirstOrDefault(p => p.Id == EmployeeId);
    }

    public async Task<bool> SaveNotesAsync()
    {
        var saved = true;
        foreach (var card in Cards.Where(c => c.HasUnsavedNote))
        {
            saved &= await card.SaveNoteAsync().ConfigureAwait(true);
        }

        return saved;
    }

    public void DiscardNotes()
    {
        foreach (var card in Cards)
        {
            card.DiscardNote();
        }
    }

    protected override async Task ReloadAsync()
    {
        if (Location is not { } location || EmployeeId is not { } employeeId)
        {
            return;
        }

        var version = NextVersion();
        LoadError = null;
        var result = await Api.GetAsync<DayPlanDto>($"api/reports/locations/{location.Id}/day-plan?date={Iso(Date)}&employeeId={employeeId}").ConfigureAwait(true);
        if (version != Version)
        {
            return;
        }

        if (result.Value is not { } plan)
        {
            LoadError = result.Error ?? "Der Tagesplan konnte nicht geladen werden.";
            Heading = "";
            Cards = [];
            ClearOverview();
            return;
        }

        Heading = $"{plan.EmployeeName}, {StockFormat.Date(plan.Date)}";
        var notes = Cards.Where(c => c.HasUnsavedNote).ToDictionary(c => c.AppointmentId, c => c.Note);
        IsLoaded = true;
        Cards = [.. plan.Cards.Select(card => new TreatmentCardRow(card, Api, Clock, Day, ReloadAsync, notes.GetValueOrDefault(card.AppointmentId)))];
        ApplyOverview(plan);
    }

    /// <summary>Status, Werte und Liste (R07 Slice 144): die Hinweise der API zuerst, dann Behandlungen, offene Vorbereitung und Dienst.</summary>
    private void ApplyOverview(DayPlanDto plan)
    {
        var points = plan.Hints.ToList();
        var (status, ok) = (points.Count, plan.Cards.Count) switch
        {
            (0, 0) => ("Keine Behandlungen heute", true),
            (0, _) => ("Nichts Dringendes offen", true),
            _ => (Plural(points.Count, "Hinweis zum Tag", "Hinweise zum Tag"), false),
        };
        SetOverview(
            status,
            ok,
            points,
            [
                new("Behandlungen", Cards.Count.ToString(CultureInfo.InvariantCulture)),
                new("Vorbereitung offen", Cards.Sum(c => c.Todo.Count(t => !t.Done)).ToString(CultureInfo.InvariantCulture)),
                new("Dienst", plan.Shifts.Count == 0 ? "kein Dienst" : string.Join(", ", plan.Shifts.Select(s => $"{Clock(s.Start)}–{Clock(s.End)}"))),
            ]);
    }

    partial void OnSelectedPersonChanged(ReportPersonChoice? value)
    {
        if (value is not null && value.Id != EmployeeId)
        {
            EmployeeId = value.Id;
            _ = ReloadAsync();
        }
    }
}

public sealed record ReportPersonChoice(Guid Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Eine Behandlungskarte: Vorbereitung abhaken, Vorlieben des Gastes und die Notiz für die nächste Behandlung bearbeiten.</summary>
public sealed partial class TreatmentCardRow : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly Func<Task> _reload;
    private readonly Guid _customerId;
    private string _savedNote;

    public TreatmentCardRow(AispaStudio.Contracts.TreatmentCard card, IWorkstationApi api, Func<DateTimeOffset, string> clock, Func<DateTimeOffset, string> day, Func<Task> reload, string? unsavedNote)
    {
        (_api, _reload, _customerId) = (api, reload, card.CustomerId);
        AppointmentId = card.AppointmentId;
        Title = $"{clock(card.Start)} {card.TreatmentName} ({card.DurationMinutes} Min.), {card.RoomName}";
        var visit = card.VisitNumber == 1 ? "Erstbesuch" : $"{card.VisitNumber}. Besuch";
        var last = card.LastVisit is { } lastVisit ? $", zuletzt {day(lastVisit)}" : "";
        var occasion = card.Occasion is { } text ? $" · {text}" : "";
        Guest = $"{card.CustomerName} · {visit}{last}{occasion}";
        Anamnesis = card.AnamnesisStatus;
        IsAnamnesisWarning = !card.AnamnesisStatus.StartsWith("Fragebogen vom", StringComparison.Ordinal);
        BookingNote = card.BookingNote is { } note ? $"Hinweis zur Buchung: {note}" : null;
        Todo = [.. card.Todo.Where(t => t.StepId is not null).Select(t => new ReportCheckRow(t.StepId!.Value, ToggleStepAsync)
        {
            Text = t.Text,
            Done = t.Done,
            Detail = t.DueAt is { } due ? $"ab {clock(due)}" : null,
        })];
        TodoNotes = [.. card.Todo.Where(t => t.StepId is null).Select(t => $"{t.Text} ({t.Source})")];
        History = [.. card.History.Select(h => $"{day(h.Start)} {h.TreatmentName} bei {h.EmployeeName}{(h.Note is { } n ? $": {n}" : "")}")];
        _savedNote = card.FollowUpNote ?? "";
        Note = unsavedNote ?? _savedNote;
        LoadPreferences(card.Preferences);
    }

    public Guid AppointmentId { get; }

    public string Title { get; }

    public string Guest { get; }

    public string Anamnesis { get; }

    public bool IsAnamnesisWarning { get; }

    public string? BookingNote { get; }

    public bool HasBookingNote => BookingNote is not null;

    public IReadOnlyList<ReportCheckRow> Todo { get; }

    public IReadOnlyList<string> TodoNotes { get; }

    public IReadOnlyList<string> History { get; }

    public bool HasHistory => History.Count > 0;

    [ObservableProperty]
    public partial string Note { get; set; } = "";

    public bool HasUnsavedNote => Note != _savedNote;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEditing))]
    public partial bool IsEditingPreferences { get; set; }

    public bool IsNotEditing => !IsEditingPreferences;

    [ObservableProperty]
    public partial string? Pressure { get; set; }

    [ObservableProperty]
    public partial string? Scent { get; set; }

    [ObservableProperty]
    public partial string? RoomTemperature { get; set; }

    [ObservableProperty]
    public partial string? Music { get; set; }

    [ObservableProperty]
    public partial string? FocusAreas { get; set; }

    [ObservableProperty]
    public partial string? AvoidAreas { get; set; }

    [ObservableProperty]
    public partial string? Other { get; set; }

    [ObservableProperty]
    public partial bool PrefersQuiet { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; set; }

    public bool HasMessage => Message is not null;

    public bool HasError => Error is not null;

    public void DiscardNote() => Note = _savedNote;

    public async Task<bool> SaveNoteAsync()
    {
        (Message, Error) = (null, null);
        var result = await _api.PutAsync<object>($"api/reports/appointments/{AppointmentId}/follow-up", new FollowUpNote(Note)).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return false;
        }

        _savedNote = Note;
        Message = "Notiz gespeichert.";
        return true;
    }

    [RelayCommand]
    private async Task SaveNote() => await SaveNoteAsync().ConfigureAwait(true);

    [RelayCommand]
    private void EditPreferences() => IsEditingPreferences = true;

    [RelayCommand]
    private void CancelPreferences() => IsEditingPreferences = false;

    [RelayCommand]
    private async Task SavePreferencesAsync()
    {
        (Message, Error) = (null, null);
        var preferences = new CustomerPreferences(Blank(Pressure), Blank(Scent), Blank(RoomTemperature), Blank(Music), PrefersQuiet, Blank(FocusAreas), Blank(AvoidAreas), Blank(Other));
        var result = await _api.PutAsync<CustomerPreferences>($"api/reports/customers/{_customerId}/preferences", preferences).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error ?? "Die Vorlieben konnten nicht gespeichert werden.";
            return;
        }

        IsEditingPreferences = false;
        Message = "Vorlieben gespeichert.";
        await _reload().ConfigureAwait(true);
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private void LoadPreferences(CustomerPreferences preferences)
    {
        (Pressure, Scent, RoomTemperature, Music) = (preferences.Pressure, preferences.Scent, preferences.RoomTemperature, preferences.Music);
        (FocusAreas, AvoidAreas, Other, PrefersQuiet) = (preferences.FocusAreas, preferences.AvoidAreas, preferences.Other, preferences.PrefersQuiet == true);
    }

    private async Task ToggleStepAsync(ReportCheckRow row)
    {
        Error = null;
        var result = await _api.PutAsync<object>($"api/reports/appointments/{AppointmentId}/preparation/{row.Id}", new PreparationCheck(row.Done)).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            row.Done = !row.Done;
        }
    }
}
