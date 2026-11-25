using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Design;

/// <summary>Stammdaten der Vorschau (Namen wie im Beispiel-Spa): Standort, Team, Räume, Behandlungen und eine Inhaberin mit allen Rechten.</summary>
internal static class DesignSpa
{
    public const string TimeZone = "Europe/Vienna";

    public static readonly LocationDto Vienna = new(Guid.NewGuid(), "Wien Innere Stadt", TimeZone, "EUR", []);

    public static readonly EmployeeDto Lena = new(Guid.NewGuid(), "Lena Huber", "#2f6f5e", [], [], ["en", "it"]);
    public static readonly EmployeeDto Marco = new(Guid.NewGuid(), "Marco Berger", "#7a4fa3", [], []);
    public static readonly EmployeeDto Sophie = new(Guid.NewGuid(), "Sophie Gruber", "#b5524a", [], []);
    public static readonly EmployeeDto Malee = new(Guid.NewGuid(), "Malee Chaiyasit", "#c28a2c", [], [], ["en"]);

    public static readonly RoomDto Lavender = new(Guid.NewGuid(), Vienna.Id, "Raum Lavendel", 10);
    public static readonly RoomDto Lotus = new(Guid.NewGuid(), Vienna.Id, "Raum Lotus", 10);
    public static readonly RoomDto Thai = new(Guid.NewGuid(), Vienna.Id, "Thai-Raum", 10);
    public static readonly RoomDto Cabin = new(Guid.NewGuid(), Vienna.Id, "Kosmetikkabine", 5);

    public static readonly TreatmentDto ClassicMassage = Treatment("Klassische Massage", 50, 89m, Lavender, Lotus);
    public static readonly TreatmentDto HotStone = Treatment("Hot-Stone-Massage", 80, 129m, Lotus);
    public static readonly TreatmentDto ThaiMassage = Treatment("Traditionelle Thai-Massage", 60, 79m, Thai);
    public static readonly TreatmentDto Facial = Treatment("Gesichtsbehandlung Classic", 60, 95m, Cabin);
    public static readonly TreatmentDto FootReflex = Treatment("Fußreflexzonenmassage", 30, 49m, Lavender);

    public static readonly LocationResources Resources = new(Vienna, [Lavender, Lotus, Thai, Cabin], [], [Lena, Marco, Sophie, Malee],
        [ClassicMassage, HotStone, ThaiMassage, Facial, FootReflex], []);

    /// <summary>Oberflächen werden als Inhaberin geprüft, damit alle Bereiche sichtbar sind.</summary>
    public static readonly CurrentUser Owner = new("design", "inhaberin@example.at", "Katrin", 1,
        new CurrentAccess(Guid.NewGuid(), "Beispiel-Spa", StaffRole.Owner, true, []), null, [.. Enum.GetValues<AccessRight>()]);

    private static TreatmentDto Treatment(string name, int minutes, decimal price, params RoomDto[] rooms) =>
        new(Guid.NewGuid(), name, minutes, 0, 10, price, [], [.. rooms.Select(r => r.Id)], [], null);
}
