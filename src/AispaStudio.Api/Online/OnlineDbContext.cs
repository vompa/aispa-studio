using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AispaStudio.Api.Online;

/// <summary>
/// Daten des Kundenbereichs, getrennt vom Fachmodell (<c>AispaDbContext</c>): Kundenkonten mit Passkeys und externen Anmeldungen,
/// Anmeldecodes, Links aus Mails, Herkunft von Online-Buchungen, Warteliste, Gutscheinbestellungen, hinterlegte Karten, Anzahlungen (Schema „online“) sowie Anamnesebögen und fotografierte
/// Papierbögen (Schema „health“).
/// Eigene Migrationen, damit Änderungen am Kundenbereich das gemeinsame Modell nicht berühren.
/// </summary>
public sealed class OnlineDbContext(DbContextOptions<OnlineDbContext> options)
    : IdentityUserContext<CustomerAccount, Guid>(options)
{
    public const string Schema = "online";
    public const string HealthSchema = "health";

    public DbSet<SignInCode> SignInCodes => Set<SignInCode>();
    public DbSet<AccessLink> AccessLinks => Set<AccessLink>();
    public DbSet<OnlineBooking> OnlineBookings => Set<OnlineBooking>();
    public DbSet<AnamnesisRecord> AnamnesisRecords => Set<AnamnesisRecord>();
    public DbSet<AnamnesisScan> AnamnesisScans => Set<AnamnesisScan>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<VoucherOrder> VoucherOrders => Set<VoucherOrder>();
    public DbSet<CardGuarantee> CardGuarantees => Set<CardGuarantee>();
    public DbSet<OnlineRefund> Refunds => Set<OnlineRefund>();
    public DbSet<DepositOrder> Deposits => Set<DepositOrder>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<CustomerAccount>(e =>
        {
            e.ToTable("CustomerAccounts");
            e.HasIndex(a => a.CustomerId);
        });
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("CustomerAccountClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("CustomerAccountLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("CustomerAccountTokens");
        builder.Entity<IdentityUserPasskey<Guid>>().ToTable("CustomerAccountPasskeys");

        builder.Entity<SignInCode>(e =>
        {
            e.Property(c => c.NormalizedEmail).HasMaxLength(256);
            e.Property(c => c.CodeHash).HasMaxLength(32);
            e.HasIndex(c => new { c.NormalizedEmail, c.CreatedAt });
        });

        builder.Entity<AccessLink>(e =>
        {
            e.HasKey(l => l.TokenHash);
            e.Property(l => l.TokenHash).HasMaxLength(32);
            e.HasIndex(l => l.AppointmentId);
            e.HasIndex(l => l.ExpiresAt);
        });

        builder.Entity<OnlineBooking>(e =>
        {
            e.HasKey(b => b.AppointmentId);
            e.Property(b => b.PrivacyNoticeVersion).HasMaxLength(32);
            e.Property(b => b.Language).HasMaxLength(8);
            e.HasIndex(b => b.CustomerId);
        });

        builder.Entity<AnamnesisRecord>(e =>
        {
            e.ToTable("AnamnesisRecords", HealthSchema);
            e.Property(r => r.FormVersion).HasMaxLength(32);
            e.Property(r => r.Channel).HasMaxLength(16);
            e.HasIndex(r => new { r.CustomerId, r.SubmittedAt });
        });

        builder.Entity<AnamnesisScan>(e =>
        {
            e.ToTable("AnamnesisScans", HealthSchema);
            e.Property(s => s.UploadedBy).HasMaxLength(64);
            e.Property(s => s.ConfirmedBy).HasMaxLength(64);
            e.Property(s => s.ContentType).HasMaxLength(32);
            e.Property(s => s.Language).HasMaxLength(8);
            e.Property(s => s.Reader).HasMaxLength(32);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(s => new { s.CustomerId, s.Status });
        });

        builder.Entity<WaitlistEntry>(e =>
        {
            e.Property(w => w.Language).HasMaxLength(8);
            e.Property(w => w.PrivacyNoticeVersion).HasMaxLength(32);
            e.Property(w => w.DayPart).HasConversion<string>().HasMaxLength(16);
            e.Property(w => w.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(w => new { w.LocationId, w.Status, w.From });
            e.HasIndex(w => new { w.CustomerId, w.Status });
        });

        builder.Entity<VoucherOrder>(e =>
        {
            e.Property(o => o.TokenHash).HasMaxLength(32);
            e.HasIndex(o => o.TokenHash).IsUnique();
            e.Property(o => o.Amount).HasPrecision(10, 2);
            e.Property(o => o.Currency).HasMaxLength(3);
            e.Property(o => o.BuyerEmail).HasMaxLength(254);
            e.Property(o => o.RecipientName).HasMaxLength(100);
            e.Property(o => o.Message).HasMaxLength(300);
            e.Property(o => o.Language).HasMaxLength(8);
            e.Property(o => o.PrivacyNoticeVersion).HasMaxLength(32);
            e.Property(o => o.Provider).HasMaxLength(32);
            e.Property(o => o.ProviderReference).HasMaxLength(128);
            e.Property(o => o.PaymentUrl).HasMaxLength(2048);
            e.Property(o => o.Error).HasMaxLength(500);
            e.Property(o => o.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(o => new { o.LocationId, o.Status });
            e.HasIndex(o => o.ReceiptId);
        });

        builder.Entity<CardGuarantee>(e =>
        {
            e.HasIndex(g => g.AppointmentId).IsUnique();
            e.HasIndex(g => g.ReceiptId);
            e.Property(g => g.Email).HasMaxLength(254);
            e.Property(g => g.Language).HasMaxLength(8);
            e.Property(g => g.FeeAmount).HasPrecision(10, 2);
            e.Property(g => g.Currency).HasMaxLength(3);
            e.Property(g => g.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(g => g.Provider).HasMaxLength(32);
            e.Property(g => g.SessionReference).HasMaxLength(128);
            e.Property(g => g.SetupUrl).HasMaxLength(2048);
            e.Property(g => g.CardReference).HasMaxLength(128);
            e.Property(g => g.ProviderCustomer).HasMaxLength(128);
            e.Property(g => g.ChargedBy).HasMaxLength(128);
            e.Property(g => g.ChargeReference).HasMaxLength(128);
            e.Property(g => g.Error).HasMaxLength(500);
        });

        builder.Entity<DepositOrder>(e =>
        {
            e.HasIndex(d => d.AppointmentId).IsUnique();
            e.HasIndex(d => d.ReceiptId);
            e.HasIndex(d => new { d.Status, d.ReleaseAt });
            e.Property(d => d.Email).HasMaxLength(254);
            e.Property(d => d.Language).HasMaxLength(8);
            e.Property(d => d.Amount).HasPrecision(10, 2);
            e.Property(d => d.Currency).HasMaxLength(3);
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(d => d.Provider).HasMaxLength(32);
            e.Property(d => d.ProviderReference).HasMaxLength(128);
            e.Property(d => d.PaymentUrl).HasMaxLength(2048);
            e.Property(d => d.Error).HasMaxLength(500);
        });

        builder.Entity<OnlineRefund>(e =>
        {
            e.HasIndex(r => r.ReceiptId).IsUnique();
            e.Property(r => r.Source).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Provider).HasMaxLength(32);
            e.Property(r => r.PaymentReference).HasMaxLength(128);
            e.Property(r => r.Amount).HasPrecision(10, 2);
            e.Property(r => r.Currency).HasMaxLength(3);
            e.Property(r => r.Email).HasMaxLength(254);
            e.Property(r => r.Language).HasMaxLength(8);
            e.Property(r => r.Reason).HasMaxLength(500);
            e.Property(r => r.RequestedBy).HasMaxLength(128);
            e.Property(r => r.RefundReference).HasMaxLength(128);
            e.Property(r => r.Error).HasMaxLength(500);
        });
    }
}

/// <summary>Für "dotnet ef migrations add … --context OnlineDbContext": baut den Kontext ohne laufende App und ohne Datenbank.</summary>
internal sealed class OnlineDbContextDesignTimeFactory : IDesignTimeDbContextFactory<OnlineDbContext>
{
    public OnlineDbContext CreateDbContext(string[] args)
    {
        // Schema-Version 3 = mit Passkeys, wie zur Laufzeit in AuthSetup.
        var services = new ServiceCollection()
            .Configure<IdentityOptions>(o => o.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .BuildServiceProvider();

        return new(new DbContextOptionsBuilder<OnlineDbContext>()
            .UseNpgsql("Host=localhost;Database=aispadb", OnlineDbContextSetup.Configure)
            .UseApplicationServiceProvider(services)
            .Options);
    }
}

internal static class OnlineDbContextSetup
{
    public static void Configure(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", OnlineDbContext.Schema);
}
