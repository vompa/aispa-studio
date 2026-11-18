using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Das gemeinsame Muster „Löschen oder nicht“ (ADR 0099): je Objekt zählt die API, was daran hängt, und nennt den Weg, der statt des Löschens bleibt. Löschen geht nur,
/// wenn nichts daran hängt; Belege und Termine bleiben immer erhalten (ADR 0010). Räume und Geräte lassen sich nie löschen, sondern nur aus der Planung nehmen, denn zu viele
/// Belege, Auswertungen und Kosten verweisen auf sie.
/// </summary>
internal static class DeletionChecks
{
    private const string Deactivate = "deactivate";

    public static async Task<DeletionCheckDto?> RoomAsync(AispaDbContext db, TimeProvider time, Guid roomId, CancellationToken ct)
    {
        if (!await db.Rooms.AnyAsync(r => r.Id == roomId && r.IsActive, ct))
        {
            return null;
        }

        var now = time.GetUtcNow();
        var booked = db.Appointments.Where(a => a.RoomId == roomId && a.Status != AppointmentStatus.Cancelled);
        var dependents = Dependents(
            ("upcomingAppointments", await booked.CountAsync(a => a.End > now, ct), "künftiger Termin", "künftige Termine"),
            ("pastAppointments", await booked.CountAsync(a => a.End <= now, ct), "vergangener Termin", "vergangene Termine"),
            ("areaVisits", await db.AreaVisits.CountAsync(v => v.RoomId == roomId && !v.IsCancelled, ct), "Eintritt", "Eintritte"),
            ("treatments", await db.Treatments.CountAsync(t => t.IsActive && t.Rooms.Any(r => r.Id == roomId), ct), "Behandlung", "Behandlungen"),
            ("storagePlaces", await db.StoragePlaces.CountAsync(p => p.RoomId == roomId, ct), "Lagerort", "Lagerorte"),
            ("closures", await db.RoomClosures.CountAsync(c => c.RoomId == roomId, ct), "Sperrzeit", "Sperrzeiten"));
        return Kept("Der Raum", dependents);
    }

    public static async Task<DeletionCheckDto?> EquipmentAsync(AispaDbContext db, TimeProvider time, Guid equipmentId, CancellationToken ct)
    {
        if (!await db.Equipment.AnyAsync(e => e.Id == equipmentId && e.IsActive, ct))
        {
            return null;
        }

        var now = time.GetUtcNow();
        var booked = db.Appointments.Where(a => a.EquipmentId == equipmentId && a.Status != AppointmentStatus.Cancelled);
        var dependents = Dependents(
            ("upcomingAppointments", await booked.CountAsync(a => a.End > now, ct), "künftiger Termin", "künftige Termine"),
            ("pastAppointments", await booked.CountAsync(a => a.End <= now, ct), "vergangener Termin", "vergangene Termine"),
            ("treatments", await db.Treatments.CountAsync(t => t.IsActive && t.Equipment.Any(e => e.Id == equipmentId), ct), "Behandlung", "Behandlungen"));
        return Kept("Das Gerät", dependents);
    }

    public static async Task<DeletionCheckDto?> QualificationAsync(AispaDbContext db, Guid qualificationId, CancellationToken ct)
    {
        if (!await db.Qualifications.AnyAsync(q => q.Id == qualificationId, ct))
        {
            return null;
        }

        var dependents = Dependents(
            ("employees", await db.Employees.CountAsync(e => e.Qualifications.Any(q => q.Id == qualificationId), ct), "Person im Team", "Personen im Team"),
            ("treatments", await db.Treatments.CountAsync(t => t.RequiredQualifications.Any(q => q.Id == qualificationId), ct), "Behandlung", "Behandlungen"));
        return dependents.Count == 0
            ? new DeletionCheckDto(true, "Die Qualifikation ist nirgends zugeordnet und lässt sich löschen.", dependents)
            : new DeletionCheckDto(false, "Die Qualifikation ist noch zugeordnet. Zuerst dort entfernen.", dependents);
    }

    public static async Task<DeletionCheckDto?> SupplierAsync(AispaDbContext db, Guid supplierId, CancellationToken ct)
    {
        var supplier = await db.StockSuppliers.AsNoTracking().Where(s => s.Id == supplierId).Select(s => new { s.IsActive }).FirstOrDefaultAsync(ct);
        if (supplier is null)
        {
            return null;
        }

        // Bestellungen halten den Namen zum Zeitpunkt der Bestellung fest und hängen nicht am Lieferanten.
        var dependents = Dependents(("items", await db.StockItems.CountAsync(i => i.SupplierId == supplierId, ct), "Material", "Materialien"));
        if (dependents.Count == 0)
        {
            return new DeletionCheckDto(true, "Am Lieferanten hängt kein Material. Bestellungen behalten seinen Namen.", dependents);
        }

        return new DeletionCheckDto(false, "Am Lieferanten hängt noch Material.", dependents,
            supplier.IsActive ? new DeletionAlternative(Deactivate, "Nicht mehr verwenden") : null);
    }

