using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Raum in der Auswahl der Raumsperre.</summary>
public sealed record RoomOption(Guid Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// Raumsperren (R05 Slice 95, ADR 0076): native Maske nach <c>RoomClosuresPage.tsx</c>. Ein Raum ist für Tage oder einen Zeitraum gesperrt (Wartung, Schaden, Umbau): in der Zeit bucht
/// der Planer ihn nicht; bestehende Termine im Raum bleiben, der Kalender meldet sie und die Umbuchungshilfe schlägt einen anderen Raum vor. Formular: Raum (nur beim Anlegen wählbar),
/// Zeitraum (mit Teil-Tagen), Art und Grund für das Team (höchstens 200 Zeichen). Verwalten darf, wer Standorte verwaltet. „Raum sperren …“ im Kalender öffnet ein neues Formular mit
/// dem Raum (<see cref="AdminTarget.Id"/>).
/// </summary>
public sealed partial class RoomClosuresViewModel : PeriodAreaViewModel
{
    public const int MaxReason = 200;

    public const string RoomField = "RoomField";

    public const string ReasonField = "ReasonField";

    private static readonly (RoomClosureKind Kind, string Label, string Short)[] KindNames =
    [
        (RoomClosureKind.Maintenance, "Wartung", "Wartung"),
        (RoomClosureKind.Damage, "Schaden (zum Beispiel Wasserschaden)", "Schaden"),
        (RoomClosureKind.Renovation, "Umbau oder Renovierung", "Umbau"),
        (RoomClosureKind.Other, "Sonstiges", "Sonstiges"),
    ];

    private readonly Dictionary<Guid, List<RoomOption>> _roomsByLocation = [];
    private List<RoomClosureDto> _closures = [];
    private RoomClosureDto? _current;

    public RoomClosuresViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time)
        : base(api, user, time, "Raumsperren", "Bitte den ersten gesperrten Tag wählen.", "Bitte den letzten gesperrten Tag wählen.") =>
        Kinds = [.. KindNames.Select(k => new KindOption(k.Kind, k.Label, () => Kind, value => Kind = (RoomClosureKind)value))];

    public override string NewText => "Neue Raumsperre";

    public override string HintText => "In der Sperre bucht der Planer den Raum nicht. Bestehende Termine bleiben, bis man sie umbucht.";

    public override string HelpTopic => "raumsperren";

    public override string HelpSection => "raumsperren";

    public override string SelectHint => "Links eine Raumsperre wählen oder mit „Neue Raumsperre“ eine anlegen.";

    public override string LoadFailedText => "Die Raumsperren konnten nicht geladen werden.";

    public override string NoLocationText => "Es gibt noch keinen Standort.";

    protected override string EmptyCurrentText => "Es ist kein Raum gesperrt.";

    protected override string EmptyEndedText => "Keine beendete Raumsperre.";

    protected override string SaveFailedText => "Die Raumsperre konnte nicht gespeichert werden. Bitte die markierten Felder prüfen.";

    protected override string FirstField => RoomField;

    protected override bool CanCreateNew => Rooms.Count > 0;

    public IReadOnlyList<KindOption> Kinds { get; }

    /// <summary>Die Räume des gewählten Standorts.</summary>
    public ObservableCollection<RoomOption> Rooms { get; } = [];

    /// <summary>Der Raum ist nur beim Anlegen wählbar; eine bestehende Sperre bleibt bei ihrem Raum.</summary>
    public bool CanSelectRoom => IsCreating;

    [ObservableProperty]
    public partial RoomOption? SelectedRoom { get; set; }

    [ObservableProperty]
    public partial RoomClosureKind Kind { get; set; }

    /// <summary>Grund für das Team, höchstens 200 Zeichen; steht im Kalender im Kopf des Raums.</summary>
    [ObservableProperty]
    public partial string Reason { get; set; } = "";

    [ObservableProperty]
    public partial string? RoomError { get; private set; }

    [ObservableProperty]
    public partial string? ReasonError { get; private set; }

    // ---------- Laden und Anzeigen ----------

    protected override async Task LoadExtraAsync(IReadOnlyList<LocationDto> locations)
    {
        var resources = await Task.WhenAll(locations.Select(l => Api.GetAsync<LocationResources>($"api/locations/{l.Id}/resources"))).ConfigureAwait(true);
        _roomsByLocation.Clear();
        for (var i = 0; i < locations.Count; i++)
        {
            _roomsByLocation[locations[i].Id] = [.. (resources[i].Value?.Rooms ?? []).Select(r => new RoomOption(r.Id, r.Name))];
        }
    }

    protected override async Task<string?> FetchAsync(Guid locationId)
    {
        var closures = await Api.GetAsync<List<RoomClosureDto>>($"api/locations/{locationId}/room-closures").ConfigureAwait(true);
        if (closures.Value is not { } list)
        {
            return closures.Error ?? LoadFailedText;
        }

        _closures = list;
        return null;
    }

    protected override IReadOnlyList<PeriodRow> BuildRows() =>
    [
        .. _closures.Select(c => new PeriodRow(c.Id, TabOf(c.Phase), $"{c.RoomName}, {KindShort(c.Kind)}", PeriodLabel(c),
            c.OpenAppointments > 0 ? $"{AppointmentsLabel(c.OpenAppointments)} offen" : c.Reason ?? "keine offenen Termine")),
    ];

    protected override Task OnLocationReadyAsync(Guid locationId)
    {
        Rooms.Clear();
        foreach (var room in _roomsByLocation.GetValueOrDefault(locationId) ?? [])
        {
            Rooms.Add(room);
        }

        NewCommand.NotifyCanExecuteChanged();
        return Task.CompletedTask;
    }

    protected override Guid? LocationFor(AdminTarget target) =>
        target.Id is { } room && _roomsByLocation.FirstOrDefault(l => l.Value.Any(r => r.Id == room)) is { Value: not null } found ? found.Key : target.Location;

    protected override bool OpensNew(AdminTarget target) =>
        base.OpensNew(target) || (target.Id is { } room && _roomsByLocation.Values.Any(rooms => rooms.Any(r => r.Id == room)));

    protected override void ApplyItem(Guid id)
    {
        _current = _closures.First(c => c.Id == id);
        Period.Set(_current.FromDate, _current.FromTime, _current.UntilDate, _current.UntilTime);
        if (Rooms.All(r => r.Id != _current.RoomId))
        {
            // Ein inzwischen entfernter Raum bleibt in der Anzeige stehen.
            Rooms.Add(new RoomOption(_current.RoomId, _current.RoomName));
        }

        SelectedRoom = Rooms.First(r => r.Id == _current.RoomId);
        Kind = _current.Kind;
        Reason = _current.Reason ?? "";
        ClearErrors();
        SetInfo($"{_current.RoomName}, {KindShort(_current.Kind)}, {PeriodLabel(_current)}", PhaseName(_current.Phase), OpenText(_current.OpenAppointments),
            $"Angelegt von {_current.CreatedBy} am {Stamp(_current.CreatedAt)}.");
    }

    protected override void ApplyBlank(AdminTarget? target)
    {
        _current = null;
        SelectedRoom = Rooms.FirstOrDefault(r => r.Id == target?.Id) ?? Rooms.FirstOrDefault();
        Kind = RoomClosureKind.Maintenance;
        Reason = "";
        ClearErrors();
    }

    protected override string FormSignature() => string.Join('|', SelectedRoom?.Id, Kind, Reason);

    protected override string? ValidateFields()
    {
        RoomError = SelectedRoom is null ? "Bitte den Raum wählen." : null;
        ReasonError = Reason.Trim().Length > MaxReason ? $"Höchstens {MaxReason} Zeichen." : null;
        return RoomError is not null ? RoomField : ReasonError is not null ? ReasonField : null;
    }

    protected override async Task<(Guid? Id, PeriodTab Tab, string? Error)> SendAsync(Guid locationId, Guid? id, PeriodValue period)
    {
        var body = new RoomClosureInput(SelectedRoom!.Id, period.From, period.FromTime, period.Until, period.UntilTime, Kind, Trimmed(Reason));
        var result = id is { } existing
            ? await Api.PutAsync<RoomClosureDto>($"api/locations/{locationId}/room-closures/{existing}", body).ConfigureAwait(true)
            : await Api.PostAsync<RoomClosureDto>($"api/locations/{locationId}/room-closures", body).ConfigureAwait(true);
        return result.Value is { } saved ? (saved.Id, TabOf(saved.Phase), null) : (null, PeriodTab.Current, result.Error);
    }

    protected override async Task<string?> DeleteItemAsync(Guid locationId, Guid id)
    {
        var result = await Api.DeleteAsync<object>($"api/locations/{locationId}/room-closures/{id}").ConfigureAwait(true);
        return result.Ok ? null : "Die Raumsperre konnte nicht gelöscht werden. Vielleicht gibt es sie nicht mehr.";
    }

    protected override ConfirmRequest DeleteRequest(Guid id) => new("Raumsperre löschen?",
        $"Die Raumsperre „{HeadingText}“ ist danach weg. Termine werden nicht angefasst; der Raum ist in der Zeit sofort wieder buchbar.", "Löschen");

    protected override void FormStateChanged() => OnPropertyChanged(nameof(CanSelectRoom));

    partial void OnSelectedRoomChanged(RoomOption? value)
    {
        RoomError = null;
        Touch();
    }

    partial void OnKindChanged(RoomClosureKind value)
    {
        foreach (var option in Kinds)
        {
            option.Refresh();
        }

        Touch();
    }

    partial void OnReasonChanged(string value)
    {
        ReasonError = null;
        Touch();
    }

    private static string PeriodLabel(RoomClosureDto c) => PeriodFormViewModel.Label(c.FromDate, c.FromTime, c.UntilDate, c.UntilTime);

    private static string KindShort(RoomClosureKind kind) => KindNames.First(k => k.Kind == kind).Short;

    private static string AppointmentsLabel(int count) => count == 1 ? "1 Termin" : $"{count} Termine";

    private void ClearErrors()
    {
        RoomError = null;
        ReasonError = null;
    }

    /// <summary>„1 Termin liegt im gesperrten Raum und bleibt gebucht, bis er umgebucht wird. Die Umbuchungshilfe steht im Kalender …“</summary>
    private static string? OpenText(int open) => open == 0
        ? null
        : $"{(open == 1 ? "1 Termin liegt" : $"{open} Termine liegen")} im gesperrten Raum und {(open == 1 ? "bleibt" : "bleiben")} gebucht, bis {(open == 1 ? "er" : "sie")} umgebucht "
            + $"{(open == 1 ? "wird" : "werden")}. Die Umbuchungshilfe steht im Kalender: in der Seitenleiste Meldungen auf „Umbuchen …“ klicken.";
}
