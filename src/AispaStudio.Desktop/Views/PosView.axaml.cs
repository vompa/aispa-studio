using System.ComponentModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

public partial class PosView : UserControl
{
    public PosView()
    {
        InitializeComponent();
        // Per Tunnel: Suchfeld und Liste behandeln Pfeiltasten selbst und ließen sie sonst nicht durch.
        PosScanField.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        TodayList.AddHandler(KeyDownEvent, OnTodayListKeyDown, RoutingStrategies.Tunnel);
        ArticleList.AddHandler(KeyDownEvent, OnArticleListKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => Watch(_attached ? DataContext as PosViewModel : null);
    }

    private bool _attached;

    private PosViewModel? _pos;

    /// <summary>Beim Öffnen der Kassa liegt der Fokus im Suchfeld „Gast, Code oder Artikel“ (Slice 137): die Rezeption tippt oder scannt sofort.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Watch(DataContext as PosViewModel);
        FocusSearch(DispatcherPriority.Input);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        Watch(null);
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Hört nur, solange die Ansicht angehängt ist: die Kassa lebt in der Shell, die Ansicht entsteht bei jeder Navigation neu.</summary>
    private void Watch(PosViewModel? pos)
    {
        if (_pos is not null)
        {
            _pos.PropertyChanged -= OnPosChanged;
        }

        _pos = pos;
        if (_pos is not null)
        {
            _pos.PropertyChanged += OnPosChanged;
        }
    }

    /// <summary>Zurück auf den Reiter „Kassieren“ (von „Belege“ oder „Kassenlade &amp; Abschluss“) liegt der Fokus wieder im Suchfeld (T-027).</summary>
    private void OnPosChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PosViewModel.IsCheckout) && _pos is { IsCheckout: true })
        {
            // Das Suchfeld wird erst über die Bindung sichtbar: nach dem Layout fokussieren.
            FocusSearch(DispatcherPriority.Background);
        }
    }

    private void FocusSearch(DispatcherPriority priority) => Dispatcher.UIThread.Post(() => PosScanField.Focus(), priority);

    /// <summary>
    /// Pfeil nach unten im Suchfeld wählt die erste Zeile der Liste und gibt ihr den Fokus; dort wählen die Pfeiltasten weiter. Passt keine Zeile, aber
    /// ein Artikel, geht der Fokus auf den ersten Artikel (T-027).
    /// </summary>
    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down)
        {
            return;
        }

        if (TodayList.ItemCount > 0)
        {
            TodayList.SelectedIndex = 0;
            TodayList.ContainerFromIndex(0)?.Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
        else if (FocusArticle(0))
        {
            e.Handled = true;
        }
    }

    /// <summary>In den Artikeln wählen die Pfeiltasten weiter; Esc und Pfeil nach oben auf dem ersten Artikel springen ins Suchfeld zurück. Enter löst der Knopf selbst aus.</summary>
    private void OnArticleListKeyDown(object? sender, KeyEventArgs e)
    {
        var index = (e.Source as Control)?.DataContext is ArticleDto article ? ArticleList.Items.IndexOf(article) : -1;
        if (e.Key == Key.Escape || (e.Key == Key.Up && index <= 0))
        {
            PosScanField.Focus();
            e.Handled = true;
        }
        else if (e.Key is Key.Down or Key.Up)
        {
            FocusArticle(index + (e.Key == Key.Down ? 1 : -1));
            e.Handled = true;
        }
    }

    private bool FocusArticle(int index) =>
        ArticleList.ContainerFromIndex(index)?.FindDescendantOfType<Button>() is { } button && button.Focus(NavigationMethod.Directional);

    /// <summary>In der Liste öffnet Enter die Rechnung der gewählten Zeile; Esc und Pfeil nach oben auf der ersten Zeile springen ins Suchfeld zurück.</summary>
    private void OnTodayListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && TodayList.SelectedItem is UnbilledItem item && DataContext is PosViewModel pos)
        {
            pos.AddUnbilledCommand.Execute(item);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape || (e.Key == Key.Up && TodayList.SelectedIndex <= 0))
        {
            PosScanField.Focus();
            e.Handled = true;
        }
    }

    /// <summary>Ein Klick auf eine Zeile setzt sie auf die Rechnung (wie bisher der Knopf je Zeile); ein Klick auf die Gruppenüberschrift darüber nicht.</summary>
    private void OnTodayListTapped(object? sender, TappedEventArgs e)
    {
        var title = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.Name == "GroupTitleText" && t.IsEffectivelyVisible);
        if (title is not null && new Rect(title.Bounds.Size).Contains(e.GetPosition(title)))
        {
            return;
        }

        if ((e.Source as Control)?.DataContext is UnbilledItem item && DataContext is PosViewModel pos)
        {
            pos.AddUnbilledCommand.Execute(item);
        }
    }

    private async void OnVoucherCodeLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: PaymentEntry entry } && DataContext is PosViewModel pos)
        {
            await pos.LookUpVoucherAsync(entry);
        }
    }

    /// <summary>Fokus in einem leeren Betragsfeld einer Zahlart: den offenen Rest eintragen und markieren (Ä312); ein Klick setzt die Marke erst nach dem Fokus, deshalb noch einmal danach.</summary>
    private void OnPaymentFieldGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source is not MoneyField { DataContext: PaymentEntry entry } field || DataContext is not PosViewModel pos)
        {
            return;
        }

        if (pos.PrefillRest(entry))
        {
            field.SelectAll();
            Dispatcher.UIThread.Post(field.SelectAll);
        }
    }

    private void OnDeposit(object? sender, RoutedEventArgs e) => SetWithdrawal(false);

    private void OnWithdrawal(object? sender, RoutedEventArgs e) => SetWithdrawal(true);

    private void SetWithdrawal(bool withdrawal)
    {
        if (DataContext is PosViewModel pos)
        {
            pos.MovementIsWithdrawal = withdrawal;
        }
    }
}
