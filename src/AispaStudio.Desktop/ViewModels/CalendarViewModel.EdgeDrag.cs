using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Welcher Rand eines Balkens gezogen wird.</summary>
public enum ResizeEdge
{
    Top,
    Bottom,
}

/// <summary>
/// Notiz- und Pausenbalken in der Höhe ziehen (Ä88, Ä89, Slice 71): am oberen Rand ändert sich der Beginn, am unteren das Ende, im Raster von fünf Minuten wie
/// beim Termin; der andere Rand bleibt stehen. Andere Sperrzeiten und Termine haben keine ziehbaren Ränder.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>So kurz darf ein Balken beim Ziehen höchstens werden.</summary>
    private const int MinEdgeMinutes = 5;

    private EdgeDragState? _edgeDrag;

    /// <summary>
    /// Ein Rand, der gerade gezogen wird: <paramref name="GrabOffset"/> ist der Abstand des Mauszeigers zum Rand, <paramref name="HasEnd"/> sagt, ob die Notiz
    /// ein Ende hat (ohne Ende ist sie ein Zettel von 15 Minuten, und das Ziehen am oberen Rand setzt keines).
    /// </summary>
    private sealed record EdgeDragState(CalendarNoteDto? Note, BlockDto? Block, ResizeEdge Edge, double GrabOffset, int Column, DateTimeOffset From,
        DateTimeOffset Until, bool HasEnd);

    public bool CanResize(CalendarNoteItem note) => CanBook && HasResourceColumns && note.Note.At is not null;

    public bool CanResize(CalendarItem item) => CanBook && HasResourceColumns && item is { IsBreak: true, Block: not null };

    /// <summary>Beginnt das Ziehen am Rand eines Zettels; <paramref name="grabOffset"/> ist der Abstand des Mauszeigers zum Rand (positiv: darunter).</summary>
    public void BeginResize(CalendarNoteItem note, ResizeEdge edge, double grabOffset)
    {
        if (IsDragging || !CanResize(note) || note.Note.At is not { } at)
        {
            return;
        }

        // Über die Id, nicht die Instanz: ein Neuaufbau zwischen Drücken und Ziehen ersetzt die Zettel.
        var column = Columns.ToList().FindIndex(c => c.Notes.Any(n => n.Id == note.Id));
        var until = note.Note.Until ?? at.AddMinutes(NotePreviewMinutes);
        BeginEdge(new EdgeDragState(note.Note, null, edge, grabOffset, column, at, until, note.Note.Until is not null));
    }

    /// <summary>Beginnt das Ziehen am Rand einer Pause.</summary>
    public void BeginResize(CalendarItem item, ResizeEdge edge, double grabOffset)
    {
        if (IsDragging || !CanResize(item) || item.Block is not { } block)
        {
            return;
        }

        var column = Columns.ToList().FindIndex(c => c.Items.Any(i => i.Block?.Id == block.Id));
        BeginEdge(new EdgeDragState(null, block, edge, grabOffset, column, block.From, block.Until, HasEnd: true));
    }

    private void BeginEdge(EdgeDragState state)
    {
        if (state.Column < 0)
        {
            return;
        }

        _edgeDrag = state;
        NotifyDragging();
        UpdateGhost();
    }

    private void DragEdgeTo(double y)
    {
        if (_edgeDrag is not { } edge)
        {
            return;
        }

        var at = DayLayout.SnapStart(DayStartOf(edge.Column), y - edge.GrabOffset, Scale);
        // Ein Zettel ohne Ende wandert am oberen Rand frei mit (seine 15 Minuten sind nur die Anzeige).
        _edgeDrag = edge.Edge == ResizeEdge.Top && !edge.HasEnd
            ? edge with { From = at, Until = at.AddMinutes(NotePreviewMinutes) }
            : edge.Edge == ResizeEdge.Top
            ? edge with { From = at < edge.Until.AddMinutes(-MinEdgeMinutes) ? at : edge.Until.AddMinutes(-MinEdgeMinutes) }
            : edge with { Until = at > edge.From.AddMinutes(MinEdgeMinutes) ? at : edge.From.AddMinutes(MinEdgeMinutes), HasEnd = true };
        UpdateGhost();
    }

    private CalendarGhost? EdgeGhost(EdgeDragState edge)
    {
        if (edge.Column < 0 || edge.Column >= Columns.Count)
        {
            return null;
        }

        var what = edge.Note is not null ? "Notiz" : "Pause";
        return new CalendarGhost(DayLayout.ColumnLeft(edge.Column, Scale), DayLayout.Top(DayStartOf(edge.Column), edge.From, Scale),
            Math.Max(DayLayout.MinNoteHeight, (edge.Until - edge.From).TotalMinutes * Scale.PixelsPerMinute),
            $"{DayLayout.TimeText(edge.From, edge.Until, _zone)} · {what}", IsMove: true);
    }

    /// <summary>Loslassen: der Server übernimmt die neue Zeit; lehnt er ab (die Pause überschneidet etwas), bleibt der Balken, wie er war.</summary>
    private async Task DropEdgeAsync(EdgeDragState edge)
    {
        CancelDrag();
        if (edge.Note is { } note)
        {
            await ChangeNoteTimeAsync(note, edge).ConfigureAwait(true);
        }
        else if (edge.Block is { } block)
        {
            await ChangeBlockTimeAsync(block, edge.From, edge.Until).ConfigureAwait(true);
        }
    }

    private async Task ChangeNoteTimeAsync(CalendarNoteDto note, EdgeDragState edge)
    {
        CalendarNoteChange change;
        if (edge.Edge == ResizeEdge.Top)
        {
            if (edge.From == note.At)
            {
                return;
            }

            change = new CalendarNoteChange(At: edge.From);
        }
        else
        {
            if (!edge.HasEnd || edge.Until == note.Until)
            {
                return;
            }

            change = new CalendarNoteChange(Until: edge.Until);
        }

        var result = await _api.PatchAsync<CalendarNoteDto>($"api/calendar-notes/{note.Id}", change).ConfigureAwait(true);
        if (result.Value is not { } changed)
        {
            ShowProblem("Nicht geändert", result.Error);
            return;
        }

        ReplaceNote(note.Id, changed);
        ShowToast("Notiz geändert");
    }

    private async Task ChangeBlockTimeAsync(BlockDto block, DateTimeOffset from, DateTimeOffset until)
    {
        if (from == block.From && until == block.Until)
        {
            return;
        }

        var result = await _api.PatchAsync<BlockDto>($"api/blocks/{block.Id}", new BlockChange(from, until)).ConfigureAwait(true);
        if (result.Value is null)
        {
            var notice = ConflictNotice.From(result.Rejection, _zone, Date, ConflictLinksOf);
            ShowProblem("Nicht geändert", notice?.Summary ?? result.Error,
                actionText: notice?.ActionText, action: notice?.ActionCommand is { } go ? () => go.Execute(null) : null);
            return;
        }

        ShowToast("Pause geändert");
        // Der Tag kommt neu, damit auch die Pausenrechnung (geplante Minuten, Warnung) zur neuen Zeit passt.
        await LoadAsync().ConfigureAwait(true);
    }
}
