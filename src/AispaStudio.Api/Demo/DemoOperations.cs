using AispaStudio.Api.Data;
using AispaStudio.Api.Operations;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Stock;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Betrieb im Haus für das Beispiel-Spa: „Zum Termin bereitstellen“ bei einigen Behandlungen (mit Lagerartikeln für Wasser, Tee und Früchte) und das
/// Rezeptionshandbuch. Nur Stammdaten, vor den Lagerbewegungen angelegt, damit abgeschlossene Behandlungen die Extras wie im Betrieb abbuchen;
/// der Verlauf selbst ändert sich dadurch nicht und läuft in beiden Wegen (Dienste und im Speicher) gleich.
/// </summary>
internal static class DemoOperations
{
    private const string Supplier = "Getränke- und Obsthandel";

    public static async Task AddAsync(IServiceProvider root, Guid tenantId, DateTimeOffset now, CancellationToken ct)
    {
        await using var scope = DemoSeeder.Open(root, tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        await AddExtrasAsync(db, ct);
        await AddHandbookAsync(db, ct);
        await db.SaveChangesAsync(ct);
        await AddRoomCareAsync(db, now, ct);
    }

    private static async Task AddExtrasAsync(AispaDbContext db, CancellationToken ct)
    {
        var supplier = new StockSupplier { Name = Supplier, Phone = "+43 1 234 56 42", LeadTimeDays = 1, OrderChannel = SupplierOrderChannel.Phone };
        db.StockSuppliers.Add(supplier);
        StockItem Item(string name, string unit, decimal cost, decimal minimum) => new()
        {
            Name = name,
            Unit = unit,
            UnitCost = cost,
            MinimumQuantity = minimum,
            Supplier = supplier,
        };

        var water = Item("Mineralwasser 0,25 l", "Stk", 0.35m, 60);
        var tea = Item("Teebeutel (Kräuter- und Früchtetee)", "Stk", 0.15m, 100);
        var fruit = Item("Früchteteller (frisches Obst)", "Stk", 2.40m, 10);
        db.StockItems.AddRange(water, tea, fruit);

        var treatments = await db.Treatments.AsNoTracking().ToDictionaryAsync(t => t.Name, t => t.Id, ct);
        var position = 0;
        void Add(string treatment, string name, ExtraTiming timing, int? minutes, StockItem? item, decimal quantity) =>
            db.TreatmentExtras.Add(new TreatmentExtra
            {
                TreatmentId = treatments[treatment],
                Position = position++,
                Name = name,
                Timing = timing,
                MinutesBeforeEnd = minutes,
                StockItemId = item?.Id,
                Quantity = quantity,
            });

        Add("Klassische Massage", "Wasser", ExtraTiming.OnArrival, null, water, 1);
        Add("Klassische Massage", "Tee", ExtraTiming.AfterTreatment, null, tea, 1);
        Add("Sportmassage", "Wasser", ExtraTiming.OnArrival, null, water, 1);
        Add("Sportmassage", "Tee", ExtraTiming.AfterTreatment, null, tea, 1);
        Add("Lava-Hot-Stone-Massage (60 Min.)", "Früchteteller", ExtraTiming.BeforeEnd, 10, fruit, 1);
        Add("Lava-Hot-Stone-Massage (60 Min.)", "Tee", ExtraTiming.AfterTreatment, null, tea, 1);
        Add("Ayurveda Abhyanga", "Früchteteller", ExtraTiming.BeforeEnd, 15, fruit, 1);
        Add("Ayurveda Abhyanga", "Tee", ExtraTiming.AfterTreatment, null, tea, 1);
        Add("Ayurveda Abhyanga", "Bademantel und Hausschuhe zur Dusche bringen", ExtraTiming.BeforeEnd, 10, null, 0);
    }

    private static Task AddHandbookAsync(AispaDbContext db, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        db.HandbookEntries.AddRange(
            Entry("Gäste mit Hund", "Empfang",
                "Hunde sind im Haus nicht erlaubt, Assistenzhunde ausgenommen. Gäste freundlich auf den Hundeparkplatz vor dem Eingang hinweisen.", false, 0, now),
            Entry("Schlüssel Saunabereich", "Sauna",
                "Der Schlüssel liegt im Schlüsselkasten hinter der Rezeption. Die Sauna wird vor der Öffnung von der Leitung freigegeben; erst danach Gäste einlassen.",
                true, Days(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday), now),
            Entry("Wäschelieferung am Montag", "Wäsche",
                "Die Wäscherei liefert montags bis 10 Uhr. Mengen laut Lieferschein zählen, Unterschrift und Lieferschein in den Ordner Wäsche legen.", true,
                Days(DayOfWeek.Monday), now),
            Entry("Tee und Obst bei Allergien", "Gäste",
                "Bei Fragen zu Allergien keine Tee- oder Obstsorten empfehlen. Auf Wünsche auf der Kundenkarte achten und bei Unsicherheit die Anamnese beachten.", true, 0, now),
            Entry("Erste Hilfe und Defibrillator", "Sicherheit",
                "Erste-Hilfe-Kasten und Defibrillator hängen im Flur vor den Behandlungsräumen. Im Notfall zuerst 144 rufen, dann die Leitung informieren.", false, 0, now));
        return Task.CompletedTask;
    }


    /// <summary>Checklisten der Tagesstart-Kontrolle für einige Räume, mit abgehakten und abgenommenen Tagen der letzten zwei Wochen.</summary>
    private static async Task AddRoomCareAsync(AispaDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        (string Room, string[] Items)[] checklists =
        [
            ("Hamam", ["Hamam aufheizen und Temperatur prüfen", "Dampfauslass und Boden kontrollieren", "Handtücher und Bademäntel auffüllen"]),
            ("Finnische Sauna", ["Sauna aufheizen", "Temperatur prüfen", "Aufgussmittel und Sanduhr bereitstellen"]),
            ("Ritual-Suite mit Dusche", ["Dusche und Wanne prüfen", "Duftöl und Kerzen bereitstellen", "Raumtemperatur prüfen"]),
            ("Raum Lavendel", ["Liege beziehen", "Raumtemperatur prüfen"]),
            ("Zirbenstube mit Dusche", ["Dusche prüfen", "Liege beziehen"]),
        ];
        var rooms = await db.Rooms.AsNoTracking().ToListAsync(ct);
        var locations = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, ct);
        var items = new List<RoomStartItem>();
        foreach (var (name, texts) in checklists)
        {
            foreach (var room in rooms.Where(r => r.Name == name))
            {
                items.AddRange(texts.Select((text, i) => new RoomStartItem { RoomId = room.Id, Position = i, Text = text }));
            }
        }

        db.RoomStartItems.AddRange(items);
        var reception = await db.Users.AsNoTracking().Where(u => u.DisplayName == "Nina Kovač").Select(u => u.Id).FirstOrDefaultAsync(ct);
        var manager = await db.Users.AsNoTracking().Where(u => u.DisplayName == "Markus Steiner").Select(u => u.Id).FirstOrDefaultAsync(ct);
        foreach (var roomItems in items.GroupBy(i => i.RoomId))
        {
            var zone = LocalTime.Zone(locations[rooms.First(r => r.Id == roomItems.Key).LocationId].TimeZone);
            var today = LocalTime.LocalDate(now, zone);
            for (var back = 14; back >= 0; back--)
            {
                var date = today.AddDays(-back);
                var at = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(8, 20)), zone), TimeSpan.Zero);
                // Heute fehlt in jedem Raum noch der letzte Punkt, damit der Hinweis beim Einchecken zu sehen ist.
                foreach (var item in back == 0 ? roomItems.OrderBy(i => i.Position).SkipLast(1) : roomItems)
                {
                    db.RoomStartChecks.Add(new RoomStartCheck { RoomId = roomItems.Key, Date = date, ItemId = item.Id, CheckedAt = at.AddMinutes(item.Position * 3), UserId = reception });
                }

