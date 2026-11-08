using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Raumansicht (Slice 77, Ä91, Ä92): der Umschalter „Alle Räume“ / „Nur verfügbare Räume“ (wie „Alle zeigen“ bei den Personen), die Eintritte von Sauna und Ruheraum als Blöcke
/// in ihren Spalten, und der angefangene Termin, der beim Umschalten aus der Teamansicht in den Raum seiner Behandlung zieht.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Die Eintritte des Tages aus dem Abruf der Bereiche; die Spalten von Sauna und Ruheraum zeichnen sie.</summary>
    private IReadOnlyList<AreaVisitDto> _areaVisits = [];

    /// <summary>„Nur verfügbare Räume“: Räume, die den ganzen Tag gesperrt sind, bekommen keine Spalte. Gilt nur in der Raumansicht und wird nicht gespeichert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoomFilterText), nameof(RoomFilterCompact), nameof(RoomFilterLabel), nameof(RoomFilterTip))]
    public partial bool OnlyAvailableRooms { get; set; }

    /// <summary>Räume, die ohne „Nur verfügbare Räume“ eine Spalte hätten und den ganzen Tag gesperrt sind; in der Teamansicht und in der 7-Tage-Ansicht 0.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleRooms), nameof(RoomFilterText), nameof(RoomFilterCompact), nameof(RoomFilterLabel), nameof(RoomFilterTip))]
    public partial int UnavailableRoomCount { get; private set; }

    /// <summary>Den Umschalter gibt es nur, wenn er etwas ändert.</summary>
    public bool CanToggleRooms => UnavailableRoomCount > 0;

    public string RoomFilterText => OnlyAvailableRooms ? "Alle Räume" : "Nur verfügbare Räume";

    public string RoomFilterCompact => OnlyAvailableRooms ? "" : UnavailableRoomCount.ToString(System.Globalization.CultureInfo.CurrentCulture);

    public string RoomFilterLabel => IsCompactHeader ? RoomFilterCompact : RoomFilterText;

    public string RoomFilterTip => ChipTip(RoomFilterText, OnlyAvailableRooms
        ? "Auch Räume, die den ganzen Tag gesperrt sind"
        : $"{UnavailableRoomCount} den ganzen Tag gesperrte Räume ausblenden");

    partial void OnOnlyAvailableRoomsChanged(bool value) => Rebuild();

    [RelayCommand]
    private void ToggleRooms() => OnlyAvailableRooms = !OnlyAvailableRooms;

    /// <summary>Der Stand der Bereiche (Kopfzeile und Spalten) nach dem Laden oder nach einem Eintritt.</summary>
    private void ShowAreas(IReadOnlyList<AreaDay> areas)
    {
        AreaGlances = [.. areas.Select(a => AreaGlance.Of(a, _time.GetUtcNow(), IsToday))];
        _areaVisits = [.. areas.SelectMany(a => a.Visits)];
        if (ByRoom)
        {
            Rebuild();
        }
    }

    /// <summary>
    /// Umschalten mit angefangenem Termin (Ä92): in der Raumansicht steht der Termin im Raum seiner Behandlung, die Vorschau dort, und der Raum rückt ins Bild.
    /// Ohne Raum mit Spalte bleibt der Termin, wo er ist.
    /// </summary>
    private void MoveBookingToRoomView()
    {
        if (NewEntry is not { Content: BookingViewModel booking } entry || booking.ColumnIsRoom && Columns.Any(c => c.Id == booking.ColumnId)
            || booking.Treatment is not { } treatment || TimeInputParser.TryParse(booking.StartText) is not { } time)
        {
            RevealPreviewColumn();
            return;
        }

        var column = treatment.RoomIds.Select(id => Columns.FirstOrDefault(c => c.Id == id && !c.IsArea)).FirstOrDefault(c => c is not null);
        if (column is null)
        {
            return;
        }

        var start = DayLayout.LocalToUtc(booking.Date, time, _zone);
        booking.MoveTo(column, true, booking.Date, start, _day is null ? null : DayLayout.GapAt(_day, byRoom: true, column.Id, start));
        entry.Anchor = (column, start);
        UpdateEntryPreview();
        RevealPreviewColumn();
    }

    /// <summary>Sauna-Eintritt in Arbeit: von Beginn bis Ende der Eingabe in der Spalte des gewählten Bereichs.</summary>
    private CalendarGhost? AreaPreview(AreasViewModel areas)
    {
        if (areas.Area is not { } area || TimeInputParser.TryParse(areas.FromText) is not { } from || TimeInputParser.TryParse(areas.UntilText) is not { } until || until <= from
            || PreviewColumn(area.RoomId, Date) is not { } column)
        {
            return null;
        }

        return PreviewAt(column, DayLayout.LocalToUtc(Date, from, _zone), DayLayout.LocalToUtc(Date, until, _zone), "Eintritt", 0, 0, warning: false);
    }

    private void RevealPreviewColumn()
    {
        if (_preview is { } preview)
        {
            ColumnRevealRequested?.Invoke(preview.Left);
        }
    }
}
