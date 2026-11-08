using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Freie Lücke einer Spalte: vom Ende des vorigen belegten Stücks bis zum Beginn des nächsten bzw. Dienstende.</summary>
public sealed record FreeGap(DateTimeOffset From, DateTimeOffset Until)
{
    /// <summary>Minuten von <paramref name="at"/> bis zur nächsten Belegung.</summary>
    public int MinutesFrom(DateTimeOffset at) => Math.Max(0, (int)(Until - at).TotalMinutes);

    /// <summary>Wie viel Pause oder Sperrzeit ab <paramref name="at"/> hineinpasst, auf fünf Minuten abgerundet (höchstens bis zur nächsten Belegung).</summary>
    public int FillMinutes(DateTimeOffset at) => MinutesFrom(at) / 5 * 5;
}

public enum FitKind
{
    /// <summary>Vor- und Nachbereitung eingerechnet passt die Behandlung in die Lücke.</summary>
    Fits,

    /// <summary>Hinten wäre Platz, aber die Vorbereitung ragt in das Stück davor.</summary>
    PrepConflict,

    /// <summary>Länger als die freie Zeit.</summary>
    TooLong,
}

/// <summary>Eine Behandlung in der Auswahl, mit Hinweis, ob sie in die freie Zeit passt. Ohne Lücke ist <paramref name="Fit"/> leer.</summary>
public sealed record TreatmentOption(TreatmentDto Treatment, FitKind? Fit, string? Hint, int Rest)
{
    public bool Fits => Fit == FitKind.Fits;

    /// <summary>Passt nicht (zu lang oder die Vorbereitung ragt in das Stück davor): Hinweis in Warnfarbe.</summary>
    public bool Misfit => Fit is FitKind.PrepConflict or FitKind.TooLong;
}

/// <summary>
/// Sortiert und kennzeichnet Behandlungen nach der freien Zeit. Das ist nur eine Hilfe für die Rezeption: ob gebucht werden darf,
/// entscheidet weiterhin der Server (<c>ResourcePlanner</c>).
/// </summary>
public static class TreatmentFits
{
    /// <summary>Gesamte Belegung: Vorbereitung, Behandlung, Nachbereitung.</summary>
    public static int Needed(TreatmentDto t) => t.PreparationMinutes + t.DurationMinutes + t.FollowUpMinutes;

    /// <summary>Was die Person bzw. der Raum der Spalte anbieten kann: Person mit allen nötigen Qualifikationen, Raum mit der Behandlung in seiner Liste.</summary>
    public static Func<TreatmentDto, bool> Suits(LocationResources resources, Guid columnId, bool byRoom)
    {
        var qualifications = resources.Employees.FirstOrDefault(e => e.Id == columnId)?.QualificationIds ?? [];
        return byRoom ? t => t.RoomIds.Contains(columnId) : t => t.RequiredQualificationIds.All(qualifications.Contains);
    }

    public static TreatmentOption Of(TreatmentDto treatment, FreeGap? gap, DateTimeOffset at)
    {
        if (gap is null)
        {
            return new TreatmentOption(treatment, null, null, 0);
        }

        var needed = Needed(treatment);
        var end = at.AddMinutes(treatment.DurationMinutes + treatment.FollowUpMinutes);
        var rest = (int)(gap.Until - end).TotalMinutes;
        if (rest >= 0 && at.AddMinutes(-treatment.PreparationMinutes) >= gap.From)
        {
            return new TreatmentOption(treatment, FitKind.Fits, rest == 0 ? "passt genau" : $"passt ({needed} von {needed + rest} Min.)", rest);
        }

        return rest >= 0 && needed <= (gap.Until - gap.From).TotalMinutes
            ? new TreatmentOption(treatment, FitKind.PrepConflict, "Vorbereitung passt nicht davor", rest)
            : new TreatmentOption(treatment, FitKind.TooLong, "länger als die freie Zeit", rest);
    }

    /// <summary>
    /// Was in die Lücke passt zuerst, darin die mit dem geringsten Rest, dann was nur wegen der Vorbereitung nicht geht,
    /// dann längere. Ohne Lücke bleibt die Reihenfolge.
    /// </summary>
    public static IReadOnlyList<TreatmentOption> Rank(IEnumerable<TreatmentDto> treatments, FreeGap? gap, DateTimeOffset at) =>
        [.. treatments.Select(t => Of(t, gap, at))
            .OrderBy(o => o.Fit)
            .ThenBy(o => o.Fit is null ? 0 : o.Fit == FitKind.Fits ? o.Rest : Needed(o.Treatment))];

    /// <summary>Keine Behandlung der Spalte passt in die Lücke, auch nicht mit etwas Verschieben: dann ist ein Termin hier unwahrscheinlich.</summary>
    public static bool NothingFits(IEnumerable<TreatmentDto> suitable, FreeGap gap, DateTimeOffset at) =>
        suitable.Any() && suitable.All(t => Of(t, gap, at).Fit == FitKind.TooLong);
}
