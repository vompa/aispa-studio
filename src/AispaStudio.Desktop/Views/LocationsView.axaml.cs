using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Standorte &amp; Räume (R05 Slice 95). Setzt den Fokus, den das Modell verlangt: ins Namensfeld nach einem Fehler (masken.md, R8), ins Feld, auf das der Assistent
/// verweist („Jetzt ergänzen“), oder in die Zeile des Raums, den der Kalender über „Raum bearbeiten“ nennt. Die Zeilen entstehen erst nach dem Wechsel des Standorts;
/// deshalb sucht die Ansicht das Feld ein paarmal nach, bis es steht. Kommt die Angabe, bevor die Maske steht, gilt sie beim Einhängen.
/// </summary>
public partial class LocationsView : UserControl
{
    private const int Attempts = 12;

    private LocationsViewModel? _model;

    public LocationsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            Detach();
            if (TopLevel.GetTopLevel(this) is not null)
            {
                Attach();
            }
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attach();
        FocusRequested();
    }

    /// <summary>Das Modell lebt länger als die Ansicht (sie wird bei jedem Bereichswechsel neu gebaut); ausgehängt hört sie nicht mehr zu.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Detach();
        base.OnDetachedFromVisualTree(e);
    }

    private void Attach()
    {
        Detach();
        _model = DataContext as LocationsViewModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelChanged;
        }
    }

    private void Detach()
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model = null;
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocationsViewModel.FocusTarget) or nameof(LocationsViewModel.IsLoaded))
        {
            FocusRequested();
        }
    }

    private void FocusRequested()
    {
        if (_model is not { FocusTarget: { } field, IsLoaded: true } model || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }

        model.FocusTarget = null;
        _ = FocusWhenReadyAsync(field);
    }

    private async Task FocusWhenReadyAsync(string field)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            if (TopLevel.GetTopLevel(this) is null)
            {
                return;
            }

            if (Find(field) is { IsEffectivelyVisible: true } control)
            {
                control.BringIntoView();
                control.Focus();
                return;
            }

            await Task.Delay(40).ConfigureAwait(true);
        }
    }

    private Control? Find(string field)
    {
        if (field.StartsWith("room:", StringComparison.Ordinal))
        {
            return Guid.TryParse(field.AsSpan(5), out var id) ? RoomControl("RoomList", "RoomNameField", row => row.Id == id) : null;
        }

        return field switch
        {
            LocationsViewModel.NameField => Named("NameField"),
            LocationsViewModel.StreetField => Named("StreetField"),
            LocationsViewModel.PhoneField => Named("PhoneField"),
            LocationsViewModel.EmailField => Named("EmailField"),
            LocationsViewModel.DirectionsField => Named("DirectionsField"),
            LocationsViewModel.RegionField => Named("RegionField"),
            LocationsViewModel.OpeningHoursField => this.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault(c => c.Name == "DayOpen"),
            LocationsViewModel.RoomsField => RoomControl("NewRoomRow", "RoomNameField", _ => true),
            LocationsViewModel.AreasField => RoomControl("NewRoomRow", "RoomAreaField", _ => true),
            LocationsViewModel.CancellationPolicyField => Named("CancellationPolicyField"),
            _ => null,
        };
    }

    private Control? Named(string name) => this.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name);

    /// <summary>Das Feld einer Raumzeile: in der Liste <paramref name="host"/> die Zeile, die <paramref name="match"/> trifft.</summary>
    private Control? RoomControl(string host, string name, Func<RoomRowViewModel, bool> match) =>
        Named(host)?.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name && c.DataContext is RoomRowViewModel row && match(row));
}
