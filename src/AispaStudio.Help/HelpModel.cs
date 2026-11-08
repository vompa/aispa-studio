namespace AispaStudio.Help;

/// <summary>Teil eines Absatzes: fett, als Code oder als Link (<c>hilfe:thema#anker</c> verweist in die Hilfe, sonst ins Web).</summary>
public sealed record HelpSpan(string Text, bool Bold = false, bool Code = false, string? Href = null);

public abstract record HelpBlock;

public sealed record HelpHeading(int Level, string Text, string Id) : HelpBlock;

public sealed record HelpParagraph(IReadOnlyList<HelpSpan> Spans) : HelpBlock;

public sealed record HelpList(bool Ordered, IReadOnlyList<IReadOnlyList<HelpSpan>> Items) : HelpBlock;

/// <summary>Bild in eigener Zeile; <c>Name</c> ist Ordner und Dateiname unter <c>web/src/help/images</c> ohne Endung (<c>app/kalender-ziehen</c>).</summary>
public sealed record HelpImage(string Name, string Alt) : HelpBlock;

/// <summary>Art eines Themas, aus der Kopfangabe <c>art</c>.</summary>
public enum HelpKind
{
    /// <summary>Alle Einstellungen eines Bereichs, Ziel der Fragezeichen.</summary>
    Setting,

    /// <summary>Eine Aufgabe Schritt für Schritt.</summary>
    Task,

    /// <summary>Was eine Seite zeigt und welche Abläufe es dort gibt.</summary>
    Page,
}

/// <summary>Kopfangaben eines Themas; Werte wie in der Datei (klein, ohne Leerzeichen am Rand).</summary>
public sealed record HelpMeta(
    HelpKind Kind,
    string? Area,
    IReadOnlyList<string> Surfaces,
    IReadOnlyList<string> Roles,
    string? Page,
    IReadOnlyList<string> Keywords)
{
    public static readonly HelpMeta Empty = new(HelpKind.Setting, null, [], [], null, []);
}

/// <summary>Ein Hilfethema; Kennung ist der Dateiname, Titel die erste Überschrift, <c>Summary</c> der erste Absatz.</summary>
public sealed record HelpTopic(string Id, string Title, IReadOnlyList<HelpBlock> Blocks, HelpMeta Meta)
{
    public HelpTopic(string id, string title, IReadOnlyList<HelpBlock> blocks)
        : this(id, title, blocks, HelpMeta.Empty)
    {
    }

    public bool HasSection(string id) => Blocks.OfType<HelpHeading>().Any(h => h.Id == id);

    public string Summary => Blocks.TakeWhile(b => b is not HelpHeading).OfType<HelpParagraph>().FirstOrDefault() is { } p ? HelpMarkdown.Plain(p.Spans) : "";

    /// <summary>Überschrift des Abschnitts und alles bis zur nächsten Überschrift gleicher oder höherer Ebene.</summary>
    public IReadOnlyList<HelpBlock> Section(string id)
    {
        var start = Blocks.ToList().FindIndex(b => b is HelpHeading h && h.Id == id);
        if (start < 0)
        {
            return [];
        }

        var level = ((HelpHeading)Blocks[start]).Level;
        return [.. Blocks.Skip(start).TakeWhile((b, i) => i == 0 || b is not HelpHeading h || h.Level > level)];
    }

    /// <summary>Alle Bilder des Themas.</summary>
    public IEnumerable<HelpImage> Images => Blocks.OfType<HelpImage>();

    /// <summary>Alle Verweise in die Hilfe als (Thema, Anker).</summary>
    public IEnumerable<(string Topic, string? Section)> Links => Blocks.SelectMany(b => b switch
        {
            HelpParagraph p => p.Spans,
            HelpList l => l.Items.SelectMany(i => i),
            _ => [],
        })
        .Select(s => HelpMarkdown.HelpLink(s.Href))
        .Where(l => l is not null)
        .Select(l => l!.Value);
}
