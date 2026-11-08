using AispaStudio.Desktop.Services;
using AispaStudio.Help;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace AispaStudio.Desktop.Controls;

/// <summary>Absatz der Hilfe mit fetten Stellen, Code und Links (Verweise <c>hilfe:</c> öffnen die Hilfe, andere den Browser).</summary>
public sealed class HelpText : TextBlock
{
    public static readonly StyledProperty<IReadOnlyList<HelpSpan>?> SpansProperty =
        AvaloniaProperty.Register<HelpText, IReadOnlyList<HelpSpan>?>(nameof(Spans));

    static HelpText() => SpansProperty.Changed.AddClassHandler<HelpText>((t, _) => t.Build());

    public HelpText() => TextWrapping = TextWrapping.Wrap;

    protected override Type StyleKeyOverride => typeof(TextBlock);

    public IReadOnlyList<HelpSpan>? Spans
    {
        get => GetValue(SpansProperty);
        set => SetValue(SpansProperty, value);
    }

    private void Build()
    {
        Inlines ??= [];
        Inlines.Clear();
        foreach (var span in Spans ?? [])
        {
            if (HelpMarkdown.HelpLink(span.Href) is var (topic, section))
            {
                var link = new HyperlinkButton { Content = span.Text, Padding = new Thickness(0) };
                link.Click += (_, _) => HelpContent.Open(topic, section);
                Inlines.Add(new InlineUIContainer(link));
            }
            else if (span.Href is { } href && Uri.TryCreate(href, UriKind.Absolute, out var uri))
            {
                Inlines.Add(new InlineUIContainer(new HyperlinkButton { Content = span.Text, NavigateUri = uri, Padding = new Thickness(0) }));
            }
            else
            {
                var run = new Run(span.Text);
                if (span.Bold)
                {
                    run.FontWeight = FontWeight.SemiBold;
                }

                if (span.Code)
                {
                    run.FontFamily = new FontFamily("Consolas, Menlo, monospace");
                }

                Inlines.Add(run);
            }
        }
    }
}
