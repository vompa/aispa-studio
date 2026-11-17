using AispaStudio.Desktop.Services;
using AispaStudio.Help;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Fragezeichen neben einer Einstellung; öffnet die Hilfe bei deren Beschreibung (<c>Topic</c> = Datei, <c>Section</c> = Anker) in der Leiste „KI und Hilfe“.
/// Der Rahmen der App fängt <see cref="RequestedEvent"/> ab und markiert das aktive Fragezeichen (Stil-Klasse <c>active</c>); ohne Rahmen geht es über <see cref="HelpContent.Open"/>.
/// </summary>
public sealed class HelpButton : Button
{
    public static readonly StyledProperty<string?> TopicProperty = AvaloniaProperty.Register<HelpButton, string?>(nameof(Topic));

    public static readonly StyledProperty<string?> SectionProperty = AvaloniaProperty.Register<HelpButton, string?>(nameof(Section));

    /// <summary>Das Fragezeichen will seine Hilfe zeigen; steigt bis zum Rahmen der App auf, der die Leiste öffnet und <c>Handled</c> setzt.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RequestedEvent = RoutedEvent.Register<HelpButton, RoutedEventArgs>("Requested", RoutingStrategies.Bubble);

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
        Click += (_, _) => Open();
    }

    /// <summary>Zeigt die Hilfe dieses Fragezeichens: über den Rahmen der App, sonst über <see cref="HelpContent.Open"/> (Fenster vor der Anmeldung).</summary>
    public void Open()
    {
        var request = new RoutedEventArgs(RequestedEvent, this);
        RaiseEvent(request);
        if (!request.Handled)
        {
            HelpContent.Open(Topic, Section);
        }
    }

    /// <summary>
    /// F1: das Fragezeichen zum Fokus. Gesucht wird im kleinsten Bereich um den Fokus, der ein sichtbares Fragezeichen enthält: Ist es genau eines, gehört es
    /// zum Abschnitt des Fokus. Enthält der Bereich mehrere (der Fokus liegt in keinem Abschnitt mit eigenem Fragezeichen) oder ist der Fokus nicht auf der Seite,
    /// gilt das Fragezeichen im Seitenkopf: das erste ohne Abschnitt, sonst das erste der Seite.
    /// </summary>
    public static HelpButton? NearestTo(Visual? focus, Visual page)
    {
        var buttons = page.GetVisualDescendants().OfType<HelpButton>().Where(b => b.IsEffectivelyVisible && b.Topic is not null).ToList();
        if (focus is HelpButton self && buttons.Contains(self))
        {
            return self;
        }

        for (var area = focus; area is not null && area != page; area = area.GetVisualParent())
        {
            var inside = buttons.Where(area.IsVisualAncestorOf).Take(2).ToList();
            if (inside.Count == 1)
            {
                return inside[0];
            }

            if (inside.Count > 1)
            {
                break;
            }
        }

        return buttons.FirstOrDefault(b => b.Section is null) ?? buttons.FirstOrDefault();
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
