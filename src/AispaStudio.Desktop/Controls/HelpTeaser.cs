using AispaStudio.Desktop.Services;
using AispaStudio.Help;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Kurzhinweis auf ein Thema oder einen Abschnitt der Hilfe, z. B. in der KI-Seitenleiste als Antwort auf „Wie mache ich …?“: Titel, der
/// erste Satz und „In der Hilfe öffnen“. Die Schritte und Bilder zeigt die Hilfe selbst (ADR 0079).
/// </summary>
public sealed class HelpTeaser : StackPanel
{
    public static readonly StyledProperty<string?> TopicProperty = AvaloniaProperty.Register<HelpTeaser, string?>(nameof(Topic));

    public static readonly StyledProperty<string?> SectionProperty = AvaloniaProperty.Register<HelpTeaser, string?>(nameof(Section));

    static HelpTeaser()
    {
        TopicProperty.Changed.AddClassHandler<HelpTeaser>((v, _) => v.Build());
        SectionProperty.Changed.AddClassHandler<HelpTeaser>((v, _) => v.Build());
    }

    public HelpTeaser() => Spacing = 8;

    public string? Topic
    {
        get => GetValue(TopicProperty);
        set => SetValue(TopicProperty, value);
    }

    public string? Section
    {
        get => GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    /// <summary>Der gezeigte Satz; leer, wenn Thema oder Abschnitt fehlen.</summary>
    public string Excerpt { get; private set; } = "";

    /// <summary>Der erste Satz des Abschnitts (ohne Abschnitt des Themas): der erste Absatz, sonst der erste Schritt oder Punkt.</summary>
    internal static string ExcerptOf(HelpTopic topic, string? section)
    {
        IEnumerable<HelpBlock> blocks = section is not null && topic.HasSection(section) ? topic.Section(section) : topic.Blocks;
        foreach (var block in blocks)
        {
            switch (block)
            {
                case HelpParagraph p:
                    return HelpMarkdown.Plain(p.Spans);
                case HelpList { Items: [var first, ..] }:
                    return HelpMarkdown.Plain(first);
            }
        }

        return "";
    }

    private void Build()
    {
        Children.Clear();
        Excerpt = "";
        var topic = Topic is null ? null : HelpContent.Find(Topic);
        if (topic is null)
        {
            return;
        }

        Excerpt = ExcerptOf(topic, Section);
        Children.Add(new TextBlock { Text = topic.Title, Classes = { "h2" }, TextWrapping = TextWrapping.Wrap });
        if (Excerpt.Length > 0)
        {
            Children.Add(new TextBlock { Text = Excerpt, Classes = { "secondary" }, TextWrapping = TextWrapping.Wrap });
        }

        var open = new Button { Content = "In der Hilfe öffnen", HorizontalAlignment = HorizontalAlignment.Left };
        open.Click += (_, _) => HelpContent.Open(topic.Id, Section);
        Children.Add(open);
    }
}
