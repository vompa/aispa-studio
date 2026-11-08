using AispaStudio.Api.Online;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Reporting;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Stock;

namespace AispaStudio.Api.Demo.Memory;

/// <summary>Alles, was der Verlauf im Speicher neu anlegt, samt den Nachschlagelisten, die Buchen und Kassieren brauchen.</summary>
internal sealed class MemoryWorld(DbScale scale)
{
    public List<Appointment> Appointments { get; } = [];

    public Dictionary<Guid, Appointment> AppointmentsById { get; } = [];

    /// <summary>Belegungen je Ressource (Person, Raum, Gerät): vorhandene aus der Datenbank und neue.</summary>
    public Dictionary<Guid, List<ResourceBooking>> BookingsByResource { get; } = [];

    public List<ResourceBooking> NewBookings { get; } = [];

    public List<OnlineBooking> OnlineBookings { get; } = [];

    public List<AppointmentFollowUp> FollowUps { get; } = [];

    public List<Bill> Bills { get; } = [];

    public Dictionary<Guid, Bill> BillsById { get; } = [];

    /// <summary>Termine, die schon auf einer Rechnung stehen (ein Termin steht auf höchstens einer).</summary>
    public HashSet<Guid> BilledAppointments { get; } = [];

    public List<Receipt> Receipts { get; } = [];

    public Dictionary<Guid, Receipt> ReceiptsById { get; } = [];

    public Dictionary<Guid, List<Receipt>> ReceiptsByRegister { get; } = [];

    public HashSet<Guid> VoidedReceipts { get; } = [];

    public List<Voucher> Vouchers { get; } = [];

    public Dictionary<string, Voucher> VouchersByCode { get; } = [];

    public Dictionary<Guid, Voucher> VouchersById { get; } = [];

    public List<CashMovement> CashMovements { get; } = [];

    public List<DayClosing> DayClosings { get; } = [];

    public List<StockMovement> StockMovements { get; } = [];

    /// <summary>Beträge einer neuen Zeile auf die Stellen der Spalte bringen (siehe <see cref="DbScale"/>).</summary>
    public void Normalize(object entity) => scale.Normalize(entity);

    public void AddBooking(ResourceBooking booking)
    {
        NewBookings.Add(booking);
        BookingsOf(booking.ResourceId).Add(booking);
    }

    public void RemoveBookings(IEnumerable<ResourceBooking> bookings)
    {
        foreach (var booking in bookings)
        {
            BookingsOf(booking.ResourceId).Remove(booking);
            NewBookings.Remove(booking);
        }
    }

    public List<ResourceBooking> BookingsOf(Guid resourceId)
    {
        if (!BookingsByResource.TryGetValue(resourceId, out var list))
        {
            BookingsByResource[resourceId] = list = [];
        }

        return list;
    }

    public void AddReceipt(Receipt receipt)
    {
        scale.Normalize(receipt);
        receipt.Lines.ForEach(scale.Normalize);
        receipt.Payments.ForEach(scale.Normalize);
        Receipts.Add(receipt);
        ReceiptsById[receipt.Id] = receipt;
        if (!ReceiptsByRegister.TryGetValue(receipt.RegisterId, out var list))
        {
            ReceiptsByRegister[receipt.RegisterId] = list = [];
        }

        list.Add(receipt);
        if (receipt.VoidsReceiptId is { } voided)
        {
            VoidedReceipts.Add(voided);
        }
    }

    public void AddVoucher(Voucher voucher)
    {
        scale.Normalize(voucher);
        Vouchers.Add(voucher);
        VouchersByCode[voucher.Code] = voucher;
        VouchersById[voucher.Id] = voucher;
    }
}
