using System.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Modell, das der Ansicht sagt, welches Feld nach einem Fehler oder beim Öffnen den Fokus bekommt (der Name des Feldes; die Ansicht leert die Angabe).</summary>
public interface IFocusTarget : INotifyPropertyChanged
{
    string? FocusTarget { get; set; }
}
