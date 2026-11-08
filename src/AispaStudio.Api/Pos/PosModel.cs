using AispaStudio.Api.Pos.Fiscal;
using AispaStudio.Api.Pos.VatIds;
using AispaStudio.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Pos;

internal static class PosModel
{
    /// <summary>Erlöskonten, die es immer gibt; weitere (eigene Geschäftsbereiche) legt die Leitung an.</summary>
    public static readonly RevenueAccount Treatments = new()
    {
        Id = new("0199f000-0000-7000-8000-000000004000"),
        Code = "4000",
        Name = "Erlöse Behandlungen",
    };

    public static readonly RevenueAccount Products = new()
    {
        Id = new("0199f000-0000-7000-8000-000000004100"),
        Code = "4100",
        Name = "Erlöse Produkte",
    };

    public static readonly RevenueAccount MultiPurposeVouchers = new()
    {
        Id = new("0199f000-0000-7000-8000-000000003600"),
        Code = "3600",
        Name = "Mehrzweckgutscheine (nicht steuerbar)",
    };

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<RevenueAccount>(e =>
        {
            e.Property(a => a.Code).HasMaxLength(20);
            e.Property(a => a.Name).HasMaxLength(200);
            e.Property(a => a.CostCenter).HasMaxLength(50);
            e.HasIndex(a => a.Code).IsUnique();
            e.HasData(
                new { Treatments.Id, Treatments.Code, Treatments.Name },
                new { Products.Id, Products.Code, Products.Name },
                new { MultiPurposeVouchers.Id, MultiPurposeVouchers.Code, MultiPurposeVouchers.Name });
        });

