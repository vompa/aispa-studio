using AispaStudio.Contracts;
using static AispaStudio.Desktop.Design.DesignSpa;

namespace AispaStudio.Desktop.Design;

/// <summary>Ein Beispieltag im Kalender der Vorschau: geöffnet 9 bis 20 Uhr, vier Personen, Termine mit gemischtem Stand.</summary>
internal static class DesignCalendar
{
    /// <summary>Die Antworten, die der Kalender beim Öffnen von <paramref name="date"/> abruft (Standorte, Ressourcen, Tag).</summary>
    public static DesignApi Api(DateOnly date) => new DesignApi()
        .On("/api/locations", new List<LocationDto> { Vienna })
        .On($"/api/locations/{Vienna.Id}/resources", Resources)
        .On($"/api/locations/{Vienna.Id}/calendar?date={date:yyyy-MM-dd}", Day(date));

    public static CalendarDay Day(DateOnly date)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(DesignSpa.TimeZone);
        DateTimeOffset At(int hour, int minute = 0)
        {
            var local = date.ToDateTime(new TimeOnly(hour, minute));
            return new DateTimeOffset(local, zone.GetUtcOffset(local));
        }

        AppointmentDto Visit(int hour, int minute, EmployeeDto employee, RoomDto room, TreatmentDto treatment, string guest,
            AppointmentStatus status = AppointmentStatus.Booked, string? note = null)
        {
            var start = At(hour, minute);
            var end = start.AddMinutes(treatment.DurationMinutes);
            return new(Guid.NewGuid(), Vienna.Id, treatment.Id, treatment.Name, Guid.NewGuid(), guest, employee.Id, room.Id, null,
                start, end, start, end.AddMinutes(treatment.FollowUpMinutes), status, treatment.Price, note, start.AddDays(-2));
        }

        return new(Vienna.Id, date, DesignSpa.TimeZone, [new TimeRange(At(9), At(20))],
            [
                new EmployeeShift(Lena.Id, At(9), At(17)),
                new EmployeeShift(Marco.Id, At(12), At(20)),
                new EmployeeShift(Sophie.Id, At(9), At(17)),
                new EmployeeShift(Malee.Id, At(11), At(19)),
            ],
            [
                Visit(9, 0, Lena, Lavender, ClassicMassage, "Markus Steiner", AppointmentStatus.Completed),
                Visit(10, 30, Lena, Lotus, HotStone, "Brigitte Müller", AppointmentStatus.CheckedIn, "Lavendelöl, wenig Gespräch, Fokus Nacken"),
                Visit(13, 0, Lena, Lavender, FootReflex, "Sabine Gruber"),
                Visit(14, 15, Lena, Lotus, ClassicMassage, "Petra Hofer"),
                Visit(12, 0, Marco, Lotus, ClassicMassage, "Dr. Wolfgang Bauer"),
                Visit(16, 0, Marco, Lotus, HotStone, "Stefan Koller"),
                Visit(18, 0, Marco, Lavender, ClassicMassage, "Tobias Wimmer"),
                Visit(9, 15, Sophie, Cabin, Facial, "Elif Berger", AppointmentStatus.NoShow),
                Visit(10, 30, Sophie, Cabin, Facial, "Laura Novak", AppointmentStatus.CheckedIn, "Erstbesuch, Anamnese liegt vor"),
                Visit(13, 30, Sophie, Cabin, Facial, "Julia Wagner"),
                Visit(11, 0, Malee, Thai, ThaiMassage, "Jana Svoboda", AppointmentStatus.Completed),
                Visit(14, 30, Malee, Thai, ThaiMassage, "Valentina Rossi") with { Source = AppointmentSource.Online },
            ],
            [new BlockDto(Guid.NewGuid(), ResourceKind.Employee, Sophie.Id, At(12, 0), At(13, 0), "Schulung Peeling")]);
    }
}
