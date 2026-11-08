using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Krankmeldung aus dem Kalender und Ausfall-Assistent für die Termine einer abwesenden Person.</summary>
public partial class CalendarViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanel))]
    public partial AbsenceAssistantViewModel? Absence { get; private set; }

    /// <summary>Krank melden ändert den Dienstplan und braucht deshalb das Recht der Standortleitung.</summary>
    public bool CanReportSick => _user.Rights.Contains(AccessRight.ManageLocation);

    /// <summary>Aktion der Meldung „Abwesenheit“: der Ausfall-Assistent für diese Person, auch aus der Raumansicht (dort ohne Personenspalte).</summary>
    private void OpenAbsenceOf(Guid employeeId)
    {
        if (ByRoom)
        {
            ShowTeam();
        }

        if (Columns.FirstOrDefault(c => c.Id == employeeId) is { } column)
        {
            OpenAbsence(column);
        }
    }

    /// <summary>„3 Termine umbuchen“ im Spaltenkopf oder Rechtsklick: Ersatz für die Termine dieser Person vorschlagen.</summary>
    public void OpenAbsence(CalendarColumn column)
    {
        if (!CanBook || _resources is null)
        {
            return;
        }

        if (column.IsRoom)
        {
            OpenRoomClosureRebooking(column);
            return;
        }

        ClosePanels();
        Ghost = null;
        var assistant = new AbsenceAssistantViewModel(_api, _resources.Location.Id, column.Id, column.Title, Date, _zone,
            async moved =>
            {
                ApplyAll(moved);
                await LoadAsync().ConfigureAwait(true);
            },
            () =>
            {
                Absence = null;
                Rebuild();
            });
        Absence = assistant;
        _ = assistant.LoadAsync();
        Rebuild();
    }

    /// <summary>
    /// Krank melden für den angezeigten Tag: Der Dienstplan bekommt einen Krankenstand (sofort gültig), danach öffnet der
    /// Ausfall-Assistent für die offenen Termine.
    /// </summary>
    public async Task ReportSickAsync(CalendarColumn column)
    {
        if (!CanReportSick || _resources is null || column.IsRoom)
        {
            return;
        }

        var result = await _api.PutAsync<RosterDayResult>($"api/locations/{_resources.Location.Id}/roster/{column.Id}/{Date:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.Sick, null, null, null)], null)).ConfigureAwait(true);
        if (result.Value is null)
        {
            ShowProblem("Krankmeldung nicht gespeichert", result.Error, action: () => _ = ReportSickAsync(column));
            return;
        }

        await LoadAsync().ConfigureAwait(true);
        var open = result.Value.AppointmentsOutsideShift.Count(a => a.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn);
        ShowToast($"{column.Title} krank gemeldet");
        if (open > 0 && Columns.FirstOrDefault(c => c.Id == column.Id) is { } current)
        {
            OpenAbsence(current);
        }
    }

    /// <summary>Rechtsklick auf den Spaltenkopf einer Person.</summary>
    public IReadOnlyList<CalendarAction> ActionsForColumn(CalendarColumn column)
    {
        if (column.IsDay)
        {
            return [];
        }

        if (column.IsRoom)
        {
            return ActionsForRoomColumn(column);
        }

        if (!CanBook)
        {
            return [];
        }

        var actions = new List<CalendarAction>();
        if (column.Items.Any(i => i.Appointment is { Status: AppointmentStatus.Booked or AppointmentStatus.CheckedIn }))
        {
            actions.Add(new("Ersatz für die Termine suchen", null, () => OpenAbsence(column)));
        }

        if (CanReportSick && !column.IsSick)
        {
            actions.Add(new($"{column.Title} krank melden", null, () => _ = ReportSickAsync(column), IsDanger: true));
        }

        return actions;
    }
}
