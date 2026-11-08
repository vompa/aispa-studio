using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Technische Angaben zu einer Einstellung (Schalter, Port, Protokoll, Verbindungszeichenfolge) hinter einem Akkordeon-Kopf „Technische Angaben“. Im Fließtext steht nur
/// der Kurzsatz, den man zum Handeln braucht (content-design.md: kein technischer Text im Hauptteil); wer es genau wissen will, klappt auf. Zu beim Öffnen der Maske.
/// </summary>
public sealed class TechnicalDetails : StackPanel
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<TechnicalDetails, string?>(nameof(Text));

    private readonly AccordionHeader _header = new() { HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0, 0, 6, 0), MinHeight = 28 };
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false };

    static TechnicalDetails() => TextProperty.Changed.AddClassHandler<TechnicalDetails>((details, e) => details._text.Text = e.NewValue as string);

    public TechnicalDetails()
    {
        Spacing = 4;
        _header.Classes.Add("more");
        AutomationProperties.SetName(_header, "Technische Angaben");
        _header.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Technische Angaben", Classes = { "small" }, FontWeight = FontWeight.Medium },
                new Icon { Data = Icons.ChevronDown, Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center, Classes = { "chevron" } },
            },
        };
        _text.Classes.Add("muted");
        _text.Classes.Add("small");
        _header.IsCheckedChanged += (_, _) => _text.IsVisible = _header.IsChecked == true;
        Children.Add(_header);
        Children.Add(_text);
    }

    /// <summary>Der technische Text, sichtbar nur im aufgeklappten Zustand.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
