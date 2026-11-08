namespace AispaStudio.Api.Closures;

/// <summary>
/// Wann eine Nachricht an den Gast rausgeht, wenn das Team eine Absage auslöst (Ä95, ADR 0081): erst nach der Wartezeit des Standorts, damit ein Versehen noch
/// zurückgenommen werden kann. Sofort geht sie ohne Wartezeit und wenn der Termin in weniger als <see cref="ImmediateWithin"/> beginnt, denn dann hilft ein Warten dem Gast nicht mehr.
/// Gäste, die selbst auslösen, laufen nicht über diese Regel.
/// </summary>
internal static class GuestMessageTiming
{
    public const int MaxDelayMinutes = 60;

    public static readonly TimeSpan ImmediateWithin = TimeSpan.FromHours(2);

    /// <summary>Der Zeitpunkt des Versands; <paramref name="appointmentStart"/> fehlt bei Nachrichten ohne Termin (Warteliste), dort gilt nur die Wartezeit.</summary>
    public static DateTimeOffset SendAt(DateTimeOffset now, DateTimeOffset? appointmentStart, int delayMinutes) =>
        delayMinutes <= 0 || appointmentStart - now < ImmediateWithin ? now : now.AddMinutes(delayMinutes);
}
