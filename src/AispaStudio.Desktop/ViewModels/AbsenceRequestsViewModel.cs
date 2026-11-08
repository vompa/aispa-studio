using System.Globalization;
using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein offener Abwesenheitsantrag mit „Genehmigen“ und „Ablehnen“.</summary>
public sealed class AbsenceRequestRow
{
    public AbsenceRequestRow(AbsenceRequestDto request, TeamContext context, Func<AbsenceRequestRow, Task> approve, Func<AbsenceRequestRow, Task> reject)
    {
        Request = request;
        Name = request.EmployeeName;
        KindText = RosterPlanViewModel.KindLabel(request.Kind);
        PeriodText = request.From == request.Until ? Day(request.From) : $"{Day(request.From)} bis {Day(request.Until)}";
        Note = request.Note;
        HasNote = !string.IsNullOrWhiteSpace(request.Note);
        RequestedText = $"Beantragt am {context.DateText(request.RequestedAt)}";
        AffectedText = request.AffectedAppointments switch
        {
            0 => null,
            1 => "In dieser Zeit liegt 1 Termin.",
            var n => $"In dieser Zeit liegen {n} Termine.",
        };
        HasAffected = AffectedText is not null;
        ApproveCommand = new AsyncRelayCommand(() => approve(this));
        RejectCommand = new AsyncRelayCommand(() => reject(this));
    }

    public AbsenceRequestDto Request { get; }

    public string Name { get; }

    public string KindText { get; }

    public string PeriodText { get; }

    /// <summary>„Urlaub, 8.3.2027 bis 12.3.2027“.</summary>
    public string Description => $"{KindText}, {PeriodText}";

    public string? Note { get; }

    public bool HasNote { get; }

    public string RequestedText { get; }

    public string? AffectedText { get; }

    public bool HasAffected { get; }

    /// <summary>„Urlaub von Lena Huber, 3.11.2026 bis 5.11.2026“ als Name der Knöpfe, damit der Screenreader weiß, welcher Antrag gemeint ist.</summary>
    public string Summary => $"{KindText} von {Name}, {PeriodText}";

    public string ApproveName => $"{Summary} genehmigen";

    public string RejectName => $"{Summary} ablehnen";

    public IAsyncRelayCommand ApproveCommand { get; }

    public IAsyncRelayCommand RejectCommand { get; }

    private static string Day(DateOnly date) => date.ToString("d.M.yyyy", CultureInfo.InvariantCulture);
}

/// <summary>
/// Offene Abwesenheitsanträge aus der Handy-App (R05 Slice 99, Ä191): Genehmigen oder Ablehnen. Die API dazu kommt aus Slice 106 (<c>GET api/absence-requests</c>, <c>POST …/approve</c>,
/// <c>POST …/reject</c>); fehlt die Route auf einem älteren Server (HTTP 404), ist die Liste leer. Jeder andere Fehler wird gemeldet, nicht als „leer“ gezeigt. Genehmigen fragt nur, wenn Termine der
/// Person in der Zeit liegen (Zahl aus <see cref="AbsenceRequestDto.AffectedAppointments"/>); Ablehnen fragt immer, denn die Entscheidung lässt sich nicht zurücknehmen.
/// </summary>
public sealed partial class AbsenceRequestsViewModel : ViewModelBase
{
    private readonly TeamContext _context;
    private bool _loaded;

    public AbsenceRequestsViewModel(TeamContext context) => _context = context;

    /// <summary>Eine Genehmigung hat den Dienstplan geändert.</summary>
    public event Action? Approved;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(CountText))]
    public partial IReadOnlyList<AbsenceRequestRow> Rows { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoaded { get; private set; }

    public bool IsEmpty => IsLoaded && Rows.Count == 0 && !HasLoadError;

    public string CountText => Rows.Count == 1 ? "1 offener Antrag" : $"{Rows.Count} offene Anträge";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(IsEmpty))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>Ergebnis der letzten Entscheidung („Urlaub von Lena Huber ist genehmigt.“) oder ihr Fehler.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessageSeverity))]
    public partial bool MessageIsError { get; private set; }

    public InfoBarSeverity MessageSeverity => MessageIsError ? InfoBarSeverity.Error : InfoBarSeverity.Success;

    public bool HasMessage => Message is not null;

    public Task EnsureLoadedAsync() => _loaded ? Task.CompletedTask : LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        _loaded = true;
        LoadError = null;
        var result = await _context.Api.GetAsync<List<AbsenceRequestDto>>("api/absence-requests").ConfigureAwait(true);
        if (result.Status == HttpStatusCode.NotFound)
        {
            // Ein älterer Server hat die Route noch nicht; dort gibt es keine Anträge.
            Rows = [];
        }
        else if (result.Value is not { } requests)
        {
            LoadError = result.Error ?? "Die Anträge wurden nicht geladen.";
            Rows = [];
        }
        else
        {
            Rows = [.. requests.OrderBy(r => r.RequestedAt).Select(r => new AbsenceRequestRow(r, _context, ApproveAsync, RejectAsync))];
        }

        IsLoaded = true;
    }

    private async Task ApproveAsync(AbsenceRequestRow row)
    {
        var affected = row.Request.AffectedAppointments;
        if (affected > 0)
        {
            var answer = await AskAsync(new ConfirmRequest("Abwesenheit genehmigen?",
                (affected == 1 ? $"1 Termin von {row.Name} liegt in dieser Zeit und muss danach umgebucht werden. " : $"{affected} Termine von {row.Name} liegen in dieser Zeit und müssen danach umgebucht werden. ")
                + $"{row.KindText} {row.PeriodText} wird im Dienstplan eingetragen.", "Genehmigen", IsDestructive: false)).ConfigureAwait(true);
            if (!answer.IsConfirmed)
            {
                return;
            }
        }

        if (await DecideAsync(row, "approve", $"{row.Summary} ist genehmigt.").ConfigureAwait(true))
        {
            Approved?.Invoke();
        }
    }

    private async Task RejectAsync(AbsenceRequestRow row)
    {
        var answer = await AskAsync(new ConfirmRequest("Antrag ablehnen?",
            $"{row.KindText} {row.PeriodText} von {row.Name} wird nicht genehmigt. Im Dienstplan ändert sich nichts.", "Ablehnen")).ConfigureAwait(true);
        if (answer.IsConfirmed)
        {
            await DecideAsync(row, "reject", $"{row.Summary} ist abgelehnt.").ConfigureAwait(true);
        }
    }

    private async Task<bool> DecideAsync(AbsenceRequestRow row, string decision, string done)
    {
        Message = null;
        var result = await _context.Api.PostAsync<object>($"api/absence-requests/{row.Request.Id}/{decision}", null).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            MessageIsError = true;
            Message = error;
            return false;
        }

        Rows = [.. Rows.Where(r => r != row)];
        MessageIsError = false;
        Message = done;
        return true;
    }

    [RelayCommand]
    private void CloseMessage() => Message = null;
}
