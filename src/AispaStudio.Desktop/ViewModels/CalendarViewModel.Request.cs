using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>KI-Anfrage (Strg+K) im Kalender: Seitenpanel mit Bausteinen und drei Vorschlägen, die zugleich im Kalender leuchten.</summary>
public partial class CalendarViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanel))]
    public partial CalendarRequestViewModel? Request { get; private set; }

    /// <summary>Wird gesetzt, sobald das Panel offen ist; die Ansicht setzt dann den Fokus ins Eingabefeld.</summary>
    public event Action? RequestOpened;

    /// <summary>Strg+K: Wunsch frei eintippen.</summary>
    [RelayCommand]
    private void OpenRequest()
    {
        if (!CanBook || _resources is null)
        {
            return;
        }

        Filter = null;
        ClosePanels();
        Ghost = null;
        var request = new CalendarRequestViewModel(_api, _resources, Date, _zone, BookProposalAsync, () =>
        {
            CloseRequest();
            Rebuild();
        }, searchFreeTime: () => OpenFreeTime(null));
        request.PropertyChanged += OnRequestChanged;
        Request = request;
        Rebuild();
        RequestOpened?.Invoke();
    }

    private void CloseRequest()
    {
        SlotMarks = [];
        if (Request is { } request)
        {
            request.PropertyChanged -= OnRequestChanged;
            Request = null;
        }
    }

    private void OnRequestChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CalendarRequestViewModel.Proposals) && Request?.Proposals is [var first, ..]
            && DateOnly.FromDateTime(DayLayout.Local(first.Proposal.Start, _zone)) is var day && day != Date)
        {
            // Der beste Vorschlag liegt an einem anderen Tag: dorthin wechseln, damit er im Kalender zu sehen ist.
            Date = day;
        }

        if (e.PropertyName is nameof(CalendarRequestViewModel.Proposals) or nameof(CalendarRequestViewModel.ActiveIndex))
        {
            UpdateSlotMarks();
        }
    }

    /// <summary>Die Vorschläge des Tages als nummerierte Flächen; der gewählte ist kräftig.</summary>
    private List<SlotMark> RequestMarks(CalendarRequestViewModel request)
    {
        var index = Columns.Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var marks = new List<SlotMark>();
        foreach (var row in request.Proposals.OrderBy(r => r == request.Active))
        {
            var proposal = row.Proposal;
            if (DateOnly.FromDateTime(DayLayout.Local(proposal.Start, _zone)) == Date
                && index.TryGetValue(ByRoom ? proposal.RoomId : proposal.EmployeeId, out var column))
            {
                marks.Add(Mark(column, proposal.Start, proposal.End, $"{row.Number} · {row.Time}", row == request.Active));
            }
        }

        return marks;
    }

    /// <summary>Vorschlag buchen: zum Tag wechseln, dann „Neuer Termin“ mit Behandlung, Person bzw. Raum und Uhrzeit.</summary>
    private async Task BookProposalAsync(CalendarProposal proposal, CustomerDto? guest)
    {
        if (_resources?.Treatments.FirstOrDefault(t => t.Id == proposal.TreatmentId) is { } treatment)
        {
            await BookSlotAsync(new AvailableSlot(proposal.Start, proposal.End, proposal.EmployeeId, proposal.RoomId, proposal.EquipmentId), treatment,
                guest, direct: guest is not null).ConfigureAwait(true);
        }
    }
}
