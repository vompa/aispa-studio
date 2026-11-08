using System.ComponentModel;
using AispaStudio.Desktop.Controls;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Vorschau beim Planen (Auftraggeber, 23.10.2026): Solange das Fenster „Neuer Termin“ offen ist, steht der geplante Eintrag als gestrichelte Karte in der Spalte
/// (Person bzw. Raum, Beginn, Dauer samt Vor- und Nachbereitung, Text). Sie nutzt dieselbe <see cref="CalendarGhost"/>-Karte wie das Ziehen, folgt jeder Änderung
/// der Eingaben und färbt sich in Warnfarbe, wenn die Zeit schon belegt ist.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Länge der Vorschau, solange die Behandlung fehlt oder die Notiz kein Ende hat.</summary>
    private const int DefaultPreviewMinutes = 30;

    private const int NotePreviewMinutes = 15;

    private CalendarGhost? _preview;
    private INotifyPropertyChanged? _watchedInput;

    /// <summary>Beobachtet die Eingaben der gerade offenen Art; nach jedem Wechsel der Art oder des Fensters neu.</summary>
    private void WatchEntryInput()
    {
        if (_watchedInput is not null)
        {
            _watchedInput.PropertyChanged -= OnEntryInputChanged;
        }

        _watchedInput = NewEntry?.Content;
        if (_watchedInput is not null)
        {
            _watchedInput.PropertyChanged += OnEntryInputChanged;
        }

        UpdateEntryPreview();
    }

    private void OnEntryInputChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Das Datumsfeld der Maske: der Kalender zeigt den gewählten Tag, damit die Vorschau dort steht (Ä83).
        if (e.PropertyName == nameof(BookingViewModel.Date) && sender is BookingViewModel booking && !Columns.Any(c => (c.Date ?? Date) == booking.Date))
        {
            Date = booking.Date;
        }

        UpdateEntryPreview();
    }

    /// <summary>
    /// Stellt die Vorschau neu: sie ersetzt die Vorschau unter dem Mauszeiger, solange das Fenster offen ist; beim Ziehen gehört die Karte dem Ziehen.
    /// </summary>
    private void UpdateEntryPreview()
    {
        var preview = BuildEntryPreview();
        if (preview is null && _preview is null)
        {
            // Ohne offenen Eintrag gehört die Karte der Viertelstunde unter dem Mauszeiger. Nach dem Neuzeichnen stünde sie sonst an der alten Stelle,
            // bis sich die Maus bewegt (Ä90, Slice 71); die nächste Bewegung zeigt sie wieder.
            if (!IsDragging)
            {
                Ghost = null;
            }

            return;
        }

        _preview = preview;
        if (!IsDragging)
        {
            Ghost = preview;
        }
    }

    private CalendarGhost? BuildEntryPreview()
    {
        if (NewEntry is not { } entry || _resources is null || _day is null)
        {
            return null;
        }

        return entry.Content switch
        {
            BookingViewModel booking => BookingPreview(booking),
            GroupBookingViewModel group => GroupPreview(group, entry.Anchor),
            CalendarEntryViewModel { IsEditing: false } input => InputPreview(input),
            AreasViewModel areas => AreaPreview(areas),
            _ => null,
        };
    }

    private CalendarGhost? BookingPreview(BookingViewModel booking)
    {
        if (TimeInputParser.TryParse(booking.StartText) is not { } time || PreviewColumn(booking.ColumnId, booking.Date) is not { } column)
        {
            return null;
        }

        var treatment = booking.Treatment;
        var start = DayLayout.LocalToUtc(booking.Date, time, _zone);
        var end = start.AddMinutes(treatment?.DurationMinutes ?? DefaultPreviewMinutes);
        var before = treatment?.PreparationMinutes ?? 0;
        var after = treatment?.FollowUpMinutes ?? 0;
        // Wie der Hinweis „länger als die freie Zeit“ in der Auswahl: belegt, oder die Behandlung passt nicht in die Lücke.
        var misfit = treatment is not null
            && TreatmentFits.Of(treatment, DayLayout.GapAt(_day!, ByRoom, booking.ColumnId, start), start).Misfit;
        var warning = misfit || IsOccupied(booking.ColumnId, start.AddMinutes(-before), end.AddMinutes(after));
        return PreviewAt(column, start, end, treatment?.Name ?? "Termin", before, after, warning);
    }

    /// <summary>Paar oder Spa-Tag suchen ihre Zeit selbst: die Vorschau steht an der angeklickten Stelle, so lang wie die längste Behandlung des ersten Abschnitts.</summary>
    private CalendarGhost? GroupPreview(GroupBookingViewModel group, (CalendarColumn Column, DateTimeOffset Start)? anchor)
    {
        if (anchor is not { } at || PreviewColumn(at.Column.Id, DateOf(at.Column)) is not { } column)
        {
            return null;
        }

        var minutes = group.Steps.FirstOrDefault()?.Entries.Select(e => e.Choice)
            .Select(choice => choice.AreaId is not null ? choice.Minutes : _resources!.Treatments.FirstOrDefault(t => t.Id == choice.Id)?.DurationMinutes ?? 0)
            .DefaultIfEmpty(0).Max() ?? 0;
        var end = at.Start.AddMinutes(minutes > 0 ? minutes : 60);
        return PreviewAt(column, at.Start, end, "Paar/Spa", 0, 0, IsOccupied(at.Column.Id, at.Start, end));
    }

    /// <summary>Pause, Sperrzeit und Notiz: von Beginn bis Ende der Eingabe; eine Notiz ohne Ende ist ein Zettel von 15 Minuten.</summary>
    private CalendarGhost? InputPreview(CalendarEntryViewModel input)
    {
        if (TimeInputParser.TryParse(input.StartText) is not { } from || PreviewColumn(input.ColumnId, input.Date) is not { } column)
        {
            return null;
        }

        var minutes = TimeInputParser.TryParse(input.EndText) is { } until && until > from ? (int)(until - from).TotalMinutes : NotePreviewMinutes;
        var start = DayLayout.LocalToUtc(input.Date, from, _zone);
        var end = start.AddMinutes(minutes);
        var text = input.Kind switch
        {
            CalendarEntryKind.Break => "Pause",
            CalendarEntryKind.Block => input.Text.Trim() is { Length: > 0 } reason ? reason : "Sperrzeit",
            _ => input.Text.Trim() is { Length: > 0 } note ? note : "Notiz",
        };
        // Eine Notiz belegt nichts; Pause und Sperrzeit lehnt der Server ab, wenn die Zeit schon belegt ist.
        return PreviewAt(column, start, end, text, 0, 0, input.Kind != CalendarEntryKind.Note && IsOccupied(input.ColumnId, start, end));
    }

    /// <summary>Die Spalte der Person bzw. des Raums an dem Tag: im Tageskalender die eine Spalte, in der 7-Tage-Ansicht die des Tages.</summary>
    private int? PreviewColumn(Guid id, DateOnly date)
    {
        var index = Columns.ToList().FindIndex(c => c.Id == id && (c.Date ?? Date) == date);
        return index < 0 ? null : index;
    }

    private CalendarGhost PreviewAt(int column, DateTimeOffset start, DateTimeOffset end, string text, int beforeMinutes, int afterMinutes, bool warning) =>
        new(DayLayout.ColumnLeft(column, Scale), DayLayout.Top(DayStartOf(column), start, Scale), Math.Max(18, (end - start).TotalMinutes * Scale.PixelsPerMinute), text,
            IsMove: false, BeforeHeight: beforeMinutes * Scale.PixelsPerMinute, AfterHeight: afterMinutes * Scale.PixelsPerMinute, IsWarning: warning);

    // ---------- Vorschau ziehen (Ä83, Slice 71) ----------

    /// <summary>Die Vorschau des neuen Termins, die gerade gezogen wird: Abstand des Mauszeigers zum oberen Rand, Zielspalte und Beginn.</summary>
    private sealed record PreviewDragState(double GrabOffset, int Column, DateTimeOffset Start);

    private PreviewDragState? _previewDrag;

    /// <summary>Liegt die Stelle auf der Vorschau eines angefangenen Termins? Dann greift ein Druck dort die Vorschau, statt eine freie Stelle zu wählen.</summary>
    public bool IsOnPreview(double x, double y) =>
        !IsDragging && NewEntry?.Content is BookingViewModel && _preview is { } preview
        && x >= preview.Left && x <= preview.Left + Scale.ColumnWidth && y >= preview.Top && y <= preview.Top + preview.Height;

    /// <summary>Beginnt das Ziehen der Vorschau; <paramref name="grabOffset"/> ist der Abstand des Mauszeigers zu ihrem oberen Rand.</summary>
    public void BeginPreviewDrag(double grabOffset)
    {
        if (IsDragging || NewEntry?.Content is not BookingViewModel || _preview is not { } preview || Columns.Count == 0)
        {
            return;
        }

        var column = DayLayout.ColumnAt(preview.Left + 1, Columns.Count, Scale);
        _previewDrag = new PreviewDragState(grabOffset, column, DayLayout.SnapStart(DayStartOf(column), preview.Top, Scale));
        NotifyDragging();
        UpdateGhost();
    }

    private void DragPreviewTo(double x, double y)
    {
        if (_previewDrag is not { } drag)
        {
            return;
        }

        var column = DayLayout.ColumnAt(x, Columns.Count, Scale);
        _previewDrag = drag with { Column = column, Start = DayLayout.SnapStart(DayStartOf(column), y - drag.GrabOffset, Scale) };
        UpdateGhost();
    }

    /// <summary>Beim Ziehen steht die Vorschau samt Vor- und Nachbereitung an der Zielstelle, mit Beginn und Spalte als Text.</summary>
    private CalendarGhost? PreviewDragGhost(PreviewDragState drag) =>
        _preview is { } preview && drag.Column >= 0 && drag.Column < Columns.Count
            ? preview with
            {
                Left = DayLayout.ColumnLeft(drag.Column, Scale),
                Top = DayLayout.Top(DayStartOf(drag.Column), drag.Start, Scale),
                Text = $"{DayLayout.Local(drag.Start, _zone):HH:mm} · {Columns[drag.Column].Title}",
                IsMove = true,
                IsWarning = false,
            }
            : null;

    /// <summary>Loslassen: der angefangene Termin zieht an die neue Stelle wie bei einem Klick auf einen anderen Slot (Ä85); die Eingaben bleiben.</summary>
    private void DropPreview(PreviewDragState drag)
    {
        CancelDrag();
        if (drag.Column >= 0 && drag.Column < Columns.Count)
        {
            OpenNewEntry(NewEntryKind.Appointment, new EntryAnchor(ResourceColumn(Columns[drag.Column]), drag.Start));
        }
    }
}
