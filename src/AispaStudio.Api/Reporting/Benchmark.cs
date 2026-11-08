using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reporting;

/// <summary>Vergleichsgruppe: Spas im selben Land mit ähnlich vielen Mitarbeitenden.</summary>
public sealed record BenchmarkPeerGroup(FiscalCountry Country, BenchmarkSize Size);

public enum BenchmarkSize
{
    /// <summary>Bis 10 Mitarbeitende.</summary>
    Small,

    /// <summary>11 bis 25 Mitarbeitende.</summary>
    Medium,

    /// <summary>26 bis 50 Mitarbeitende.</summary>
    Large,
}

/// <summary>Viertelwerte einer Kennzahl in der Vergleichsgruppe.</summary>
public sealed record PeerQuartiles(decimal P25, decimal Median, decimal P75);

/// <summary>Anonymisierte Werte der Vergleichsgruppe.</summary>
public sealed record PeerFigures(int PeerCount, bool IsSampleData, IReadOnlyDictionary<string, PeerQuartiles> Metrics);

/// <summary>
/// Quelle der Vergleichswerte. Echte Werte kommen später aus einem zentralen Vergleichsdienst, an dem Spas freiwillig teilnehmen:
/// Er gibt nur Viertelwerte von Gruppen mit mindestens <see cref="MinimumPeers"/> Spas heraus, nie Werte einzelner Spas oder Namen.
/// </summary>
public interface IBenchmarkPeers
{
    public const int MinimumPeers = 10;

    Task<PeerFigures?> FiguresAsync(BenchmarkPeerGroup group, CancellationToken ct);
}

/// <summary>
/// Beispieldaten für den Prototyp: plausible Werte für Day-Spas in Österreich, Deutschland und der Schweiz, Geldbeträge in der
/// Landeswährung. Jede Antwort ist als Beispieldaten gekennzeichnet.
/// </summary>
internal sealed class SampleBenchmarkPeers : IBenchmarkPeers
{
    private static readonly Dictionary<string, PeerQuartiles> Austria = new()
    {
        ["revenuePerTreatmentHour"] = new(72m, 86m, 101m),
        ["averageTicket"] = new(68m, 84m, 104m),
        ["staffUtilization"] = new(52m, 63m, 74m),
        ["roomUtilization"] = new(41m, 53m, 65m),
        ["noShowRate"] = new(7.5m, 4.5m, 2.5m),
        ["cancellationRate"] = new(12m, 8m, 5m),
        ["returningRate"] = new(38m, 49m, 60m),
        ["rebookingRate"] = new(18m, 27m, 38m),
    };

    private static readonly Dictionary<FiscalCountry, decimal> MoneyFactor = new()
    {
        [FiscalCountry.AT] = 1m,
        [FiscalCountry.DE] = 0.94m,
        [FiscalCountry.CH] = 1.55m,
    };

    private static readonly Dictionary<(FiscalCountry, BenchmarkSize), int> Counts = new()
    {
        [(FiscalCountry.AT, BenchmarkSize.Small)] = 48,
        [(FiscalCountry.AT, BenchmarkSize.Medium)] = 21,
        [(FiscalCountry.AT, BenchmarkSize.Large)] = 12,
        [(FiscalCountry.DE, BenchmarkSize.Small)] = 136,
        [(FiscalCountry.DE, BenchmarkSize.Medium)] = 64,
        [(FiscalCountry.DE, BenchmarkSize.Large)] = 27,
        [(FiscalCountry.CH, BenchmarkSize.Small)] = 39,
        [(FiscalCountry.CH, BenchmarkSize.Medium)] = 17,
        [(FiscalCountry.CH, BenchmarkSize.Large)] = 10,
    };

    private static readonly HashSet<string> Money = ["revenuePerTreatmentHour", "averageTicket"];
    private static readonly HashSet<string> Utilization = ["staffUtilization", "roomUtilization"];

