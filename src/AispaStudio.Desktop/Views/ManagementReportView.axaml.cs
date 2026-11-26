using System.ComponentModel;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Reiter „Management“; das Verhalten steht im Modell. Die Ansicht zeichnet nur die Markierung, die ein Kennzahl-Chip der KI-Auswertung setzt (T-104):
/// die Kachel mit dieser Beschriftung bekommt die Klasse <c>marked</c> und rollt ins Bild.
/// </summary>
public partial class ManagementReportView : UserControl
{
    private ManagementReportViewModel? _model;

    public ManagementReportView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model is not null)
            {
                _model.PropertyChanged -= OnModelChanged;
            }

            _model = DataContext as ManagementReportViewModel;
            if (_model is not null)
            {
                _model.PropertyChanged += OnModelChanged;
            }
        };
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ManagementReportViewModel.MarkedKpi))
        {
            Dispatcher.UIThread.Post(ShowMarkedTile, DispatcherPriority.Background);
        }
    }

    private void ShowMarkedTile()
    {
        var marked = _model?.MarkedKpi;
        StatTile? target = null;
        foreach (var tile in ManagementKpiGroups.GetVisualDescendants().OfType<StatTile>())
        {
            var isMarked = marked is not null && tile.Label == marked;
            tile.Classes.Set("marked", isMarked);
            target = isMarked ? tile : target;
        }

        target?.BringIntoView();
    }
}
