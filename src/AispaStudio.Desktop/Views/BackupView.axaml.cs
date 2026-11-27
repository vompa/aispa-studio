using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

public partial class BackupView : UserControl
{
    private BackupViewModel? _model;

    public BackupView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model is not null)
            {
                _model.FocusRequested -= FocusField;
            }

            _model = DataContext as BackupViewModel;
            if (_model is not null)
            {
                _model.FocusRequested += FocusField;
            }
        };
    }

    /// <summary>Ohne Ordner springt der Fokus nach „Speichern und sichern“ ins Ordnerfeld (masken.md, R8).</summary>
    private void FocusField(string name) => Dispatcher.UIThread.Post(() => this.FindControl<Control>(name)?.Focus());
}
