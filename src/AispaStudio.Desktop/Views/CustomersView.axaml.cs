using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

public partial class CustomersView : UserControl
{
    private CustomersViewModel? _customers;

    public CustomersView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Subscribe();
        KeyDown += OnKeyDown;
    }

    private void Subscribe()
    {
        if (_customers is not null)
        {
            _customers.PropertyChanged -= OnCustomersChanged;
        }

        _customers = DataContext as CustomersViewModel;
        if (_customers is not null)
        {
            _customers.PropertyChanged += OnCustomersChanged;
            _customers.ContactActions ??= new CalendarView.WindowContactActions(this);
            _customers.FilePicker ??= new StorageFilePicker(this);
        }
    }

    private void OnToastEntered(object? sender, PointerEventArgs e) => _customers?.HoldToast(true);

    private void OnToastExited(object? sender, PointerEventArgs e) => _customers?.HoldToast(false);

    /// <summary>Beim Öffnen der Kundendaten-Maske liegt der Fokus im ersten Feld (masken.md, R13), damit man gleich tippen kann; ebenso bei den Karten.</summary>
    private void OnCustomersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CustomersViewModel.IsEditingContact) && _customers is { IsEditingContact: true })
        {
            Dispatcher.UIThread.Post(() => ContactForm.FocusFirstField(), DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(CustomersViewModel.ShowNew) && _customers is { ShowNew: true })
        {
            // „Neue Kundin“ öffnet mit dem Fokus im Vornamen: sofort tippen (masken.md, R13).
            Dispatcher.UIThread.Post(() => NewFirstNameBox.Focus(), DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(CustomersViewModel.FocusTarget) && _customers is { FocusTarget: { } field } model)
        {
            // Fehler beim Anlegen: das erste ungültige Feld bekommt den Fokus (masken.md R8, meldungen.md M2).
            model.FocusTarget = null;
            Dispatcher.UIThread.Post(() => this.FindControl<Control>(field)?.Focus(), DispatcherPriority.Loaded);
        }
        else if (_customers is { } customers && CardFocus(customers, e.PropertyName) is { } card)
        {
            FocusCard(card);
        }
    }

    /// <summary>Beim Verlassen des Telefonfelds der Maske „Neue Kundin“ steht die Nummer im Format „+43 664 1234567“ (T-019).</summary>
    private async void OnNewPhoneLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CustomersViewModel customers)
        {
            await customers.FormatNewPhoneAsync();
        }
    }

    /// <summary>Beim Verlassen des E-Mail-Felds der Maske „Neue Kundin“ steht ein Fehler gleich am Feld, nicht erst beim Anlegen (Befund B5).</summary>
    private void OnNewEmailLostFocus(object? sender, RoutedEventArgs e) => (DataContext as CustomersViewModel)?.ValidateNewEmail();

    /// <summary>Esc schließt die offene Karte ohne zu speichern (masken.md R13), aber erst, wenn kein Feld und keine Auswahl die Taste gebraucht hat.</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Enter in einem Feld der Maske „Neue Kundin“ legt an (ohne Buchung); der Hauptknopf „Anlegen und Termin buchen“ gehört der Maus.
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && _customers is { ShowNew: true } customers && e.Source is TextBox box
            && NewMask.IsVisualAncestorOf(box) && customers.CreateCommand.CanExecute(null))
        {
            customers.CreateCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && _customers is not null && _customers.CancelCard())
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Zu einem Abschnittszustand: ob er offen ist, sein erstes Feld (Fokus beim Öffnen) und seine Öffner in Reihenfolge (Fokus nach dem Schließen geht an
    /// den ersten sichtbaren: Stift im Abschnitt, sonst der Link unter „Noch nicht erfasst“, sonst das Menü „…“).
    /// </summary>
    private (bool IsOpen, Func<Control> FirstField, Control[] Openers)? CardFocus(CustomersViewModel c, string? state) => state switch
    {
        nameof(CustomersViewModel.IsAddingFlag) => (c.IsAddingFlag, () => c.CanAddFlag ? FlagBox : CancelFlagButton, [AddFlagButton, AddFlagLink, MoreButton]),
        nameof(CustomersViewModel.IsEditingWishes) => (c.IsEditingWishes, () => WishPressureBox, [EditWishesButton, AddWishesLink]),
        nameof(CustomersViewModel.IsEditingReceptionNote) => (c.IsEditingReceptionNote, () => ReceptionNoteBox, [EditReceptionNoteButton, AddReceptionNoteLink]),
        nameof(CustomersViewModel.IsEditingBooking) => (c.IsEditingBooking, () => BookingEmployeeBox, [EditBookingButton, AddBookingLink]),
        nameof(CustomersViewModel.IsEditingBilling) => (c.IsEditingBilling, () => BillingNameBox, [EditBillingButton, MoreButton]),
        nameof(CustomersViewModel.IsEditingDiscount) => (c.IsEditingDiscount, () => DiscountPercentBox, [EditDiscountButton, MoreButton]),
        nameof(CustomersViewModel.IsAddingEmail) => (c.IsAddingEmail, () => OtherEmailBox, [MoreButton]),
        _ => null,
    };

    /// <summary>
    /// Abschnitt geöffnet: Fokus ins erste Feld. Abschnitt geschlossen: lag der Fokus in ihm (oder ist er weg), zurück auf den sichtbaren Öffner,
    /// sonst hätte niemand mehr den Fokus und Tasten kämen nicht mehr an.
    /// </summary>
    private void FocusCard((bool IsOpen, Func<Control> FirstField, Control[] Openers) card) => Dispatcher.UIThread.Post(() =>
    {
        if (card.IsOpen)
        {
            card.FirstField().Focus();
            return;
        }

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        // Logisch statt visuell: ein ausgeblendetes Feld hängt nicht immer im visuellen Baum.
        var section = card.FirstField().GetLogicalAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("section"));
        if (focused is null || !focused.IsEffectivelyVisible || section is null || focused.GetVisualAncestors().Contains(section))
        {
            (card.Openers.FirstOrDefault(o => o.IsEffectivelyVisible) ?? card.Openers[^1]).Focus();
        }
    }, DispatcherPriority.Background);
}
