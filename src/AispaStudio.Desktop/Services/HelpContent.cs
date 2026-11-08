using AispaStudio.Contracts;
using AispaStudio.Help;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Hilfe und Handbuch in der App. Die Texte kommen aus <see cref="HelpLibrary"/> (dieselben Dateien wie die Hilfe-Seite, eingebaut) und
/// dienen dem Auffinden: Namen der Fragezeichen, Erklärung zur Seite und Verweise des Assistenten. Angezeigt wird die Hilfe von der
/// Hilfe-Seite (<c>help/index.html</c>) in einem eingebetteten Browser (ADR 0079).
/// </summary>
public static class HelpContent
{
    public const string Surface = "windows-app";

    public static IReadOnlyList<HelpTopic> Topics => HelpLibrary.Topics;

    public static HelpTopic? Find(string id) => HelpLibrary.Find(id);

    /// <summary>Das Seitenthema zu einer Seite der App (<c>kalender</c>, <c>kassa</c> …), für die Erklärung zur aktuellen Seite.</summary>
    public static HelpTopic? ForPage(string page) => HelpLibrary.ForPage(page);

    public static string? RoleKey(StaffRole? role) => role switch
    {
        StaffRole.Owner => "inhaber",
        StaffRole.GeneralManager => "manager",
        StaffRole.Manager => "leitung",
        StaffRole.Backoffice => "backoffice",
        StaffRole.Reception => "rezeption",
        StaffRole.Therapist => "therapeut",
        _ => null,
    };

    /// <summary>Zeigt die Hilfe im Rahmen der App; ohne Rahmen (vor der Anmeldung) öffnet sich ein eigenes Fenster.</summary>
    public static Func<string?, string?, bool>? ShowInApp { get; set; }

    public static Action<string?, string?>? ShowWindow { get; set; }

    public static void Open(string? topic, string? section = null)
    {
        if (ShowInApp?.Invoke(topic, section) != true)
        {
            ShowWindow?.Invoke(topic, section);
        }
    }
}
