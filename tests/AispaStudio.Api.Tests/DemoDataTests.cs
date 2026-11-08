using AispaStudio.Api.Costs;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

public sealed class DemoDataTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Jede_Beispielbehandlung_ist_buchbar_und_rechnet_sich()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await DemoData.SeedAsync(scope.ServiceProvider, TimeProvider.System, Ct);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var booking = scope.ServiceProvider.GetRequiredService<BookingService>();
        var treatments = await db.Treatments.AsNoTracking().Include(t => t.Rooms).OrderBy(t => t.Name).ToListAsync(Ct);
        var locations = await db.Locations.AsNoTracking().ToListAsync(Ct);
        var customer = await db.Customers.AsNoTracking().FirstAsync(Ct);
        Assert.True(treatments.Count >= 25);

        // Jede Behandlung findet in der Testwoche (Montag, 1. März 2027) Person, Raum und Gerät.
        var unbookable = new List<string>();
        foreach (var treatment in treatments)
        {
            var booked = false;
            foreach (var location in locations.Where(l => treatment.Rooms.Any(r => r.LocationId == l.Id)))
            {
                var zone = LocalTime.Zone(location.TimeZone);
                for (var day = 0; day < 6 && !booked; day++)
                {
                    for (var hour = 9; hour <= 17 && !booked; hour++)
                    {
                        var start = LocalTime.ToUtc(new DateOnly(2027, 3, 1).AddDays(day), new TimeOnly(hour, 0), zone);
                        booked = await booking.BookAsync(new AppointmentRequest(location.Id, treatment.Id, customer.Id, start, null, null, null, null), Ct)
                            is BookingOutcome.Success;
                    }
                }
            }

            if (!booked)
            {
                unbookable.Add(treatment.Name);
            }
        }

        Assert.Empty(unbookable);

        // Die Kostenansicht hat für jede Beispielbehandlung alle Angaben; die meisten decken ihre Vollkosten.
        var costs = scope.ServiceProvider.GetRequiredService<CostAnalysis>();
        foreach (var location in locations)
        {
            var analysis = await costs.AnalyzeAsync(location.Id, 2027, 3, Ct);
            Assert.NotNull(analysis);
            Assert.All(analysis.Treatments, t =>
            {
                Assert.Empty(t.Missing);
                Assert.True(t.Material > 0, $"{t.TreatmentName}: kein Material erfasst");
            });
            Assert.True(analysis.Treatments.Count(t => t.Margin > 0) * 4 >= analysis.Treatments.Count * 3, $"{location.Name}: zu viele Behandlungen unter den Vollkosten");
        }
    }
}
