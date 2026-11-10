using System.Linq.Expressions;
using System.Reflection;
using AispaStudio.Api.Access;
using AispaStudio.Api.Accounting;
using AispaStudio.Api.BusinessInfo;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Costs;
using AispaStudio.Api.Exports;
using AispaStudio.Api.Importing;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Online;
using AispaStudio.Api.Operations;
using AispaStudio.Api.Payroll;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Pos.Terminals;
using AispaStudio.Api.Pos.VatIds;
using AispaStudio.Api.Promotions;
using AispaStudio.Api.Reminders;
using AispaStudio.Api.Reporting;
using AispaStudio.Api.Retention;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Stock;
using AispaStudio.Api.Tenancy;
using AispaStudio.Api.TimeTracking;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AispaStudio.Api.Data;

public sealed class AispaDbContext(DbContextOptions<AispaDbContext> options, TenantContext tenant)
    : IdentityDbContext<AispaUser>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<Qualification> Qualifications => Set<Qualification>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Treatment> Treatments => Set<Treatment>();
    public DbSet<TreatmentLocationPrice> TreatmentLocationPrices => Set<TreatmentLocationPrice>();
    public DbSet<TreatmentPriceRule> TreatmentPriceRules => Set<TreatmentPriceRule>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerFlag> CustomerFlags => Set<CustomerFlag>();
    public DbSet<CustomerEmail> CustomerEmails => Set<CustomerEmail>();
    public DbSet<CustomerNameChangeEntry> CustomerNameChanges => Set<CustomerNameChangeEntry>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentSeries> AppointmentSeries => Set<AppointmentSeries>();
    public DbSet<ResourceBooking> ResourceBookings => Set<ResourceBooking>();
    public DbSet<AreaVisit> AreaVisits => Set<AreaVisit>();
    public DbSet<CalendarNote> CalendarNotes => Set<CalendarNote>();
    public DbSet<StaffAccess> StaffAccess => Set<StaffAccess>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<RosterDay> RosterDays => Set<RosterDay>();
    public DbSet<AbsenceRequest> AbsenceRequests => Set<AbsenceRequest>();
    public DbSet<RosterDraft> RosterDrafts => Set<RosterDraft>();
    public DbSet<RosterPublication> RosterPublications => Set<RosterPublication>();
    public DbSet<TimeRecord> TimeRecords => Set<TimeRecord>();
    public DbSet<TimeRecordChange> TimeRecordChanges => Set<TimeRecordChange>();
    public DbSet<InboundMail> InboundMails => Set<InboundMail>();
    public DbSet<ChannelBooking> ChannelBookings => Set<ChannelBooking>();
    public DbSet<SenderRule> SenderRules => Set<SenderRule>();
    public DbSet<ChannelMapping> ChannelMappings => Set<ChannelMapping>();
    public DbSet<CalendarFeed> CalendarFeeds => Set<CalendarFeed>();

    public DbSet<RevenueAccount> RevenueAccounts => Set<RevenueAccount>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<TreatmentPosSetting> TreatmentPosSettings => Set<TreatmentPosSetting>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillLine> BillLines => Set<BillLine>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptRecipient> ReceiptRecipients => Set<ReceiptRecipient>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<AppointmentDeposit> AppointmentDeposits => Set<AppointmentDeposit>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<DayClosing> DayClosings => Set<DayClosing>();
    public DbSet<PaymentTerminal> PaymentTerminals => Set<PaymentTerminal>();
    public DbSet<CardPayment> CardPayments => Set<CardPayment>();
    public DbSet<VatIdCheck> VatIdChecks => Set<VatIdCheck>();
    public DbSet<AccountingSettings> AccountingSettings => Set<AccountingSettings>();
    public DbSet<LocationCostSettings> LocationCostSettings => Set<LocationCostSettings>();
    public DbSet<RoomCost> RoomCosts => Set<RoomCost>();
    public DbSet<EmployeeCost> EmployeeCosts => Set<EmployeeCost>();
    public DbSet<TreatmentMaterial> TreatmentMaterials => Set<TreatmentMaterial>();

    public DbSet<StockItem> StockItems => Set<StockItem>();

    public DbSet<TreatmentRecommendedArticle> TreatmentRecommendedArticles => Set<TreatmentRecommendedArticle>();

    public DbSet<StockSupplier> StockSuppliers => Set<StockSupplier>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StoragePlace> StoragePlaces => Set<StoragePlace>();
    public DbSet<StoragePlaceMinimum> StoragePlaceMinimums => Set<StoragePlaceMinimum>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<StockCountSession> StockCountSessions => Set<StockCountSession>();
    public DbSet<StockCountLine> StockCountLines => Set<StockCountLine>();
    public DbSet<TreatmentPreparationStep> TreatmentPreparationSteps => Set<TreatmentPreparationStep>();
    public DbSet<CustomerPreference> CustomerPreferences => Set<CustomerPreference>();
    public DbSet<AppointmentPreparationCheck> AppointmentPreparationChecks => Set<AppointmentPreparationCheck>();
    public DbSet<AppointmentFollowUp> AppointmentFollowUps => Set<AppointmentFollowUp>();

    public DbSet<TreatmentExtra> TreatmentExtras => Set<TreatmentExtra>();
    public DbSet<AppointmentExtraCheck> AppointmentExtraChecks => Set<AppointmentExtraCheck>();
    public DbSet<HandbookEntry> HandbookEntries => Set<HandbookEntry>();
    public DbSet<RoomCareCheck> RoomCareChecks => Set<RoomCareCheck>();
    public DbSet<RoomCareRestockAnswer> RoomCareRestockAnswers => Set<RoomCareRestockAnswer>();
    public DbSet<RoomStartItem> RoomStartItems => Set<RoomStartItem>();
    public DbSet<RoomStartCheck> RoomStartChecks => Set<RoomStartCheck>();
    public DbSet<RoomApproval> RoomApprovals => Set<RoomApproval>();

    public DbSet<BriefingSummary> BriefingSummaries => Set<BriefingSummary>();
    public DbSet<PayrollSettings> PayrollSettings => Set<PayrollSettings>();
    public DbSet<EmployeePayroll> EmployeePayrolls => Set<EmployeePayroll>();
    public DbSet<LocationOnlineSettings> LocationOnlineSettings => Set<LocationOnlineSettings>();
    public DbSet<ReminderSettings> ReminderSettings => Set<ReminderSettings>();
    public DbSet<SpaMailbox> SpaMailboxes => Set<SpaMailbox>();
    public DbSet<IntakeRoute> IntakeRoutes => Set<IntakeRoute>();
    public DbSet<AppointmentReminder> AppointmentReminders => Set<AppointmentReminder>();
    public DbSet<RebookSettings> RebookSettings => Set<RebookSettings>();
    public DbSet<TreatmentRebookInterval> TreatmentRebookIntervals => Set<TreatmentRebookInterval>();
    public DbSet<MarketingConsent> MarketingConsents => Set<MarketingConsent>();
    public DbSet<RebookReminder> RebookReminders => Set<RebookReminder>();
    public DbSet<PaymentAccount> PaymentAccounts => Set<PaymentAccount>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionLocation> PromotionLocations => Set<PromotionLocation>();
    public DbSet<PromotionTreatment> PromotionTreatments => Set<PromotionTreatment>();
    public DbSet<CalendarSubscription> CalendarSubscriptions => Set<CalendarSubscription>();
    public DbSet<CalendarSubscriptionEntry> CalendarSubscriptionEntries => Set<CalendarSubscriptionEntry>();
    public DbSet<CalendarSubscriptionHidden> CalendarSubscriptionHidden => Set<CalendarSubscriptionHidden>();
    public DbSet<CalendarSubscriptionDecision> CalendarSubscriptionDecisions => Set<CalendarSubscriptionDecision>();
    public DbSet<CalendarSubscriptionNotice> CalendarSubscriptionNotices => Set<CalendarSubscriptionNotice>();
    public DbSet<BusinessContact> BusinessContacts => Set<BusinessContact>();
    public DbSet<BusinessContactLocation> BusinessContactLocations => Set<BusinessContactLocation>();
    public DbSet<Closure> Closures => Set<Closure>();
    public DbSet<ClosureNotice> ClosureNotices => Set<ClosureNotice>();
    public DbSet<RoomClosure> RoomClosures => Set<RoomClosure>();
    public DbSet<Documents.SentDocument> SentDocuments => Set<Documents.SentDocument>();
    public DbSet<ExportEntry> Exports => Set<ExportEntry>();
    public DbSet<ImportEntry> Imports => Set<ImportEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("aispa");
        SchedulingModel.Configure(builder);
        AccessModel.Configure(builder);
        PosModel.Configure(builder);
        TerminalModel.Configure(builder);
        VatIdModel.Configure(builder);
        RosterModel.Configure(builder);
        TimeTrackingModel.Configure(builder);
        MailIntakeModel.Configure(builder);
        SenderRuleModel.Configure(builder);
        AccountingModel.Configure(builder);
        CostModel.Configure(builder);
        StockModel.Configure(builder);
        StockCountSessionModel.Configure(builder);
        ReportModel.Configure(builder);
        OperationsModel.Configure(builder);
        PayrollModel.Configure(builder);
        OnlineRulesModel.Configure(builder);
        ReminderModel.Configure(builder);
        IntakeModel.Configure(builder);
        RetentionModel.Configure(builder);
        PaymentAccountModel.Configure(builder);
        PromotionModel.Configure(builder);
        CalendarSubscriptionModel.Configure(builder);
        BusinessContactModel.Configure(builder);
        ClosureModel.Configure(builder);
        RoomClosureModel.Configure(builder);
        ExportModel.Configure(builder);
        ImportModel.Configure(builder);
        HrFile.HrFileModel.Configure(builder);
        Staff.StaffRegulationModel.Configure(builder);
        Staff.StaffDutyModel.Configure(builder);
        Documents.SentDocumentModel.Configure(builder);

        // Zuletzt, damit alle Fachtabellen bekannt sind.
        TenantModel.Configure(builder, (type, shared) =>
            (LambdaExpression)FilterMethod.MakeGenericMethod(type).Invoke(this, [shared])!);
    }

    /// <summary>Mandant für die Abfragefilter; Guid.Empty, wenn keiner bekannt ist (dann sieht man nichts).</summary>
    internal Guid CurrentTenantId => tenant.TenantId ?? Guid.Empty;

    internal bool AllTenants => tenant.AllTenants;

    /// <summary>Mandant eines geladenen Eintrags (z. B. einer Einladung, die ohne Anmeldung geöffnet wird).</summary>
    public Guid TenantOf(object entity) => (Guid)Entry(entity).Property(TenantModel.Property).CurrentValue!;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AddMainStoragePlaces();
        StampTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AddMainStoragePlaces();
        StampTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Jeder neue Standort bekommt sein Hauptlager (ADR 0068), egal auf welchem Weg er angelegt wird (Verwaltung, Einrichtung, Beispieldaten).</summary>
    private void AddMainStoragePlaces()
    {
        foreach (var entry in ChangeTracker.Entries<Location>().Where(e => e.State == EntityState.Added).ToList()
            .Where(e => !StoragePlaces.Local.Any(p => p.LocationId == e.Entity.Id)))
        {
            StoragePlaces.Add(new StoragePlace { LocationId = entry.Entity.Id, Name = StoragePlace.MainName, Kind = StoragePlaceKind.Main });
        }
    }

    /// <summary>Neue Einträge gehören zum Mandanten des Aufrufs; ohne bekannten Mandanten wird nicht gespeichert.</summary>
    private void StampTenant()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added && TenantModel.IsTenantOwned(e.Metadata)))
        {
            var property = entry.Property(TenantModel.Property);
            if (property.CurrentValue is Guid id && id != Guid.Empty)
            {
                continue;
            }

            property.CurrentValue = tenant.TenantId
                ?? throw new InvalidOperationException($"Mandant unbekannt beim Speichern von {entry.Metadata.ClrType.Name}.");
        }
    }

    private static readonly MethodInfo FilterMethod =
        typeof(AispaDbContext).GetMethod(nameof(TenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private Expression<Func<T, bool>> TenantFilter<T>(bool shared) =>
        shared
            ? e => AllTenants || EF.Property<Guid?>(e!, TenantModel.Property) == null || EF.Property<Guid?>(e!, TenantModel.Property) == CurrentTenantId
            : e => AllTenants || EF.Property<Guid>(e!, TenantModel.Property) == CurrentTenantId;
}

/// <summary>Für "dotnet ef migrations add": baut den Kontext ohne laufende App und ohne Datenbank.</summary>
internal sealed class AispaDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AispaDbContext>
{
    public AispaDbContext CreateDbContext(string[] args)
    {
        // Identity liest die Schema-Version (3 = mit Passkeys) aus den IdentityOptions, wie zur Laufzeit in AuthSetup.
        var services = new ServiceCollection()
            .Configure<IdentityOptions>(o => o.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .BuildServiceProvider();

        return new(new DbContextOptionsBuilder<AispaDbContext>()
            .UseNpgsql("Host=localhost;Database=aispadb")
            .UseApplicationServiceProvider(services)
            .Options, new TenantContext(new HttpContextAccessor()));
    }
}
