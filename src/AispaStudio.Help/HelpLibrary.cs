using System.Globalization;
using System.Text;

namespace AispaStudio.Help;

/// <summary>Alle eingebauten Hilfethemen (Einstellungen, Abläufe, Seiten), sortiert nach Titel.</summary>
public static class HelpLibrary
{
    private const string Prefix = "HelpText.";

    private static readonly Lazy<IReadOnlyList<HelpTopic>> All = new(Load);

    public static IReadOnlyList<HelpTopic> Topics => All.Value;

    /// <summary>Ob das Thema nur im Server-Betrieb gilt (Kopfangabe <c>betrieb: server</c>); unbekannte Kennungen zählen nicht.</summary>
    public static bool IsServerOnly(string id) => Find(id)?.Meta.ServerOnly == true;

    public static HelpTopic? Find(string id) => Topics.FirstOrDefault(t => t.Id == id);

    /// <summary>Das Seitenthema („Erklärung zur aktuellen Seite“) zu einer Seitenkennung wie <c>kalender</c> oder <c>masterdata</c>.</summary>
    public static HelpTopic? ForPage(string page, bool standalone = false) =>
        Topics.FirstOrDefault(t => t.Meta.Kind == HelpKind.Page && !(standalone && t.Meta.ServerOnly) && string.Equals(t.Meta.Page, page, StringComparison.OrdinalIgnoreCase));

    private static List<HelpTopic> Load()
    {
        var assembly = typeof(HelpLibrary).Assembly;
        return [.. assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal))
            .Select(n =>
            {
                using var stream = assembly.GetManifestResourceStream(n)!;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return HelpMarkdown.ToTopic(n[Prefix.Length..^3], reader.ReadToEnd());
            })
            .OrderBy(t => t.Title, StringComparer.Create(CultureInfo.GetCultureInfo("de-AT"), false))];
    }
}
