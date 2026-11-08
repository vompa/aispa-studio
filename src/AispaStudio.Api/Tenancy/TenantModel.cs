using System.Linq.Expressions;
using AispaStudio.Api.Accounting;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Online;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Pos.Fiscal;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AispaStudio.Api.Tenancy;

/// <summary>
/// Mandantentrennung für das ganze Fachmodell, ohne dass Fachcode sie kennen muss:
/// jede Fachtabelle bekommt eine Spalte TenantId (Schatteneigenschaft), jede Abfrage einen Filter darauf,
/// und neue Einträge erhalten beim Speichern den Mandanten des Aufrufs (siehe <see cref="AispaDbContext"/>).
/// Gilt automatisch auch für künftige Tabellen im Namensraum AispaStudio.Api.
/// </summary>
internal static class TenantModel
{
    public const string Property = "TenantId";

    /// <summary>Tabellen mit gemeinsamen Vorgaben für alle Mandanten (TenantId leer) plus eigenen Einträgen je Mandant.</summary>
    private static readonly HashSet<Type> SharedDefaults = [typeof(RevenueAccount)];

    /// <summary>Eindeutigkeit gilt je Mandant, nicht global.</summary>
    private static readonly (Type Type, string[] Properties)[] UniquePerTenant =
    [
        (typeof(Qualification), [nameof(Qualification.Name)]),
        (typeof(RevenueAccount), [nameof(RevenueAccount.Code)]),
        (typeof(CashRegister), [nameof(CashRegister.Code)]),
        (typeof(TestTseClientState), [nameof(TestTseClientState.ClientId)]),
        (typeof(InboundMail), [nameof(InboundMail.MessageId)]),
        (typeof(SenderRule), [nameof(SenderRule.Pattern)]),
        (typeof(ChannelBooking), [nameof(ChannelBooking.Channel), nameof(ChannelBooking.ExternalId)]),
        (typeof(ChannelMapping), [nameof(ChannelMapping.Channel), nameof(ChannelMapping.Kind), nameof(ChannelMapping.NormalizedName)]),
        (typeof(AccountingSettings), [nameof(AccountingSettings.Country)]),
        (typeof(PurchaseOrder), [nameof(PurchaseOrder.Number)]),
        (typeof(StockSupplier), [nameof(StockSupplier.Name)]),
        (typeof(StockItem), [nameof(StockItem.Barcode)]),
        (typeof(CalendarSubscription), [nameof(CalendarSubscription.Preset)]),
        (typeof(Staff.DutyType), [nameof(Staff.DutyType.Key)]),
    ];

    public static bool IsTenantOwned(IReadOnlyEntityType type) =>
        !type.HasSharedClrType
        && !type.IsOwned()
        && type.ClrType != typeof(AispaUser)
        && type.ClrType != typeof(Tenant)
        // Eine neue Anzeige kennt ihr Spa erst nach dem Koppeln (Slice 104).
        && type.ClrType != typeof(Access.DisplayPairing)
        && type.ClrType.Namespace?.StartsWith("AispaStudio.Api", StringComparison.Ordinal) == true;

    /// <summary>Mandant eines Eintrags, ohne Filter gesucht (für Abläufe, die den Mandanten erst daraus erfahren).</summary>
    public static Task<Guid?> TenantOfAsync<T>(IQueryable<T> query, CancellationToken ct)
        where T : class =>
        query.IgnoreQueryFilters().Select(e => (Guid?)EF.Property<Guid>(e, Property)).FirstOrDefaultAsync(ct);

    /// <param name="filter">Baut den Filter je Tabelle; muss auf den DbContext selbst verweisen, damit EF ihn je Instanz auswertet.</param>
    public static void Configure(ModelBuilder builder, Func<Type, bool, LambdaExpression> filter)
    {
        builder.Entity<Tenant>(e =>
        {
            e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.Name).HasMaxLength(200);
            e.Property(t => t.Country).HasMaxLength(2).IsFixedLength();
            e.Property(t => t.LockReason).HasMaxLength(500);
            e.Property(t => t.Plan).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.LegalName).HasMaxLength(200);
            e.Property(t => t.Street).HasMaxLength(200);
            e.Property(t => t.PostalCode).HasMaxLength(10);
            e.Property(t => t.City).HasMaxLength(100);
            e.Property(t => t.Phone).HasMaxLength(40);
            e.Property(t => t.Email).HasMaxLength(254);
            e.Property(t => t.Website).HasMaxLength(200);
            e.Property(t => t.VatId).HasMaxLength(20);
            e.Property(t => t.CompanyRegisterNumber).HasMaxLength(30);
            e.Property(t => t.CompanyRegisterCourt).HasMaxLength(100);
            e.Property(t => t.AccountHolder).HasMaxLength(70);
            e.Property(t => t.Iban).HasMaxLength(34);
            e.Property(t => t.Bic).HasMaxLength(11);
            e.Property(t => t.BusinessPurpose).HasMaxLength(300);
            e.Property(t => t.SupervisoryAuthority).HasMaxLength(200);
            e.Property(t => t.ChamberAndRules).HasMaxLength(300);
            e.Property(t => t.DiscountLimitPercent).HasPrecision(5, 2);
            e.Property(t => t.CashDiscountPercent).HasPrecision(5, 2);
            e.Property(t => t.EmbedAllowedOrigins).HasDefaultValueSql("'{}'");
        });

        foreach (var type in builder.Model.GetEntityTypes().Where(IsTenantOwned).ToList())
        {
            var shared = SharedDefaults.Contains(type.ClrType);
            var entity = builder.Entity(type.ClrType);
            entity.Property(shared ? typeof(Guid?) : typeof(Guid), Property);
            entity.HasIndex(Property);
            entity.HasOne(typeof(Tenant)).WithMany().HasForeignKey(Property).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(filter(type.ClrType, shared));
        }

        foreach (var (type, properties) in UniquePerTenant)
        {
            var entityType = builder.Entity(type).Metadata;
            if (entityType.FindIndex([.. properties.Select(p => entityType.FindProperty(p)!)]) is { } index)
            {
                entityType.RemoveIndex(index);
            }

            builder.Entity(type).HasIndex([Property, .. properties]).IsUnique();
        }

        // Höchstens eine Standardzeile (Land leer) je Mandant.
        builder.Entity<AccountingSettings>().HasIndex([Property, nameof(AccountingSettings.Country)]).AreNullsDistinct(false);

        // Ein Zahlungskonto je Mandant (ADR 0054).
        builder.Entity<PaymentAccount>().HasIndex(Property).IsUnique();
    }

}
