using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AispaStudio.Desktop.Views;

public partial class RestoreView : UserControl
{
    public RestoreView()
    {
        InitializeComponent();
    }

    /// <summary>Datei-Dialog von Windows; der Pfad landet im Eingabefeld.</summary>
    private async void PickFile(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not RestoreViewModel restore || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
        {
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Sicherung wählen",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Sicherung von AISPA Studio") { Patterns = ["aispa-*.dump"] }],
        });
        if (files is [var file] && file.TryGetLocalPath() is { } path)
        {
            restore.DumpFile = path;
        }
    }
}
