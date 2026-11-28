using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

public partial class RegisterWizardView : UserControl
{
    private RegisterWizardViewModel? _model;

    public RegisterWizardView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _model = DataContext as RegisterWizardViewModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelChanged;
            FocusStep();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _model is { } model)
        {
            model.Close();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RegisterWizardViewModel.Step) or nameof(RegisterWizardViewModel.IsBusy))
        {
            FocusStep();
        }
    }

    private void FocusStep() => Dispatcher.UIThread.Post(() =>
    {
        if (_model is not { IsBusy: false } model)
        {
            return;
        }

        RegisterWizardScroll.Offset = default;
        Control target = model switch
        {
            { ShowPlace: true, Locations.Count: > 0 } => RegisterLocationBox,
            { ShowProvider: true, IsPractice: true, Providers.Count: 1 } => ConfirmPracticeBox,
            { ShowProvider: true, Providers.Count: > 0 } => RegisterProviderBox,
            { ShowDetails: true } => RegisterCodeBox,
            { ShowCreate: true } => RegisterSubmitButton,
            { ShowFinish: true } => RegisterFinishButton,
            _ => RegisterCloseButton,
        };
        target.Focus();
    }, DispatcherPriority.Loaded);
}