    public Task<PeerFigures?> FiguresAsync(BenchmarkPeerGroup group, CancellationToken ct)
    {
        // Größere Häuser planen dichter und verkaufen mehr Pakete; kleine haben mehr Stammgäste.
        var (utilization, ticket, returning) = group.Size switch
        {
            BenchmarkSize.Small => (-4m, 0.95m, 4m),
            BenchmarkSize.Medium => (0m, 1m, 0m),
            _ => (4m, 1.08m, -5m),
        };

        PeerQuartiles Adjust(string key, PeerQuartiles q)
        {
            if (Money.Contains(key))
            {
                var factor = MoneyFactor[group.Country] * (key == "averageTicket" ? ticket : 1m);
                return new(Math.Round(q.P25 * factor), Math.Round(q.Median * factor), Math.Round(q.P75 * factor));
            }

            var shift = Utilization.Contains(key) ? utilization : key == "returningRate" ? returning : 0m;
            return new(q.P25 + shift, q.Median + shift, q.P75 + shift);
        }

        var metrics = Austria.ToDictionary(m => m.Key, m => Adjust(m.Key, m.Value));
        return Task.FromResult<PeerFigures?>(new PeerFigures(Counts[(group.Country, group.Size)], true, metrics));
    }
}

/// <summary>
/// Ein-Klick-Vergleich: nimmt die Kennzahlen des Management-Cockpits und stellt sie neben die Viertelwerte der Vergleichsgruppe.
/// Die eigenen Zahlen verlassen dabei den Standort nicht; abgefragt wird nur die Gruppe (Land, Größenklasse).
/// </summary>
internal sealed class Benchmark(AispaDbContext db, ManagementReports reports, IBenchmarkPeers peers)
{
    private sealed record Definition(string Key, string Label, string Unit, bool HigherIsBetter, string Tip);

    private static readonly Definition[] Definitions =
    [
        new("revenuePerTreatmentHour", "Umsatz je Behandlungsstunde (netto)", "€", true,
            "Preise der meistgebuchten Behandlungen prüfen und Zusatzleistungen (Upgrade, Produkt) beim Buchen anbieten."),
        new("averageTicket", "Durchschnittsbon netto", "€", true,
            "Pakete und Gutscheine sichtbarer machen, an der Kassa Pflegeprodukte zur Behandlung empfehlen."),
        new("staffUtilization", "Auslastung Personal", "%", true,
            "Dienstplan an die Buchungszeiten anpassen; ruhige Zeiten online und über die Warteliste füllen."),
        new("roomUtilization", "Auslastung Räume", "%", true,
            "Wenig genutzte Räume für andere Behandlungen freigeben oder Öffnungszeiten an die Nachfrage anpassen."),
        new("noShowRate", "No-Show-Quote", "%", false,
            "Erinnerung per SMS am Vortag, Anzahlung oder Ausfallgebühr bei Neukunden."),
        new("cancellationRate", "Storno-Quote", "%", false,
            "Stornofrist klar kommunizieren; frei gewordene Termine gehen automatisch an die Warteliste."),
        new("returningRate", "Stammgäste-Anteil", "%", true,
            "Nach dem ersten Besuch nachfassen, Treueangebot oder Gutschein für den zweiten Termin."),
        new("rebookingRate", "Folgetermin gebucht", "%", true,
            "Folgetermin schon an der Kassa vorschlagen; Therapeutinnen geben dazu eine Empfehlung mit."),
    ];

    /// <summary>Ohne <paramref name="amounts"/> (Recht „Berichte mit Beträgen“) bleiben die Kennzahlen in Prozent; die in Euro fehlen.</summary>
    public async Task<BenchmarkReport?> CompareAsync(Guid locationId, DateOnly from, DateOnly until, bool amounts, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null || await reports.ManagementAsync(locationId, from, until, ct) is not { } report)
        {
            return null;
        }

        var country = await db.CashRegisters.AsNoTracking().Where(r => r.LocationId == locationId && r.IsActive)
            .OrderBy(r => r.Code).Select(r => (FiscalCountry?)r.Country).FirstOrDefaultAsync(ct) ?? CountryOf(location.TimeZone);
        var staff = await db.Employees.AsNoTracking().CountAsync(e => e.IsActive && e.WorkingHours.Any(w => w.LocationId == locationId), ct);
        var size = staff switch
        {
            <= 10 => BenchmarkSize.Small,
            <= 25 => BenchmarkSize.Medium,
            _ => BenchmarkSize.Large,
        };
        var group = new BenchmarkPeerGroup(country, size);
        var figures = await peers.FiguresAsync(group, ct);
        var label = $"Day-Spas in {CountryName(country)} mit {SizeName(size)} Mitarbeitenden";
        if (figures is null || figures.PeerCount < IBenchmarkPeers.MinimumPeers)
        {
            // Zu wenige Spas in der Gruppe: lieber kein Vergleich als einer, der auf einzelne Spas schließen lässt.
            return new BenchmarkReport(locationId, location.Name, from, until, location.Currency, label, figures?.PeerCount ?? 0,
                figures?.IsSampleData ?? false, [],
                [$"Noch zu wenige vergleichbare Spas (mindestens {IBenchmarkPeers.MinimumPeers}), daher kein Vergleich."]);
        }

