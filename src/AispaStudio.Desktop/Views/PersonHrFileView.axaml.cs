using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>Reiter „Personalakte“ der Personenkarte (R05 Slice 110); das Verhalten steht im Modell, Speicherdialog und Fokus kommen vom Fenster.</summary>
public partial class PersonHrFileView : UserControl
{
    private PersonHrFileViewModel? _model;

    public PersonHrFileView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model is not null)
            {
                _model.FocusRequested -= FocusField;
            }

            _model = DataContext as PersonHrFileViewModel;
            if (_model is not null)
            {
                _model.Files ??= new StorageFileSaver(this);
                _model.FocusRequested += FocusField;
            }
        };
    }

    /// <summary>Erst nach dem Einblenden des Formulars lässt sich ein Feld fokussieren.</summary>
    private void FocusField(string name) => Dispatcher.UIThread.Post(() => this.FindControl<Control>(name)?.Focus());
}
