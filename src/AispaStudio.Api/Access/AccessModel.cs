using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Access;

/// <summary>Zugang eines Kontos: eine Rolle, gültig für alle oder für ausgewählte Standorte. Ohne Eintrag kein Zugriff.</summary>
public sealed class StaffAccess
{
    public required string UserId { get; init; }
    public StaffRole Role { get; set; }
    public bool AllLocations { get; set; }
    public List<Guid> LocationIds { get; set; } = [];

    /// <summary>Gesperrt wirkt sofort beim nächsten Aufruf, auch mit noch gültigem Anmelde-Cookie.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Befristeter Zugang (Aushilfe, Praktikum, Saison): ab diesem Zeitpunkt wie gesperrt. Inhaber sind nie befristet.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Für welches Ende der Hinweis vor Ablauf schon verschickt ist; nach einer Verlängerung kommt ein neuer.</summary>
    public DateTimeOffset? ExpiryNoticeFor { get; set; }

    /// <summary>Einzeln vergebenes Recht „Berichte mit Beträgen“; nur für Rollen, die es nicht ohnehin haben (<see cref="Roles.CanBeGranted"/>).</summary>
    public bool AmountReports { get; set; }

    /// <summary>
    /// Einzeln abschaltbares Recht „Auswertungen ohne Beträge“ im Assistenten (Ä55): wirkt nur bei der Rolle Therapeut:in, die es über das Kalenderrecht standardmäßig hat; bei den
    /// anderen Rollen ohne Wirkung und immer <c>true</c>.
    /// </summary>
    public bool PlainReports { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Einladung per Link. Gespeichert wird nur der SHA-256-Hash des Tokens; einmal annehmbar, 7 Tage gültig.</summary>
public sealed class Invitation
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required byte[] TokenHash { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public StaffRole Role { get; init; }
    public bool AllLocations { get; init; }
    public List<Guid> LocationIds { get; init; } = [];
    public Guid? EmployeeId { get; init; }

    /// <summary>Befristung des Zugangs, der beim Annehmen entsteht (nicht die Gültigkeit des Links).</summary>
    public DateTimeOffset? AccessExpiresAt { get; init; }

    /// <summary>Einzeln vergebenes Recht „Berichte mit Beträgen“ für den Zugang, der beim Annehmen entsteht.</summary>
    public bool AmountReports { get; init; }

    /// <summary>„Auswertungen ohne Beträge“ im Assistenten für den Zugang, der beim Annehmen entsteht (nur Therapeut:in, Standard an).</summary>
    public bool PlainReports { get; init; } = true;

    public required string CreatedByUserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public string? AcceptedByUserId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

internal static class AccessModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<StaffAccess>(e =>
        {
            e.ToTable("StaffAccess");
            e.HasKey(a => a.UserId);
            e.Property(a => a.UserId).HasMaxLength(450);
            e.Property(a => a.Role).HasConversion<string>().HasMaxLength(32);
            e.HasOne<AispaUser>().WithOne().HasForeignKey<StaffAccess>(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Invitation>(e =>
        {
            e.Property(i => i.Id).ValueGeneratedNever();
            e.Property(i => i.Email).HasMaxLength(256);
            e.Property(i => i.DisplayName).HasMaxLength(200);
            e.Property(i => i.Role).HasConversion<string>().HasMaxLength(32);
            e.Property(i => i.CreatedByUserId).HasMaxLength(450);
            e.Property(i => i.AcceptedByUserId).HasMaxLength(450);
            e.HasIndex(i => i.TokenHash).IsUnique();
            e.HasIndex(i => i.Email);
        });

        DeviceSignIn.Configure(builder);
        DisplayDevices.Configure(builder);
        QuickSwitch.Configure(builder);
    }
}
