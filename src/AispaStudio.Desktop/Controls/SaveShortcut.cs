using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Strg+S speichert in jeder Maske der Seitenleiste (Auftraggeber, 29.10.2026): ein Bereich (<see cref="IsScopeProperty"/>, die Seitenleiste) hört am Fenster im Tunnel
/// auf Strg+S, damit die Taste auch ankommt, wenn nach dem Schließen eines Felds nichts den Fokus hat. Sie löst den sichtbaren Knopf mit
/// <see cref="IsSaveProperty"/> aus, und zwar den, der dem Fokus am nächsten liegt; wie ein Klick nur, wenn er bedienbar ist. Ein Feld mit Fokus
/// übernimmt vorher seinen Text wie bei Enter; ist er unlesbar, bleibt es beim Fehler am Feld. Liegt der Fokus außerhalb des Bereichs, tut Strg+S hier nichts.
/// </summary>
public static class SaveShortcut
{
    public static readonly AttachedProperty<bool> IsScopeProperty = AvaloniaProperty.RegisterAttached<Control, Control, bool>("IsScope");

    public static readonly AttachedProperty<bool> IsSaveProperty = AvaloniaProperty.RegisterAttached<Control, Button, bool>("IsSave");

    private static readonly AttachedProperty<TopLevel?> ListeningOnProperty = AvaloniaProperty.RegisterAttached<Control, Control, TopLevel?>("ListeningOn");

    private static readonly ConditionalWeakTable<Control, EventHandler<KeyEventArgs>> Handlers = [];

    static SaveShortcut()
    {
        IsScopeProperty.Changed.AddClassHandler<Control>((scope, e) =>
        {
            scope.AttachedToVisualTree -= OnScopeAttached;
            scope.DetachedFromVisualTree -= OnScopeDetached;
            StopListening(scope);
            if (e.NewValue is true)
            {
                scope.AttachedToVisualTree += OnScopeAttached;
                scope.DetachedFromVisualTree += OnScopeDetached;
                StartListening(scope);
            }
        });
    }

    public static bool GetIsScope(Control control) => control.GetValue(IsScopeProperty);

    public static void SetIsScope(Control control, bool value) => control.SetValue(IsScopeProperty, value);

    public static bool GetIsSave(Button button) => button.GetValue(IsSaveProperty);

    public static void SetIsSave(Button button, bool value) => button.SetValue(IsSaveProperty, value);

    /// <summary>Wie Strg+S im Bereich: true, wenn der Bereich die Taste für sich nimmt (ein Speichern-Knopf ist sichtbar), auch wenn er gerade nicht bedienbar ist.</summary>
    public static bool TrySave(Control scope, Visual? focused)
    {
        var candidates = scope.GetVisualDescendants().OfType<Button>().Where(b => GetIsSave(b) && b.IsEffectivelyVisible).ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        if (focused?.FindAncestorOfType<ParsedField>(includeSelf: true) is { } field && !field.Commit())
        {
            return true;
        }

        var button = NearestTo(focused, scope, candidates);
        var parameter = button.CommandParameter;
        if (button.Command is { } command && command.CanExecute(parameter) && IsEnabledUpTo(button, scope))
        {
            command.Execute(parameter);
        }

        return true;
    }

    private static Button NearestTo(Visual? focused, Control scope, List<Button> candidates)
    {
        for (var ancestor = focused; ancestor is not null && ancestor != scope; ancestor = ancestor.GetVisualParent())
        {
            if (candidates.FirstOrDefault(ancestor.IsVisualAncestorOf) is { } near)
            {
                return near;
            }
        }

        return candidates[0];
    }

    /// <summary>Der Knopf selbst und alles bis zum Bereich ist eingeschaltet (<c>IsEnabled="{Binding !IsBusy}"</c> und Ähnliches).</summary>
    private static bool IsEnabledUpTo(Button button, Control scope)
    {
        for (Visual? v = button; v is not null && v != scope; v = v.GetVisualParent())
        {
            if (v is InputElement { IsEnabled: false })
            {
                return false;
            }
        }

        return scope.IsEffectivelyEnabled;
    }

    private static void OnScopeAttached(object? sender, VisualTreeAttachmentEventArgs e) => StartListening((Control)sender!);

    private static void OnScopeDetached(object? sender, VisualTreeAttachmentEventArgs e) => StopListening((Control)sender!);

    private static void StartListening(Control scope)
    {
        if (scope.GetValue(ListeningOnProperty) is not null || TopLevel.GetTopLevel(scope) is not { } root)
        {
            return;
        }

        scope.SetValue(ListeningOnProperty, root);
        root.AddHandler(InputElement.KeyDownEvent, Handler(scope), RoutingStrategies.Tunnel);
    }

    private static void StopListening(Control scope)
    {
        if (scope.GetValue(ListeningOnProperty) is { } root)
        {
            root.RemoveHandler(InputElement.KeyDownEvent, Handler(scope));
            scope.ClearValue(ListeningOnProperty);
        }
    }

    private static EventHandler<KeyEventArgs> Handler(Control scope) => Handlers.GetValue(scope, s => (_, e) => OnKeyDown(s, e));

    private static void OnKeyDown(Control scope, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.S || e.KeyModifiers != KeyModifiers.Control || !scope.IsEffectivelyVisible)
        {
            return;
        }

        var focused = TopLevel.GetTopLevel(scope)?.FocusManager?.GetFocusedElement() as Visual;
        if (focused is not null && focused != scope && !scope.IsVisualAncestorOf(focused))
        {
            return;
        }

        e.Handled = TrySave(scope, focused);
    }
}
