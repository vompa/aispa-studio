using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ersetzt den Dateidialog von Windows: liefert die vorbereitete Datei (oder keine) und merkt sich, was gefragt wurde.</summary>
internal sealed class FakeFilePicker : IFilePicker
{
    public PickedFile? File { get; set; }

    public List<(string Title, string TypeName, IReadOnlyList<string> Patterns, long MaxBytes)> Asked { get; } = [];

    public Task<PickedFile?> PickTextAsync(string title, string typeName, IReadOnlyList<string> patterns, long maxBytes)
    {
        Asked.Add((title, typeName, patterns, maxBytes));
        return Task.FromResult(File);
    }

    public byte[]? Bytes { get; set; }

    public Exception? BytesFailure { get; set; }

    public Task<byte[]?> PickBytesAsync(string title, string typeName, IReadOnlyList<string> patterns, long maxBytes)
    {
        Asked.Add((title, typeName, patterns, maxBytes));
        return BytesFailure is { } failure ? Task.FromException<byte[]?>(failure) : Task.FromResult(Bytes);
    }
}
