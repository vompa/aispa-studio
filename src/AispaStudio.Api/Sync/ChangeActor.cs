using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Sync;

/// <summary>
/// Wer eine Änderung macht und über welchen Weg (Änderungsverlauf, ADR 0082). In Anfragen des Teams die angemeldete Person (aus der Mitarbeiter-App mit
/// eigenem Kanal, Slice 104), im Kundenbereich
/// (<c>/api/online…</c>) der Kanal Online ohne Person, sonst ein Hintergrundjob. Hintergrundjobs, die im Namen eines Kanals schreiben (Terminannahme
/// per SMS, Mail, Treatwell), setzen ihn mit <see cref="Use"/> für ihren Ablauf.
/// </summary>
public static class ChangeActor
{
    private static readonly AsyncLocal<(ChangeChannel Channel, string? UserId)?> Override = new();

    /// <summary>Gilt bis zum Dispose für den laufenden asynchronen Ablauf.</summary>
    public static IDisposable Use(ChangeChannel channel, string? userId = null)
    {
        var previous = Override.Value;
        Override.Value = (channel, userId);
        return new Restore(previous);
    }

    public static (ChangeChannel Channel, string? UserId) Resolve(HttpContext? http)
    {
        if (Override.Value is { } chosen)
        {
            return chosen;
        }

        if (http is null)
        {
            return (ChangeChannel.Job, null);
        }

        if (http.Request.Path.StartsWithSegments("/api/online", StringComparison.OrdinalIgnoreCase))
        {
            return (ChangeChannel.Online, null);
        }

        if (http.User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userId)
        {
            return (ChangeChannel.Job, null);
        }

        return Channels.Of(http.User) == AccessChannel.StaffApp ? (ChangeChannel.StaffApp, userId) : (ChangeChannel.Team, userId);
    }

    private sealed class Restore((ChangeChannel, string?)? previous) : IDisposable
    {
        public void Dispose() => Override.Value = previous;
    }
}
