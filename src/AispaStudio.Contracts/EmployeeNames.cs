namespace AispaStudio.Contracts;

/// <summary>
/// Name im Haus für Mitarbeitende (ADR 0064, Nachtrag): Der Rufname ersetzt im Haus den vollständigen Namen (Kalender, Mein Tag, Assistent,
/// Posteingang); Lohn, Export, Zugänge und Einladungen behalten den vollständigen Namen.
/// </summary>
public static class EmployeeNames
{
    public const int MaxPreferredNameLength = 40;

    public static string InHouse(string displayName, string? preferredName) =>
        string.IsNullOrWhiteSpace(preferredName) ? displayName : preferredName.Trim();

    public static string InHouseName(this EmployeeDto employee) => InHouse(employee.DisplayName, employee.PreferredName);

    /// <summary>Name für die Texterkennung (Terminannahme, Assistent): beide Namen, damit „Siri“ und „Siriporn“ dieselbe Person treffen.</summary>
    public static string Recognizable(string displayName, string? preferredName) =>
        string.IsNullOrWhiteSpace(preferredName) ? displayName : $"{preferredName.Trim()} ({displayName})";
}
