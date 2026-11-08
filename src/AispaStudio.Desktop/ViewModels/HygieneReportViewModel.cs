using System.Collections.ObjectModel;
using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Reiter „Hygiene“ nach web/src/reports/HygieneView.tsx: Tagesstart und Raumpflege abhaken, von der Leitung abnehmen (auch per PIN), Hygieneprotokoll speichern.</summary>
public sealed partial class HygieneReportViewModel(IWorkstationApi api, TimeProvider time, CurrentUser user) : ReportTabViewModel(api, time)
{
    private const string ApprovalReason = "Wählen Sie die Leitung und geben Sie ihre PIN ein.";

    public bool IsLeader => user.Rights.Contains(AccessRight.ManageLocation);

    public bool NeedsPin => !IsLeader;

    [ObservableProperty]
    public partial IReadOnlyList<QuickSwitchPerson> Approvers { get; private set; } = [];

    [ObservableProperty]
    public partial QuickSwitchPerson? SelectedApprover { get; set; }

    [ObservableProperty]
    public partial string Pin { get; set; } = "";

    public ObservableCollection<HygieneRoomRow> Rooms { get; } = [];

    public ObservableCollection<ReportCheckRow> CareTasks { get; } = [];

    public ObservableCollection<RoomCareApprovalRow> CareApprovals { get; } = [];

    public bool NoRooms => IsLoaded && Rooms.Count == 0;

    public bool NoCareTasks => IsLoaded && CareTasks.Count == 0;

    protected override async Task ReloadAsync()
    {
        if (Location is not { } location)
        {
            return;
        }

        var version = NextVersion();
        LoadError = null;
        var start = await Api.GetAsync<RoomStartDayDto>($"api/locations/{location.Id}/room-start?date={Iso(Date)}").ConfigureAwait(true);
        var care = await Api.GetAsync<RoomCareDay>($"api/locations/{location.Id}/room-care?date={Iso(Date)}").ConfigureAwait(true);
        if (version != Version)
        {
            return;
        }

        if (start.Value is not { } startDay || care.Value is not { } careDay)
        {
            LoadError = start.Error ?? care.Error ?? "Die Hygiene-Übersicht konnte nicht geladen werden.";
            Rooms.Clear();
            CareTasks.Clear();
            CareApprovals.Clear();
            ClearOverview();
            OnPropertyChanged(nameof(NoRooms));
            OnPropertyChanged(nameof(NoCareTasks));
            return;
        }

        if (NeedsPin)
        {
            // Die Leitung hängt am Standort: bei jedem Laden neu holen, sonst bestätigt die des vorigen Standorts.
            var approvers = await Api.GetAsync<List<QuickSwitchPerson>>($"api/locations/{location.Id}/room-approvers").ConfigureAwait(true);
            if (version != Version)
            {
                return;
            }

            Approvers = approvers.Value ?? [];
        }

        Apply(startDay, careDay);
        ApplyOverview(startDay, careDay);
        IsLoaded = true;
        OnPropertyChanged(nameof(NoRooms));
        OnPropertyChanged(nameof(NoCareTasks));
    }

    private void Apply(RoomStartDayDto start, RoomCareDay care)
    {
        ReportRows.Apply(Rooms, start.Rooms, r => r.RoomId, r => r.RoomId, r =>
        {
            var row = new HygieneRoomRow(r.RoomId, ToggleStartAsync, ApproveDayStartAsync);
            row.Update(r, Clock);
            return row;
        }, (row, r) => row.Update(r, Clock));
        ReportRows.Apply(CareTasks, care.Tasks, r => r.Id, t => t.AppointmentId, t =>
        {
            var row = new ReportCheckRow(t.AppointmentId, ToggleCareAsync);
            Update(row, t);
            return row;
        }, Update);
        var approvable = care.Rooms.Where(r => r.CareOpen + r.CareOverdue == 0 && r.CareApprovedAt is null && care.Tasks.Any(t => t.RoomId == r.RoomId)).ToList();
        ReportRows.Apply(CareApprovals, approvable, r => r.RoomId, r => r.RoomId, r => new RoomCareApprovalRow(r.RoomId, $"Raumpflege in {r.RoomName} abnehmen", ApproveCareAsync), (_, _) => { });
    }

    /// <summary>Status, Werte und Liste (R07 Slice 144): offene Punkte aus Tagesstart und Raumpflege, davon überfällige, und wie viele Räume schon abgenommen sind.</summary>
    private void ApplyOverview(RoomStartDayDto start, RoomCareDay care)
    {
        var overdue = care.Tasks.Count(t => t.State == RoomCareState.Overdue);
        var open = start.Rooms.Sum(r => r.Items.Count(i => !i.Done)) + care.Tasks.Count(t => t.State != RoomCareState.Done);
        var status = open == 0 ? "Alles erledigt" : $"{Plural(open, "Punkt", "Punkte")} offen{(overdue > 0 ? $", {overdue} überfällig" : "")}";
        SetOverview(
            status,
            open == 0,
            [],
            [
                new("Räume", start.Rooms.Count.ToString(CultureInfo.InvariantCulture)),
                new("Offen", open.ToString(CultureInfo.InvariantCulture)),
                new("Überfällig", overdue.ToString(CultureInfo.InvariantCulture)),
                new("Abgenommen", start.Rooms.Count(r => r.State == RoomStartState.Approved).ToString(CultureInfo.InvariantCulture)),
            ]);
    }

