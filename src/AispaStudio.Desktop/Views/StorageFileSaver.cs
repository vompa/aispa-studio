using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AispaStudio.Desktop.Views;

/// <summary>Der Speicherdialog von Windows für die Masken der Verwaltung: fragt nach dem Ort und schreibt die Bytes unverändert.</summary>
public sealed class StorageFileSaver(Visual host) : IFileSaver, IReportsSavedPath
{
    public string? LastSavedPath { get; private set; }
    private ISavesFiles? DefaultFor { get; init; }
    private Func<bool>? OwnerIsCurrent { get; init; }

    internal static void BindWindow(ISavesFiles model, TopLevel window)
    {
        if (model.Files is null || model.Files is StorageFileSaver saver && saver.DefaultFor == model && saver.Window != window)
        {
            var main = window.DataContext as MainViewModel;
            var shell = main?.Content as ShellViewModel;
            model.Files = new StorageFileSaver(window)
            {
                DefaultFor = model,
                // Navigation wechselt nur die Seite. Beim Abmelden oder Serverwechsel gibt Main die Shell frei.
                OwnerIsCurrent = () => ReferenceEquals(window.DataContext as MainViewModel, main)
                    && (main is null || shell is not null && ReferenceEquals(main.Content, shell)),
            };
        }
    }

    private TopLevel? Window => TopLevel.GetTopLevel(host);
    private bool CanSave => OwnerIsCurrent?.Invoke() != false;

    public async Task<bool> SaveAsync(string suggestedName, string typeName, IReadOnlyList<string> patterns, byte[] content)
    {
        LastSavedPath = null;
        if (!CanSave || Window?.StorageProvider is not { CanSave: true } storage)
        {
            return false;
        }

        using var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType(typeName) { Patterns = [.. patterns] }],
        }).ConfigureAwait(true);
        if (file is null || !CanSave)
        {
            return false;
        }

        await using (var stream = await file.OpenWriteAsync().ConfigureAwait(true))
        {
            if (!CanSave)
            {
                return false;
            }
            await stream.WriteAsync(content).ConfigureAwait(true);
        }
        if (!CanSave)
        {
            return false;
        }
        LastSavedPath = file.TryGetLocalPath();
        return true;
    }
}
