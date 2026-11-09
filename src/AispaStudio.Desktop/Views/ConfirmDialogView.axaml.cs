using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Der Rückfragedialog über dem Fenster (Design-Brief 5.27). Die Ansicht kümmert sich um den Fokus: beim Öffnen auf „Abbrechen“ (zerstörende Aktion) bzw. auf den primären Knopf,
/// Esc ist „Abbrechen“, und beim Schließen kehrt der Fokus zum Auslöser zurück. Wer sonst die Tastatur bedient, bleibt im Dialog (Tab läuft im Kreis).
/// </summary>
public partial class ConfirmDialogView : UserControl
{
    private ConfirmDialogHost? _host;
    private IInputElement? _trigger;

    public ConfirmDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Subscribe();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        SizeChanged += (_, _) => FitCard();
    }

    /// <summary>
    /// Eine Rückfrage mit Liste und Textfeld (Absage wegen einer Schließzeit) darf das Fenster nicht sprengen: die Karte wird höchstens so hoch wie das Fenster abzüglich 24 px Rand je Seite,
    /// der Inhalt scrollt in sich und die Knöpfe bleiben unten sichtbar. Kurze Rückfragen merken davon nichts.
    /// </summary>
    private void FitCard()
    {
        if (this.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "DialogCard") is { } card)
        {
            card.MaxHeight = Math.Max(240, Bounds.Height - 48);
        }
    }

    private void Subscribe()
    {
        if (_host is { } old)
        {
            old.PropertyChanged -= OnHostChanged;
        }

        _host = DataContext as ConfirmDialogHost;
        if (_host is { } host)
        {
            host.PropertyChanged += OnHostChanged;
        }
    }

    private void OnHostChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConfirmDialogHost.Current))
        {
            return;
        }

        if (_host?.Current is { } dialog)
        {
            // Der Auslöser ist der Knopf (oder das Feld), das gerade den Fokus hat.
            _trigger = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            Dispatcher.UIThread.Post(() =>
            {
                FitCard();
                FocusInitial(dialog);
            }, DispatcherPriority.Loaded);
        }
        else if (_trigger is { } trigger)
        {
            _trigger = null;
            Dispatcher.UIThread.Post(() =>
            {
                if (trigger is Visual { IsEffectivelyVisible: true } visual && TopLevel.GetTopLevel(visual) is not null)
                {
                    trigger.Focus();
                }
            }, DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// Mit Eingabefeld (z. B. Grund beim Storno) bekommt das Feld den Anfangsfokus (Fluent 2); Enter im Feld löst nichts aus. Ohne Feld: zerstörende Aktion auf „Abbrechen“,
    /// damit Enter sie nie auslöst; sonst auf den primären Knopf.
    /// </summary>
    private void FocusInitial(ConfirmDialogViewModel dialog)
    {
        // „Per Mail senden“ ohne vorbelegte Adresse: der Fokus liegt im Feld „An“; mit Adresse auf „Senden“ (ADR 0091).
        if (dialog is SendDocumentDialogViewModel { HasAddress: false })
        {
            this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(b => b.Name == "SendTo")?.Focus(NavigationMethod.Tab);
            return;
        }

        if (dialog.HasInput)
        {
            this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(b => b.Name == "DialogInput")?.Focus(NavigationMethod.Tab);
            return;
        }

        var name = dialog.IsDestructive ? "CancelButton" : "ConfirmButton";
        this.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == name)?.Focus(NavigationMethod.Tab);
    }

    /// <summary>
    /// Esc ist „Abbrechen“, außer der Dialog speichert gerade. Enter in einem einzeiligen Feld verbraucht die Ansicht: sonst stiege die Taste bis zum Fenster und löste einen Standardknopf (<c>IsDefault</c>) hinter dem Overlay aus.
    /// </summary>
    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (_host?.Current is not { } dialog)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (dialog.CancelCommand.CanExecute(null))
            {
                dialog.CancelCommand.Execute(null);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.Source is TextBox { AcceptsReturn: false })
        {
            // Im Dialog „Per Mail senden“ sendet Enter aus einem einzeiligen Feld (Adresse, Betreff); sonst löst Enter im Feld nichts aus.
            if (dialog is SendDocumentDialogViewModel send && send.SendCommand.CanExecute(null))
            {
                send.SendCommand.Execute(null);
            }

            e.Handled = true;
        }
    }
}