    public static async Task<DeletionCheckDto?> LocationAsync(AispaDbContext db, Guid locationId, CancellationToken ct)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
        {
            return null;
        }

        if (!await db.Locations.AnyAsync(l => l.Id != locationId, ct))
        {
            return new DeletionCheckDto(false, "Der letzte Standort lässt sich nicht löschen.", []);
        }

        var dependents = Dependents(
            ("rooms", await db.Rooms.CountAsync(r => r.LocationId == locationId, ct), "Raum", "Räume"),
            ("equipment", await db.Equipment.CountAsync(e => e.LocationId == locationId, ct), "Gerät", "Geräte"),
            ("appointments", await db.Appointments.CountAsync(a => a.LocationId == locationId, ct), "Termin", "Termine"),
            ("closures", await db.Closures.CountAsync(c => c.LocationId == locationId, ct), "Schließzeit", "Schließzeiten"),
            ("employees", await db.Employees.CountAsync(e => e.WorkingHours.Any(w => w.LocationId == locationId), ct), "Person mit Arbeitszeiten hier", "Personen mit Arbeitszeiten hier"),
            ("registers", await db.CashRegisters.CountAsync(r => r.LocationId == locationId, ct), "Kassa", "Kassen"),
            ("movements", await db.StockMovements.CountAsync(m => db.StoragePlaces.Any(p => p.Id == m.StoragePlaceId && p.LocationId == locationId), ct), "Lagerbewegung", "Lagerbewegungen"));
        return dependents.Count == 0
            ? new DeletionCheckDto(true, "Am Standort hängt nichts. Er lässt sich löschen.", dependents)
            : new DeletionCheckDto(false, "Am Standort hängen Termine, Belege oder Einrichtung. Sie bleiben erhalten; der Standort lässt sich nur schließen.", dependents,
                new DeletionAlternative(LocationDeletion.Close, "Schließen ab Datum …"));
    }

    public static async Task<DeletionCheckDto?> StoragePlaceAsync(AispaDbContext db, Guid placeId, CancellationToken ct)
    {
        var place = await db.StoragePlaces.AsNoTracking().Where(p => p.Id == placeId).Select(p => new { p.Kind, p.IsActive }).FirstOrDefaultAsync(ct);
        if (place is null)
        {
            return null;
        }

        if (place.Kind == StoragePlaceKind.Main)
        {
            return new DeletionCheckDto(false, "Das Hauptlager gehört zum Standort und lässt sich nicht löschen.", []);
        }

        var dependents = Dependents(("movements",
            await db.StockMovements.CountAsync(m => m.StoragePlaceId == placeId || m.TargetStoragePlaceId == placeId, ct), "Lagerbewegung", "Lagerbewegungen"));
        if (dependents.Count == 0)
        {
            return new DeletionCheckDto(true, "Der Lagerort hat keine Bewegungen und lässt sich löschen.", dependents);
        }

        return new DeletionCheckDto(false, "Der Lagerort hat Bewegungen. Sie bleiben für die Nachvollziehbarkeit erhalten.", dependents,
            place.IsActive ? new DeletionAlternative(Deactivate, "Nicht mehr verwenden") : null);
    }

    /// <summary>Der Standardfall für Räume und Geräte: nie löschbar, die Arten, die daran hängen, und „Nicht mehr verwenden“ als Weg.</summary>
    private static DeletionCheckDto Kept(string subject, List<DependentCount> dependents) =>
        new(false, dependents.Count == 0
            ? $"{subject} bleibt für Auswertungen und Belege im Stamm und lässt sich nur aus der Planung nehmen."
            : $"{subject} ist in Verwendung. Termine und Belege bleiben immer erhalten.",
            dependents, new DeletionAlternative(Deactivate, "Nicht mehr verwenden"));

    private static List<DependentCount> Dependents(params (string Kind, int Count, string One, string Many)[] counts) =>
        [.. counts.Where(c => c.Count > 0).Select(c => new DependentCount(c.Kind, c.Count, c.Count == 1 ? $"1 {c.One}" : $"{c.Count} {c.Many}"))];
}
