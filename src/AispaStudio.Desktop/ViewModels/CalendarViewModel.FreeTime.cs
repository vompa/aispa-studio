using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>„Freie Zeit suchen“ im Kalender: Seitenpanel mit der Liste, im Kalender leuchten die passenden Zeiten.</summary>
public partial class CalendarViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanel))]
    public partial FreeTimeViewModel? FreeTime { get; private set; }

    /// <summary>Freie Zeiten als Flächen über den Spalten; die gewählte ist kräftig und beschriftet.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SlotMark> SlotMarks { get; private set; } = [];

    /// <summary>Strg+N: freie Zeit suchen, vorbelegt mit der Behandlung des markierten Termins.</summary>
    [RelayCommand]
    private void OpenFreeTime() => OpenFreeTime(null);

    /// <summary>
    /// „Freie Zeit suchen“ aus der Warteliste, dem Posteingang oder dem Assistenten: mit Behandlung, Person, Raum, Gast und Tageszeit vorbelegt.
    /// <paramref name="day"/> ist der Tag, ab dem gesucht wird; der Kalender wechselt dorthin.
    /// </summary>
    public async Task ShowFreeTimeAsync(FreeTimePrefill prefill, DateOnly? day = null)
    {
        if (day is { } target && target != Date)
        {
            Date = target;
            await LoadAsync().ConfigureAwait(true);
        }

        OpenFreeTime(prefill);
    }

    private void OpenFreeTime(FreeTimePrefill? prefill)
    {
        if (!CanBook || _resources is null)
        {
            return;
        }

        // Freie Zeiten leuchten in den Spalten eines Tages: erst zurück zur Tagesansicht.
        Filter = null;
        var treatment = prefill is null && Marked is { } marked ? _resources.Treatments.FirstOrDefault(t => t.Id == marked.TreatmentId) : null;
        ClosePanels();
        Ghost = null;
        var freeTime = new FreeTimeViewModel(_api, _resources, Date, _zone, IsToday ? _time.GetUtcNow() : DateTimeOffset.MinValue,
            BookSlotAsync, () =>
            {
                CloseFreeTime();
                Rebuild();
            }, treatment, prefill);
        freeTime.PropertyChanged += OnFreeTimeChanged;
        FreeTime = freeTime;
        Rebuild();
    }

    private void CloseFreeTime()
    {
        // Erst die Markierungen, dann die Seite: ein Reiter, der danach wieder aufgeht, setzt seine eigenen.
        SlotMarks = [];
        if (FreeTime is { } freeTime)
        {
            freeTime.PropertyChanged -= OnFreeTimeChanged;
            FreeTime = null;
        }
    }

    private void OnFreeTimeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FreeTimeViewModel.Slots) or nameof(FreeTimeViewModel.ActiveIndex))
        {
            UpdateSlotMarks();
        }
    }

    /// <summary>
    /// Aufeinanderfolgende freie Zeiten derselben Spalte werden zu einer Fläche zusammengefasst, damit der Kalender ruhig bleibt.
    /// Die gewählte Zeit liegt als eigener Rahmen darüber.
    /// </summary>
    private void UpdateSlotMarks()
    {
        if (Request is { } request && Columns.Count > 0)
        {
            SlotMarks = RequestMarks(request);
            return;
        }

        if (FreeTime is not { } freeTime || Columns.Count == 0)
        {
            SlotMarks = [];
            return;
        }

        var marks = new List<SlotMark>();
        var index = Columns.Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
        foreach (var column in freeTime.Slots.Where(r => r.OnThisDay).GroupBy(r => ByRoom ? r.Slot.RoomId : r.Slot.EmployeeId))
        {
            if (!index.TryGetValue(column.Key, out var i))
            {
                continue;
            }

            DateTimeOffset? from = null;
            var until = DateTimeOffset.MinValue;
            foreach (var slot in column.Select(r => r.Slot).DistinctBy(s => s.Start).OrderBy(s => s.Start))
            {
                if (from is not null && slot.Start > until)
                {
                    marks.Add(Mark(i, from.Value, until, null, isActive: false));
                    from = null;
                }

                from ??= slot.Start;
                until = slot.End > until ? slot.End : until;
            }

            if (from is not null)
            {
                marks.Add(Mark(i, from.Value, until, null, isActive: false));
            }
        }

        if (freeTime.Active is { OnThisDay: true } active && index.TryGetValue(ByRoom ? active.Slot.RoomId : active.Slot.EmployeeId, out var activeColumn))
        {
            marks.Add(Mark(activeColumn, active.Slot.Start, active.Slot.End, $"{active.Time} · {(ByRoom ? active.Who : active.Room)}", isActive: true));
        }

        SlotMarks = marks;
    }

    private SlotMark Mark(int column, DateTimeOffset from, DateTimeOffset until, string? text, bool isActive) =>
        new(DayLayout.ColumnLeft(column, Scale), DayLayout.Top(_dayStart, from, Scale), Math.Max(18, (until - from).TotalMinutes * Scale.PixelsPerMinute),
            Scale.ItemWidth, text, isActive);

    /// <summary>Ist gebucht, ist die Suche erledigt: ihr Reiter fällt weg, statt mit der alten Liste wiederzukommen.</summary>
    private async Task BookSlotAsync(FreeTimeBooking booking)
    {
        if (await TryBookSlotAsync(booking.Slot, booking.Treatment, booking.Guest, booking.Direct, booking.Note).ConfigureAwait(true))
        {
            CloseWithoutNextTab(CloseFreeTime);
            Rebuild();
        }
    }

    /// <summary>
    /// Gewählte freie Zeit buchen: „Neuer Termin“ mit Behandlung, Person bzw. Raum und genauer Uhrzeit, an dem Tag, an dem sie liegt.
    /// Mit Gast ist er schon eingetragen; mit <paramref name="direct"/> wird gleich gebucht (der Klick auf „Für … buchen“ ist die Bestätigung),
    /// bei einer Ablehnung bleibt „Neuer Termin“ mit der Begründung offen.
    /// </summary>
    private async Task BookSlotAsync(AvailableSlot slot, TreatmentDto treatment, CustomerDto? guest = null, bool direct = false) =>
        await TryBookSlotAsync(slot, treatment, guest, direct).ConfigureAwait(true);

    /// <summary>Wie <see cref="BookSlotAsync(AvailableSlot, TreatmentDto, CustomerDto?, bool)"/>; <c>true</c>, wenn der Termin gebucht ist.</summary>
    private async Task<bool> TryBookSlotAsync(AvailableSlot slot, TreatmentDto treatment, CustomerDto? guest = null, bool direct = false, string? note = null)
    {
        var day = DateOnly.FromDateTime(DayLayout.Local(slot.Start, _zone));
        if (day != Date)
        {
            Date = day;
            await LoadAsync().ConfigureAwait(true);
        }

        var column = Columns.FirstOrDefault(c => c.Id == (ByRoom ? slot.RoomId : slot.EmployeeId));
        if (column is null)
        {
            return false;
        }

        StartBooking(column, DayLayout.Top(_dayStart, slot.Start, Scale));
        if (Booking is not { } booking)
        {
            return false;
        }

        booking.Treatment = booking.Treatments.FirstOrDefault(t => t.Id == treatment.Id) ?? booking.Treatment;
        booking.StartText = $"{DayLayout.Local(slot.Start, _zone):HH:mm}";
        booking.Note = note ?? booking.Note;
        if (guest is not null)
        {
            booking.Customers.Insert(0, guest);
            booking.Customer = guest;
        }

        if (direct && guest is not null && booking.BookCommand.CanExecute(null))
        {
            await booking.BookCommand.ExecuteAsync(null).ConfigureAwait(true);
            if (Booking is null)
            {
                ShowToast($"Gebucht: {guest.InHouseName}, {DayLayout.DateText(day)} {DayLayout.Local(slot.Start, _zone):HH:mm}");

                return true;
            }
        }

        return false;
    }
}

/// <summary>Freie Zeit im Kalender, in Pixeln über den Spalten.</summary>
public sealed record SlotMark(double Left, double Top, double Height, double Width, string? Text, bool IsActive);