        builder.Entity<Article>(e =>
        {
            e.Property(a => a.Name).HasMaxLength(200);
            e.Property(a => a.Price).HasPrecision(10, 2);
            e.Property(a => a.TaxCategory).HasConversion<string>().HasMaxLength(16);
            e.HasOne<RevenueAccount>().WithMany().HasForeignKey(a => a.RevenueAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TreatmentPosSetting>(e =>
        {
            e.HasKey(s => s.TreatmentId);
            e.Property(s => s.TaxCategory).HasConversion<string>().HasMaxLength(16);
            e.HasOne<Treatment>().WithOne().HasForeignKey<TreatmentPosSetting>(s => s.TreatmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RevenueAccount>().WithMany().HasForeignKey(s => s.RevenueAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CashRegister>(e =>
        {
            e.Property(r => r.Code).HasMaxLength(50);
            e.Property(r => r.Country).HasConversion<string>().HasMaxLength(2);
            e.Property(r => r.ReceiptHeader).HasMaxLength(1000);
            e.Property(r => r.PayeeName).HasMaxLength(70);
            e.Property(r => r.Iban).HasMaxLength(34);
            e.Property(r => r.Bic).HasMaxLength(11);
            e.Property(r => r.LastChainValue).HasMaxLength(2000);
            e.Property(r => r.TurnoverKey).HasMaxLength(100);
            e.Property(r => r.CertificateSerial).HasMaxLength(100);
            e.HasIndex(r => r.Code).IsUnique();
            e.HasOne<Location>().WithMany().HasForeignKey(r => r.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Bill>(e =>
        {
            e.Property(b => b.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne<CashRegister>().WithMany().HasForeignKey(b => b.RegisterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Customer>().WithMany().HasForeignKey(b => b.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(b => b.Lines).WithOne().HasForeignKey(l => l.BillId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(b => new { b.RegisterId, b.Status });
        });

        builder.Entity<BillLine>(e =>
        {
            e.Property(l => l.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(l => l.Description).HasMaxLength(300);
            e.Property(l => l.UnitPrice).HasPrecision(10, 2);
            e.Property(l => l.Discount).HasPrecision(10, 2);
            e.Property(l => l.DiscountNote).HasMaxLength(60);
            e.Property(l => l.DiscountApprovedByUserId).HasMaxLength(450);
            e.Property(l => l.Amount).HasPrecision(10, 2);
            e.Property(l => l.PaidAmount).HasPrecision(10, 2);
            e.Property(l => l.TaxCategory).HasConversion<string>().HasMaxLength(16);
            e.Property(l => l.TaxPercent).HasPrecision(5, 2);
            e.Property(l => l.VoucherKind).HasConversion<string>().HasMaxLength(16);
            // Ein Termin steht nur einmal als Behandlung auf einer Rechnung; Anzahlungen darauf sind eigene Positionen.
            e.HasIndex(l => l.AppointmentId).IsUnique().HasFilter("\"AppointmentId\" IS NOT NULL AND \"Kind\" = 'Appointment'");
            e.HasOne<Appointment>().WithMany().HasForeignKey(l => l.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => l.AreaVisitId).IsUnique().HasFilter("\"AreaVisitId\" IS NOT NULL");
            e.HasOne<AreaVisit>().WithMany().HasForeignKey(l => l.AreaVisitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<RevenueAccount>().WithMany().HasForeignKey(l => l.RevenueAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReceiptRecipient>(e =>
        {
            e.Property(r => r.Id).ValueGeneratedNever();
            e.HasOne<Receipt>().WithMany().HasForeignKey(r => r.ReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => r.ReceiptId).IsUnique();
            e.Property(r => r.Name).HasMaxLength(200);
            e.Property(r => r.Address).HasMaxLength(300);
            e.Property(r => r.VatId).HasMaxLength(20);
            e.HasOne<VatIdCheck>().WithMany().HasForeignKey(r => r.VatIdCheckId).OnDelete(DeleteBehavior.Restrict);
            e.Property(r => r.UserId).HasMaxLength(450);
        });

        builder.Entity<Receipt>(e =>
        {
            e.Property(r => r.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Total).HasPrecision(12, 2);
            e.Property(r => r.Change).HasPrecision(10, 2);
            e.Property(r => r.VoidReason).HasMaxLength(500);
            e.Property(r => r.FiscalCode).HasMaxLength(2000);
            e.Property(r => r.FiscalSignature).HasMaxLength(4000);
            e.Property(r => r.CreatedByUserId).HasMaxLength(450);
            e.HasIndex(r => new { r.RegisterId, r.Number }).IsUnique();
            e.HasIndex(r => new { r.RegisterId, r.IssuedAt });
            // Ein Beleg kann nur einmal storniert werden.
            e.HasIndex(r => r.VoidsReceiptId).IsUnique().HasFilter("\"VoidsReceiptId\" IS NOT NULL");
            e.HasIndex(r => r.BillId);
            e.HasOne<CashRegister>().WithMany().HasForeignKey(r => r.RegisterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Receipt>().WithMany().HasForeignKey(r => r.VoidsReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.ReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r => r.Payments).WithOne().HasForeignKey(p => p.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReceiptLine>(e =>
        {
            e.Property(l => l.Description).HasMaxLength(300);
            e.Property(l => l.Amount).HasPrecision(10, 2);
            e.Property(l => l.Discount).HasPrecision(10, 2);
            e.Property(l => l.DiscountNote).HasMaxLength(60);
            e.Property(l => l.TaxCategory).HasConversion<string>().HasMaxLength(16);
            e.Property(l => l.TaxPercent).HasPrecision(5, 2);
            e.HasIndex(l => l.AppointmentId);
            e.HasOne<RevenueAccount>().WithMany().HasForeignKey(l => l.RevenueAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppointmentDeposit>(e =>
        {
            e.Property(d => d.Id).ValueGeneratedNever();
            e.Property(d => d.Amount).HasPrecision(10, 2);
            e.Property(d => d.TaxCategory).HasConversion<string>().HasMaxLength(16);
            e.Property(d => d.TaxPercent).HasPrecision(5, 2);
            e.HasIndex(d => d.AppointmentId);
            e.HasIndex(d => d.ReceiptId);
            e.HasIndex(d => d.SettledOnReceiptId);
            e.HasOne<Appointment>().WithMany().HasForeignKey(d => d.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Receipt>().WithMany().HasForeignKey(d => d.ReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Receipt>().WithMany().HasForeignKey(d => d.SettledOnReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<RevenueAccount>().WithMany().HasForeignKey(d => d.RevenueAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReceiptPayment>(e =>
        {
            e.Property(p => p.Method).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.Amount).HasPrecision(10, 2);
            e.Property(p => p.Reference).HasMaxLength(200);
            e.HasIndex(p => p.VoucherId);
        });

        builder.Entity<Voucher>(e =>
        {
            e.Property(v => v.Code).HasMaxLength(20);
            e.Property(v => v.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(v => v.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(v => v.InitialValue).HasPrecision(10, 2);
            e.Property(v => v.Balance).HasPrecision(10, 2);
            e.Property(v => v.Version).IsRowVersion();
            e.HasIndex(v => v.Code).IsUnique();
            e.HasIndex(v => v.CustomerId);
            e.HasOne<Treatment>().WithMany().HasForeignKey(v => v.TreatmentId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Vouchers_Balance", "\"Balance\" >= 0 AND \"Balance\" <= \"InitialValue\""));
        });

        builder.Entity<CashMovement>(e =>
        {
            e.Property(m => m.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(m => m.Amount).HasPrecision(10, 2);
            e.Property(m => m.Reason).HasMaxLength(500);
            e.Property(m => m.UserId).HasMaxLength(450);
            e.HasIndex(m => new { m.RegisterId, m.At });
            e.HasOne<CashRegister>().WithMany().HasForeignKey(m => m.RegisterId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DayClosing>(e =>
        {
            e.Property(c => c.CountedCash).HasPrecision(10, 2);
            e.Property(c => c.UserId).HasMaxLength(450);
            e.Property(c => c.Report).HasColumnType("jsonb");
            e.HasIndex(c => new { c.RegisterId, c.Until });
            e.HasOne<CashRegister>().WithMany().HasForeignKey(c => c.RegisterId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TestSignatureKey>(e =>
        {
            e.HasKey(k => k.RegisterId);
            e.HasOne<CashRegister>().WithOne().HasForeignKey<TestSignatureKey>(k => k.RegisterId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TestTseClientState>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.ClientId).HasMaxLength(30);
            e.HasIndex(s => s.ClientId).IsUnique();
        });

        builder.Entity<TestTseLog>(e =>
        {
            e.Property(l => l.Id).ValueGeneratedNever();
            e.Property(l => l.ClientId).HasMaxLength(30);
            e.Property(l => l.ProcessType).HasMaxLength(30);
            e.Property(l => l.ProcessData).HasMaxLength(500);
            e.Property(l => l.Signature).HasMaxLength(200);
            e.HasIndex(l => new { l.ClientId, l.End });
        });

        builder.Entity<TseExport>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.RequestedBy).HasMaxLength(450);
            e.Property(x => x.FileName).HasMaxLength(100);
            e.Property(x => x.Error).HasMaxLength(300);
            e.HasIndex(x => new { x.RegisterId, x.RequestedAt });
            e.HasOne<CashRegister>().WithMany().HasForeignKey(x => x.RegisterId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
