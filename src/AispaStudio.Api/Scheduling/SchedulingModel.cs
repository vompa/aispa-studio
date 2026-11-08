using AispaStudio.Api.Pos;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

internal static class SchedulingModel
{
    /// <summary>Höchstlänge der Anreise am Standort (Freitext).</summary>
    public const int LocationDirectionsLength = 500;

    public static void Configure(ModelBuilder builder)
    {
        // Für die Ausschlussbedingung gegen Doppelbuchungen (Gleichheit auf uuid und Überschneidung auf Zeiträumen in einem GiST-Index).
        builder.HasPostgresExtension("btree_gist");

        // Ids vergibt die Anwendung (Guid v7). Ohne diese Angabe hielte EF neue Kind-Einträge, die über eine Navigation
        // dazukommen (z. B. neue Öffnungszeiten), für bestehende und würde sie aktualisieren statt anlegen.
        foreach (var type in new[]
        {
            typeof(Location), typeof(LocationOpeningHours), typeof(Room), typeof(Equipment), typeof(Qualification),
            typeof(Employee), typeof(EmployeeWorkingHours), typeof(Treatment), typeof(Customer), typeof(Appointment),
            typeof(ResourceBooking), typeof(AreaVisit), typeof(CalendarNote), typeof(CustomerFlag), typeof(CustomerEmail),
        })
        {
            builder.Entity(type).Property(nameof(Location.Id)).ValueGeneratedNever();
        }

        builder.Entity<Location>(e =>
        {
            e.Property(l => l.Name).HasMaxLength(200);
            e.Property(l => l.TimeZone).HasMaxLength(64);
            e.Property(l => l.Currency).HasMaxLength(3).IsFixedLength();
            e.Property(l => l.Region).HasMaxLength(6);
            e.Property(l => l.Street).HasMaxLength(200);
            e.Property(l => l.LegalName).HasMaxLength(200);
            e.Property(l => l.VatId).HasMaxLength(20);
            e.Property(l => l.CompanyRegisterNumber).HasMaxLength(30);
            e.Property(l => l.CompanyRegisterCourt).HasMaxLength(100);
            e.Property(l => l.PostalCode).HasMaxLength(10);
            e.Property(l => l.City).HasMaxLength(100);
            e.Property(l => l.Phone).HasMaxLength(40);
            e.Property(l => l.Email).HasMaxLength(254);
            e.Property(l => l.Directions).HasMaxLength(LocationDirectionsLength);
            e.HasMany(l => l.OpeningHours).WithOne().HasForeignKey(o => o.LocationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Room>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(200);
            e.HasOne<Location>().WithMany().HasForeignKey(r => r.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Article>().WithMany().HasForeignKey(r => r.AdmissionArticleId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Equipment>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(200);
            e.HasOne<Location>().WithMany().HasForeignKey(r => r.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Qualification>(e =>
        {
            e.Property(q => q.Name).HasMaxLength(200);
            e.HasIndex(q => q.Name).IsUnique();
        });

        builder.Entity<Employee>(e =>
        {
            e.Property(p => p.DisplayName).HasMaxLength(200);
            e.Property(p => p.PreferredName).HasMaxLength(EmployeeNames.MaxPreferredNameLength);
            e.Ignore(p => p.InHouseName);
            e.Property(p => p.Color).HasMaxLength(16);
            e.Property(p => p.Gender).HasConversion<string>().HasMaxLength(8);
            e.Property(p => p.UserId).HasMaxLength(450);
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasMany(p => p.Qualifications).WithMany().UsingEntity("EmployeeQualifications");
            e.HasMany(p => p.WorkingHours).WithOne().HasForeignKey(w => w.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeWorkingHours>(e =>
        {
            e.HasOne<Location>().WithMany().HasForeignKey(w => w.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(w => new { w.LocationId, w.Day });
        });

        builder.Entity<Treatment>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(200);
            e.Property(t => t.Price).HasPrecision(10, 2);
            e.Property(t => t.Category).HasMaxLength(60);
            e.Property(t => t.GuestHint).HasMaxLength(300);
            e.HasMany(t => t.LocationPrices).WithOne().HasForeignKey(p => p.TreatmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(t => t.RequiredQualifications).WithMany().UsingEntity("TreatmentQualifications");
            e.HasMany(t => t.Rooms).WithMany().UsingEntity("TreatmentRooms");
            e.HasMany(t => t.Equipment).WithMany().UsingEntity("TreatmentEquipment");
        });

        builder.Entity<TreatmentPriceRule>(e =>
        {
            e.Property(r => r.Id).ValueGeneratedNever();
            e.HasOne<Treatment>().WithMany(t => t.PriceRules).HasForeignKey(r => r.TreatmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Location>().WithMany().HasForeignKey(r => r.LocationId).OnDelete(DeleteBehavior.Cascade);
            e.Property(r => r.Name).HasMaxLength(100);
            e.Property(r => r.Price).HasPrecision(10, 2);
        });

        builder.Entity<TreatmentLocationPrice>(e =>
        {
            e.HasKey(p => new { p.TreatmentId, p.LocationId });
            e.Property(p => p.Price).HasPrecision(10, 2);
            e.HasOne<Location>().WithMany().HasForeignKey(p => p.LocationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CustomerFlag>(e =>
        {
            e.Property(f => f.Kind).HasConversion<string>().HasMaxLength(24);
            e.Property(f => f.Reason).HasMaxLength(500);
            e.Property(f => f.CreatedByUserId).HasMaxLength(450);
            e.Property(f => f.RemovedByUserId).HasMaxLength(450);
            e.HasIndex(f => f.CustomerId);
            e.HasOne<Customer>().WithMany().HasForeignKey(f => f.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CustomerNameChangeEntry>(e =>
        {
            e.Property(n => n.OldName).HasMaxLength(201);
            e.Property(n => n.NewName).HasMaxLength(201);
            e.Property(n => n.ChangedByUserId).HasMaxLength(450);
            e.HasIndex(n => n.CustomerId);
            e.HasOne<Customer>().WithMany().HasForeignKey(n => n.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CustomerEmail>(e =>
        {
            e.Property(m => m.Address).HasMaxLength(256);
            e.Property(m => m.NormalizedAddress).HasMaxLength(256);
            e.HasIndex(m => new { m.CustomerId, m.NormalizedAddress }).IsUnique();
            e.HasIndex(m => m.NormalizedAddress);
            e.HasOne<Customer>().WithMany().HasForeignKey(m => m.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Customer>(e =>
        {
            e.Property(c => c.CreatedFromChannel).HasConversion<string>().HasMaxLength(16);
            e.Property(c => c.FirstName).HasMaxLength(100);
            e.Property(c => c.LastName).HasMaxLength(100);
            e.Property(c => c.Email).HasMaxLength(256);
            e.Property(c => c.Phone).HasMaxLength(50);
            e.Property(c => c.Note).HasMaxLength(2000);
            e.Property(c => c.ReceptionNote).HasMaxLength(1000);
            e.Property(c => c.Language).HasMaxLength(8);
            e.Property(c => c.BillingName).HasMaxLength(200);
            e.Property(c => c.BillingAddress).HasMaxLength(300);
            e.Property(c => c.BillingVatId).HasMaxLength(20);
            e.Property(c => c.DiscountPercent).HasPrecision(5, 2);
            e.Property(c => c.DiscountScope).HasConversion<string>().HasMaxLength(16);
            e.Property(c => c.PreferredGender).HasConversion<string>().HasMaxLength(8);
            e.Property(c => c.DiscountApprovedByUserId).HasMaxLength(450);
            e.Property(c => c.PreferredName).HasMaxLength(100);
            e.Property(c => c.Pronouns).HasMaxLength(40);
            e.Property(c => c.Salutation).HasConversion<string>().HasMaxLength(8);
            e.Property(c => c.TitlesBefore).HasMaxLength(100);
            e.Property(c => c.TitlesAfter).HasMaxLength(100);
            e.Property(c => c.Street).HasMaxLength(200);
            e.Property(c => c.PostalCode).HasMaxLength(16);
            e.Property(c => c.City).HasMaxLength(100);
            e.Property(c => c.Country).HasMaxLength(2);
            e.Ignore(c => c.InHouseName);
            e.Ignore(c => c.LegalName);
            e.HasIndex(c => new { c.LastName, c.FirstName });
            e.HasIndex(c => c.Email);
            e.HasIndex(c => c.Phone);
        });

        builder.Entity<Appointment>(e =>
        {
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(a => a.Source).HasConversion<string>().HasMaxLength(16);
            e.Property(a => a.PlatformPayment).HasConversion<string>().HasMaxLength(16);
            e.Property(a => a.PrepaidAmount).HasPrecision(10, 2);
            e.Property(a => a.Price).HasPrecision(10, 2);
            e.Property(a => a.Note).HasMaxLength(2000);
            e.Property(a => a.CancelReason).HasMaxLength(500);
            e.Property(a => a.Version).IsRowVersion();
            e.HasOne<Location>().WithMany().HasForeignKey(a => a.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Treatment).WithMany().HasForeignKey(a => a.TreatmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Customer).WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Employee>().WithMany().HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Room>().WithMany().HasForeignKey(a => a.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Equipment>().WithMany().HasForeignKey(a => a.EquipmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(a => a.Bookings).WithOne().HasForeignKey(b => b.AppointmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.LocationId, a.Start });
            e.HasIndex(a => a.CustomerId);
            e.HasIndex(a => a.GroupId);
            e.HasIndex(a => a.SeriesId);
            e.HasOne<AppointmentSeries>().WithMany().HasForeignKey(a => a.SeriesId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AppointmentSeries>(e =>
        {
            e.Property(r => r.Mode).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Problem).HasMaxLength(500);
            e.HasOne<Location>().WithMany().HasForeignKey(r => r.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Customer>().WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Treatment>().WithMany().HasForeignKey(r => r.TreatmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Employee>().WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AreaVisit>(e =>
        {
            e.Property(v => v.Note).HasMaxLength(500);
            e.HasOne<Location>().WithMany().HasForeignKey(v => v.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Room>().WithMany().HasForeignKey(v => v.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(v => v.Customer).WithMany().HasForeignKey(v => v.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(v => v.Bookings).WithOne().HasForeignKey(b => b.AreaVisitId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(v => new { v.LocationId, v.From });
            e.HasIndex(v => v.CustomerId);
            e.HasIndex(v => v.GroupId);
        });

        builder.Entity<ResourceBooking>(e =>
        {
            e.Property(b => b.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(b => b.Reason).HasMaxLength(500);
            e.HasIndex(b => new { b.ResourceId, b.From });
            // Die Ausschlussbedingung gegen Überschneidungen steht in der Migration (EF Core kennt keine EXCLUDE-Constraints).
            e.ToTable(t => t.HasCheckConstraint("CK_ResourceBookings_Period", "\"From\" < \"Until\""));
        });

        builder.Entity<CalendarNote>(e =>
        {
            e.Property(n => n.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(n => n.Text).HasMaxLength(500);
            e.HasOne<Location>().WithMany().HasForeignKey(n => n.LocationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => new { n.LocationId, n.Date });
        });
    }
}
