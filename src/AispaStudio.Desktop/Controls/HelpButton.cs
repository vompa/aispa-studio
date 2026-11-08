using AispaStudio.Desktop.Services;
using AispaStudio.Help;
using Avalonia;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Controls;

/// <summary>Fragezeichen neben einer Einstellung; öffnet die Hilfe bei deren Beschreibung (<c>Topic</c> = Datei, <c>Section</c> = Anker).</summary>
public sealed class HelpButton : Button
{
    public static readonly StyledProperty<string?> TopicProperty = AvaloniaProperty.Register<HelpButton, string?>(nameof(Topic));

    public static readonly StyledProperty<string?> SectionProperty = AvaloniaProperty.Register<HelpButton, string?>(nameof(Section));

    static HelpButton()
    {
        TopicProperty.Changed.AddClassHandler<HelpButton>((button, _) => button.NameFromTopic());
        SectionProperty.Changed.AddClassHandler<HelpButton>((button, _) => button.NameFromTopic());
    }

    public HelpButton()
    {
        Content = "?";
        Classes.Add("help");
        ToolTip.SetTip(this, "Hilfe");
        Avalonia.Automation.AutomationProperties.SetName(this, "Hilfe");
        Click += (_, _) => HelpContent.Open(Topic, Section);
    }

    /// <summary>Der Name nennt, wozu die Hilfe gehört („Hilfe zu Kontakt“), sonst klingen mehrere Fragezeichen einer Maske für den Screenreader gleich.</summary>
    private void NameFromTopic()
    {
        var topic = Topic is null ? null : HelpContent.Find(Topic);
        var subject = topic is not null && Section is not null && topic.Blocks.OfType<HelpHeading>().FirstOrDefault(h => h.Id == Section) is { } heading ? heading.Text : topic?.Title;
        Avalonia.Automation.AutomationProperties.SetName(this, subject is null ? "Hilfe" : $"Hilfe zu {subject}");
    }

    protected override Type StyleKeyOverride => typeof(Button);

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
}
