using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Tag im Dienstplan planen (R05 Slice 99); das Verhalten steht im Modell. Die Ansicht setzt nur den Fokus: beim Öffnen des Tages ins erste Feld (masken.md, R13; die Art-Leiste ist ein
/// Umschalter und zählt nicht), nach abgelehntem Speichern ins falsche Feld (R8). Ein neuer Tag kann dieselbe Ansicht weiterverwenden, deshalb hängt das an <see cref="StyledElement.DataContext"/>.
/// </summary>
public partial class RosterDayEditorView : UserControl
{
    private RosterDayEditorViewModel? _model;

    public RosterDayEditorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            Listen(DataContext as RosterDayEditorViewModel);
            if (_model is not null)
            {
                Dispatcher.UIThread.Post(FocusFirstField, DispatcherPriority.Loaded);
            }
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Listen(DataContext as RosterDayEditorViewModel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Listen(null);
        base.OnDetachedFromVisualTree(e);
    }

    private void Listen(RosterDayEditorViewModel? model)
    {
        if (_model is not null)
        {
            _model.InvalidField -= OnInvalidField;
        }

        _model = model;
        if (_model is not null)
        {
            _model.InvalidField += OnInvalidField;
        }
    }

    private void FocusFirstField()
    {
        DayEditor.BringIntoView();
        Control? first = ShiftList.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault(c => c.IsEffectivelyVisible);
        (first ?? DayNote).Focus();
    }

    private void OnInvalidField(ShiftRow shift) => Dispatcher.UIThread.Post(() =>
    {
        var row = ShiftList.ContainerFromItem(shift);
        Control? field = row is null ? null
            : shift.Location?.Id is null ? row.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault()
            : TimeFieldIn(row, shift.Start is not null && shift.End is null ? shift.EndName : shift.StartName);
        field?.BringIntoView();
        field?.Focus();
    }, DispatcherPriority.Loaded);

    private static TimeField? TimeFieldIn(Control row, string name) =>
        row.GetVisualDescendants().OfType<TimeField>().FirstOrDefault(f => AutomationProperties.GetName(f) == name);
}
