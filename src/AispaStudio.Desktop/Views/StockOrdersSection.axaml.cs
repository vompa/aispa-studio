using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace AispaStudio.Desktop.Views;

/// <summary>Bestellungen des Lagers. Nur der Kopier-Knopf braucht die Ansicht (Zwischenablage); alles andere steht im Modell.</summary>
public sealed partial class StockOrdersSection : UserControl
{
    public StockOrdersSection() => InitializeComponent();

    private void OnExpiresFieldChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ParsedField.IsValidProperty && sender is ParsedField { DataContext: PurchaseOrderLineRow row } field)
        {
            row.IsExpiresValid = field.IsValid;
        }
    }

    private async void OnCopyTextClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: PurchaseOrderCard { OrderText: { } text } card })
        {
            return;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text).ConfigureAwait(true);
            card.CopyHint = "Kopiert.";
            return;
        }

        card.CopyHint = "Die Zwischenablage ist nicht erreichbar. Den Text markieren und mit Strg+C kopieren.";
    }
}