        var own = report.Kpis.ToDictionary(k => k.Key, k => (decimal?)k.Value);
        var hours = report.Employees.Sum(e => e.TreatmentHours);
        own["revenuePerTreatmentHour"] = hours > 0 ? Math.Round(report.Employees.Sum(e => (e.RevenuePerHour ?? 0) * e.TreatmentHours) / hours, 2) : null;
        var appointments = own.GetValueOrDefault("appointments") ?? 0;
        if (appointments == 0)
        {
            // Ohne Termine sind Quoten 0 und nicht „gut“: dann gibt es keinen eigenen Wert.
            foreach (var key in own.Keys.ToList())
            {
                own[key] = null;
            }
        }

        var metrics = Definitions.Where(d => figures.Metrics.ContainsKey(d.Key) && (amounts || d.Unit != "€")).Select(d =>
            {
                var q = figures.Metrics[d.Key];
                var value = own.GetValueOrDefault(d.Key);
                var rating = value is { } v ? Rate(v, q, d.HigherIsBetter) : (BenchmarkRating?)null;
                return new BenchmarkMetric(d.Key, d.Label, d.Unit, value, q.P25, q.Median, q.P75, d.HigherIsBetter, rating,
                    rating is BenchmarkRating.BelowMedian or BenchmarkRating.Bottom ? d.Tip : null);
            })
            .ToList();

        var notes = new List<string>();
        if (figures.IsSampleData)
        {
            notes.Add("Die Vergleichswerte sind Beispieldaten. Echte Werte kommen, sobald genug Spas am Vergleich teilnehmen.");
        }

        if (appointments is > 0 and < 30)
        {
            notes.Add($"Nur {appointments:0} Behandlungen im Zeitraum: die eigenen Werte schwanken noch stark.");
        }

        if (location.Currency != (country == FiscalCountry.CH ? "CHF" : "EUR"))
        {
            notes.Add("Die Währung des Standorts passt nicht zum Land der Vergleichsgruppe; Geldbeträge nur grob vergleichen.");
        }

        return new BenchmarkReport(locationId, location.Name, from, until, location.Currency, label, figures.PeerCount,
            figures.IsSampleData, metrics, notes);
    }

    internal static BenchmarkRating Rate(decimal value, PeerQuartiles q, bool higherIsBetter)
    {
        // Bei „weniger ist besser“ sind die Viertelwerte schon in Richtung „besser“ geordnet (P75 = niedrigster Wert).
        var better = higherIsBetter ? value - q.Median : q.Median - value;
        var top = higherIsBetter ? value >= q.P75 : value <= q.P75;
        var bottom = higherIsBetter ? value < q.P25 : value > q.P25;
        return top ? BenchmarkRating.Top : bottom ? BenchmarkRating.Bottom : better >= 0 ? BenchmarkRating.AboveMedian : BenchmarkRating.BelowMedian;
    }

    private static FiscalCountry CountryOf(string timeZone) => timeZone switch
    {
        "Europe/Berlin" => FiscalCountry.DE,
        "Europe/Zurich" => FiscalCountry.CH,
        _ => FiscalCountry.AT,
    };

    private static string CountryName(FiscalCountry country) => country switch
    {
        FiscalCountry.DE => "Deutschland",
        FiscalCountry.CH => "der Schweiz",
        _ => "Österreich",
    };

    private static string SizeName(BenchmarkSize size) => size switch
    {
        BenchmarkSize.Small => "bis zu 10",
        BenchmarkSize.Medium => "11 bis 25",
        _ => "26 bis 50",
    };
}
