using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Vorschlag des Ausfall-Assistenten; ohne Ersatz lässt er sich nicht übernehmen.</summary>
public partial class AbsenceRow : ViewModelBase
{
    public AbsenceRow(AbsenceProposalItem item, TimeZoneInfo zone, bool withDate = false)
    {
        Item = item;
        Guest = item.Appointment.CustomerName;
        // Eine Raumsperre reicht über mehrere Tage: dann gehört der Tag zum Termin.
        var day = withDate ? DayLayout.ShortDay(DateOnly.FromDateTime(DayLayout.Local(item.Appointment.Start, zone))) + " " : "";
        Before = $"{day}{DayLayout.TimeText(item.Appointment.Start, item.Appointment.End, zone)} · {item.Appointment.TreatmentName}";
        After = item.Option switch
        {
            AbsenceOption.SameTime => $"gleiche Zeit bei {item.EmployeeName}, {item.RoomName}",
            AbsenceOption.OtherTime => $"{DayLayout.Local(item.Start!.Value, zone):HH:mm} bei {item.EmployeeName}, {item.RoomName}",
            AbsenceOption.OtherLocation => item.Start == item.Appointment.Start
                ? $"in {item.LocationName} zur gleichen Zeit bei {item.EmployeeName}, {item.RoomName}"
                : $"in {item.LocationName} um {DayLayout.Local(item.Start!.Value, zone):HH:mm} bei {item.EmployeeName}, {item.RoomName}",
            _ => withDate ? "kein anderer Raum an diesem Tag" : "kein Ersatz an diesem Tag",
        };
        CanApply = item.Option != AbsenceOption.None;

        // Ein anderer Standort ist ein größerer Einschnitt für den Gast: erst mit ihm sprechen, dann anhaken.
        IsChosen = CanApply && !IsOtherLocation;
    }

    public AbsenceProposalItem Item { get; }

    public bool IsOtherLocation => Item.Option == AbsenceOption.OtherLocation;

    public string Guest { get; }

    public string Before { get; }

    public string After { get; }

    public string Explanation => Item.Explanation;

    public bool CanApply { get; }

    public bool NeedsCall => !CanApply;

    [ObservableProperty]
    public partial bool IsChosen { get; set; }

    /// <summary>Nach dem Übernehmen: verschoben oder warum nicht.</summary>
    [ObservableProperty]
    public partial string? Outcome { get; set; }
}

/// <summary>
/// Ausfall-Assistent im Kalender: schlägt für die offenen Termine einer abwesenden Person Ersatz vor (gleiche Zeit bei jemand anderem,
/// sonst die nächste freie Zeit am selben Tag). Geht hier gar nichts, zeigt der Abschnitt „Andere Standorte“ Vorschläge von dort; sie sind nicht vorausgewählt,
/// weil der Gast zustimmen muss. Die Rezeption wählt aus und übernimmt; verschoben wird über den Server.
/// Dasselbe Fenster dient als Umbuchungshilfe für eine Raumsperre (ADR 0076): für die offenen Termine im gesperrten Raum ein anderer Raum zur gleichen Zeit,
/// sonst die nächste freie Zeit am selben Tag; übernommen wird über denselben Weg.
/// </summary>
public partial class AbsenceAssistantViewModel : ViewModelBase
{
    private readonly IWorkstationApi _api;
    private readonly Guid _locationId;
    private readonly Guid _employeeId;
    private readonly DateOnly _date;
    private readonly Guid? _roomClosureId;
    private readonly string _where = "";
    private readonly TimeZoneInfo _zone;
    private readonly Func<IReadOnlyList<AppointmentDto>, Task> _moved;
    private readonly Action _close;

    public AbsenceAssistantViewModel(IWorkstationApi api, Guid locationId, Guid employeeId, string name, DateOnly date, TimeZoneInfo zone,
        Func<IReadOnlyList<AppointmentDto>, Task> moved, Action close)
    {
        _api = api;
        _locationId = locationId;
        _employeeId = employeeId;
        _date = date;
        _zone = zone;
        _moved = moved;
        _close = close;
        Name = name;
    }

