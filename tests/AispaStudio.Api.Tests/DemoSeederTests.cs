using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Demo;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AispaStudio.Api.Tests;

public sealed class DemoSeederTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Testdatensatz_fuellt_Kalender_Kassa_und_Berichte_und_die_Testkonten_melden_sich_an()
    {
        // Mit der echten Uhr wie beim Start der API: Online-Gutscheine stellt die Queue danach zur echten Zeit aus.
        // Kürzer als in der Entwicklung, damit der Test schnell bleibt.
        var now = TimeProvider.System.GetUtcNow();
        var tenantId = await DemoSeeder.SeedAsync(fixture.Factory.Services, now, new DemoDataOptions { WeeksBack = 2, WeeksAhead = 1 },
            NullLogger.Instance, Ct);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();

        // Vergangenheit erledigt oder abgesagt, Zukunft gebucht, an allen drei Standorten.
        var appointments = await db.Appointments.AsNoTracking().ToListAsync(Ct);
        Assert.Equal(3, appointments.Select(a => a.LocationId).Distinct().Count());
        Assert.Contains(appointments, a => a.Status == AppointmentStatus.Completed);
        Assert.Contains(appointments, a => a.Status == AppointmentStatus.Cancelled);
        Assert.All(appointments.Where(a => a.Status == AppointmentStatus.Booked), a => Assert.True(a.End > now));
        Assert.Contains(appointments, a => a.Status == AppointmentStatus.Booked && a.Start > now.AddDays(5));

        // Belegkette je Kasse lückenlos ab dem Startbeleg, Tagesabschlüsse vorhanden, Gutscheine verkauft.
        var receipts = await db.Receipts.AsNoTracking().ToListAsync(Ct);
        foreach (var register in receipts.GroupBy(r => r.RegisterId))
        {
            var numbers = register.OrderBy(r => r.Number).ToList();
            Assert.Equal(ReceiptKind.Start, numbers[0].Kind);
            Assert.Equal(Enumerable.Range(1, numbers.Count).Select(n => (long)n), numbers.Select(r => r.Number));
            var outOfOrder = numbers.Zip(numbers.Skip(1)).Where(p => p.First.IssuedAt > p.Second.IssuedAt)
                .Select(p => $"Nr. {p.First.Number} {p.First.Kind} {p.First.IssuedAt:O} vor Nr. {p.Second.Number} {p.Second.Kind} {p.Second.IssuedAt:O}").ToList();
            Assert.True(outOfOrder.Count == 0, "Belege nicht in zeitlicher Reihenfolge: " + string.Join("; ", outOfOrder));
        }

        Assert.True(receipts.Count(r => r.Kind == ReceiptKind.Sale) > 100);
        Assert.True(await db.DayClosings.CountAsync(Ct) >= 20);
        Assert.True(await db.Vouchers.AnyAsync(Ct));
        Assert.True(await db.AppointmentDeposits.AnyAsync(Ct), "Anzahlung in Graz fehlt");
        Assert.InRange(await db.InboundMails.CountAsync(Ct), 7, 8);

        // Jede Rolle meldet sich ohne Passkey an; fremde Adressen nicht.
        using var anonymous = fixture.CreateClient();
        var accounts = await anonymous.GetFromJsonAsync<List<DevAccount>>("/api/auth/dev/accounts", TestSpa.Json, Ct);
        Assert.Equal(20, accounts!.Count);
        Assert.Equal(Enum.GetValues<StaffRole>().Order(), accounts.Select(a => a.Role).Distinct().Order());
        var rejected = await anonymous.PostAsJsonAsync("/api/auth/dev/login", new DevLogin("jemand@example.at"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);

        using var owner = await SignInAsync(DemoAccounts.Email("inhaberin"));
        var me = await owner.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);
        Assert.Equal(StaffRole.Owner, me!.Access!.Role);
        Assert.Equal(tenantId, me.Access.TenantId);

        var vienna = await db.Locations.AsNoTracking().FirstAsync(l => l.Name == "Wien Innere Stadt", Ct);
        var today = LocalTime.LocalDate(now, LocalTime.Zone(vienna.TimeZone));
        var report = await owner.GetFromJsonAsync<ManagementReportDto>(
            $"/api/reports/locations/{vienna.Id}/management?from={today.AddDays(-10):yyyy-MM-dd}&until={today.AddDays(-1):yyyy-MM-dd}", TestSpa.Json, Ct);
        Assert.True(report!.Kpis.Single(k => k.Key == "revenue").Value > 1000);
        Assert.True(report.Kpis.Single(k => k.Key == "appointments").Value > 30);

        // Lager: Verbrauch der Behandlungen und Verkäufe gebucht, nirgends negativ, in Graz ein Bestellvorschlag.
        var levels = await db.StockMovements.AsNoTracking().GroupBy(m => new { m.LocationId, m.ItemId }).Select(g => g.Sum(m => m.Quantity)).ToListAsync(Ct);
        Assert.All(levels, q => Assert.True(q >= 0));

        // Lagerorte: je Standort Hauptlager und Verkaufsregal, je Behandlungsraum ein Schrank; auch am einzelnen Ort nirgends negativ,
        // der Verbrauch kommt zum Teil aus den Schränken.
        var placeLevels = await db.StockMovements.AsNoTracking().GroupBy(m => new { m.StoragePlaceId, m.ItemId }).Select(g => g.Sum(m => m.Quantity)).ToListAsync(Ct);
        Assert.All(placeLevels, q => Assert.True(q >= 0));
        foreach (var location in await db.Locations.AsNoTracking().ToListAsync(Ct))
        {
            var kinds = await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == location.Id).Select(p => p.Kind).ToListAsync(Ct);
            Assert.Equal(1, kinds.Count(k => k == StoragePlaceKind.Main));
            Assert.Equal(1, kinds.Count(k => k == StoragePlaceKind.Sales));
            Assert.Contains(StoragePlaceKind.Room, kinds);
            Assert.True(kinds.Count <= 10);
        }

        var cabinets = await db.StoragePlaces.AsNoTracking().Where(p => p.Kind == StoragePlaceKind.Room).Select(p => p.Id).ToListAsync(Ct);
        Assert.True(await db.StockMovements.AnyAsync(m => m.Kind == StockMovementKind.Consumption && cabinets.Contains(m.StoragePlaceId), Ct));
        Assert.True(await db.StockMovements.AnyAsync(m => m.Kind == StockMovementKind.Refill && cabinets.Contains(m.StoragePlaceId), Ct));
        Assert.True(await db.StoragePlaceMinimums.AnyAsync(Ct));
        Assert.True(await db.StockMovements.AnyAsync(m => m.Kind == StockMovementKind.Consumption && m.At < now.AddDays(-1), Ct));
        Assert.True(await db.StockMovements.AnyAsync(m => m.Kind == StockMovementKind.Sale, Ct));
        var graz = await db.Locations.AsNoTracking().FirstAsync(l => l.Name == "Graz Zentrum", Ct);
        var stock = await owner.GetFromJsonAsync<StockOverviewDto>($"/api/stock/locations/{graz.Id}", TestSpa.Json, Ct);
        Assert.Contains(stock!.Items, i => i.BelowMinimum && i.Suggested > 0);
        Assert.Equal([PurchaseOrderStatus.Ordered, PurchaseOrderStatus.Received],
            await db.PurchaseOrders.AsNoTracking().OrderBy(o => o.Status).Select(o => o.Status).ToListAsync(Ct));
        Assert.True(await db.StockMovements.AnyAsync(m => m.PurchaseOrderId != null && m.At < now.AddDays(-6), Ct));
        var batches = await owner.GetFromJsonAsync<StockBatchesDto>($"/api/stock/locations/{vienna.Id}/batches", TestSpa.Json, Ct);
        Assert.Contains(batches!.Batches, b => b.ExpiresSoon && !b.Expired && b.Batch is not null);

        using var therapist = await SignInAsync(DemoAccounts.EmployeeEmail("Lena Huber"));
        var lena = await therapist.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);
        Assert.Equal(StaffRole.Therapist, lena!.Access!.Role);
        Assert.NotNull(lena.EmployeeId);
    }

    private async Task<HttpClient> SignInAsync(string email)
    {
        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/dev/login", new DevLogin(email), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        return fixture.CreateClient(string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
    }

    [Fact]
    public async Task Termine_verteilen_sich_wie_im_echten_Spa_auf_viele_Gaeste()
    {
        // Der volle Zeitraum der Entwicklung: 8 Wochen zurück, 4 voraus.
        // Eigene Datenbank, damit sich das Einspielen nicht mit dem Testdatensatz der anderen Tests der Klasse (gleiche Konten) überschneidet.
        var connection = await fixture.CreateDatabaseAsync(Ct);
        using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", connection));
        var tenantId = await DemoSeeder.SeedAsync(host.Services, TimeProvider.System.GetUtcNow(), new DemoDataOptions(), NullLogger.Instance, Ct);
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();

        var perCustomer = (await db.Appointments.AsNoTracking().GroupBy(a => a.CustomerId).Select(g => g.Count()).ToListAsync(Ct)).Order().ToList();
        var median = perCustomer[perCustomer.Count / 2];

        // Höchstens ein Besuch je Woche: 12 Wochen überdecken bis zu 13 Kalenderwochen, daher höchstens 13 Besuche. Ein Spa-Tag oder ein Paar
        // besteht aus mehreren Terminen mit gleicher GroupId und zählt als ein Besuch (ein Gast mit zwei Spa-Tagen hat leicht 15 Termine).
        var visitsPerCustomer = (await db.Appointments.AsNoTracking().Select(a => new { a.CustomerId, Visit = a.GroupId ?? a.Id }).Distinct()
            .GroupBy(a => a.CustomerId).Select(g => g.Count()).ToListAsync(Ct)).Max();
        Assert.True(visitsPerCustomer <= 13, $"Meistbesuchter Gast hat {visitsPerCustomer} Besuche");
        Assert.True(perCustomer.Count >= 300, $"Nur {perCustomer.Count} Gäste mit Terminen");
        Assert.True(median is > 1 and <= 6, $"Median {median}");
        Assert.True(perCustomer.Count(n => n == 1) >= 20, "Zu wenige Gäste mit nur einem Termin");
        Assert.True(perCustomer.Count(n => n >= 8) >= 10, "Zu wenige Stammgäste");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(75)]
    [InlineData(-200)]
    public async Task Kein_Gast_hat_mehr_als_einen_Termin_je_Kalenderwoche_ausser_innerhalb_einer_Gruppe(int hoursFromNow)
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", connection));
        // Der Verlauf hängt von der Uhrzeit des Einspielens ab; mehrere Zeitpunkte decken andere Wochengrenzen und Tagesstände ab.
        var now = TimeProvider.System.GetUtcNow().AddHours(hoursFromNow);
        var tenantId = await DemoSeeder.SeedAsync(host.Services, now, new DemoDataOptions(), NullLogger.Instance, Ct);
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();

        // Ein Spa-Tag (mehrere Schritte eines Gastes mit gleicher GroupId) zählt als ein Termin; Paare haben zwei verschiedene Gäste.
        // Weitere Ausnahmen gibt es bewusst nicht.
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Customer).ToListAsync(Ct);
        var zones = (await db.Locations.AsNoTracking().ToListAsync(Ct)).ToDictionary(l => l.Id, l => LocalTime.Zone(l.TimeZone));
        var problems = appointments
            .GroupBy(a => (a.CustomerId, Week: LocalTime.LocalDate(a.Start, zones[a.LocationId]).DayNumber / 7))
            .Select(g => (g.Key, Units: g.Select(a => a.GroupId ?? a.Id).Distinct().ToList(), Items: g.ToList()))
            .Where(g => g.Units.Count > 1)
            .Select(g => $"{g.Items[0].Customer.FirstName} {g.Items[0].Customer.LastName}: "
                + string.Join(", ", g.Items.OrderBy(a => a.Start).Select(a => $"{a.Start:dd.MM. HH:mm}{(a.GroupId is null ? "" : " (Gruppe)")}")))
            .ToList();

        Assert.True(problems.Count == 0, $"{problems.Count} Gäste mit mehreren Terminen in einer Woche: {string.Join("; ", problems.Take(10))}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(18)]
    [InlineData(21)]
    public async Task Jeder_Oeffnungstag_hat_mehrere_Paarbuchungen_und_mit_Paarraum_eine_im_Paarraum(int utcHour)
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", connection));
        // Feste Tage und Uhrzeiten statt der echten Uhr: Der Verlauf hängt von der Uhrzeit des Einspielens ab (Termine, die schon vorbei sind,
        // verbrauchen andere Zufallszüge), und der Test soll an jedem Tag und zu jeder Uhrzeit gleich ausfallen.
        var now = new DateTimeOffset(2026, 10, 22, utcHour, 0, 0, TimeSpan.Zero);
        var options = new DemoDataOptions { WeeksBack = 2, WeeksAhead = 2 };
        var tenantId = await DemoSeeder.SeedAsync(host.Services, now, options, NullLogger.Instance, Ct);
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();

        var locations = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).ToListAsync(Ct);
        var rooms = await db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, Ct);
        var employees = await db.Employees.AsNoTracking().Include(e => e.Qualifications).ToListAsync(Ct);
        var roster = scope.ServiceProvider.GetRequiredService<RosterService>();
        const string BodyQualification = "Körperbehandlung und Hamam";
        var groups = (await db.Appointments.AsNoTracking().Where(a => a.GroupId != null).ToListAsync(Ct))
            .GroupBy(a => a.GroupId!.Value).Select(g => g.ToList()).ToList();
        var pairs = groups.Where(g => g.Count == 2 && g.Select(a => a.CustomerId).Distinct().Count() == 2
            && g[0].Start == g[1].Start && g.All(a => a.Status != AppointmentStatus.Cancelled)).ToList();
        var spaDays = groups.Where(g => g.Count >= 2 && g.Select(a => a.CustomerId).Distinct().Count() == 1).ToList();

        var problems = new List<string>();
        foreach (var location in locations)
        {
            var zone = LocalTime.Zone(location.TimeZone);
            var today = LocalTime.LocalDate(now, zone);
            var hasPairRoom = rooms.Values.Any(r => r.LocationId == location.Id && !r.IsArea && r.Places >= 2);
            for (var date = today.AddDays(-7 * options.WeeksBack); date <= today.AddDays(7 * options.WeeksAhead); date = date.AddDays(1))
            {
                if (location.OpeningHours.All(h => h.Day != date.DayOfWeek))
                {
                    continue;
                }

                // Mit einer Person im Dienst ist kein Paar möglich, für den Paarraum brauchen beide die Qualifikation für Körperbehandlungen.
                var onDuty = (await roster.ShiftsAsync(location.Id, date, zone, Ct)).Select(s => s.EmployeeId).Distinct().ToList();
                if (onDuty.Count < 2)
                {
                    continue;
                }

                var bodyTherapists = employees.Count(e => onDuty.Contains(e.Id) && e.Qualifications.Any(q => q.Name == BodyQualification));
                var ofDay = pairs.Where(g => g[0].LocationId == location.Id && LocalTime.LocalDate(g[0].Start, zone) == date).ToList();
                var inPairRoom = ofDay.Count(g => g.All(a => rooms[a.RoomId].Places >= 2));
                if (ofDay.Count < 2 || (hasPairRoom && bodyTherapists >= 2 && inPairRoom < 1))
                {
                    problems.Add($"{location.Name} {date:dd.MM.}: {ofDay.Count} Paare, {inPairRoom} im Paarraum");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("; ", problems));
        Assert.True(spaDays.Count >= 5, $"Nur {spaDays.Count} Spa-Tage");
    }
}
