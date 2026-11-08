using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _idle = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        InitializeComponent();

        // Jede Eingabe zählt als Tätigkeit, auch wenn ein Steuerelement sie schon verarbeitet (Leerlauf am Empfangsgerät).
        AddHandler(PointerPressedEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        _idle.Tick += async (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                await vm.TickAsync();
            }
        };
        _idle.Start();
        Closed += (_, _) => _idle.Stop();
    }

    private void OnActivity(object? sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.NoteActivity();
}
