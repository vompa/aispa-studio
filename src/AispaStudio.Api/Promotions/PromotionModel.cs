using AispaStudio.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Promotions;

internal static class PromotionModel
{
    public const int NameLength = 40;
    public const int GuestTextLength = 200;

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Promotion>(e =>
        {
            e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.Name).HasMaxLength(NameLength);
            e.Property(p => p.GuestText).HasMaxLength(GuestTextLength);
            e.Property(p => p.DiscountPercent).HasPrecision(5, 2);
            e.Property(p => p.DiscountAmount).HasPrecision(10, 2);
            e.Property(p => p.Scope).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(p => new { p.ValidFrom, p.ValidUntil });
            e.HasMany(p => p.Locations).WithOne().HasForeignKey(l => l.PromotionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Treatments).WithOne().HasForeignKey(t => t.PromotionId).OnDelete(DeleteBehavior.Cascade);
            // Prozent oder Betrag, nie beides und nie keins.
            e.ToTable(t => t.HasCheckConstraint("CK_Promotions_Discount",
                "(\"DiscountPercent\" IS NOT NULL AND \"DiscountAmount\" IS NULL) OR (\"DiscountPercent\" IS NULL AND \"DiscountAmount\" IS NOT NULL)"));
        });

        builder.Entity<PromotionLocation>(e =>
        {
            e.Property(l => l.Id).ValueGeneratedNever();
            e.HasIndex(l => new { l.PromotionId, l.LocationId }).IsUnique();
            e.HasOne<Location>().WithMany().HasForeignKey(l => l.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PromotionTreatment>(e =>
        {
            e.Property(t => t.Id).ValueGeneratedNever();
            e.HasIndex(t => new { t.PromotionId, t.TreatmentId }).IsUnique();
            e.HasOne<Treatment>().WithMany().HasForeignKey(t => t.TreatmentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
