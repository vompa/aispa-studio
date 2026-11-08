using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Api.Operations;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reporting;

/// <summary>Tagesplan der Therapeuten und Tagesbriefing der Rezeption, beides je Standort und Tag (Ortszeit).</summary>
internal sealed class DailyReports(AispaDbContext db, RosterService roster, TreatmentCards cards, PosService pos, ServingService servings)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private static readonly AppointmentStatus[] Gone = [AppointmentStatus.Cancelled, AppointmentStatus.NoShow];

    public async Task<DayPlanDto?> DayPlanAsync(Guid locationId, Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
        var employee = await db.Employees.AsNoTracking().Include(e => e.WorkingHours).FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (location is null || employee is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var (from, until) = LocalTime.Day(date, zone);
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Treatment).Include(a => a.Customer)
            .Where(a => a.EmployeeId == employeeId && a.LocationId == locationId && a.Start >= from && a.Start < until && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.Start)
            .ToListAsync(ct);
        var plans = await roster.PlansAsync([employee], date.AddDays(-1), date, ct);
        var today = plans[(employeeId, date)];
        var shifts = today.Shifts.Where(s => s.LocationId == locationId)
            .Select(s => new ShiftDto(LocalTime.ToUtc(date, s.Start!.Value, zone), LocalTime.ToUtc(date, s.End!.Value, zone)))
            .ToList();
        var dayCards = await cards.BuildAsync(appointments, zone, ct);

        var hints = new List<string>(WorkingTime.DayWarnings(today, plans[(employeeId, date.AddDays(-1))], await roster.RulesAsync(ct)));
        if (today.Entries.FirstOrDefault(e => e.Kind != RosterKind.Work) is { } absence)
        {
            hints.Add($"Laut Dienstplan: {RosterLabel(absence.Kind)}.");
        }

        foreach (var card in dayCards.Where(c => !shifts.Any(s => s.Start <= c.Start && c.End <= s.End)))
        {
            hints.Add($"{Time(card.Start, zone)} {card.TreatmentName} liegt außerhalb des Dienstes.");
        }

        foreach (var (previous, next) in dayCards.Zip(dayCards.Skip(1)))
        {
            if (previous.RoomName != next.RoomName)
            {
                hints.Add($"{Time(next.Start, zone)} Raumwechsel: {previous.RoomName} → {next.RoomName}.");
            }

            if (next.Start < previous.End.AddMinutes(5))
            {
                hints.Add($"{Time(next.Start, zone)} weniger als 5 Minuten zwischen zwei Behandlungen.");
            }
        }

        return new(employeeId, employee.InHouseName, date, shifts, dayCards, hints);
    }

    public async Task<BriefingDto?> BriefingAsync(Guid locationId, DateOnly date, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var (from, until) = LocalTime.Day(date, zone);
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Treatment).Include(a => a.Customer)
            .Where(a => a.LocationId == locationId && a.Start >= from && a.Start < until && !Gone.Contains(a.Status))
            .OrderBy(a => a.Start)
            .ToListAsync(ct);
        var dayCards = (await cards.BuildAsync(appointments, zone, ct)).ToDictionary(c => c.AppointmentId);

        var employees = await roster.EmployeesAt(locationId, date).AsNoTracking().Include(e => e.WorkingHours).OrderBy(e => e.PreferredName ?? e.DisplayName).ToListAsync(ct);
        var plans = await roster.PlansAsync(employees, date, date, ct);
        var shifts = await roster.ShiftsAsync(locationId, date, zone, ct);
        var names = employees.ToDictionary(e => e.Id, e => e.InHouseName);

        var staff = new List<BriefingStaff>();
        foreach (var e in employees)
        {
            var plan = plans[(e.Id, date)];
            var here = plan.Shifts.Where(s => s.LocationId == locationId).ToList();
            if (here.Count > 0)
            {
                staff.Add(new(e.Id, e.InHouseName, "Dienst", string.Join(", ", here.Select(s => $"{s.Start:HH\\:mm}–{s.End:HH\\:mm}"))));
            }
            else if (plan.Entries.FirstOrDefault(x => x.Kind != RosterKind.Work) is { } absence
                && e.WorkingHours.Any(w => w.LocationId == locationId && w.Day == date.DayOfWeek))
            {
                staff.Add(new(e.Id, e.InHouseName, RosterLabel(absence.Kind), null));
            }
        }

        var action = new List<BriefingItem>();
        foreach (var a in appointments)
        {
            if (!shifts.Any(s => s.EmployeeId == a.EmployeeId && s.Start <= a.Start && a.End <= s.End))
            {
                action.Add(new(a.Id, a.Start, $"{Time(a.Start, zone)} {a.Treatment.Name}: {names.GetValueOrDefault(a.EmployeeId, "Therapeut")} hat zu der Zeit keinen Dienst."));
            }

            if (dayCards[a.Id].AnamnesisStatus.StartsWith("Fragebogen fehlt", StringComparison.Ordinal))
            {
                action.Add(new(a.Id, a.Start, $"{Time(a.Start, zone)} {dayCards[a.Id].CustomerName}: Erstbesuch ohne Gesundheitsfragebogen."));
            }
        }

        var (yesterdayFrom, _) = LocalTime.Day(date.AddDays(-1), zone);
        var billed = db.BillLines.Where(l => l.AppointmentId != null && db.Bills.Any(b => b.Id == l.BillId && b.Status == BillStatus.Settled))
            .Select(l => l.AppointmentId);
        var unbilled = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == locationId && a.Start >= yesterdayFrom && a.Start < from && a.Status == AppointmentStatus.Completed && !billed.Contains(a.Id))
            .CountAsync(ct);
        if (unbilled > 0)
        {
            action.Add(new(null, null, $"{unbilled} abgeschlossene Termine von gestern sind noch nicht abgerechnet."));
        }

        var guests = new List<BriefingItem>();
        foreach (var card in dayCards.Values.OrderBy(c => c.Start))
        {
            if (card.VisitNumber == 1)
            {
                guests.Add(new(card.AppointmentId, card.Start, $"{Time(card.Start, zone)} {card.CustomerName}: Erstbesuch ({card.TreatmentName})."));
            }

            if (card.Occasion is { } occasion)
            {
                guests.Add(new(card.AppointmentId, card.Start, $"{Time(card.Start, zone)} {card.CustomerName}: {occasion}."));
            }
        }

        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var equipment = await db.Equipment.AsNoTracking().Where(e => e.LocationId == locationId).ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        var resourceIds = rooms.Keys.Concat(equipment.Keys).ToList();
        var blocks = await db.ResourceBookings.AsNoTracking()
            .Where(b => b.AppointmentId == null && resourceIds.Contains(b.ResourceId) && b.From < until && b.Until > from)
            .OrderBy(b => b.From)
            .ToListAsync(ct);
        var roomItems = blocks.Select(b => new BriefingItem(null, b.From,
            $"{rooms.GetValueOrDefault(b.ResourceId) ?? equipment.GetValueOrDefault(b.ResourceId)} gesperrt {Time(b.From, zone)}–{Time(b.Until, zone)}: {b.Reason}")).ToList();

        var registerItems = new List<BriefingItem>();
        var registers = new List<BriefingRegister>();
        foreach (var register in await db.CashRegisters.AsNoTracking().Where(r => r.LocationId == locationId && r.IsActive).OrderBy(r => r.Code).ToListAsync(ct))
        {
            var lastClosing = await db.DayClosings.AsNoTracking().Where(c => c.RegisterId == register.Id).OrderByDescending(c => c.Until)
                .Select(c => (decimal?)c.CountedCash).FirstOrDefaultAsync(ct);
            registerItems.Add(new(null, null, lastClosing is { } cash
                ? $"Kassa {register.Code}: Anfangsbestand laut letztem Abschluss {cash.ToString("0.00", German)} €."
                : $"Kassa {register.Code}: noch kein Tagesabschluss."));
            var warnings = pos.ToDto(register).Warnings;
            registerItems.AddRange(warnings.Select(w => new BriefingItem(null, null, $"Kassa {register.Code}: {w}")));
            registers.Add(new(register.Id, register.Code, register.Country == FiscalCountry.CH ? "CHF" : "EUR", lastClosing, warnings));
        }

        var shiftMinutes = shifts.Sum(s => (s.End - s.Start).TotalMinutes);
        var bookedMinutes = appointments.Sum(a => (a.End - a.Start).TotalMinutes);
        var utilization = shiftMinutes > 0 ? (int)Math.Round(bookedMinutes / shiftMinutes * 100) : 0;
        var onDuty = staff.Count(s => s.State == "Dienst");
        var absent = staff.Count - onDuty;
        var summary = $"Heute {appointments.Count} Behandlungen, Auslastung {utilization} %. "
            + $"{onDuty} {(onDuty == 1 ? "Person" : "Personen")} im Dienst{(absent > 0 ? $", {absent} abwesend" : "")}. "
            + (action.Count > 0 ? $"{action.Count} {(action.Count == 1 ? "Punkt braucht" : "Punkte brauchen")} Aufmerksamkeit" : "Nichts Dringendes offen")
            + (guests.Count(g => g.Text.Contains("Erstbesuch", StringComparison.Ordinal)) is var first and > 0 ? $", {first} Erstbesuch{(first == 1 ? "" : "e")}." : ".");

        var servingTotals = (await servings.DayAsync(locationId, date, ct))?.Totals ?? [];
        var reminders = await Handbook.RemindersAsync(db, date, ct);

        return new(locationId, date, summary, appointments.Count, utilization, staff, action, guests, roomItems, registerItems,
            Servings: servingTotals, Reminders: reminders, Registers: registers);
    }

    internal static string RosterLabel(RosterKind kind) => kind switch
    {
        RosterKind.Vacation => "Urlaub",
        RosterKind.Sick => "Krankenstand",
        RosterKind.Training => "Schulung",
        RosterKind.DayOff => "frei",
        _ => "Dienst",
    };

    private static string Time(DateTimeOffset instant, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(instant, zone).ToString("HH:mm", German);
}
