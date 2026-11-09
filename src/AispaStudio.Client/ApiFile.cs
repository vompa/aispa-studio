namespace AispaStudio.Client;

/// <summary>Eine Datei der API, z. B. ein PDF: Dateiname aus <c>Content-Disposition</c>, Inhaltstyp und die Bytes (ADR 0091).</summary>
public sealed record ApiFile(string FileName, string ContentType, byte[] Content);
