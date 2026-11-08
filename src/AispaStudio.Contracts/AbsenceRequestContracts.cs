namespace AispaStudio.Contracts;

/// <summary>Stand eines Abwesenheitsantrags aus Sicht der Person, die ihn gestellt hat.</summary>
public enum AbsenceRequestState
{
    Open,
    Approved,
    Rejected,
}

/// <summary>Antrag aus der Handy-App: Art der Abwesenheit und die Tage von bis (beide eingeschlossen).</summary>
public sealed record AbsenceRequestInput(RosterKind Kind, DateOnly From, DateOnly Until, string? Note);

/// <summary>Der eigene Antrag mit Entscheidung, wie ihn die Handy-App zeigt.</summary>
public sealed record MyAbsenceRequestDto(
    Guid Id,
    RosterKind Kind,
    DateOnly From,
    DateOnly Until,
    string? Note,
    DateTimeOffset RequestedAt,
    AbsenceRequestState State,
    DateTimeOffset? DecidedAt);
