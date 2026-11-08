using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Neue Einträge an einer Stelle des Kalenders: ein gemeinsames Fenster mit Umschaltleiste (<see cref="NewEntryViewModel"/>).
/// Ein Klick auf eine freie Stelle schlägt die Art nach der freien Zeit vor; alle Kürzel und Menüeinträge öffnen dasselbe Fenster mit gewählter Art.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Wo ein neuer Eintrag beginnt; ohne Spalte (keine Personen oder Räume) gehen nur Paar/Spa-Tag und Sauna.</summary>
    /// <summary>Wo das Fenster öffnet; <paramref name="Minutes"/> gibt eine Länge vor (Pause aus der Meldung „Pause fehlt“), sonst rechnet das Fenster selbst.</summary>
    private sealed record EntryAnchor(CalendarColumn? Column, DateTimeOffset Start, int? Minutes = null);

    private EntryAnchor AnchorAt(double x, double y)
    {
        if (Columns.Count == 0)
        {
            return new(null, DayLayout.TimeAt(_dayStart, y, Scale));
        }

        var index = DayLayout.ColumnAt(x, Columns.Count, Scale);
        return new(ResourceColumn(Columns[index]), DayLayout.TimeAt(DayStartOf(index), y, Scale));
    }

    /// <summary>
    /// Die Person bzw. der Raum, in dessen Spalte etwas eingetragen wird. In der 7-Tage-Ansicht trägt die Spalte den Tag als Titel; der Eintrag
    /// braucht die Person bzw. den Raum (Titel, Dienst) und merkt sich den Tag der Spalte.
    /// </summary>
    private CalendarColumn ResourceColumn(CalendarColumn column) =>
        Filter is not { } filter || !column.IsDay
            ? column
            : new CalendarColumn(filter.Id, filter.Title, filter.Color, column.Subtitle, filter.ByRoom)
            {
                Date = column.Date,
                OffDuty = column.OffDuty,
                Absence = column.Absence,
                BreakMinutes = column.BreakMinutes,
                ShiftText = column.ShiftText,
            };

    /// <summary>Unter dem Mauszeiger, sonst ab jetzt (heute) bzw. Tagesbeginn in der ersten Spalte.</summary>
    private EntryAnchor AnchorHere() =>
        _hoverAt is { } at ? AnchorAt(at.X, at.Y) : AnchorAt(0, IsToday && NowTop is { } now ? now : 0);

    /// <summary>Klick auf eine freie Stelle über allen Spalten (und Taste N unter dem Mauszeiger): Art nach der freien Zeit vorgeschlagen.</summary>
    public void StartBookingAt(double x, double y, bool suggest = true, bool offDutyConfirmed = false)
    {
        if (Columns.Count == 0 || x < 0 || x > ColumnsWidth)
        {
            return;
        }

        // Neben einer Karte in belegter Zeit (oder knapp davor) gibt es nichts zu buchen, wie beim Überfahren ohne Vorschau (Auftraggeber, 05.11.2026);
        // „Termin hier“ im Rechtsklick-Menü (suggest: false) bleibt die bewusste Wahl.
        var index = DayLayout.ColumnAt(x, Columns.Count, Scale);
        var slot = DayLayout.TimeAt(DayStartOf(index), y, Scale);
        if (suggest && IsOccupied(Columns[index].Id, slot, slot.AddMinutes(15)))
        {
            return;
        }

        // Doppelklick: wie „Trotzdem buchen“, die Rückfrage für eine Person ohne Dienst entfällt.
        _offDutyConfirmed = offDutyConfirmed;
        try
        {
            OpenNewEntry(suggest ? null : NewEntryKind.Appointment, AnchorAt(x, y));
        }
        finally
        {
            _offDutyConfirmed = false;
        }
    }

    public void StartBooking(CalendarColumn column, double y) =>
        OpenNewEntry(NewEntryKind.Appointment, new EntryAnchor(ResourceColumn(column), DayLayout.TimeAt(DayStartOf(Columns.ToList().IndexOf(column)), y, Scale)));

    /// <summary>„+ Termin“: nächste freie Viertelstunde ab jetzt (heute) bzw. ab Tagesbeginn in der ersten Spalte.</summary>
    [RelayCommand]
    private void NewAppointment()
    {
        if (Columns.Count > 0 && HasResourceColumns)
        {
            StartBooking(Columns[0], IsToday && NowTop is { } now ? now + 15 * Scale.PixelsPerMinute : 0);
        }
    }

    /// <summary>„Anlegen und Termin buchen“ in den Kunden: die Buchung öffnet wie „+ Termin“, der Gast steht schon darin.</summary>
    public void OpenBookingFor(CustomerDto guest)
    {
        NewAppointment();
        if (Booking is { } booking)
        {
            booking.Customers.Insert(0, guest);
            booking.Customer = guest;
        }
    }

    /// <summary>Rechtsklick „Sperrzeit hier“ oder Taste B.</summary>
    public void OpenBlockAt(double x, double y) => OpenNewEntry(NewEntryKind.Block, AnchorAt(x, y));

    public void OpenNoteAt(double x, double y) => OpenNewEntry(NewEntryKind.Note, AnchorAt(x, y));

    /// <summary>Rechtsklick „Pause hier“: die Pause der Person, höchstens so lang wie die freie Zeit.</summary>
    public void OpenBreakAt(double x, double y) => OpenNewEntry(NewEntryKind.Break, AnchorAt(x, y));

    /// <summary>„Paar / Spa-Tag“: mehrere Gäste und Behandlungen gemeinsam buchen.</summary>
    [RelayCommand]
    private void OpenGroupBooking() => OpenKind(NewEntryKind.Group);

    /// <summary>„Sauna und Ruheraum“: Belegung und Eintritte des angezeigten Tages.</summary>
    [RelayCommand]
    private void OpenAreas() => OpenKind(NewEntryKind.Area);

    /// <summary>
    /// Taste oder Menü ohne eigene Stelle: ist das Fenster schon offen, wechselt es nur die Art (Eingaben bleiben stehen);
    /// sonst öffnet es an der Stelle unter dem Mauszeiger.
    /// </summary>
    private void OpenKind(NewEntryKind kind)
    {
        if (!CanBook || (kind == NewEntryKind.Break && ByRoom) || NewEntry?.Select(kind) == true)
        {
            return;
        }

        OpenNewEntry(kind, AnchorHere());
    }

    /// <summary>Alt+1 bis Alt+6 im offenen Fenster.</summary>
    public bool SelectNewEntry(int number) => NewEntry?.SelectNumber(number) == true;

    /// <summary>Öffnet das Fenster; ohne <paramref name="kind"/> schlägt es die Art nach der freien Zeit der Spalte vor.</summary>
    private void OpenNewEntry(NewEntryKind? kind, EntryAnchor anchor)
    {
        // Die Ansicht eines Gastes hat keine Spalten je Person oder Raum, in die sich buchen ließe.
        if (!CanBook || !HasResourceColumns || _resources is null || _day is null)
        {
            return;
        }

        var (column, start, minutes) = anchor;
        // In die Vergangenheit trägt der Kalender nichts ein; heute geht den ganzen Tag, damit sich ein Gast nachtragen lässt.
        if (IsBeforeToday(start))
        {
            ShowToast("Termine lassen sich nicht in der Vergangenheit eintragen.");
            return;
        }

        // Sauna und Ruheraum nehmen Eintritte, keine Termine (Ä92).
        if (column is { IsArea: true } && kind is (null or NewEntryKind.Appointment))
        {
            kind = NewEntryKind.Area;
        }

        var gap = column is null ? null : DayLayout.GapAt(_day, ByRoom, column.Id, start);
        var shortGap = column is not null && gap is not null && IsShortGap(column, gap, start);
        var suggested = kind is null && shortGap;
        var chosen = kind ?? (shortGap ? (ByRoom ? NewEntryKind.Block : NewEntryKind.Break) : NewEntryKind.Appointment);
        if (chosen == NewEntryKind.Break && ByRoom)
        {
            chosen = NewEntryKind.Block;
        }

        // Person ohne Dienst an diesem Tag (nur mit „Alle Mitarbeitenden“ sichtbar, oder mit Terminen trotz fehlenden Dienstes): erst bestätigen lassen.
        if (chosen == NewEntryKind.Appointment && column is { OffDuty: true } && !_offDutyConfirmed)
        {
            AskOffDuty(column, () =>
            {
                _offDutyConfirmed = true;
                try
                {
                    OpenNewEntry(kind, anchor);
                }
                finally
                {
                    _offDutyConfirmed = false;
                }
            });
            return;
        }

        // Ein angefangener Termin bleibt stehen und wird an die angeklickte Stelle verlegt (Ä85).
        if (column is not null && kind is (null or NewEntryKind.Appointment) && NewEntry is { VisitedBooking: { } open } entry)
        {
            OffDuty = null;
            entry.ShowBookingForMove();
            open.MoveTo(column, ByRoom, DateOf(column), start, gap);
            entry.Anchor = (column, start);
            UpdateEntryPreview();
            return;
        }

        OffDuty = null;
        ClosePanels();
        Ghost = null;
        NewEntry = new NewEntryViewModel(AvailableKinds(column is not null), chosen, (k, carried) => CreateEntry(k, column, start, gap, suggested, minutes, carried))
        {
            Anchor = column is null ? null : (column, start),
        };
    }

    /// <summary>Liegt der Beginn an einem Kalendertag vor heute (Ortszeit des Standorts)?</summary>
    private bool IsBeforeToday(DateTimeOffset start) =>
        DateOnly.FromDateTime(DayLayout.Local(start, _zone)) < DateOnly.FromDateTime(DayLayout.Local(_time.GetUtcNow(), _zone));

    /// <summary>Die Rolle darf buchen (sonst gibt es das Fenster nicht); Pausen gehören zu Personen, Sauna nur mit Bereichen.</summary>
    private IReadOnlyList<NewEntryKind> AvailableKinds(bool hasColumn) =>
        [.. new[] { NewEntryKind.Appointment, NewEntryKind.Group, NewEntryKind.Break, NewEntryKind.Block, NewEntryKind.Note, NewEntryKind.Area }
            .Where(k => k switch
            {
                NewEntryKind.Group => true,
                NewEntryKind.Area => HasAreas,
                NewEntryKind.Break => hasColumn && !ByRoom,
                _ => hasColumn,
            })];

    /// <summary>Die Lücke ist kürzer als jede Behandlung, die diese Person bzw. dieser Raum anbieten kann (mit Vor- und Nachbereitung).</summary>
    private bool IsShortGap(CalendarColumn column, FreeGap gap, DateTimeOffset start) =>
        gap.FillMinutes(start) >= 5
        && TreatmentFits.NothingFits(_resources!.Treatments.Where(TreatmentFits.Suits(_resources, column.Id, ByRoom)), gap, start);

    /// <summary>
    /// Länge der Pause: die gesetzliche Pause der Person (sonst 30 Minuten), nicht länger als die Lücke. Hat das Fenster die Pause selbst
    /// vorgeschlagen (<paramref name="fillGap"/>), füllt sie die ganze Lücke.
    /// </summary>
    private static int BreakMinutes(CalendarColumn column, FreeGap? gap, DateTimeOffset start, bool fillGap)
    {
        var normal = column.BreakMinutes > 0 ? column.BreakMinutes : 60;
        var fill = gap?.FillMinutes(start) ?? 0;
        return fill < 5 ? normal : fillGap ? fill : Math.Min(normal, fill);
    }

    /// <summary>
    /// Der geladene Tag für die Vorabprüfung in „Neuer Termin“ (Slice 78), nur wenn er genau <paramref name="date"/> ist: die 7-Tage-Ansicht fasst die Tage zusammen
    /// (Öffnungszeiten und Dienste der ganzen Woche), damit würde die Prüfung einen Schließtag als „geschlossen“ ablehnen.
    /// </summary>
    private CalendarDay? SingleDay(DateOnly date) => _week is null && _day?.Date == date ? _day : null;

    /// <param name="carried">Datum der bisherigen Eingabe beim Wechsel der Art; Sauna nimmt es (Ä153), die übrigen Arten setzen es selbst oder gehören zur Spalte.</param>
    private ViewModelBase? CreateEntry(NewEntryKind kind, CalendarColumn? column, DateTimeOffset start, FreeGap? gap, bool suggested, int? minutes = null,
        DateOnly? carried = null)
    {
        if (_resources is null || _day is null)
        {
            return null;
        }

        void Clear() => NewEntry = null;
        var date = column is null ? Date : DateOf(column);
        var fill = gap?.FillMinutes(start) ?? 0;
        switch (kind)
        {
            case NewEntryKind.Appointment when column is not null:
                return new BookingViewModel(_api, _resources, date, start, _zone, column, ByRoom,
                    async booked =>
                    {
                        Clear();
                        Apply(booked);
                        ShowWarnings(booked);
                        ShowPending();
                        await Task.CompletedTask.ConfigureAwait(true);
                    },
                    Clear, gap, BookingViewModel.NoticeFor(_day), _time, SingleDay(date))
                {
                    Links = ConflictLinksOf,
                    FreeTimeSearch = (prefill, day) => ShowFreeTimeAsync(prefill, day),
                };
            case NewEntryKind.Group:
                return CreateGroupBooking(date, Clear);
            case NewEntryKind.Break when column is not null:
                return EntryPanel(CalendarEntryKind.Break, column, start, minutes ?? BreakMinutes(column, gap, start, suggested), Clear);
            case NewEntryKind.Block when column is not null:
                return EntryPanel(CalendarEntryKind.Block, column, start, fill >= 5 ? Math.Min(60, fill) : 60, Clear);
            case NewEntryKind.Note when column is not null:
                return EntryPanel(CalendarEntryKind.Note, column, start, 15, Clear);
            case NewEntryKind.Area:
                var areas = new AreasViewModel(_api, _resources.Location.Id, carried ?? date, _zone, _day.Opening, CanBook, _time, Clear, ShowAreaGlances,
                    CanBook ? CheckoutVisitRequested : null);
                _ = areas.LoadAsync();
                return areas;
            default:
                return null;
        }
    }

    private GroupBookingViewModel CreateGroupBooking(DateOnly date, Action clear) =>
        new(_api, _resources!, date, _zone,
            async (result, queued) =>
            {
                clear();
                if (queued)
                {
                    ShowToast("Ohne Verbindung vorgemerkt, Abgleich folgt");
                    return;
                }

                ApplyAll(result.Appointments);

                var first = result.Appointments.MinBy(a => a.Start);
                ShowToast(first is null ? "Gemeinsam gebucht." : $"Gemeinsam gebucht: {result.Appointments.Count} Termine ab {DayLayout.Local(first.Start, _zone):HH:mm}.");
                await Task.CompletedTask.ConfigureAwait(true);
            },
            clear, _time)
        {
            Links = ConflictLinksOf,
        };

    /// <summary>Eingabe für Sperrzeit, Pause oder Notiz an einer Zeit; <paramref name="minutes"/> bestimmt das vorgeschlagene Ende.</summary>
    private CalendarEntryViewModel EntryPanel(CalendarEntryKind kind, CalendarColumn column, DateTimeOffset start, int minutes, Action clear,
        CalendarNoteDto? existing = null) =>
        new(_api, _resources!.Location.Id, DateOf(column), _zone, kind, column, start,
            async () =>
            {
                // Nach dem Speichern geht kein früherer Reiter auf (ein Termin, die Leiste „Meldungen“ mit Filter): die Eingabe ist erledigt, der Kalender zeigt das Ergebnis.
                CloseWithoutNextTab(clear);
                await LoadAsync().ConfigureAwait(true);
                ShowToast(kind switch
                {
                    CalendarEntryKind.Block => "Sperrzeit eingetragen.",
                    CalendarEntryKind.Break => "Pause eingetragen.",
                    _ => existing is null ? "Notiz gespeichert." : "Notiz geändert.",
                });
            },
            () =>
            {
                clear();
                Rebuild();
            },
            minutes,
            existing)
        {
            Links = ConflictLinksOf,
        };
}
