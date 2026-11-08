using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Operations;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Stock;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reporting;

/// <summary>
/// Baut Behandlungskarten aus Termin, Kundenprofil, Behandlungsrezept, Raum und Verlauf.
/// Gesundheitsdaten bleiben verschlüsselt: die Karte nennt nur, ob und wann der Fragebogen ausgefüllt wurde.
/// </summary>
internal sealed class TreatmentCards(AispaDbContext db, OnlineDbContext online, RoomCareService roomCare)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public async Task<List<TreatmentCard>> BuildAsync(IReadOnlyList<Appointment> appointments, TimeZoneInfo zone, CancellationToken ct)
    {
        if (appointments.Count == 0)
        {
            return [];
        }

        var ids = appointments.Select(a => a.Id).ToList();
        var customerIds = appointments.Select(a => a.CustomerId).Distinct().ToList();
        var treatmentIds = appointments.Select(a => a.TreatmentId).Distinct().ToList();
        var roomIds = appointments.Select(a => a.RoomId).Distinct().ToList();
        var latest = appointments.Max(a => a.Start);

        var rooms = await db.Rooms.AsNoTracking().Where(r => roomIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var equipment = await db.Equipment.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        var employees = await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);
        var steps = (await db.TreatmentPreparationSteps.AsNoTracking().Where(s => treatmentIds.Contains(s.TreatmentId)).ToListAsync(ct))
            .ToLookup(s => s.TreatmentId);
        var stepItemIds = steps.SelectMany(g => g).Where(s => s.StockItemId != null).Select(s => s.StockItemId!.Value).Distinct().ToList();
        var stepItems = await db.StockItems.AsNoTracking().Where(i => stepItemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var recipes = await StockRecipes.ForAsync(db, treatmentIds, ct);
        var checks = (await db.AppointmentPreparationChecks.AsNoTracking().Where(c => ids.Contains(c.AppointmentId)).ToListAsync(ct))
            .Select(c => (c.AppointmentId, c.StepId)).ToHashSet();
        var preferences = await db.CustomerPreferences.AsNoTracking().Where(p => customerIds.Contains(p.CustomerId)).ToDictionaryAsync(p => p.CustomerId, ct);
        var visits = (await db.Appointments.AsNoTracking()
                .Where(a => customerIds.Contains(a.CustomerId) && a.Start <= latest && a.Status != AppointmentStatus.Cancelled)
                .Select(a => new { a.Id, a.CustomerId, a.Start, a.Status, Treatment = a.Treatment.Name, a.EmployeeId })
                .ToListAsync(ct))
            .ToLookup(a => a.CustomerId);
        var visitIds = visits.SelectMany(v => v).Select(v => v.Id).ToList();
        var followUps = await db.AppointmentFollowUps.AsNoTracking().Where(f => visitIds.Contains(f.AppointmentId))
            .ToDictionaryAsync(f => f.AppointmentId, f => f.Note, ct);
        var anamnesis = (await online.AnamnesisRecords.AsNoTracking().Where(r => customerIds.Contains(r.CustomerId))
                .GroupBy(r => r.CustomerId).Select(g => new { CustomerId = g.Key, Last = g.Max(r => r.SubmittedAt) }).ToListAsync(ct))
            .ToDictionary(r => r.CustomerId, r => r.Last);

        var restock = await roomCare.RestockOfAsync(appointments.Where(a => a.Status == AppointmentStatus.Completed), ct);

        var cards = new List<TreatmentCard>();
        foreach (var a in appointments.OrderBy(a => a.Start))
        {
            var earlier = visits[a.CustomerId].Where(v => v.Start < a.Start && v.Status != AppointmentStatus.NoShow).OrderByDescending(v => v.Start).ToList();
            var preference = preferences.GetValueOrDefault(a.CustomerId);
            var prefs = preference is null
                ? new CustomerPreferences(null, null, null, null, null, null, null, null)
                : new CustomerPreferences(preference.Pressure, preference.Scent, preference.RoomTemperature, preference.Music,
                    preference.PrefersQuiet, preference.FocusAreas, preference.AvoidAreas, preference.Other);
            var history = earlier.Take(3)
                .Select(v => new PastTreatment(v.Start, v.Treatment, employees.GetValueOrDefault(v.EmployeeId, ""), followUps.GetValueOrDefault(v.Id)))
                .ToList();
            var date = LocalTime.LocalDate(a.Start, zone);
            var occasion = Occasion(a.Customer, date);
            var anamnesisStatus = anamnesis.TryGetValue(a.CustomerId, out var submitted)
                ? a.Start - submitted > TimeSpan.FromDays(365)
                    ? $"Fragebogen vom {submitted.ToString("dd.MM.yyyy", German)}, älter als ein Jahr: bitte erneuern lassen"
                    : $"Fragebogen vom {submitted.ToString("dd.MM.yyyy", German)}"
                : earlier.Count == 0 ? "Fragebogen fehlt (Erstbesuch)" : "Kein Fragebogen hinterlegt";

            var todo = new List<TodoItem>();
            todo.AddRange(steps[a.TreatmentId].OrderBy(s => s.Position).Select(s => new TodoItem(
                s.Id, s.Text + MaterialNote(s, recipes.GetValueOrDefault(a.TreatmentId), stepItems), s.MinutesBefore is { } m ? a.Start.AddMinutes(-m) : null, "Rezept",
                checks.Contains((a.Id, s.Id)))));
            if (a.EquipmentId is { } equipmentId && equipment.TryGetValue(equipmentId, out var equipmentName))
            {
                todo.Add(new(null, $"{equipmentName} bereitstellen", null, "Termin", false));
            }

            void Prefer(string? value, string format)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    todo.Add(new(null, string.Format(German, format, value.Trim()), null, "Vorliebe", false));
                }
            }

            Prefer(prefs.Scent, "Duft: {0}");
            Prefer(prefs.RoomTemperature, "Raumtemperatur: {0}");
            Prefer(prefs.Music, "Musik: {0}");
            Prefer(prefs.Pressure, "Druck: {0}");
            Prefer(prefs.FocusAreas, "Fokus: {0}");
            Prefer(prefs.AvoidAreas, "Nicht behandeln: {0}");
            if (prefs.PrefersQuiet == true)
            {
                todo.Add(new(null, "Wenig Gespräch gewünscht", null, "Vorliebe", false));
            }

            if (history.FirstOrDefault(h => h.Note is not null) is { } last)
            {
                todo.Add(new(null, $"Letzte Notiz ({last.Start.ToString("dd.MM.", German)}, {last.EmployeeName}): {last.Note}", null, "Notiz", false));
            }

            if (earlier.Count == 0)
            {
                todo.Add(new(null, "Erstbesuch: Ablauf erklären und Gesundheitsfragebogen vor Beginn prüfen", null, "Anlass", false));
            }

            if (occasion is not null)
            {
                todo.Add(new(null, $"{occasion}: Grußkarte von der Rezeption", null, "Anlass", false));
            }

            cards.Add(new(
                a.Id, a.Start, a.End, a.Status, a.Treatment.Name, a.Treatment.DurationMinutes,
                rooms.GetValueOrDefault(a.RoomId, ""), a.EquipmentId is { } eq ? equipment.GetValueOrDefault(eq) : null,
                employees.GetValueOrDefault(a.EmployeeId, ""),
                a.CustomerId, a.Customer.InHouseName,
                earlier.Count + 1, earlier.FirstOrDefault()?.Start, occasion, a.Note, anamnesisStatus, prefs, history, todo,
                followUps.GetValueOrDefault(a.Id),
                restock.GetValueOrDefault(a.Id),
                a.StartedAt));
        }

        return cards;
    }

    /// <summary>Material des Schritts aus dem Lager, z. B. „ (Massageöl, 20 ml)“; die Menge steht im Rezept der Behandlung.</summary>
    private static string MaterialNote(TreatmentPreparationStep step, Dictionary<Guid, decimal>? recipe, Dictionary<Guid, StockItem> items)
    {
        if (step.StockItemId is not { } id || !items.TryGetValue(id, out var item))
        {
            return "";
        }

        return recipe is not null && recipe.TryGetValue(id, out var quantity)
            ? string.Format(German, " ({0}, {1:0.###} {2})", item.Name, quantity, item.Unit)
            : string.Format(German, " ({0})", item.Name);
    }

    /// <summary>Geburtstag in den drei Tagen vor oder nach dem Termin.</summary>
    private static string? Occasion(Customer customer, DateOnly date)
    {
        if (customer.BirthDate is not { } birth)
        {
            return null;
        }

        for (var offset = -3; offset <= 3; offset++)
        {
            var day = date.AddDays(offset);
            if (day.Month == birth.Month && day.Day == birth.Day)
            {
                return offset == 0 ? "Geburtstag heute" : $"Geburtstag am {day.ToString("dd.MM.", German)}";
            }
        }

        return null;
    }
}
