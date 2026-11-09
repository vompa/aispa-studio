using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Knopf „Exportieren“ mit Menü der Formate (Design-Brief 5.1 sekundär, 5.21 Menü), überall gleich: <c>DataContext</c> ist ein <see cref="ExportViewModel"/> der Liste;
/// ohne Modell (kein Recht) ist der Knopf unsichtbar. Während die Datei entsteht, ist er gesperrt und heißt „Wird erstellt …“ (kein Spinner, Brief 4.6). Das Menü bekommt
/// die Tastatur von Avalonia: Enter öffnet, Pfeile wählen, Enter löst aus, Esc schließt. Der Speicherdialog des Fensters kommt beim Einhängen in die Ansicht.
/// </summary>
public sealed class ExportButton : Button
{
    public const string Label = "Exportieren";
    public const string BusyLabel = "Wird erstellt …";
    public const string Hint = "Die Liste als Datei speichern";

    private readonly TextBlock _text = new() { Text = Label, VerticalAlignment = VerticalAlignment.Center };
    private readonly MenuFlyout _menu = new() { Placement = PlacementMode.BottomEdgeAlignedRight };
    private ExportViewModel? _model;

    public ExportButton()
    {
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { new Icon { Data = Icons.Download, Width = 16, Height = 16 }, _text },
        };
        Flyout = _menu;
        ToolTip.SetTip(this, Hint);
        AutomationProperties.SetName(this, Label);
        DataContextChanged += (_, _) => Bind(DataContext as ExportViewModel);
        IsVisible = false;
    }

    protected override Type StyleKeyOverride => typeof(Button);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_model is not null)
        {
            _model.Files ??= new StorageFileSaver(this);
        }
    }

    private void Bind(ExportViewModel? model)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
        }

        _model = model;
        _menu.Items.Clear();
        IsVisible = model is not null;
        if (model is null)
        {
            return;
        }

        model.PropertyChanged += OnModelChanged;
        foreach (var option in model.Options)
        {
            _menu.Items.Add(new MenuItem { Header = option.Label, Command = model.ExportCommand, CommandParameter = option });
        }

        if (TopLevel.GetTopLevel(this) is not null)
        {
            model.Files ??= new StorageFileSaver(this);
        }

        ShowBusy(model.IsBusy);
    }

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExportViewModel.IsBusy) && _model is not null)
        {
            ShowBusy(_model.IsBusy);
        }
    }

    private void ShowBusy(bool busy)
    {
        IsEnabled = !busy;
        _text.Text = busy ? BusyLabel : Label;
    }
}
