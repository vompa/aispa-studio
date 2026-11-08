using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Fußleiste der Masken in der Verwaltung (R05 Slice 93): „Speichern“ (primär, Strg+S über <see cref="SaveShortcut"/>), „Verwerfen“ (nur mit Änderungen
/// bedienbar) und rechts daneben der Stand („Gespeichert.“), den der Screenreader ansagt. Die Maske um die Leiste ist der Bereich für Strg+S
/// (<c>ui:SaveShortcut.IsScope="True"</c>).
/// </summary>
public sealed class SaveBar : StackPanel
{
    public static readonly StyledProperty<ICommand?> SaveCommandProperty = AvaloniaProperty.Register<SaveBar, ICommand?>(nameof(SaveCommand));

    public static readonly StyledProperty<ICommand?> DiscardCommandProperty = AvaloniaProperty.Register<SaveBar, ICommand?>(nameof(DiscardCommand));

    public static readonly StyledProperty<bool> CanDiscardProperty = AvaloniaProperty.Register<SaveBar, bool>(nameof(CanDiscard));

    public static readonly StyledProperty<string?> StatusProperty = AvaloniaProperty.Register<SaveBar, string?>(nameof(Status));

    private readonly Button _save = new() { Name = "SaveButton", Content = "Speichern" };
    private readonly Button _discard = new() { Name = "DiscardButton", Content = "Verwerfen" };
    private readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center };

    static SaveBar()
    {
        SaveCommandProperty.Changed.AddClassHandler<SaveBar>((bar, e) => bar._save.Command = e.NewValue as ICommand);
        DiscardCommandProperty.Changed.AddClassHandler<SaveBar>((bar, e) => bar._discard.Command = e.NewValue as ICommand);
        CanDiscardProperty.Changed.AddClassHandler<SaveBar>((bar, e) => bar._discard.IsEnabled = e.NewValue is true);
        StatusProperty.Changed.AddClassHandler<SaveBar>((bar, e) => bar._status.Text = e.NewValue as string);
    }

    public SaveBar()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        _save.Classes.Add("primary");
        SaveShortcut.SetIsSave(_save, true);
        ToolTip.SetTip(_save, "Speichern (Strg+S)");
        _discard.Classes.Add("ghost");
        _discard.IsEnabled = false;
        ToolTip.SetTip(_discard, "Zurück zum gespeicherten Stand");
        DisabledHint.SetText(_discard, "Verfügbar, sobald etwas geändert ist.");
        _status.Classes.Add("muted");
        _status.Margin = new Thickness(4, 0, 0, 0);
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        Children.Add(_save);
        Children.Add(_discard);
        Children.Add(_status);
    }

    public ICommand? SaveCommand
    {
        get => GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }

    public ICommand? DiscardCommand
    {
        get => GetValue(DiscardCommandProperty);
        set => SetValue(DiscardCommandProperty, value);
    }

    /// <summary>Es gibt ungespeicherte Änderungen; nur dann lässt sich verwerfen.</summary>
    public bool CanDiscard
    {
        get => GetValue(CanDiscardProperty);
        set => SetValue(CanDiscardProperty, value);
    }

    /// <summary>Stand nach dem Speichern, z. B. „Gespeichert.“; leer, sobald wieder etwas geändert wird.</summary>
    public string? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
}
