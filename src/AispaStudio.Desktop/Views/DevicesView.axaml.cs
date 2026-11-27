using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

public partial class DevicesView : UserControl
{
    private DevicesViewModel? _model;

    public DevicesView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model is not null)
            {
                _model.FocusRequested -= FocusField;
            }

            _model = DataContext as DevicesViewModel;
            if (_model is not null)
            {
                _model.FocusRequested += FocusField;
            }
        };
    }

    /// <summary>Nach einem Fehler beim Anlegen, Koppeln oder Anmelden springt der Fokus ins erste ungültige Feld (masken.md, R8).</summary>
    private void FocusField(string name) => Dispatcher.UIThread.Post(() => this.FindControl<Control>(name)?.Focus());
}
