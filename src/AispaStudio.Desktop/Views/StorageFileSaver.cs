using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AispaStudio.Desktop.Views;

/// <summary>Der Speicherdialog von Windows für die Masken der Verwaltung: fragt nach dem Ort und schreibt die Bytes unverändert.</summary>
public sealed class StorageFileSaver(Visual host) : IFileSaver, IReportsSavedPath
{
    public string? LastSavedPath { get; private set; }
    internal TopLevel? Window => TopLevel.GetTopLevel(host);

    public async Task<bool> SaveAsync(string suggestedName, string typeName, IReadOnlyList<string> patterns, byte[] content)
    {
        LastSavedPath = null;
        if (Window?.StorageProvider is not { CanSave: true } storage)
        {
            return false;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType(typeName) { Patterns = [.. patterns] }],
        }).ConfigureAwait(true);
        if (file is null)
        {
            return false;
        }

        await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
        await stream.WriteAsync(content).ConfigureAwait(true);
        LastSavedPath = file.TryGetLocalPath();
        return true;
    }
}
