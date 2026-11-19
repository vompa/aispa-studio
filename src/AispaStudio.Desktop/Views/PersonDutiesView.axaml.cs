using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>Reiter „Zuständigkeiten“ der Personenkarte (R05 Slice 117); das Verhalten steht im Modell, die Ansicht gibt ihm Dateiauswahl und Speicherdialog des Fensters.</summary>
public partial class PersonDutiesView : UserControl
{
    private PersonDutiesViewModel? _model;

    public PersonDutiesView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model is not null)
            {
                _model.FocusRequested -= FocusField;
            }

            _model = DataContext as PersonDutiesViewModel;
            if (_model is not null)
            {
                _model.FocusRequested += FocusField;
                _model.Picker ??= new StorageFilePicker(this);
                _model.Files ??= new StorageFileSaver(this);
            }
        };
    }

    private void OnToastEntered(object? sender, PointerEventArgs e) => _model?.Toast.Hold(true);

    private void OnToastExited(object? sender, PointerEventArgs e) => _model?.Toast.Hold(false);

    /// <summary>Erst nach dem Einblenden des Formulars lässt sich ein Feld fokussieren.</summary>
    private void FocusField(string name) => Dispatcher.UIThread.Post(() => this.FindControl<Control>(name)?.Focus());
}
