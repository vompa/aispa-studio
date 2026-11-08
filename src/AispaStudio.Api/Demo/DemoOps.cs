using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Reporting;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Demo;

/// <summary>Wie die Beispieldaten entstehen: über die Dienste (langsam, jeder Schritt wie im Betrieb) oder im Speicher mit anschließendem <c>COPY</c>.</summary>
public enum DemoMode
{
    /// <summary>Buchen und Kassieren über <c>BookingService</c> und <c>PosService</c> gegen die Datenbank.</summary>
    Services,

    /// <summary>Dieselbe Fachlogik im Speicher, danach ein <c>COPY</c> je Tabelle in einer Transaktion.</summary>
    Bulk,
}

/// <summary>
/// Die Schritte, die der Verlauf (<see cref="DemoHistory"/>) braucht: Termine buchen und ändern, kassieren, Kassa abschließen. Zwei Umsetzungen
/// führen denselben Ablauf aus: <see cref="ServiceDemoBackend"/> gegen die Dienste und <see cref="MemoryDemoBackend"/> im Speicher.
/// </summary>
internal interface IDemoBackend : IAsyncDisposable
{
    /// <summary>Beginnt einen Abschnitt (Tag und Standort); die Dienste bekommen dafür einen eigenen Scope.</summary>
    IDemoOps Open();

    /// <summary>Schließt den Verlauf ab; im Speicher schreibt es alles in die Datenbank.</summary>
    Task CompleteAsync(CancellationToken ct);
}

internal interface IDemoOps : IAsyncDisposable
{
    // Termine
    Task<List<AvailableSlot>> FreeSlotsAsync(Guid locationId, Guid treatmentId, DateOnly date, CancellationToken ct);

    Task<BookingOutcome> BookAsync(AppointmentRequest request, CancellationToken ct);

    /// <summary>Freie Startzeiten für eine gemeinsame Buchung (Paar, Spa-Tag) mit dem Gruppenplaner.</summary>
    Task<List<GroupSlot>> FreeGroupSlotsAsync(Guid locationId, GroupAvailabilityRequest request, CancellationToken ct);

    Task<BookingOutcome> BookGroupAsync(GroupBookingRequest request, CancellationToken ct);

    Task<BookingOutcome> CancelAsync(Guid appointmentId, string? reason, CancellationToken ct);

    Task<BookingOutcome> ChangeStatusAsync(Guid appointmentId, AppointmentStatus status, CancellationToken ct);

    Task AddOnlineBookingsAsync(IReadOnlyCollection<OnlineBooking> bookings, CancellationToken ct);

    Task AddFollowUpsAsync(IReadOnlyCollection<AppointmentFollowUp> followUps, CancellationToken ct);

    // Kassa
    Task<PosResult<BillDto>> CreateBillAsync(BillInput input, CancellationToken ct);

    Task<PosResult<BillDto>> AddLineAsync(Guid billId, BillLineInput input, bool supervisor, CancellationToken ct);

    /// <summary>Wendet eine laufende Aktion auf die passenden Positionen der offenen Rechnung an (wie der Klick an der Kassa).</summary>
    Task<PosResult<BillDto>> ApplyPromotionAsync(Guid billId, Guid promotionId, CancellationToken ct);

    Task<decimal> OpenAmountAsync(Guid billId, CancellationToken ct);

    Task<PosError?> DeleteBillAsync(Guid billId, CancellationToken ct);

    Task<PosResult<ReceiptDto>> CheckoutAsync(Guid billId, CheckoutRequest request, string? userId, CancellationToken ct);

    Task<PosResult<ReceiptDto>> VoidAsync(Guid receiptId, string? reason, string? userId, bool supervisor, CancellationToken ct);

    Task<PosResult<CashMovementDto>> AddCashMovementAsync(Guid registerId, CashMovementInput input, string? userId, bool supervisor, CancellationToken ct);

    Task<PosResult<DayClosingDto>> DayReportAsync(Guid registerId, DayClosingInput? close, string? userId, CancellationToken ct);
}

/// <summary>Der bisherige Weg: jeder Schritt über die Dienste, wie im Betrieb, mit zurückgestellter Uhr.</summary>
internal sealed class ServiceDemoBackend(IServiceProvider root, Guid tenantId, DemoClock clock) : IDemoBackend
{
    public IDemoOps Open() => new ServiceDemoOps(DemoSeeder.Open(root, tenantId), clock);