    /// <summary>Umbuchungshilfe für eine Raumsperre: <paramref name="name"/> ist der Raum, <paramref name="period"/> der Zeitraum der Sperre in Worten.</summary>
    public AbsenceAssistantViewModel(IWorkstationApi api, Guid locationId, Guid roomClosureId, string name, string period, TimeZoneInfo zone,
        Func<IReadOnlyList<AppointmentDto>, Task> moved, Action close)
        : this(api, locationId, Guid.Empty, name, default(DateOnly), zone, moved, close)
    {
        _roomClosureId = roomClosureId;
        _where = period;
    }

    public string Name { get; }

    public bool IsRoomClosure => _roomClosureId is not null;

    public string Title => IsRoomClosure ? $"Umbuchung aus {Name}" : $"Ersatz für {Name}";

    public string Where => IsRoomClosure ? _where : DayLayout.DateText(_date);

    /// <summary>Text, wenn nichts umzubuchen ist.</summary>
    public string EmptyText => IsRoomClosure ? "Keine offenen Termine im gesperrten Raum." : "Keine offenen Termine an diesem Tag.";

    /// <summary>Vorschläge am eigenen Standort (und die Termine ohne Ersatz).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows), nameof(IsEmpty))]
    public partial IReadOnlyList<AbsenceRow> Rows { get; private set; } = [];

    /// <summary>Vorschläge von anderen Standorten: eigener Abschnitt, nicht vorausgewählt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows), nameof(HasOtherRows), nameof(IsEmpty))]
    public partial IReadOnlyList<AbsenceRow> OtherRows { get; private set; } = [];

    public bool HasRows => Rows.Count > 0 || OtherRows.Count > 0;

    public bool HasOtherRows => OtherRows.Count > 0;

    public bool IsEmpty => !IsLoading && !HasRows && Error is null;

    /// <summary>Gäste per Mail über neue Zeit oder neue Person informieren (nur wer eine Mailadresse hat).</summary>
    [ObservableProperty]
    public partial bool NotifyCustomers { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    public partial string? Summary { get; private set; }

    public bool HasSummary => Summary is not null;

    public async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var result = _roomClosureId is { } closureId
                ? await _api.GetAsync<AbsenceProposal>($"api/locations/{_locationId}/room-closures/{closureId}/rebooking").ConfigureAwait(true)
                : await _api.PostAsync<AbsenceProposal>($"api/locations/{_locationId}/absence/proposal", new AbsenceProposalRequest(_employeeId, _date)).ConfigureAwait(true);
            if (result.Value is null)
            {
                Error = result.Error;
                return;
            }

            var rows = result.Value.Items.Select(i => new AbsenceRow(i, _zone, IsRoomClosure)).ToList();
            Rows = [.. rows.Where(r => !r.IsOtherLocation)];
            OtherRows = [.. rows.Where(r => r.IsOtherLocation)];
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Gewählte Vorschläge übernehmen; was der Server ablehnt, steht danach an der Zeile.</summary>
    [RelayCommand]
    public async Task ApplyAsync()
    {
        var chosen = Rows.Concat(OtherRows).Where(r => r.IsChosen && r.CanApply).ToList();
        if (chosen.Count == 0)
        {
            Error = "Bitte mindestens einen Vorschlag wählen.";
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            var moves = chosen.Select(r => new AbsenceMove(r.Item.Appointment.Id, r.Item.Start!.Value, r.Item.EmployeeId!.Value, r.Item.RoomId!.Value, r.Item.LocationId)).ToList();
            var result = await _api.PostAsync<AbsenceApplyResult>($"api/locations/{_locationId}/absence/apply",
                new AbsenceApplyRequest(moves, NotifyCustomers)).ConfigureAwait(true);
            if (result.Value is not { } applied)
            {
                Error = result.Error;
                return;
            }

            foreach (var row in chosen)
            {
                row.IsChosen = false;
                row.Outcome = applied.Failed.FirstOrDefault(f => f.AppointmentId == row.Item.Appointment.Id)?.Message is { } message
                    ? $"Nicht verschoben: {message}"
                    : "Verschoben";
            }

            Summary = $"{Count(applied.Moved.Count, "Termin", "Termine")} umgebucht"
                + (applied.Failed.Count > 0 ? $", {applied.Failed.Count} nicht" : "")
                + (applied.Notified > 0 ? $", {Count(applied.Notified, "Gast", "Gäste")} per Mail informiert." : ".");
            await _moved(applied.Moved).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Count(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n} {many}";

    [RelayCommand]
    private void Close() => _close();
}
