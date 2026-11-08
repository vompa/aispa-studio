using AispaStudio.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.BusinessInfo;

internal static class BusinessContactModel
{
    public const int DesignationLength = 80;
    public const int NameLength = 120;
    public const int PhoneLength = 40;
    public const int EmailLength = 200;
    public const int TextLength = 300;
    public const int NumberLength = 60;
    public const int NotesLength = 1000;

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<BusinessContact>(e =>
        {
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Category).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Visibility).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Designation).HasMaxLength(DesignationLength);
            e.Property(c => c.Company).HasMaxLength(NameLength);
            e.Property(c => c.ContactPerson).HasMaxLength(NameLength);
            e.Property(c => c.Insurer).HasMaxLength(NameLength);
            e.Property(c => c.Phone).HasMaxLength(PhoneLength);
            e.Property(c => c.Mobile).HasMaxLength(PhoneLength);
            e.Property(c => c.ClaimsHotline).HasMaxLength(PhoneLength);
            e.Property(c => c.Email).HasMaxLength(EmailLength);
            e.Property(c => c.Address).HasMaxLength(TextLength);
            e.Property(c => c.Responsibility).HasMaxLength(TextLength);
            e.Property(c => c.Coverage).HasMaxLength(TextLength);
            e.Property(c => c.CustomerNumber).HasMaxLength(NumberLength);
            e.Property(c => c.PolicyNumber).HasMaxLength(NumberLength);
            e.Property(c => c.Notes).HasMaxLength(NotesLength);
            e.HasIndex(c => c.Category);
            e.HasMany(c => c.Locations).WithOne().HasForeignKey(l => l.BusinessContactId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BusinessContactLocation>(e =>
        {
            e.Property(l => l.Id).ValueGeneratedNever();
            e.HasIndex(l => new { l.BusinessContactId, l.LocationId }).IsUnique();
            e.HasOne<Location>().WithMany().HasForeignKey(l => l.LocationId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
