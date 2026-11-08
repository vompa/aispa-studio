using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Demo;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AispaStudio.Api.Tests;

/// <summary>Slice 146: Der Reiter „Belege“ der Kassa fragt <c>api/pos/registers/{id}/receipts?date=…</c>; mit frischen Beispieldaten muss er füllen.</summary>
public sealed class DemoReceiptsTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(12)]
    public async Task Belege_der_Beispieldaten_stehen_an_Kasse_WIEN_1_fuer_heute_und_gestern(int utcHour)
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", connection));
        var now = new DateTimeOffset(2026, 10, 22, utcHour, 0, 0, TimeSpan.Zero);
        var tenantId = await DemoSeeder.SeedAsync(host.Services, now, new DemoDataOptions { WeeksBack = 2, WeeksAhead = 1 }, NullLogger.Instance, Ct);

        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var register = await db.CashRegisters.AsNoTracking().SingleAsync(r => r.Code == "WIEN-1", Ct);
        var zone = LocalTime.Zone(await db.Locations.Where(l => l.Id == register.LocationId).Select(l => l.TimeZone).SingleAsync(Ct));
        var today = LocalTime.LocalDate(now, zone);

        using var anonymous = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await anonymous.PostAsJsonAsync("/api/auth/dev/login", new DevLogin(DemoAccounts.Email("inhaberin")), TestSpa.Json, Ct);
        login.EnsureSuccessStatusCode();
        using var owner = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        owner.DefaultRequestHeaders.Add("Cookie", string.Join("; ", login.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));

        var problems = new List<string>();
        foreach (var date in new[] { today, today.AddDays(-1) })
        {
            var receipts = await owner.GetFromJsonAsync<List<ReceiptDto>>($"/api/pos/registers/{register.Id}/receipts?date={date:yyyy-MM-dd}", TestSpa.Json, Ct);
            var sales = receipts!.Count(r => r.Kind == ReceiptKind.Sale);
            if (sales == 0)
            {
                problems.Add($"{date:dd.MM.yyyy}: {receipts!.Count} Belege, davon 0 Verkäufe");
            }
        }

        Assert.True(problems.Count == 0, "Kasse WIEN-1 ohne Belege: " + string.Join("; ", problems));
    }
}
