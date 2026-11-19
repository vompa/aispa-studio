using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Rahmen der App. F6 und Umschalt+F6 wechseln zwischen den großen Bereichen (<see cref="RegionNavigation"/>). „KI und Hilfe“ folgt denselben Regeln wie die Seiten der Seitenleiste im Kalender,
/// wo der Assistent ein Reiter ist (Ä66), und auf den übrigen Seiten die Leiste der Shell: Strg+Leertaste öffnet und schließt,
/// Esc schließt ihn, der Fokus geht beim Öffnen ins Fragefeld und beim Schließen zum Knopf zurück.
/// Ein Fragezeichen (<see cref="HelpButton"/>) öffnet die Hilfe in derselben Leiste und ist markiert, solange seine Hilfe steht; Esc schließt dann zuerst die Hilfe.
/// F1 zeigt außerhalb des Kalenders die Hilfe zum Fokus (im Kalender bleibt F1 die Tastenhilfe).
/// </summary>
public partial class ShellView : UserControl
{
    private CompanionViewModel? _companion;
    private ShellViewModel? _shell;
    private TopLevel? _topLevel;

    /// <summary>Das Fragezeichen, dessen Hilfe gerade in der Leiste steht (Stil-Klasse <c>active</c>).</summary>
    private HelpButton? _activeHelp;

    public ShellView()
    {
        InitializeComponent();

        // Ein Mausklick beendet den Tastaturmodus: die Leiste bewegt sich dann wieder.
        AddHandler(PointerPressedEvent, (_, _) =>
        {
            if (_companion is { Presence.UsingKeyboard: true })
            {
                _companion.Presence.UsingKeyboard = false;
            }

            if ((DataContext as ShellViewModel)?.Calendar.Presence is { UsingKeyboard: true } calendar)
            {
                calendar.UsingKeyboard = false;
            }
        }, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => Subscribe();
        AddHandler(HelpButton.RequestedEvent, OnHelpRequested);
    }

    // Der Mauszeiger über dem Toast hält seine Frist an (meldungen.md M1).
    private void OnToastEntered(object? sender, PointerEventArgs e) => (DataContext as ShellViewModel)?.Confirmation.Hold(true);

    private void OnToastExited(object? sender, PointerEventArgs e) => (DataContext as ShellViewModel)?.Confirmation.Hold(false);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Tunnel am Fenster, damit Strg+Leertaste, Esc und F6 auf jeder Seite gelten: auch wenn der Kalender oder ein Eingabefeld den Fokus hat,
        // und auch, wenn nach einem Seitenwechsel nichts mehr den Fokus hat (Verwaltung, Hilfe) und eine Taste nur noch das Fenster erreicht.
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnKey);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void Subscribe()
    {
        if (_companion is { } old)
        {
            old.Toggled -= OnToggled;
            old.PropertyChanged -= OnCompanionChanged;
        }

        if (_shell is { } oldShell)
        {
            oldShell.HelpKeyRequested -= ShowHelpForFocus;
        }

        _shell = DataContext as ShellViewModel;
        _companion = _shell?.Companion;
        if (_companion is { } companion)
        {
            companion.Toggled += OnToggled;
            companion.PropertyChanged += OnCompanionChanged;
        }

        if (_shell is { } shell)
        {
            shell.HelpKeyRequested += ShowHelpForFocus;
        }
    }

    /// <summary>Ein Fragezeichen will seine Hilfe zeigen: in der Leiste „KI und Hilfe“, die Seite bleibt stehen; das Fragezeichen ist danach markiert.</summary>
    private void OnHelpRequested(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not HelpButton button || _shell is not { } shell)
        {
            return;
        }

