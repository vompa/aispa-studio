namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine vom Menschen gewählte Textdatei: Name, Größe in Bytes und Inhalt (leer, wenn die Datei größer als das Limit war und gar nicht gelesen wurde).</summary>
public sealed record PickedFile(string Name, long Size, string Content);

/// <summary>Eine vom Menschen gewählte Datei, unverändert als Bytes: Name, Größe und Inhalt (leer, wenn die Datei größer als das Limit war und gar nicht gelesen wurde).</summary>
public sealed record PickedBytes(string Name, long Size, byte[] Content);

/// <summary>Wer eine Datei zum Einlesen wählen lässt: die Windows-App zeigt den Dateidialog von Windows, Tests antworten selbst.</summary>
public interface IFilePicker
{
    /// <summary>Lässt eine Datei wählen und liest sie als Text, wenn sie höchstens <paramref name="maxBytes"/> groß ist; null, wenn nichts gewählt wurde.</summary>
    Task<PickedFile?> PickTextAsync(string title, string typeName, IReadOnlyList<string> patterns, long maxBytes);

    /// <summary>Lässt eine Datei wählen und liest sie nur in den Speicher, wenn sie höchstens <paramref name="maxBytes"/> groß ist (sonst leer); null, wenn nichts gewählt wurde.</summary>
    Task<byte[]?> PickBytesAsync(string title, string typeName, IReadOnlyList<string> patterns, long maxBytes);

    /// <summary>Wie <see cref="PickBytesAsync"/>, nennt aber auch Namen und Größe der Datei (z. B. für den Upload mit Dateiendung); null, wenn nichts gewählt wurde.</summary>
    Task<PickedBytes?> PickFileAsync(string title, string typeName, IReadOnlyList<string> patterns, long maxBytes);
}

/// <summary>Wer eine Datei speichern lässt: die Windows-App zeigt den Speicherdialog von Windows, Tests antworten selbst.</summary>
public interface IFileSaver
{
    /// <summary>Fragt nach dem Speicherort und schreibt die Bytes unverändert; false, wenn nichts gewählt wurde.</summary>
    Task<bool> SaveAsync(string suggestedName, string typeName, IReadOnlyList<string> patterns, byte[] content);
}

/// <summary>Eine Maske, die Dateien speichert; die Ansicht gibt ihr den Dialog des Fensters.</summary>
public interface ISavesFiles
{
    IFileSaver? Files { get; set; }
}
