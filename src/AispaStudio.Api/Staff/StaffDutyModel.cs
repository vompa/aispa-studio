using AispaStudio.Api.Data;
using AispaStudio.Api.HrFile;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Staff;

/// <summary>Eintrag der Liste der Zuständigkeiten (R05 Slice 117, Ä274): sieben Vorgaben mit festem Schlüssel, dazu eigene Einträge ohne Schlüssel.</summary>
public sealed class DutyType
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Lager-Zuordnung und Dienstplan-Prüfung hängen am Schlüssel; Vorgaben lassen sich deshalb umbenennen und ausblenden, nicht löschen.</summary>
    public DutyTypeKey? Key { get; init; }

    public string Name { get; set; } = "";

    public string? RequiredCertificate { get; set; }

    /// <summary>Nur beim Ersthelfer (E14): so viele müssen je Tag und Standort im Dienst sein; 0 = keine Warnung.</summary>
    public int? MinimumOnDuty { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Zuständigkeit einer Person; gilt für alle Standorte. Im Schema <c>hr</c>, damit der Verlauf nur Art, Person und Zeit schreibt.</summary>
public sealed class StaffDuty
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid EmployeeId { get; init; }

    public Employee Employee { get; init; } = null!;

    public Guid DutyTypeId { get; set; }

    public DutyType DutyType { get; init; } = null!;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidUntil { get; set; }

    public Guid? DeputyId { get; set; }

    public Employee? Deputy { get; init; }
}

/// <summary>Zertifikat einer Person. Ohne „gültig bis“ unbefristet; ein neueres Zertifikat gleicher Bezeichnung erneuert ein älteres, das alte bleibt als Verlauf.</summary>
public sealed class StaffCertificate
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid EmployeeId { get; init; }

    public Employee Employee { get; init; } = null!;

    public string Name { get; set; } = "";

    public DateOnly? IssuedOn { get; set; }

    public DateOnly? ValidUntil { get; set; }

    public string? IssuedBy { get; set; }

    public int RemindDays { get; set; } = StaffDutyRules.DefaultRemindDays;
}

/// <summary>Scan eines Zertifikats (1:1, Schlüssel = Zertifikat): Inhalt verschlüsselt wie die Personalakte (<see cref="Online.HealthDataProtector"/>).</summary>
public sealed class StaffCertificateScan
{
    public Guid CertificateId { get; init; }

    /// <summary>Für den Verlauf: der Trigger im Schema <c>hr</c> ordnet die Zeile über diese Spalte der Person zu.</summary>
    public Guid EmployeeId { get; init; }

    public string FileName { get; set; } = "";

    public string ContentType { get; set; } = "";

    public required byte[] Content { get; set; }
}

internal static class StaffDutyModel
{
    public const string ScanContext = "certificate";

    /// <summary>Die Vorgaben in der Reihenfolge der Liste; nur der Ersthelfer verlangt ein Zertifikat und hat eine Mindestzahl.</summary>
    public static readonly (DutyTypeKey Key, string Name, string? Certificate, int? Minimum)[] Defaults =
    [
        (DutyTypeKey.FirstAid, "Ersthelfer", "Erste-Hilfe-Kurs", 1),
        (DutyTypeKey.SafetyRep, "Sicherheitsvertrauensperson", null, null),
        (DutyTypeKey.Stock, "Lager", null, null),
        (DutyTypeKey.FireSafety, "Brandschutz", null, null),
        (DutyTypeKey.Hygiene, "Hygiene", null, null),
        (DutyTypeKey.CashResponsibility, "Kassa-Verantwortung", null, null),
        (DutyTypeKey.Training, "Ausbildung", null, null),
    ];

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<DutyType>(e =>
        {
            e.ToTable("DutyTypes", HrFileModel.Schema);
            e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.Key).HasConversion<string>().HasMaxLength(32);
            e.Property(t => t.Name).HasMaxLength(100);
            e.Property(t => t.RequiredCertificate).HasMaxLength(200);
        });
        builder.Entity<StaffDuty>(e =>
        {
            e.ToTable("StaffDuties", HrFileModel.Schema);
            e.Property(d => d.Id).ValueGeneratedNever();
            e.HasIndex(d => d.EmployeeId);
            e.HasIndex(d => d.DutyTypeId);
            e.HasOne(d => d.Employee).WithMany().HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Deputy).WithMany().HasForeignKey(d => d.DeputyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.DutyType).WithMany().HasForeignKey(d => d.DutyTypeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<StaffCertificate>(e =>
        {
            e.ToTable("StaffCertificates", HrFileModel.Schema);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Name).HasMaxLength(200);
            e.Property(c => c.IssuedBy).HasMaxLength(200);
            e.HasIndex(c => c.EmployeeId);
            e.HasOne(c => c.Employee).WithMany().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<StaffCertificateScan>(e =>
        {
            e.ToTable("StaffCertificateScans", HrFileModel.Schema);
            e.HasKey(s => s.CertificateId);
            e.Property(s => s.CertificateId).ValueGeneratedNever();
            e.Property(s => s.FileName).HasMaxLength(200);
            e.Property(s => s.ContentType).HasMaxLength(64);
            e.HasOne<StaffCertificate>().WithOne().HasForeignKey<StaffCertificateScan>(s => s.CertificateId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// Legt die Vorgaben an, die dem Spa noch fehlen. Unabhängig vom Change-Tracker und gegen gleichzeitige erste Aufrufe sicher: der eindeutige Index auf Mandant und Schlüssel
    /// lässt einen zweiten Eintrag gleichen Schlüssels nicht zu.
    /// </summary>
    public static async Task EnsureDefaultsAsync(AispaDbContext db, CancellationToken ct)
    {
        if (await db.Set<DutyType>().CountAsync(t => t.Key != null, ct) >= Defaults.Length)
        {
            return;
        }

        var tenantId = db.CurrentTenantId;
        foreach (var (key, name, certificate, minimum) in Defaults)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO hr."DutyTypes" ("Id","TenantId","Key","Name","RequiredCertificate","MinimumOnDuty","IsActive")
                VALUES ({Guid.CreateVersion7()}, {tenantId}, {key.ToString()}, {name}, {certificate}, {minimum}, TRUE)
                ON CONFLICT ("TenantId","Key") DO NOTHING
                """, ct);
        }
    }

    /// <summary>Vorgaben in der festen Reihenfolge, danach eigene Einträge nach Name.</summary>
    public static IOrderedEnumerable<DutyType> InListOrder(this IEnumerable<DutyType> types) =>
        types.OrderBy(t => t.Key is null).ThenBy(t => t.Key).ThenBy(t => t.Name, StringComparer.Ordinal).ThenBy(t => t.Id);
}