        e.Handled = shell.ShowHelpInPanel(button.Topic, button.Section);
        MarkActive(_companion?.ShowsHelp == true ? button : null);
    }

    private void OnCompanionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompanionViewModel.ShowsHelp) && _companion?.ShowsHelp != true)
        {
            MarkActive(null);
        }
    }

    /// <summary>Nur das Fragezeichen der gezeigten Hilfe ist markiert; ein weiteres tauscht die Markierung.</summary>
    private void MarkActive(HelpButton? button)
    {
        _activeHelp?.Classes.Set("active", false);
        _activeHelp = button;
        _activeHelp?.Classes.Set("active", true);
    }

    /// <summary>F1: die Hilfe zum Fokus (das Fragezeichen im Bereich des Fokus, sonst das im Seitenkopf, sonst die Erklärung zur Seite).</summary>
    private void ShowHelpForFocus()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var inPage = focused is not null && PageHost.IsVisualAncestorOf(focused) ? focused : null;
        if (HelpButton.NearestTo(inPage, PageHost) is { } button)
        {
            button.Open();
        }
        else
        {
            _shell?.ShowHelpInPanel(null, null);
        }
    }

    /// <summary>
    /// Offen: der Fokus geht ins Fragefeld, damit man gleich lostippen kann (ohne KI bleibt er, wo er war).
    /// Zu: war der Fokus in der Leiste, geht er zum Knopf unten rechts zurück, sonst bleibt er, wo er ist.
    /// </summary>
    private void OnToggled(bool open)
    {
        if (open)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_companion is { CanAsk: true })
                {
                    CompanionHost()?.FocusQuestion();
                }
            }, DispatcherPriority.Loaded);
        }
        else if (FocusInsideCompanion())
        {
            // Der Knopf erscheint erst, wenn die Bindung die Leiste als zu erkannt hat: danach fokussieren.
            Dispatcher.UIThread.Post(() => CompanionFab.Focus(), DispatcherPriority.Loaded);
        }
    }

    /// <summary>Der Inhalt von „KI und Hilfe“, wo er gerade steht: im Reiter der Seitenleiste (Kalender) oder in der Leiste der Shell; es gibt immer nur einen.</summary>
    private CompanionContent? CompanionHost() => this.GetVisualDescendants().OfType<CompanionContent>().FirstOrDefault();

    private bool FocusInsideCompanion() =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused && CompanionHost() is { } host && host.IsVisualAncestorOf(focused);

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ShellViewModel { Companion: { } companion } shell)
        {
            return;
        }

        // Ist die Shell verborgen (keine Verbindung) oder liegt der Fokus außerhalb (Rückfragedialog über allem), gehören die Tasten nicht ihr.
        if (!IsEffectivelyVisible
            || (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused && focused != this && !this.IsVisualAncestorOf(focused)))
        {
            return;
        }

        if (e.Key == Key.F6 && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            // Bereichswechsel (Design-Brief 9): Navigation, Kopfzeile, Kalenderfläche, Seitenleiste, Reiter unten, KI und Hilfe; die Seite ohne eigene Bereiche zählt als einer.
            e.Handled = RegionNavigation.Move(this, PageHost, backwards: e.KeyModifiers == KeyModifiers.Shift);
        }
        else if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
        {
            // Zuerst merken, dass es ein Kurzbefehl ist: die Leiste soll ohne Bewegung erscheinen oder verschwinden.
            // Im Kalender wählt das den Reiter „KI und Hilfe“ (öffnet die Seitenleiste, falls sie zu ist) oder schließt ihn; die übrigen Reiter bleiben (siehe ShellViewModel).
            companion.Presence.UsingKeyboard = true;
            shell.Calendar.Presence.UsingKeyboard = true;
            var hadFocus = FocusInsideCompanion();
            companion.Toggle();
            if (!companion.IsOpen && !hadFocus)
            {
                CompanionFab.Focus();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.F1 && e.KeyModifiers == KeyModifiers.None && shell.SelectedPage?.Page != shell.Calendar)
        {
            // Im Kalender bleibt F1 die Tastenhilfe (CalendarViewModel.Keyboard); sonst die Hilfe zum Feld mit dem Fokus.
            ShowHelpForFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && companion.IsOpen && !shell.Calendar.IsDragging && CanCloseWithEscape())
        {
            // Esc schließt die oberste Leiste: überall in der Leiste, sonst nur, wenn nicht gerade ein anderes Eingabefeld (z. B. die Suche) Esc braucht.
            // Steht eine Hilfe, geht zuerst nur sie weg (war die Leiste nur für sie offen, mit ihr die Leiste).
            companion.Presence.UsingKeyboard = true;
            shell.Calendar.Presence.UsingKeyboard = true;
            if (companion.ShowsHelp)
            {
                companion.CloseHelp();
            }
            else
            {
                companion.Dismiss();
            }

            e.Handled = true;
        }
    }

    private bool CanCloseWithEscape() =>
        FocusInsideCompanion() || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not TextBox;
}
