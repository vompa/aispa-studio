using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Das Menü „⋯“ im Kopf einer Karte oder am Ende einer Zeile (ADR 0099): „Nicht mehr verwenden“ und „Löschen …“ stehen nie als eigener Knopf neben „Speichern“, sondern hier.
/// Wo der erste Eintrag nicht reicht, kommt ein zweiter neutraler dazwischen (Aktion: „Pausieren“ und „Archivieren“). Ein Eintrag erscheint nur, wenn sein Befehl gesetzt ist; ob der Knopf selbst zu sehen ist, bestimmt die Maske (<c>IsVisible</c>), und ein Eintrag, dessen Befehl nicht ausführbar ist, ist grau. Das Menü bekommt die Tastatur von Avalonia (Enter öffnet, Pfeile wählen, Esc schließt);
/// „Löschen …“ ist rot und trägt die drei Punkte, weil danach ein Dialog fragt.
/// </summary>
public sealed class MoreMenuButton : Button
{
    public const string Hint = "Weitere Aktionen";
    public const string DeactivateLabel = "Nicht mehr verwenden";
    public const string DeleteLabel = "Löschen …";

    /// <summary>Drei Punkte auf der Mittellinie (Linien-Symbol wie die übrigen, auf dem 24×24-Raster).</summary>
    private const string Dots = "M5 12h.01M12 12h.01M19 12h.01";

    public static readonly StyledProperty<ICommand?> DeactivateCommandProperty = AvaloniaProperty.Register<MoreMenuButton, ICommand?>(nameof(DeactivateCommand));

    public static readonly StyledProperty<ICommand?> DeleteCommandProperty = AvaloniaProperty.Register<MoreMenuButton, ICommand?>(nameof(DeleteCommand));

    /// <summary>Eigener Text für den ersten Eintrag, wo „Nicht mehr verwenden“ nicht trifft (Person: „Zugang sperren“); leer = Standardtext.</summary>
    public static readonly StyledProperty<string?> DeactivateTextProperty = AvaloniaProperty.Register<MoreMenuButton, string?>(nameof(DeactivateText));

    public static readonly StyledProperty<ICommand?> SecondaryCommandProperty = AvaloniaProperty.Register<MoreMenuButton, ICommand?>(nameof(SecondaryCommand));

    /// <summary>Text des zweiten neutralen Eintrags zwischen dem ersten und „Löschen …“ (Aktion: „Archivieren“); er steht nur mit gesetztem Befehl und Text, ohne drei Punkte und nicht rot.</summary>
    public static readonly StyledProperty<string?> SecondaryTextProperty = AvaloniaProperty.Register<MoreMenuButton, string?>(nameof(SecondaryText));

    /// <summary>Eigener Text für den letzten Eintrag, wo „Löschen …“ nicht trifft (Person: „Aus dem Team nehmen …“); er bleibt rot und trägt die drei Punkte, weil danach eine Rückfrage kommt.</summary>
    public static readonly StyledProperty<string?> DeleteTextProperty = AvaloniaProperty.Register<MoreMenuButton, string?>(nameof(DeleteText));

    /// <summary>Der Gegenstand für den Screenreader: „Weitere Aktionen zu Hauptlager“.</summary>
    public static readonly StyledProperty<string?> SubjectProperty = AvaloniaProperty.Register<MoreMenuButton, string?>(nameof(Subject));

    private readonly MenuFlyout _menu = new() { Placement = PlacementMode.BottomEdgeAlignedRight };

    static MoreMenuButton()
    {
        DeactivateCommandProperty.Changed.AddClassHandler<MoreMenuButton>((button, _) => button.Rebuild());
        DeleteCommandProperty.Changed.AddClassHandler<MoreMenuButton>((button, _) => button.Rebuild());
        DeactivateTextProperty.Changed.AddClassHandler<MoreMenuButton>((button, _) => button.Rebuild());
        DeleteTextProperty.Changed.AddClassHandler<MoreMenuButton>((button, _) => button.Rebuild());
        SecondaryCommandProperty.Changed.AddClassHandler<MoreMenuButton>((button, _) => button.Rebuild());
        SecondaryTextProperty.Changed.AddClassHandler<MoreMenuButton>((button, _) => button.Rebuild());
        SubjectProperty.Changed.AddClassHandler<MoreMenuButton>((button, _) => button.UpdateName());
    }

    public MoreMenuButton()
    {
        Classes.Add("ghost");
        Classes.Add("icon");
        Width = 28;
        Height = 28;
        MinHeight = 28;
        Content = new Icon { Data = Dots, Width = 16, Height = 16, StrokeThickness = 3 };
        Flyout = _menu;
        ToolTip.SetTip(this, Hint);
        UpdateName();
        Rebuild();
    }

    protected override Type StyleKeyOverride => typeof(Button);

    public ICommand? DeactivateCommand
    {
        get => GetValue(DeactivateCommandProperty);
        set => SetValue(DeactivateCommandProperty, value);
    }

    public ICommand? SecondaryCommand
    {
        get => GetValue(SecondaryCommandProperty);
        set => SetValue(SecondaryCommandProperty, value);
    }

    public string? SecondaryText
    {
        get => GetValue(SecondaryTextProperty);
        set => SetValue(SecondaryTextProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public string? DeactivateText
    {
        get => GetValue(DeactivateTextProperty);
        set => SetValue(DeactivateTextProperty, value);
    }

    public string? DeleteText
    {
        get => GetValue(DeleteTextProperty);
        set => SetValue(DeleteTextProperty, value);
    }

    public string? Subject
    {
        get => GetValue(SubjectProperty);
        set => SetValue(SubjectProperty, value);
    }

    private void UpdateName() => AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Subject) ? Hint : $"{Hint} zu {Subject}");

    private void Rebuild()
    {
        _menu.Items.Clear();
        if (DeactivateCommand is { } deactivate)
        {
            _menu.Items.Add(new MenuItem { Header = string.IsNullOrWhiteSpace(DeactivateText) ? DeactivateLabel : DeactivateText, Command = deactivate });
        }

        if (SecondaryCommand is { } secondary && !string.IsNullOrWhiteSpace(SecondaryText))
        {
            _menu.Items.Add(new MenuItem { Header = SecondaryText, Command = secondary });
        }

        if (DeleteCommand is { } delete)
        {
            var item = new MenuItem { Header = string.IsNullOrWhiteSpace(DeleteText) ? DeleteLabel : DeleteText, Command = delete };
            item.Classes.Add("danger");
            _menu.Items.Add(item);
        }
    }
}
