using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Stock;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Costs;

// Kostensätze für die Vollkostenrechnung, bewusst neben dem Kalendermodell: Planung braucht sie nicht,
// und Stundensätze sind vertraulich (nur Inhaber und Standortleitung).

public sealed class LocationCostSettings
{
    public Guid LocationId { get; init; }
    public decimal CleaningHourlyCost { get; set; }
    public decimal DefaultStaffHourlyCost { get; set; }
    public decimal OtherMonthlyCosts { get; set; }
    public int PlannedUtilizationPercent { get; set; } = 70;
    public int StaffProductivityPercent { get; set; } = 75;
}

public sealed class RoomCost
{
    public Guid RoomId { get; init; }
    public decimal MonthlyCost { get; set; }
    public decimal CleaningMaterialCost { get; set; }
}

public sealed class EmployeeCost
{
    public Guid EmployeeId { get; init; }
    public decimal HourlyCost { get; set; }
}

public sealed class TreatmentMaterial
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid TreatmentId { get; init; }
    public int Position { get; init; }
    public required string Name { get; init; }
    public decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public decimal UnitCost { get; init; }

    /// <summary>Material aus dem Lager; dann gelten dessen Bezeichnung, Einheit und Einstandspreis, und abgeschlossene Behandlungen buchen ab.</summary>
    public Guid? StockItemId { get; init; }
}

internal static class CostModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<LocationCostSettings>(e =>
        {
            e.HasKey(s => s.LocationId);
            e.HasOne<Location>().WithOne().HasForeignKey<LocationCostSettings>(s => s.LocationId).OnDelete(DeleteBehavior.Cascade);
            e.Property(s => s.CleaningHourlyCost).HasPrecision(12, 2);
            e.Property(s => s.DefaultStaffHourlyCost).HasPrecision(12, 2);
            e.Property(s => s.OtherMonthlyCosts).HasPrecision(12, 2);
        });

        builder.Entity<RoomCost>(e =>
        {
            e.HasKey(c => c.RoomId);
            e.HasOne<Room>().WithOne().HasForeignKey<RoomCost>(c => c.RoomId).OnDelete(DeleteBehavior.Cascade);
            e.Property(c => c.MonthlyCost).HasPrecision(12, 2);
            e.Property(c => c.CleaningMaterialCost).HasPrecision(12, 2);
        });

        builder.Entity<EmployeeCost>(e =>
        {
            e.HasKey(c => c.EmployeeId);
            e.HasOne<Employee>().WithOne().HasForeignKey<EmployeeCost>(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            e.Property(c => c.HourlyCost).HasPrecision(12, 2);
        });

        builder.Entity<TreatmentMaterial>(e =>
        {
            e.HasOne<Treatment>().WithMany().HasForeignKey(m => m.TreatmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => new { m.TreatmentId, m.Position });
            e.Property(m => m.Name).HasMaxLength(200);
            e.Property(m => m.Unit).HasMaxLength(20);
            e.Property(m => m.Quantity).HasPrecision(12, 3);
            e.Property(m => m.UnitCost).HasPrecision(12, 4);
            e.HasOne<StockItem>().WithMany().HasForeignKey(m => m.StockItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
