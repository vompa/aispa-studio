using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zeile des Raumchecks: „Massageöl neutral: 200 von 500 ml, 300 ml nachfüllen“ mit Menge und den Knöpfen „Aufgefüllt“ und „Reicht noch“.</summary>
public sealed partial class RestockRow(RestockLine line, bool canAnswer, Func<RestockRow, RestockState, Task> answer) : ViewModelBase
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public RestockLine Line { get; } = line;

    public bool IsOpen => Line.State == RestockState.Open;

    public bool IsRefilled => Line.State == RestockState.Refilled;

    public bool IsEnough => Line.State == RestockState.Enough;

    public bool CanAnswer => canAnswer;

    /// <summary>Beantwortete Zeilen stehen blass.</summary>
    public double RowOpacity => IsOpen ? 1 : 0.6;

    public string Unit => Line.Unit;

    public string Text => IsOpen
        ? $"{Line.ItemName}: {Number(Line.Quantity)} von {Number(Line.Minimum)} {Line.Unit}, {Number(Line.Suggested)} {Line.Unit} nachfüllen"
        : $"{Line.ItemName}: {Number(Line.Quantity)} von {Number(Line.Minimum)} {Line.Unit}";

    /// <summary>Menge für „Aufgefüllt“, vorbelegt mit der vorgeschlagenen.</summary>
    [ObservableProperty]
    public partial decimal? Quantity { get; set; } = line.Suggested;

    [RelayCommand(CanExecute = nameof(CanAnswer))]
    private Task RefillAsync() => answer(this, RestockState.Refilled);

    [RelayCommand(CanExecute = nameof(CanAnswer))]
    private Task EnoughAsync() => answer(this, RestockState.Enough);

    private static string Number(decimal value) => value.ToString("0.###", German);
}

/// <summary>
/// Seitenleiste „Raumpflege &lt;Raum&gt;“ (ADR 0068): die Materialien des Raumschranks unter ihrer Mindestmenge nach der Behandlung. Je Zeile „Aufgefüllt“ (bucht der Server
/// vom Hauptlager in den Raumschrank) oder „Reicht noch“; „Raumpflege erledigt“ ist erst aktiv, wenn keine Zeile mehr offen ist. Ohne Verbindung wird nur angezeigt.
/// </summary>
public sealed partial class RoomCarePanelViewModel : ViewModelBase, ISidePanel
{
    private readonly IWorkstationApi _api;
    private readonly Func<Task> _changed;
    private readonly Action<string> _warn;
    private readonly Action _close;
    private readonly bool _canCheck;
    private RoomCareTaskDto _task;

    public RoomCarePanelViewModel(IWorkstationApi api, RoomCareTaskDto task, bool canCheck, Func<Task> changed, Action<string> warn, Action close)
    {
        _api = api;
        _task = task;
        _canCheck = canCheck;
        _changed = changed;
        _warn = warn;
        _close = close;
        Rows = RowsOf(task);
    }

    public string PanelTitle => $"Raumpflege {_task.RoomName}";

    public string? PanelSubtitle => _task.Text;

    public PanelKind PanelKind => PanelKind.RoomCare;

    public string PanelIcon => Controls.Icons.Door;

    public string PanelTabText => "Raumpflege";

    public IReadOnlyList<RestockRow> Rows { get; private set; }

    public bool CanFinish => _canCheck && Rows.All(r => !r.IsOpen);

    [ObservableProperty]
    public partial string? Error { get; private set; }

    private IReadOnlyList<RestockRow> RowsOf(RoomCareTaskDto task) =>
        [.. (task.Restock ?? []).Select(l => new RestockRow(l, _canCheck, AnswerAsync))];

    private async Task AnswerAsync(RestockRow row, RestockState state)
    {
        if (_api.IsOffline)
        {
            _warn("Nur mit Verbindung");
            return;
        }

        Error = null;
        var result = await _api.PutAsync<RoomCareTaskDto>($"api/appointments/{_task.AppointmentId}/room-care/restock/{row.Line.ItemId}",
            new RestockAnswer(state, state == RestockState.Refilled ? row.Quantity : null)).ConfigureAwait(true);
        if (!result.Ok || result.Value is null)
        {
            Error = $"Nicht gespeichert: {result.Error}";
            return;
        }

        _task = result.Value;
        var entered = Rows.ToDictionary(r => r.Line.ItemId, r => r.Quantity);
        Rows = RowsOf(_task);
        foreach (var open in Rows.Where(r => r.IsOpen && entered.ContainsKey(r.Line.ItemId)))
        {
            open.Quantity = entered[open.Line.ItemId];
        }

        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(CanFinish));
        FinishCommand.NotifyCanExecuteChanged();
        await _changed().ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private async Task FinishAsync()
    {
        if (_api.IsOffline)
        {
            _warn("Nur mit Verbindung");
            return;
        }

        Error = null;
        var result = await _api.PutAsync<object>($"api/appointments/{_task.AppointmentId}/room-care", new RoomCareCheckInput(true)).ConfigureAwait(true);
        if (!result.Ok)
        {
            Error = $"Raumpflege nicht abgehakt: {result.Error}";
            return;
        }

        _close();
        await _changed().ConfigureAwait(true);
    }

    [RelayCommand]
    private void Close() => _close();
}
