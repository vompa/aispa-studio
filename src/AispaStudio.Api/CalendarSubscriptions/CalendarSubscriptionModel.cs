using AispaStudio.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.CalendarSubscriptions;

internal static class CalendarSubscriptionModel
{
    public const int NameLength = 120;
    public const int UrlLength = 500;
    public const int TitleLength = 200;
    public const int RegionLength = 10;

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<CalendarSubscription>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.Name).HasMaxLength(NameLength);
            e.Property(s => s.Url).HasMaxLength(UrlLength);
            e.Property(s => s.Region).HasMaxLength(RegionLength);
            e.Property(s => s.Preset).HasMaxLength(RegionLength);
            e.Property(s => s.LastError).HasMaxLength(500);
            e.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.LastResult).HasConversion<string>().HasMaxLength(20);
            e.HasOne<Location>().WithMany().HasForeignKey(s => s.LocationId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Entries).WithOne().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => s.NextCheckAt);
        });

        builder.Entity<CalendarSubscriptionEntry>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasMaxLength(TitleLength);
            e.Property(x => x.RegionNames).HasMaxLength(400);
            e.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Source).HasMaxLength(10);
            e.HasIndex(x => new { x.SubscriptionId, x.From });
        });

        builder.Entity<CalendarSubscriptionHidden>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.UserId).HasMaxLength(450);
            e.HasIndex(x => new { x.SubscriptionId, x.UserId }).IsUnique();
            e.HasOne<CalendarSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CalendarSubscriptionDecision>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.SubscriptionId, x.Date }).IsUnique();
            e.HasOne<CalendarSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CalendarSubscriptionNotice>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Key).HasMaxLength(120);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Text).HasMaxLength(1000);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Key);
            e.HasOne<CalendarSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
