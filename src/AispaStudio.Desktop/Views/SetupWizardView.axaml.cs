using System.ComponentModel;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Assistent „Betrieb einrichten“. Beim Wechsel des Schritts geht der Fokus ins erste Feld (Öffnungszeiten: das erste Häkchen, Räume: der Name) bzw. auf den Hauptknopf;
/// Esc schließt nur, wenn keine Eingabe verloren ginge.
/// </summary>
public partial class SetupWizardView : UserControl
{
    private SetupWizardViewModel? _model;

    public SetupWizardView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch(DataContext as SetupWizardViewModel);
        AttachedToVisualTree += (_, _) => FocusStep();
    }

    private void Watch(SetupWizardViewModel? model)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
        }

        _model = model;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelChanged;
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SetupWizardViewModel.Index) or nameof(SetupWizardViewModel.IsLoaded) or nameof(SetupWizardViewModel.ReadinessLoaded))
        {
            FocusStep();
        }
        else if (e.PropertyName is nameof(SetupWizardViewModel.FirstName) or nameof(SetupWizardViewModel.LastName) && _model is { FirstName.Length: 0, LastName.Length: 0 })
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var box in new[] { FirstNameBox, LastNameBox }.Where(b => string.IsNullOrEmpty(b.Text)))
                {
                    FieldBehaviors.Confirm(box);
                }
            }, DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(SetupWizardViewModel.NewRoomName) && _model?.NewRoomName.Length == 0)
        {
            // Das Modell hat das Feld geleert: sonst holt Esc den eben angelegten Namen zurück.
            Dispatcher.UIThread.Post(() =>
            {
                if (string.IsNullOrEmpty(NewRoomBox.Text))
                {
                    FieldBehaviors.Confirm(NewRoomBox);
                }
            }, DispatcherPriority.Background);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _model is not null && _model.TryCloseWithEscape())
        {
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void FocusStep()
    {
        if (_model is not { IsLoaded: true })
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Control? target = _model switch
            {
                { IsHoursStep: true } => this.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault(c => c.Name == "DayOpen" && c.IsEffectivelyVisible),
                { IsFinishStep: true } => _model.IsReady ? OpenCalendarButton : _model.HasBack ? BackButton : CloseButton,
                { IsRoomsStep: true } => NewRoomBox,
                { IsTreatmentsStep: true } => this.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault(c => c.Name == "PackageCheck" && c.IsEffectivelyVisible),
                { IsPeopleStep: true } => FirstNameBox,
                { OpenAdminIsDefault: true } => OpenAdminButton,
                { NextIsDefault: true } => NextButton,
                _ => CloseButton,
            };
            target?.Focus();
        }, DispatcherPriority.Background);
    }
}
