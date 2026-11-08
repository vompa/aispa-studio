using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Gemeinsames Verhalten aller Textfelder (Design-Brief 5.16), per Stil an jedes <c>TextBox</c> gehängt (<c>FieldBehaviors.IsActive</c>):
/// <list type="bullet">
/// <item>Tab, Umschalt+Tab und programmatischer Fokus setzen die Marke ans Ende und markieren nichts (Bearbeiten; die schnellen Felder
/// <see cref="ParsedField"/> markieren dagegen alles, weil man dort ersetzt); ein Klick setzt immer nur die Marke.</item>
/// <item>Esc in einem geänderten Feld stellt den Wert beim Betreten bzw. nach Enter wieder her, markiert ihn und verbraucht die Taste;
/// ein unverändertes Feld lässt Esc durch, damit die Maske schließt.</item>
/// <item>Enter in einem einzeiligen Feld bestätigt den Wert (Stand für Esc) und löst den Standardknopf der Maske aus (<c>Button.IsDefault</c>,
/// das erledigt Avalonia); in mehrzeiligen Feldern ist Enter der Zeilenumbruch und Strg+Enter löst den Standardknopf aus.</item>
/// </list>
/// </summary>
public static class FieldBehaviors
{
    public static readonly AttachedProperty<bool> IsActiveProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("IsActive", typeof(FieldBehaviors));

    private static readonly AttachedProperty<string?> ConfirmedProperty =
        AvaloniaProperty.RegisterAttached<TextBox, string?>("Confirmed", typeof(FieldBehaviors));

    static FieldBehaviors() => IsActiveProperty.Changed.AddClassHandler<TextBox>(OnActiveChanged);

    public static bool GetIsActive(TextBox box) => box.GetValue(IsActiveProperty);

    public static void SetIsActive(TextBox box, bool value) => box.SetValue(IsActiveProperty, value);

    /// <summary>Ob Esc in diesem Feld einen geänderten Wert zurückstellen würde (dann soll kein Fremder die Taste vorher verbrauchen).</summary>
    public static bool HasPendingChange(object? focused) => focused switch
    {
        ParsedField field => field.HasPendingChange,
        SearchField search => !string.IsNullOrEmpty(search.Text),
        TextBox box => (box.Text ?? "") != (box.GetValue(ConfirmedProperty) ?? ""),
        _ => false,
    };

    private static void OnActiveChanged(TextBox box, AvaloniaPropertyChangedEventArgs e)
    {
        box.GotFocus -= OnGotFocus;
        box.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        if (e.NewValue is true)
        {
            box.GotFocus += OnGotFocus;
            box.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        }
    }

    private static void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is not TextBox box || box is ParsedField or SearchField)
        {
            return;
        }

        box.SetValue(ConfirmedProperty, box.Text);
        if (e.NavigationMethod != NavigationMethod.Pointer)
        {
            CaretToEnd(box);
        }
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box is ParsedField or SearchField || e.Handled)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape when e.KeyModifiers == KeyModifiers.None && HasPendingChange(box):
                box.Text = box.GetValue(ConfirmedProperty);
                box.SelectAll();
                e.Handled = true;
                break;
            case Key.Enter when e.KeyModifiers == KeyModifiers.None && !box.AcceptsReturn:
                box.SetValue(ConfirmedProperty, box.Text);
                break;
            case Key.Enter when e.KeyModifiers == KeyModifiers.Control && box.AcceptsReturn:
                e.Handled = InvokeDefaultButton(box);
                break;
        }
    }

    internal static void CaretToEnd(TextBox box)
    {
        box.ClearSelection();
        box.CaretIndex = box.Text?.Length ?? 0;
    }

    /// <summary>Löst den Standardknopf (<c>IsDefault</c>) im Fenster des Feldes aus; false, wenn es keinen sichtbaren, bedienbaren gibt.</summary>
    internal static bool InvokeDefaultButton(Visual from)
    {
        if (TopLevel.GetTopLevel(from) is not { } root)
        {
            return false;
        }

        var button = root.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsDefault && b.IsEffectivelyVisible && b.IsEffectivelyEnabled);
        if (button is null)
        {
            return false;
        }

        if (button.Command is { } command && command.CanExecute(button.CommandParameter))
        {
            command.Execute(button.CommandParameter);
        }

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        return true;
    }
}