    public Task CompleteAsync(CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class ServiceDemoOps : IDemoOps
{
    private readonly AsyncServiceScope _scope;
    private readonly BookingService _booking;
    private readonly PosService _pos;
    // Die Kontexte gehören dem Dienst-Scope, der in DisposeAsync freigegeben wird.
#pragma warning disable CA2213
    private readonly AispaDbContext _db;
    private readonly OnlineDbContext _online;
#pragma warning restore CA2213

    public ServiceDemoOps(AsyncServiceScope scope, DemoClock clock)
    {
        _scope = scope;
        var services = scope.ServiceProvider;
        _booking = ActivatorUtilities.CreateInstance<BookingService>(services, clock, SilentMessageBus.Create());
        _pos = ActivatorUtilities.CreateInstance<PosService>(services, clock);
        _db = services.GetRequiredService<AispaDbContext>();
        _online = services.GetRequiredService<OnlineDbContext>();
    }

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    public async Task<List<AvailableSlot>> FreeSlotsAsync(Guid locationId, Guid treatmentId, DateOnly date, CancellationToken ct)
    {
        var context = await _booking.LoadContextAsync(locationId, treatmentId, date, null, ct);
        return context is null ? [] : new ResourcePlanner(context).FreeSlots(date, TimeSpan.FromMinutes(15), null).ToList();
    }

    public Task<BookingOutcome> BookAsync(AppointmentRequest request, CancellationToken ct) => _booking.BookAsync(request, ct);

    public async Task<List<GroupSlot>> FreeGroupSlotsAsync(Guid locationId, GroupAvailabilityRequest request, CancellationToken ct) =>
        await _booking.GroupAvailabilityAsync(locationId, request, ct) is BookingOutcome.GroupSlots found ? found.Slots : [];

    public Task<BookingOutcome> BookGroupAsync(GroupBookingRequest request, CancellationToken ct) => _booking.BookGroupAsync(request, ct);

    public Task<BookingOutcome> CancelAsync(Guid appointmentId, string? reason, CancellationToken ct) => _booking.CancelAsync(appointmentId, reason, ct);

    public Task<BookingOutcome> ChangeStatusAsync(Guid appointmentId, AppointmentStatus status, CancellationToken ct) =>
        _booking.ChangeStatusAsync(appointmentId, status, ct);

    public async Task AddOnlineBookingsAsync(IReadOnlyCollection<OnlineBooking> bookings, CancellationToken ct)
    {
        _online.OnlineBookings.AddRange(bookings);
        await _online.SaveChangesAsync(ct);
    }

    public async Task AddFollowUpsAsync(IReadOnlyCollection<AppointmentFollowUp> followUps, CancellationToken ct)
    {
        _db.AppointmentFollowUps.AddRange(followUps);
        await _db.SaveChangesAsync(ct);
    }

    public Task<PosResult<BillDto>> CreateBillAsync(BillInput input, CancellationToken ct) => _pos.CreateBillAsync(input, ct);

    public Task<PosResult<BillDto>> AddLineAsync(Guid billId, BillLineInput input, bool supervisor, CancellationToken ct) =>
        _pos.AddLineAsync(billId, input, supervisor, ct);

    public Task<PosResult<BillDto>> ApplyPromotionAsync(Guid billId, Guid promotionId, CancellationToken ct) => _pos.ApplyPromotionAsync(billId, promotionId, ct);

    public async Task<decimal> OpenAmountAsync(Guid billId, CancellationToken ct) => (await _pos.GetBillAsync(billId, ct))!.Open;

    public Task<PosError?> DeleteBillAsync(Guid billId, CancellationToken ct) => _pos.DeleteBillAsync(billId, ct);

    public Task<PosResult<ReceiptDto>> CheckoutAsync(Guid billId, CheckoutRequest request, string? userId, CancellationToken ct) =>
        _pos.CheckoutAsync(billId, request, userId, ct);

    public Task<PosResult<ReceiptDto>> VoidAsync(Guid receiptId, string? reason, string? userId, bool supervisor, CancellationToken ct) =>
        _pos.VoidAsync(receiptId, reason, userId, supervisor, ct);

    public Task<PosResult<CashMovementDto>> AddCashMovementAsync(Guid registerId, CashMovementInput input, string? userId, bool supervisor, CancellationToken ct) =>
        _pos.AddCashMovementAsync(registerId, input, userId, supervisor, ct);

    public Task<PosResult<DayClosingDto>> DayReportAsync(Guid registerId, DayClosingInput? close, string? userId, CancellationToken ct) =>
        _pos.DayReportAsync(registerId, close, userId, ct);
}
