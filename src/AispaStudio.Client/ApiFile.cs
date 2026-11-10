namespace AispaStudio.Client;

/// <summary>Eine Datei vom Server (Export): die Bytes und der Dateiname aus <c>Content-Disposition</c>, wenn er mitkam.</summary>
public sealed record ApiFile(byte[] Content, string? FileName);
