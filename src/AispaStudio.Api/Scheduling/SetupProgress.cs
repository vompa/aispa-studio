using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Roster;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// „Betrieb einrichten“ (ADR 0099, Entscheidung 6; ADR 0104, Entscheidung 6): die Karte oben auf „Firmendaten“ zeigt, welche Schritte der Ersteinrichtung noch fehlen. Die API entscheidet,
/// die App zeigt nur an und springt je Schritt in die passende Maske. Ein Schritt gilt als erledigt, sobald er für das Buchen taugt; sonst nennt er, was fehlt. Es ist keine Pflicht und sperrt nichts.
/// </summary>
internal static partial class MasterDataEndpoints
{
    private static void MapSetupProgress(RouteGroupBuilder group)
    {
        MapSetupCatalog(group);
        group.MapGet("/setup/progress", async (AispaDbContext db, BookingService booking, RosterService roster, TimeProvider time, bool? readiness, bool? includeRegister, CancellationToken ct) =>
                await SetupProgress.LoadAsync(db, booking, roster, time.GetUtcNow(), readiness == true, includeRegister != false, ct))
            .WithName("GetSetupProgress")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Liefert die Schritte der Ersteinrichtung (Standort, Öffnungszeiten, Räume, Behandlungen, Team, Kassa) mit ihrem Stand, mit „readiness=true“ auch, ob sich in den nächsten 14 Tagen eine Behandlung buchen lässt.");
    }
}

/// <summary>Die Schritte der Ersteinrichtung in der Reihenfolge, in der man sie sinnvoll abarbeitet.</summary>
internal static class SetupProgress
{
    internal const string NoHours = "Niemand im Team hat Arbeitszeiten oder Dienste. Unter „Team“ die Arbeitszeiten eintragen.";

    /// <param name="readiness">Auch die Bereitschaftsprüfung rechnen; ohne bleibt <c>Readiness</c> leer und der Aufruf lädt nur die Grunddaten.</param>
    public static async Task<SetupProgressDto> LoadAsync(
        AispaDbContext db, BookingService booking, RosterService roster, DateTimeOffset now, bool readiness, bool includeRegister, CancellationToken ct)
    {
        var facts = await SetupFacts.LoadAsync(db, now, includeRegister, ct);
        var anyLocation = facts.Locations.Count > 0;
        var hours = facts.Locations.Any(l => l.OpeningHours.Count > 0);
        var treatments = facts.Treatments.Any(t => t.RoomLocationIds.Count > 0);
        var people = facts.Employees.Any(e => e.HasHours && facts.Treatments.Any(t => t.CanBeDoneBy(e)));
        List<SetupStepDto> steps =
        [
            new("location", "Standort anlegen", "Name, Zeitzone und Anschrift des Spas.", anyLocation, "masterData", "locations",
                facts.AllLocationsClosed ? SetupReadiness.AllLocationsClosed : null),
            new("hours", "Öffnungszeiten eintragen", "Wann Gäste buchen können.", hours, "masterData", "locations",
                hours ? null : facts.AllLocationsClosed ? SetupReadiness.AllLocationsClosed : anyLocation ? SetupReadiness.NoOpeningHours : "Zuerst einen Standort anlegen, dann unter „Standorte“ die Öffnungszeiten eintragen."),
            new("rooms", "Räume anlegen", "Behandlungsräume und Bereiche wie die Sauna.", facts.AnyTreatmentRoom, "masterData", "locations",
                facts.AnyTreatmentRoom ? null
                : facts.AnyRoom ? "Es gibt nur Bereiche wie die Sauna. Unter „Standorte“ einen Behandlungsraum anlegen."
                : "Noch kein Raum angelegt. Unter „Standorte“ einen Behandlungsraum anlegen."),
            new("treatments", "Behandlungen anlegen", "Was das Spa anbietet, mit Dauer und Preis.", treatments, "masterData", "treatments",
                treatments ? null
                : facts.Treatments.Count == 0 ? SetupReadiness.NoTreatment
                : "Keine Behandlung hat einen Raum. Unter „Behandlungen“ jeder Behandlung mindestens einen Raum zuordnen.",
                MissingPrices(facts.Treatments.Count(t => !t.PricedEverywhere))),
            new("people", "Team anlegen", "Wer behandelt und wer an der Rezeption arbeitet.", people, "masterData", "team",
                people ? null : PeopleMissing(facts)),
        ];
        if (includeRegister)
        {
            steps.Add(new("register", "Kassa einrichten", "Kassa-ID, Belegkopf und Land für Belege.", facts.AnyRegister, "masterData", "kassa"));
        }

        return new SetupProgressDto(steps, readiness ? await SetupReadiness.CheckAsync(facts, db, booking, roster, now, ct) : null);
    }

    /// <summary>Hinweis auf Behandlungen ohne Preis: Grundpreis 0 und nicht an jedem Standort mit Behandlungsraum ein Standortpreis über 0 (Preisregeln zählen nicht, außerhalb der Regel gilt der Grundpreis); lässt den Schritt erledigt, weil eine Behandlung ohne Preis buchbar ist.</summary>
    internal static string? MissingPrices(int count) => count switch
    {
        0 => null,
        1 => "Bei einer Behandlung fehlt der Preis. Unter „Behandlungen“ eintragen.",
        _ => $"Bei {count} Behandlungen fehlt der Preis. Unter „Behandlungen“ eintragen.",
    };

    /// <summary>Was im Team fehlt, damit jemand eine Behandlung durchführen kann: zuerst eine Person, dann Arbeitszeiten, dann eine Behandlung, dann die Qualifikation.</summary>
    private static string PeopleMissing(SetupFacts facts) =>
        facts.Employees.Count == 0 ? SetupReadiness.NoEmployee
        : !facts.Employees.Any(e => e.HasHours) ? NoHours
        : facts.Treatments.Count == 0 ? "Ohne Behandlung kann niemand im Team behandeln. Zuerst unter „Behandlungen“ eine Behandlung anlegen."
        : "Niemand mit Arbeitszeiten hat die Qualifikationen für eine Behandlung. Unter „Team“ die Qualifikationen eintragen.";
}
