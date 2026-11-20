using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>Reiter „Qualifikationen“ (R05 Slice 98); das Verhalten steht im Modell, die Ansicht setzt den Fokus, den es nach einem Fehler verlangt.</summary>
public partial class PersonQualificationsView : UserControl
{
    private PersonQualificationsViewModel? _model;

    public PersonQualificationsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model is not null)
            {
                _model.FocusRequested -= FocusField;
            }

            _model = DataContext as PersonQualificationsViewModel;
            if (_model is not null)
            {
                _model.FocusRequested += FocusField;
            }
        };
    }

    /// <summary>Heißt ein Feld in mehreren Zeilen gleich, bekommt das mit dem Fehler den Fokus.</summary>
    private void FocusField(string name) => Dispatcher.UIThread.Post(() =>
    {
        var candidates = this.GetVisualDescendants().OfType<Control>().Where(c => c.Name == name && c.IsEffectivelyVisible).ToList();
        (candidates.FirstOrDefault(c => c.GetVisualAncestors().OfType<FormField>().FirstOrDefault()?.Error is { Length: > 0 }) ?? candidates.FirstOrDefault())?.Focus();
    }, DispatcherPriority.Loaded);
}
