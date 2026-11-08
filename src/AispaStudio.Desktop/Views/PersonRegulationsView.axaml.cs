using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>Reiter „Regelungen und Vollmachten“ der Personenkarte (R05 Slice 111); das Verhalten steht im Modell.</summary>
public partial class PersonRegulationsView : UserControl
{
    private PersonRegulationsViewModel? _model;

    public PersonRegulationsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model is not null)
            {
                _model.FocusRequested -= FocusField;
            }

            _model = DataContext as PersonRegulationsViewModel;
            if (_model is not null)
            {
                _model.FocusRequested += FocusField;
            }
        };
    }

    /// <summary>Erst nach dem Einblenden des Formulars lässt sich ein Feld fokussieren.</summary>
    private void FocusField(string name) => Dispatcher.UIThread.Post(() => this.FindControl<Control>(name)?.Focus());
}
