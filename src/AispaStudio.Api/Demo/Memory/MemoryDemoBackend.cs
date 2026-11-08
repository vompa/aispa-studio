using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Pos.Fiscal;
using AispaStudio.Api.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Der Verlauf im Speicher: Termine, Belege, Gutscheine und Abschlüsse entstehen mit denselben Regeln wie über die Dienste, aber ohne Datenbankzugriff;
/// am Ende schreibt <see cref="CompleteAsync"/> alles mit <c>COPY</c> in einer Transaktion. Bricht etwas ab, bleibt die Datenbank, wie sie war.
/// </summary>
internal sealed class MemoryDemoBackend : IDemoBackend
{
    private readonly AsyncServiceScope _scope;
    private readonly Memory.MemoryWorld _world;
    private readonly Guid _tenantId;
    private readonly MemoryDemoOps _ops;

    private MemoryDemoBackend(AsyncServiceScope scope, Memory.MemoryWorld world, Guid tenantId, MemoryDemoOps ops)
    {
        (_scope, _world, _tenantId, _ops) = (scope, world, tenantId, ops);
    }

    public static async Task<MemoryDemoBackend> CreateAsync(IServiceProvider root, Guid tenantId, DemoClock clock, CancellationToken ct)
    {
        var scope = DemoSeeder.Open(root, tenantId);
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AispaDbContext>();
        var snapshot = await Memory.DemoSnapshot.LoadAsync(db, tenantId, ct);
        var world = new Memory.MemoryWorld(new Memory.DbScale(db.Model));
        foreach (var booking in snapshot.ExistingBookings)
        {
            world.BookingsOf(booking.ResourceId).Add(booking);
        }

        // Die Startbelege stehen schon in der Datenbank; der Tagesabschluss braucht sie in der Liste der Kasse.
        foreach (var (registerId, receipts) in snapshot.RegisterReceipts)
        {
            world.ReceiptsByRegister[registerId] = [.. receipts];
            foreach (var receipt in receipts)
            {
                world.ReceiptsById[receipt.Id] = receipt;
            }
        }

        var ops = new MemoryDemoOps(snapshot, world, clock, services.GetRequiredService<FiscalizationResolver>(),
            services.GetRequiredService<IOptions<PaymentOptions>>().Value, services.GetRequiredService<SiteIdentity>().Get(db));
        return new MemoryDemoBackend(scope, world, tenantId, ops);
    }

    public IDemoOps Open() => _ops;

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    public async Task CompleteAsync(CancellationToken ct)
    {
        var services = _scope.ServiceProvider;
        var db = services.GetRequiredService<AispaDbContext>();
        var online = services.GetRequiredService<OnlineDbContext>();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await db.Database.OpenConnectionAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL DEFERRED", ct);
            var writer = new Memory.BulkWriter(connection, _tenantId);
            var model = db.Model;

            // Reihenfolge nach den Fremdschlüsseln: Termine vor Belegungen, Rechnungen vor Belegen, Belege vor Gutscheinen vor Belegzeilen.
            await writer.WriteAsync(model, _world.Appointments, ct);
            await writer.WriteAsync(model, _world.NewBookings, ct);
            await writer.WriteAsync(online.Model, _world.OnlineBookings, ct);
            await writer.WriteAsync(model, _world.FollowUps, ct);
            await writer.WriteAsync(model, _world.Bills, ct);
            await writer.WriteAsync(model, _world.Bills.SelectMany(b => b.Lines), ct);
            await writer.WriteAsync(model, _world.Receipts, ct);
            await writer.WriteAsync(model, _world.Vouchers, ct);
            await writer.WriteAsync(model, _world.Receipts.SelectMany(r => r.Lines), ct);
            await writer.WriteAsync(model, _world.Receipts.SelectMany(r => r.Payments), ct);
            await writer.WriteAsync(model, _world.CashMovements, ct);
            await writer.WriteAsync(model, _world.DayClosings, ct);

            // Stand der Kassen (Belegnummer, Umsatzzähler, Kette): die Kassen verfolgt der Kontext, nur diese Zeilen ändern sich.
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
