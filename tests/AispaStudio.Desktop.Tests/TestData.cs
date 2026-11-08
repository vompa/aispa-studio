using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Tests;

internal static class TestData
{
    public static readonly DateOnly Monday = new(2027, 3, 1);
    public static readonly Guid LocationId = Guid.NewGuid();
    public static readonly LocationDto Location = new(LocationId, "Wien", "Europe/Vienna", "EUR", [new OpeningHours(DayOfWeek.Monday, new(9, 0), new(20, 0))],
        null, LocationContact.Empty);
    public static readonly Guid Massage = Guid.NewGuid();
    public static readonly EmployeeDto Anna = new(Guid.NewGuid(), "Anna", "#2f6f5e", [Massage], []);
    public static readonly EmployeeDto Ben = new(Guid.NewGuid(), "Ben", "#7a4fa3", [], []);
    public static readonly RoomDto Room = new(Guid.NewGuid(), LocationId, "Raum Zeder", 10);
    public static readonly TreatmentDto Facial = new(Guid.NewGuid(), "Gesicht", 60, 0, 0, 89m, [Guid.NewGuid()], [Room.Id], [], "Gesicht");
    public static readonly TreatmentDto MassageTreatment = new(Guid.NewGuid(), "Massage", 50, 5, 10, 79m, [Massage], [Room.Id], [], "Massagen");
    public static readonly LocationResources Resources = new(Location, [Room], [], [Anna, Ben], [Facial, MassageTreatment], []);

    /// <summary>Wiener Ortszeit am Testmontag (Winterzeit, UTC+1).</summary>
    public static DateTimeOffset At(int hour, int minute = 0) => new(2027, 3, 1, hour - 1, minute, 0, TimeSpan.Zero);

    public static AppointmentDto Appointment(int hour, EmployeeDto employee, AppointmentStatus status = AppointmentStatus.Booked, DateTimeOffset? updated = null) =>
        new(Guid.NewGuid(), LocationId, MassageTreatment.Id, "Massage", Guid.NewGuid(), "Eva Muster", employee.Id, Room.Id, null,
            At(hour), At(hour, 50), At(hour).AddMinutes(-5), At(hour).AddMinutes(60), status, 79m, null, updated ?? At(8));

    public static CalendarDay Day(params AppointmentDto[] appointments) =>
        new(LocationId, Monday, "Europe/Vienna", [new TimeRange(At(9), At(20))],
            [new EmployeeShift(Anna.Id, At(9), At(17)), new EmployeeShift(Ben.Id, At(12), At(20))], appointments, []);

    /// <summary>Standortleitung: verwaltet den Standort und bucht, darf also Termine einer Schließzeit absagen (ADR 0073).</summary>
    public static CurrentUser Manager() =>
        new("u2", "leitung@aispa.test", "Markus", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Manager, true, []), null,
            [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus, AccessRight.ManageLocation]);

    /// <summary>Inhaber (oder Manager neben dem Inhaber): alle Rechte; nur sie dürfen eine Anzahlung retournieren (ADR 0073, Ä47).</summary>
    public static CurrentUser Owner(StaffRole role = StaffRole.Owner) =>
        new("u3", "inhaber@aispa.test", "Katrin", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", role, true, []), null, [.. Enum.GetValues<AccessRight>()]);

    public static CurrentUser Reception(Guid? employeeId = null) =>
        new("u1", "rita@aispa.test", "Rita", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Reception, true, []), employeeId,
            [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus]);
}