                if (back > 0)
                {
                    db.RoomApprovals.Add(new RoomApproval
                    {
                        RoomId = roomItems.Key,
                        Date = date,
                        Kind = RoomApprovalKind.DayStart,
                        ApprovedAt = at.AddMinutes(20),
                        ApprovedByUserId = manager,
                        RecordedByUserId = reception,
                    });
                }
            }
        }

        // Raumpflege nach vergangenen Terminen ist abgehakt, nur die der letzten zwei Stunden steht noch offen.
        var cleaning = rooms.Where(r => r.CleaningMinutes > 0).ToDictionary(r => r.Id);
        var users = await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.UserId, ct);
        var past = await db.Appointments.AsNoTracking()
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow && a.BlockedUntil < now)
            .Select(a => new { a.Id, a.RoomId, a.BlockedUntil, a.EmployeeId }).ToListAsync(ct);
        foreach (var a in past.Where(a => cleaning.TryGetValue(a.RoomId, out var room) && a.BlockedUntil.AddMinutes(room.CleaningMinutes) < now.AddHours(-2)))
        {
            db.RoomCareChecks.Add(new RoomCareCheck
            {
                AppointmentId = a.Id,
                RoomId = a.RoomId,
                CheckedAt = a.BlockedUntil.AddMinutes(cleaning[a.RoomId].CleaningMinutes / 2),
                UserId = users.GetValueOrDefault(a.EmployeeId),
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static HandbookEntry Entry(string title, string area, string text, bool remind, int days, DateTimeOffset now) =>
        new() { Title = title, Area = area, Text = text, RemindAtDayStart = remind, Days = days, UpdatedAt = now };

    private static int Days(params DayOfWeek[] days) => Handbook.MaskOf(days);
}
