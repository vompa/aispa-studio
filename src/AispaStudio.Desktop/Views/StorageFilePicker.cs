using System.Text;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AispaStudio.Desktop.Views;

/// <summary>Der Dateidialog von Windows für die Masken der Verwaltung: wählt eine Datei und liest sie als UTF-8-Text, wenn sie nicht größer als das Limit ist.</summary>
public sealed class StorageFilePicker(Visual host) : IFilePicker
{
    public async Task<PickedFile?> PickTextAsync(string title, string typeName, IReadOnlyList<string> patterns, long maxBytes)
    {
        if (TopLevel.GetTopLevel(host)?.StorageProvider is not { CanOpen: true } storage)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(typeName) { Patterns = [.. patterns] }],
        }).ConfigureAwait(true);
        if (files is not [var file])
        {
            return null;
        }

        var properties = await file.GetBasicPropertiesAsync().ConfigureAwait(true);
        var size = (long?)properties.Size ?? 0;
        if (size > maxBytes)
        {
            return new PickedFile(file.Name, size, "");
        }

        await using var stream = await file.OpenReadAsync().ConfigureAwait(true);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync().ConfigureAwait(true);
        return new PickedFile(file.Name, Math.Max(size, stream.CanSeek ? stream.Length : 0), content);
    }

    public async Task<byte[]?> PickBytesAsync(string title, string typeName, IReadOnlyList<string> patterns, long maxBytes) =>
        (await PickFileAsync(title, typeName, patterns, maxBytes).ConfigureAwait(true))?.Content;

    public async Task<PickedBytes?> PickFileAsync(string title, string typeName, IReadOnlyList<string> patterns, long maxBytes)
    {
        if (TopLevel.GetTopLevel(host)?.StorageProvider is not { CanOpen: true } storage)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(typeName) { Patterns = [.. patterns] }],
        }).ConfigureAwait(true);
        if (files is not [var file])
        {
            return null;
        }

        var properties = await file.GetBasicPropertiesAsync().ConfigureAwait(true);
        var size = (long?)properties.Size ?? 0;
        if (size > maxBytes)
        {
            return new PickedBytes(file.Name, size, []);
        }

        await using var stream = await file.OpenReadAsync().ConfigureAwait(true);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer).ConfigureAwait(true);
        return new PickedBytes(file.Name, Math.Max(size, buffer.Length), buffer.ToArray());
    }
}