    private void Update(ReportCheckRow row, RoomCareTaskDto task)
    {
        row.Text = task.Text;
        row.Done = task.State == RoomCareState.Done;
        row.IsWarning = task.State == RoomCareState.Overdue;
        row.Detail = task.State switch
        {
            RoomCareState.Done => task.DoneAt is { } doneAt ? $"erledigt ({(task.DoneBy is { } by ? $"{by}, " : "")}{Clock(doneAt)})" : "erledigt",
            RoomCareState.Overdue => "überfällig",
            _ => "offen",
        };
    }

    private Task ToggleStartAsync(HygieneRoomRow room, ReportCheckRow item) =>
        RunAsync(Api.PutAsync<object>($"api/rooms/{room.RoomId}/start-checks/{item.Id}", new RoomStartCheckInput(Date, item.Done)));

    private Task ToggleCareAsync(ReportCheckRow row) =>
        RunAsync(Api.PutAsync<object>($"api/appointments/{row.Id}/room-care", new RoomCareCheckInput(row.Done)));

    private Task ApproveDayStartAsync(HygieneRoomRow room) => ApproveAsync(room.RoomId, RoomApprovalKind.DayStart);

    private Task ApproveCareAsync(RoomCareApprovalRow room) => ApproveAsync(room.RoomId, RoomApprovalKind.RoomCare);

    private async Task ApproveAsync(Guid roomId, RoomApprovalKind kind)
    {
        Message = null;
        Error = null;
        if (NeedsPin && (SelectedApprover is null || Pin.Length == 0))
        {
            Error = ApprovalReason;
            return;
        }

        var approval = SelectedApprover is { } approver && NeedsPin ? new SupervisorApproval(approver.UserId, Pin) : null;
        await RunAsync(Api.PostAsync<RoomApprovalDto>($"api/rooms/{roomId}/approvals", new RoomApprovalInput(Date, kind, approval)), "Abgenommen.").ConfigureAwait(true);
        Pin = "";
    }

    private async Task RunAsync<T>(Task<ApiResult<T>> request, string? done = null)
    {
        Message = null;
        Error = null;
        var result = await request.ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
        }
        else
        {
            Message = done;
        }

        await ReloadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private Task SaveLogAsync() => SaveFileAsync(
        $"api/locations/{Location!.Id}/hygiene-log.csv?from={Iso(Date.AddDays(-30))}&until={Iso(Date)}", "hygieneprotokoll.csv", "CSV-Datei", "*.csv");
}

/// <summary>Ein Raum im Tagesstart: Checkliste, Ausstattung und Abnahme.</summary>
public sealed partial class HygieneRoomRow(Guid roomId, Func<HygieneRoomRow, ReportCheckRow, Task> toggle, Func<HygieneRoomRow, Task> approve) : ViewModelBase
{
    public Guid RoomId { get; } = roomId;

    [ObservableProperty]
    public partial string Heading { get; private set; } = "";

    public ObservableCollection<ReportCheckRow> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEquipment))]
    public partial string? Equipment { get; private set; }

    [ObservableProperty]
    public partial bool EquipmentIsWarning { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> EquipmentLines { get; private set; } = [];

    [ObservableProperty]
    public partial bool CanApprove { get; private set; }

    public bool HasEquipment => Equipment is not null;

    public void Update(RoomStartRoomDto room, Func<DateTimeOffset, string> clock)
    {
        Heading = $"{room.RoomName}: {StateText(room.State)}{(room.ApprovedBy is { } by ? $" von {by}" : "")}";
        CanApprove = room.State == RoomStartState.Checked;
        var approved = room.State == RoomStartState.Approved;
        ReportRows.Apply(Items, room.Items, r => r.Id, i => i.ItemId, i =>
        {
            var row = new ReportCheckRow(i.ItemId, item => toggle(this, item));
            Update(row, i, approved, clock);
            return row;
        }, (row, i) => Update(row, i, approved, clock));
        Equipment = room.Equipment is { } equipment
            ? $"Ausstattung vollständig: {(equipment.Complete ? "ja" : $"{(equipment.Below.Count == 1 ? "1 Material" : $"{equipment.Below.Count} Materialien")} unter Minimum")}"
            : null;
        EquipmentIsWarning = room.Equipment is { Complete: false };
        EquipmentLines = room.Equipment is { Complete: false } below
            ? [.. below.Below.Select(l => $"{l.ItemName}: {StockFormat.Quantity(l.Quantity)} von {StockFormat.Quantity(l.Minimum)} {l.Unit}, {StockFormat.Quantity(l.Suggested)} {l.Unit} nachfüllen")]
            : [];
    }

    [RelayCommand]
    private Task ApproveAsync() => approve(this);

    private static void Update(ReportCheckRow row, RoomStartItemStateDto item, bool approved, Func<DateTimeOffset, string> clock)
    {
        row.Text = item.Text;
        row.Done = item.Done;
        row.CanToggle = !approved;
        row.Detail = item.Done && item.DoneAt is { } doneAt ? $"({(item.DoneBy is { } by ? $"{by}, " : "")}{clock(doneAt)})" : null;
    }

    private static string StateText(RoomStartState state) => state switch
    {
        RoomStartState.NoChecklist => "ohne Checkliste",
        RoomStartState.Open => "offen",
        RoomStartState.Partial => "teilweise abgehakt",
        RoomStartState.Checked => "freigegeben",
        _ => "abgenommen",
    };
}

/// <summary>Ein Raum, dessen Raumpflege erledigt ist und auf die Abnahme wartet.</summary>
public sealed partial class RoomCareApprovalRow(Guid roomId, string label, Func<RoomCareApprovalRow, Task> approve) : ViewModelBase
{
    public Guid RoomId { get; } = roomId;

    public string Label { get; } = label;

    [RelayCommand]
    private Task ApproveAsync() => approve(this);
}
