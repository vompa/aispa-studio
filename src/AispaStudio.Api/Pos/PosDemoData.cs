using AispaStudio.Api.Data;
using AispaStudio.Api.Stock;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Pos;

/// <summary>Beispiel-Artikel und -Kassen für das Beispiel-Spa (nach <see cref="Scheduling.DemoData"/>), nur mit <c>Database:SeedDemoData</c>.</summary>
internal static class PosDemoData
{
    public const string SaunaDayPass = "Tageseintritt Sauna";

    /// <summary>Artikel im Mandanten, der gerade gesetzt ist.</summary>
    public static async Task AddArticlesAsync(AispaDbContext db, CancellationToken ct)
    {
        // Steuersätze der Beispiele mit der Steuerberatung bestätigen; Bäder/Saunen sind in Österreich ermäßigt (13 %).
        db.Articles.AddRange(
            new Article { Name = "Massageöl Lavendel 100 ml", Price = 24.90m, TaxCategory = TaxCategory.Normal, RevenueAccountId = PosModel.Products.Id },
            new Article { Name = "Gesichtscreme Hydro 50 ml", Price = 39.00m, TaxCategory = TaxCategory.Normal, RevenueAccountId = PosModel.Products.Id },
            new Article { Name = "Körperpeeling Kokos 200 g", Price = 29.50m, TaxCategory = TaxCategory.Normal, RevenueAccountId = PosModel.Products.Id },
            new Article { Name = "Zirbenkissen", Price = 34.00m, TaxCategory = TaxCategory.Normal, RevenueAccountId = PosModel.Products.Id },
            new Article { Name = SaunaDayPass, Price = 32.00m, TaxCategory = TaxCategory.Reduced2, RevenueAccountId = PosModel.Treatments.Id });
        await db.SaveChangesAsync(ct);

        // Eintritte in die Sauna rechnet die Kassa mit dem Tageseintritt ab; der Ruheraum bleibt ohne Artikel (kostenlos zum Termin).
        var dayPass = await db.Articles.Where(a => a.Name == SaunaDayPass).Select(a => a.Id).FirstAsync(ct);
        await db.Rooms.Where(r => r.IsArea && r.Name == "Finnische Sauna").ExecuteUpdateAsync(s => s.SetProperty(r => r.AdmissionArticleId, dayPass), ct);
        await AddRecommendationsAsync(db, ct);
    }

    /// <summary>„Passt dazu“ (Slice 115): Massagen mit Öl empfehlen das Massageöl, Gesichtsbehandlungen die Gesichtscreme.</summary>
    private static async Task AddRecommendationsAsync(AispaDbContext db, CancellationToken ct)
    {
        (string Article, string[] Treatments)[] pairs =
        [
            ("Massageöl Lavendel 100 ml", ["Lava-Hot-Stone-Massage (60 Min.)", "Lava-Hot-Stone-Massage (90 Min.)", "Klassische Massage"]),
            ("Gesichtscreme Hydro 50 ml", ["Gesichtsbehandlung Klassik", "Hydrafacial"]),
        ];
        foreach (var (article, treatments) in pairs)
        {
            var articleId = await db.Articles.Where(a => a.Name == article).Select(a => a.Id).FirstAsync(ct);
            var treatmentIds = await db.Treatments.Where(t => treatments.Contains(t.Name)).Select(t => t.Id).ToListAsync(ct);
            db.TreatmentRecommendedArticles.AddRange(treatmentIds.Select(id => new TreatmentRecommendedArticle { TreatmentId = id, ArticleId = articleId }));
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Eine Kasse je Standort; der Startbeleg trägt die Zeit von <paramref name="pos"/>.</summary>
    public static async Task<Dictionary<Guid, Guid>> CreateRegistersAsync(PosService pos, AispaDbContext db, CancellationToken ct)
    {
        var registers = new Dictionary<Guid, Guid>();
        // Erfundene Firmendaten für Beleg, Rechnung, Sofortüberweisung und Impressum; die Standorte übernehmen sie (ADR 0052).
        await db.Tenants.Where(t => t.Id == db.CurrentTenantId).ExecuteUpdateAsync(s => s
            .SetProperty(t => t.LegalName, "AISPA Day Spa GmbH")
            .SetProperty(t => t.Street, "Kärntner Straße 1")
            .SetProperty(t => t.PostalCode, "1010")
            .SetProperty(t => t.City, "Wien")
            .SetProperty(t => t.Phone, "+43 1 000 00 00")
            .SetProperty(t => t.Email, "office@aispa.test")
            .SetProperty(t => t.Website, "https://aispa.test")
            .SetProperty(t => t.VatId, "ATU00000000")
            .SetProperty(t => t.CompanyRegisterNumber, "FN 000000a")
            .SetProperty(t => t.CompanyRegisterCourt, "Handelsgericht Wien")
            .SetProperty(t => t.AccountHolder, "AISPA Day Spa GmbH")
            .SetProperty(t => t.Iban, "AT611904300234573201")
            .SetProperty(t => t.Bic, "BKAUATWW")
            .SetProperty(t => t.BusinessPurpose, "Betrieb von Day-Spas mit Massagen, Kosmetik und Saunabereich")
            .SetProperty(t => t.SupervisoryAuthority, "Magistratisches Bezirksamt für den 1. Bezirk")
            .SetProperty(t => t.ChamberAndRules, "Mitglied der Wirtschaftskammer Wien; Gewerbeordnung, abrufbar unter www.ris.bka.gv.at"), ct);
        var locations = await db.Locations.AsNoTracking().OrderBy(l => l.Name).ToListAsync(ct);
        var n = 0;
        foreach (var location in locations)
        {
            n++;
            var code = location.Name.Split(' ')[0].ToUpperInvariant().Replace("Ö", "OE", StringComparison.Ordinal) + "-1";
            var created = await pos.CreateRegisterAsync(new CashRegisterInput(location.Id, code, FiscalCountry.AT,
                $"AISPA Day Spa {location.Name}\nBeispielgasse {n}, Österreich\nUID ATU00000000",
                "AISPA Day Spa GmbH", "AT611904300234573201", null), ct);
            registers[location.Id] = created.Value?.Id ?? throw new InvalidOperationException(created.Error?.Message);
        }

        return registers;
    }
}
