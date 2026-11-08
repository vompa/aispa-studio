namespace AispaStudio.Contracts;

public enum TseExportStatus
{
    Pending,
    Done,
    Failed,
}

/// <summary>Zeitraum des TSE-Exports in Ortszeit des Standorts, beide Tage eingeschlossen.</summary>
public sealed record TseExportInput(DateOnly From, DateOnly Until);

public sealed record TseExportDto(
    Guid Id,
    Guid RegisterId,
    DateOnly From,
    DateOnly Until,
    TseExportStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    string? FileName,
    long? Size,
    string? Error);
