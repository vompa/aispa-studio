using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

public partial class LoginView : UserControl
{
    private LoginViewModel? _login;

    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch(DataContext as LoginViewModel);
        Loaded += (_, _) => FocusSetupField();
    }

    private void Watch(LoginViewModel? login)
    {
        if (_login is not null)
        {
            _login.PropertyChanged -= OnLoginPropertyChanged;
        }

        _login = login;
        if (_login is not null)
        {
            _login.PropertyChanged += OnLoginPropertyChanged;
        }
    }

    private void OnLoginPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LoginViewModel.SetupStage) or nameof(LoginViewModel.ShowSetupForm))
        {
            FocusSetupField();
        }
    }

    /// <summary>Beim Schrittwechsel von „Spa einrichten“ geht der Fokus auf das erste Feld des Schritts.</summary>
    private void FocusSetupField()
    {
        if (_login is not { ShowSetupForm: true })
        {
            return;
        }

        var field = _login.SetupStage switch
        {
            SetupStep.Spa => SetupSpaNameBox,
            SetupStep.Location => SetupLocationBox,
            _ => SetupOwnerBox,
        };
        Dispatcher.UIThread.Post(() => field.Focus(), DispatcherPriority.Background);
    }
}
