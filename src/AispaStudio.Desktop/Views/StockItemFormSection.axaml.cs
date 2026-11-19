using Avalonia.Controls;

namespace AispaStudio.Desktop.Views;

/// <summary>Material anlegen im Lager (R05 Slice 101).</summary>
public partial class StockItemFormSection : FocusRequestView
{
    public StockItemFormSection() => InitializeComponent();

    /// <summary>
    /// Das Formular steht unter der Liste im Seitentext: Nach „Material anlegen“ und „Ändern“ rollt die Seite zum Feld, die Knopfzeile am Ende würde darunter liegen
    /// (masken.md, R5). Deshalb folgt ihr die Knopfzeile ins Bild.
    /// </summary>
    protected override void Reveal(Control control)
    {
        base.Reveal(control);
        this.FindControl<Control>("CreateItemButton")?.BringIntoView();
    }
}
