using AispaStudio.Api.Access;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Sync;

/// <summary>
/// Verlauf je Standort für die Seite „Verlauf“ (Arbeitsanweisung Verlauf, Aufgabe A): alles, was im Betrieb passiert ist, seitenweise, neueste zuerst. Die Rechte
/// folgen den Verlauf-Endpunkten je Datensatz (<see cref="HistoryEndpoints"/>, ADR 0082 Punkt 5): Termine, Kunden und Behandlungen mit dem Kalenderrecht, Zahlungen
/// und Anfragen mit dem Recht der Kassa und der Anfragen (Termine buchen), Dienstplan, Zugänge und Firmendaten nur für Inhaber und Manager. Arten außerhalb der Rechte fehlen still; 403 nur, wenn ausschließlich
/// verbotene Arten angefragt sind.
/// </summary>
internal static class ActivityEndpoints
{
    private const string PaymentsOnly = "Zahlungen im Verlauf sieht, wer die Kassa sieht.";

    private const string RequestsOnly = "Anfragen im Verlauf sieht, wer die Anfragen sieht.";

    private static readonly ActivityKind[] CalendarKinds =
    [
        ActivityKind.Booked, ActivityKind.Moved, ActivityKind.Cancelled, ActivityKind.Restored, ActivityKind.StatusChanged, ActivityKind.AppointmentChanged,
        ActivityKind.CustomerCreated, ActivityKind.CustomerChanged, ActivityKind.TreatmentChanged,
    ];

    private static readonly ActivityKind[] PaymentKinds = [ActivityKind.Paid, ActivityKind.Deposit, ActivityKind.ReceiptVoided];

    private static readonly ActivityKind[] RequestKinds = [ActivityKind.Request];

    private static readonly ActivityKind[] LeadershipKinds = [ActivityKind.RosterChanged, ActivityKind.AccessChanged, ActivityKind.CompanyChanged];

    /// <summary>Welche Arten dieser Zugang sehen darf; die App zeigt nur dafür Chips.</summary>
    public static IReadOnlySet<ActivityKind> AllowedKinds(UserAccess access)
    {
        var allowed = new HashSet<ActivityKind>(CalendarKinds);
        if (access.Has(AccessRight.Book))
        {
            allowed.UnionWith(PaymentKinds);
            allowed.UnionWith(RequestKinds);
        }

        if (access.IsOwnerLevel)
        {
            allowed.UnionWith(LeadershipKinds);
        }

        return allowed;
    }

    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/locations/{locationId:guid}/history", async Task<IResult> (
                Guid locationId, DateOnly? from, DateOnly? until, ActivityKind[]? kinds, string? actor, ActivityChannel? channel, Guid? customerId, string? search,
                long? before, int? take, AccessService accessService, ActivityFeed feed, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var allowed = AllowedKinds(access);
                var requested = kinds is { Length: > 0 } ? kinds.ToHashSet() : null;
                if (requested is not null && !requested.Overlaps(allowed))
                {
                    return PermissionExtensions.Forbidden(
                        requested.IsSubsetOf(PaymentKinds) ? PaymentsOnly : requested.IsSubsetOf(RequestKinds) ? RequestsOnly : HistoryEndpoints.LeadershipOnly);
                }

                var effective = requested is null ? allowed : requested.Intersect(allowed).ToHashSet();
                var query = new ActivityFeed.Query(locationId, from, until, effective, string.IsNullOrWhiteSpace(actor) ? null : actor, channel, customerId,
                    string.IsNullOrWhiteSpace(search) ? null : search.Trim(), before, Math.Clamp(take ?? 50, 1, 200));
                return await feed.ReadAsync(query, ct) is { } page ? TypedResults.Ok(page) : TypedResults.NotFound();
            })
            .WithTags("Verlauf")
            .WithName("GetLocationHistory")
            .WithSummary("Verlauf eines Standorts, neueste zuerst: Buchungen, Verschiebungen, Stornos, Status, Zahlungen (auch Verkäufe ohne Termin), Anfragen, Kunden, Behandlungen, Dienstplan und Zugänge "
                + "mit Filtern nach Zeitraum (Ortszeit), Arten, Person, Kanal, Gast und Suchtext; seitenweise über before und take (höchstens 200). "
                + "Arten außerhalb der Rechte fehlen; 403 nur, wenn ausschließlich verbotene Arten angefragt sind.")
            .Produces<ActivityPage>().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status403Forbidden)
            .RequirePermission(AccessRight.ViewCalendar);

        return routes;
    }
}
