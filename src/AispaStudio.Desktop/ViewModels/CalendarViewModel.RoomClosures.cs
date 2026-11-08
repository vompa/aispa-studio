using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Raumsperren im Kalender (ADR 0076): die schraffierte Fläche in der Spalte des Raums, die Meldung „n Termine im gesperrten Raum“ mit dem Knopf „Umbuchen …“ und die
/// Umbuchungshilfe (dasselbe Seitenfenster wie der Ausfall-Assistent). Der Kalender zeichnet nur, was der Server liefert; umgebucht wird erst nach „Gewählte übernehmen“.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Sperren anlegen darf, wer Standorte verwaltet; der Kalender führt dazu in die Verwaltung (Reiter „Raumsperren“).</summary>
    public bool CanCloseRooms => AdminRequested is not null && _user.Rights.Contains(AccessRight.ManageLocation);

    /// <summary>
    /// Die Meldungen „n Termine im gesperrten Raum“: je Raumsperre des Tages und je fällige mit offenen Terminen, gleich wie fern sie beginnt. Eine Warnung, solange die Sperre später
    /// als in 14 Tagen beginnt, ein Fehler, wenn sie läuft oder in 14 Tagen beginnt (<see cref="RoomClosureBand.Urgent"/>, der Server rechnet es). Die Meldung bleibt, bis die Termine
    /// umgebucht sind; sie bucht nichts von selbst um.
    /// </summary>
    private void AddRoomClosures(MessageState state, List<MessageItem> all)
    {
        var bands = (_day?.RoomClosures ?? []).Concat(_day?.RoomClosuresDue ?? []).Where(b => b.OpenAppointments > 0).GroupBy(b => b.Id).Select(g => g.First()).OrderBy(b => b.From);
        foreach (var band in bands)
        {
            var termine = band.OpenAppointments == 1 ? "1 Termin" : $"{band.OpenAppointments} Termine";
            var period = DayLayout.ClosurePeriod(band.From, band.Until, _zone);
            var lines = new List<string> { $"{band.RoomName}: {band.Title}, {period}" };
            if (!string.IsNullOrWhiteSpace(band.Reason))
            {
                lines.Add($"Grund: {band.Reason}");
            }

            all.Add(Message(state, $"roomclosure:{band.Id}", MessageKind.RoomClosure, band.Urgent ? MessageSeverity.Error : MessageSeverity.Warning, "Raumsperre",
                $"{band.RoomName} ist gesperrt ({band.Title}, {period}): {termine} umbuchen.", $"{termine} im gesperrten Raum", trigger: band.OpenAppointments.ToString(System.Globalization.CultureInfo.InvariantCulture),
                subjects: [new(MessageSubjectKind.Room, band.RoomId, band.RoomName)], lines: lines,
                actionLabel: CanBook ? "Umbuchen …" : null, action: CanBook ? () => OpenRoomClosureRebooking(band) : null));
        }
    }

    /// <summary>„n Termine umbuchen“ im Kopf des gesperrten Raums und „Umbuchen …“ der Meldung: die Umbuchungshilfe für die offenen Termine im Raum.</summary>
    private void OpenRoomClosureRebooking(RoomClosureBand band)
    {
        if (!CanBook || _resources is null)
        {
            return;
        }

        ClosePanels();
        Ghost = null;
        var assistant = new AbsenceAssistantViewModel(_api, _resources.Location.Id, band.Id, band.RoomName, DayLayout.ClosurePeriod(band.From, band.Until, _zone), _zone,
            async moved =>
            {
                ApplyAll(moved);
                await LoadAsync().ConfigureAwait(true);
            },
            () =>
            {
                Absence = null;
                Rebuild();
            });
        Absence = assistant;
        _ = assistant.LoadAsync();
        Rebuild();
    }

    /// <summary>Spaltenkopf eines gesperrten Raums: die Raumsperre mit den offenen Terminen suchen und die Umbuchungshilfe öffnen.</summary>
    private void OpenRoomClosureRebooking(CalendarColumn column)
    {
        var band = (_day?.RoomClosures ?? []).Concat(_day?.RoomClosuresDue ?? []).FirstOrDefault(b => b.Id == column.RoomClosureId);
        if (band is not null)
        {
            OpenRoomClosureRebooking(band);
        }
    }

    /// <summary>Rechtsklick auf den Kopf eines Raums: Termine umbuchen (wenn gesperrt mit offenen Terminen) und den Raum sperren.</summary>
    private List<CalendarAction> ActionsForRoomColumn(CalendarColumn column)
    {
        var actions = new List<CalendarAction>();
        if (CanBook && column.RoomClosureId is not null)
        {
            actions.Add(new("Termine in andere Räume umbuchen", null, () => OpenRoomClosureRebooking(column)));
        }

        if (CanCloseRooms)
        {
            actions.Add(new("Raum sperren …", null, () => AdminRequested!("masterData", "roomClosures", column.Id), Icon: Icons.Lock));
        }

        return actions;
    }
}
