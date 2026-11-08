using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Grund für einen deaktivierten Knopf (Design-Brief 5 und 5.22, Fluent 2 „Button“): ein deaktivierter Knopf, dessen Grund nicht selbstverständlich ist, nennt
/// im Tooltip, wann er verfügbar wird („Verfügbar, sobald ein Name eingetragen ist.“). Solange der Knopf bedienbar ist, gilt sein eigener Tooltip (oder keiner).
/// Gesetzt wird der Text einmal im XAML (<c>ui:DisabledHint.Text="…"</c>), auch als Bindung; der eigene Tooltip des Knopfes muss ein fester Text sein.
/// Deaktivierte Steuerelemente zeigen Tooltips nur mit <see cref="ToolTip.ShowOnDisabledProperty"/>, deshalb schaltet dieser Baustein es mit ein.
/// Ein gesperrter Knopf nimmt keinen Tastaturfokus; damit der Grund auch ohne Maus lesbar ist, steht er solange zusätzlich im Hilfetext des Knopfes
/// (<see cref="AutomationProperties.HelpTextProperty"/>), den der Screenreader in der Elementliste und im Lesemodus mit dem Namen vorliest (W-Muster 12);
/// wie der Tooltip muss auch ein eigener Hilfetext des Knopfes ein fester Text sein.
/// </summary>
public static class DisabledHint
{
    /// <summary>Der Grund und der Weg, wie der Knopf verfügbar wird; leer = kein Hinweis.</summary>
    public static readonly AttachedProperty<string?> TextProperty = AvaloniaProperty.RegisterAttached<Control, Control, string?>("Text");

    /// <summary>Der eigene Tooltip des Knopfes, solange der Grund angezeigt wird.</summary>
    private static readonly AttachedProperty<object?> OwnTipProperty = AvaloniaProperty.RegisterAttached<Control, Control, object?>("OwnTip");

    /// <summary>Der eigene Hilfetext des Knopfes, solange der Grund angezeigt wird.</summary>
    private static readonly AttachedProperty<string?> OwnHelpTextProperty = AvaloniaProperty.RegisterAttached<Control, Control, string?>("OwnHelpText");

    private static readonly AttachedProperty<bool> ShowingProperty = AvaloniaProperty.RegisterAttached<Control, Control, bool>("Showing");

    static DisabledHint()
    {
        TextProperty.Changed.AddClassHandler<Control>((control, _) => Refresh(control));
        InputElement.IsEffectivelyEnabledProperty.Changed.AddClassHandler<Control>((control, _) => Refresh(control));
    }

    public static string? GetText(Control control) => control.GetValue(TextProperty);

    public static void SetText(Control control, string? value) => control.SetValue(TextProperty, value);

    /// <summary>Der Tooltip zeigt gerade den Grund und nicht den eigenen Text des Knopfes.</summary>
    public static bool IsShowing(Control control) => control.GetValue(ShowingProperty);

    private static void Refresh(Control control)
    {
        var reason = GetText(control);
        var wanted = !string.IsNullOrEmpty(reason) && !control.IsEffectivelyEnabled;
        if (wanted)
        {
            if (!IsShowing(control))
            {
                control.SetValue(OwnTipProperty, control.GetValue(ToolTip.TipProperty));
                control.SetValue(OwnHelpTextProperty, AutomationProperties.GetHelpText(control));
                control.SetValue(ShowingProperty, true);
                control.SetValue(ToolTip.ShowOnDisabledProperty, true);
            }

            control.SetValue(ToolTip.TipProperty, reason);
            AutomationProperties.SetHelpText(control, reason);
        }
        else if (IsShowing(control))
        {
            control.SetValue(ShowingProperty, false);
            control.SetValue(ToolTip.TipProperty, control.GetValue(OwnTipProperty));
            if (control.GetValue(OwnHelpTextProperty) is { } ownHelp)
            {
                AutomationProperties.SetHelpText(control, ownHelp);
            }
            else
            {
                control.ClearValue(AutomationProperties.HelpTextProperty);
            }

            control.ClearValue(OwnTipProperty);
            control.ClearValue(OwnHelpTextProperty);
            control.ClearValue(ToolTip.ShowOnDisabledProperty);
        }
    }
}
