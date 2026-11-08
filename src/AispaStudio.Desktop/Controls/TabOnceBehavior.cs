using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Tab hängt nicht an ausgeblendeten Listen (R04 Slice 83, Ä146): Eine Liste ist für Tab ein Stopp (<see cref="KeyboardNavigationMode.Once"/>) und merkt sich den zuletzt
/// fokussierten Eintrag als Ziel. Wird sie danach ausgeblendet (Trefferliste nach der Wahl eines Gastes), will Tab trotzdem in diesen Eintrag, der Fokus bleibt stehen und
/// Tab kommt nicht weiter. Ein solcher Bereich vergisst sein Ziel, wenn er (oder ein Bereich um ihn) ausgeblendet wird oder das Ziel unsichtbar ist; wird eine Liste
/// wieder eingeblendet, ist ihr Ziel wieder der gewählte Eintrag.
/// </summary>
public static class TabOnceBehavior
{
    private static bool Registered;

    /// <summary>Trägt das Verhalten ein; mehrfaches Aufrufen schadet nicht.</summary>
    public static void Register()
    {
        if (Registered)
        {
            return;
        }

        Registered = true;
        Visual.IsVisibleProperty.Changed.AddClassHandler<InputElement>((element, e) =>
        {
            if (e.NewValue is false)
            {
                Forget(element);
            }
            else if (e.NewValue is true)
            {
                Restore(element);
            }
        });
        // Ein Klick auf einen Eintrag wählt ihn, und die Wahl blendet die Liste aus, bevor der Eintrag als Ziel gemerkt wird: dann gleich danach vergessen.
        KeyboardNavigation.TabOnceActiveElementProperty.Changed.AddClassHandler<InputElement>((area, e) =>
        {
            if (e.NewValue is Visual { IsEffectivelyVisible: false })
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (KeyboardNavigation.GetTabOnceActiveElement(area) is Visual { IsEffectivelyVisible: false })
                    {
                        KeyboardNavigation.SetTabOnceActiveElement(area, null);
                    }
                });
            }
        });
    }

    private static void Forget(InputElement hidden)
    {
        foreach (var area in hidden.GetVisualDescendants().OfType<InputElement>().Prepend(hidden))
        {
            if (KeyboardNavigation.GetTabNavigation(area) == KeyboardNavigationMode.Once && KeyboardNavigation.GetTabOnceActiveElement(area) is not null)
            {
                KeyboardNavigation.SetTabOnceActiveElement(area, null);
            }
        }
    }

    private static void Restore(InputElement shown)
    {
        foreach (var list in shown.GetVisualDescendants().OfType<SelectingItemsControl>().Prepend(shown as SelectingItemsControl))
        {
            if (list is { SelectedIndex: >= 0 } && KeyboardNavigation.GetTabNavigation(list) == KeyboardNavigationMode.Once
                && KeyboardNavigation.GetTabOnceActiveElement(list) is null && list.ContainerFromIndex(list.SelectedIndex) is { } chosen)
            {
                KeyboardNavigation.SetTabOnceActiveElement(list, chosen);
            }
        }
    }
}
