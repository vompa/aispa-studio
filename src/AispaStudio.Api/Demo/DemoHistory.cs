using System.Diagnostics;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Promotions;
using AispaStudio.Api.Reporting;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Betrieb der letzten und der kommenden Wochen, Tag für Tag in zeitlicher Reihenfolge: Dienstplan, Buchungen (der Planer lehnt
/// Überschneidungen ab wie an der Rezeption), Check-in, Absagen und No-Shows, Kassieren mit allen Zahlungsarten, Gutscheinverkauf und
/// -einlösung, Storno, Einlagen und Entnahmen, Tagesabschluss mit gelegentlicher Kassendifferenz. Die Kassa signiert jeden Beleg
/// mit der Test-Signatureinrichtung, Belegkette und Monatsbelege entstehen also wie im Betrieb.
/// </summary>
internal static partial class DemoHistory
{
    /// <summary>Wechselgeld, das nach dem Tagesabschluss in der Lade bleibt; der Rest geht am nächsten Morgen zur Bank.</summary>
    private const decimal Float = 300m;

    // Paare je Tag und Standort, Versuche je Tag, längste Behandlung eines Paars und eines Spa-Tag-Schritts (Minuten), Pause zwischen den Schritten.
    private const int MinPairsPerDay = 2;
    private const int MaxPairsPerDay = 4;
    private const int MaxGroupAttempts = 16;
    // Würfe je Tag: Eine Kombination, die an dem Tag schon ohne freie Zeit blieb, wird ohne Suche übergangen und zählt nicht als Versuch.
    private const int MaxGroupDraws = 4 * MaxGroupAttempts;
    private const int MaxPairMinutes = 90;
    private const int MaxSpaDayStepMinutes = 60;
    private const int SpaDayBreakMinutes = 15;
    private const double SpaDayChance = 0.4;

    // Weiter als so viele Tage voraus ist der Kalender dünn belegt: dann höchstens ein Paar und selten ein Spa-Tag.
    private const int FullGroupDays = 14;
    private const double DistantSpaDayChance = 0.1;

    // Beliebte Behandlungen kommen öfter dran.
    private static readonly string[] Popular =
    [
        "Klassische Massage", "Lava-Hot-Stone-Massage (60 Min.)", "Traditionelle Thai-Massage (60 Min.)", "Thai-Aromaölmassage (60 Min.)",
        "Rückenmassage Express", "Gesichtsbehandlung Klassik", "AISPA Signaturmassage", "Hamam-Ritual mit Schaumwaschung",
    ];

    private static readonly string[] BookingNotes =
    [
        "Gutschein wird mitgebracht", "Erster Besuch", "Bitte ruhigen Raum", "Kommt mit Partnerin, gleiche Uhrzeit nebenan",
        "Geburtstagsgeschenk", "Möchte vorher die Sauna nutzen", "Wunsch: Therapeutin",
    ];

    private static readonly string[] CancelReasons =
    [
        "Gast krank", "Terminkollision beim Gast", "Per Mail abgesagt", "Kinderbetreuung ausgefallen", "Anreise verschoben",
    ];

    private static readonly string[] FollowUps =
    [
        "Nacken stark verspannt, nächstes Mal 90 Minuten empfehlen.",
        "Druck war angenehm, gerne wieder dieselbe Therapeutin.",
        "Leichte Rötung nach dem Peeling, beim nächsten Mal sanfteres Produkt.",
        "Hat nach dem Öl gefragt, Massageöl Lavendel empfohlen.",
        "Rechte Schulter schmerzempfindlich, dort nur leicht arbeiten.",
        "Möchte das Ritual als Gutschein für die Mutter kaufen.",
        "Kopfhautmassage gewünscht, beim nächsten Termin einplanen.",
    ];

    private sealed record DemoLocation(Location Location, TimeZoneInfo Zone, List<OfferedTreatment> Offered, string? ReceptionUserId, string? ManagerUserId);

    /// <param name="Rooms">Räume, in denen sie am Standort laufen kann (zwei Gäste gleichzeitig brauchen zwei, sofern es keinen Paarraum gibt).</param>
    /// <param name="PairRoom">Läuft am Standort in einem Raum mit mindestens zwei Plätzen (Paarraum).</param>
    private sealed record OfferedTreatment(Guid Id, string Name, int Duration, int Weight, int Rooms, bool PairRoom);

    private sealed record Article(Guid Id, string Name, bool SaunaPass);

    private sealed class Till(Guid id)
    {
        public Guid Id { get; } = id;
        public decimal Counted { get; set; }
        public bool Started { get; set; }
    }

    private sealed class SoldVoucher(string code, VoucherKind kind, Guid? treatmentId, decimal balance, DateTimeOffset soldAt)
    {
        public string Code { get; } = code;
        public DateTimeOffset SoldAt { get; } = soldAt;
        public VoucherKind Kind { get; } = kind;
        public Guid? TreatmentId { get; } = treatmentId;
        public decimal Balance { get; set; } = balance;
    }

    private sealed class Run(Random random, DemoClock clock, DateTimeOffset now, List<Guid> customers, List<Article> articles, DemoTimings timings,
        string privacyNoticeVersion)
    {
        public string PrivacyNoticeVersion { get; } = privacyNoticeVersion;
        public DemoTimings Timings { get; } = timings;
        public Random Random { get; } = random;
        public DemoClock Clock { get; } = clock;
        public DateTimeOffset Now { get; } = now;
        public List<Guid> Customers { get; } = customers;
        public List<Article> Articles { get; } = articles;

        /// <summary>Die Aktionen des Spas (ADR 0071); laufende wendet die Kassa an den Tagen ihrer Laufzeit an.</summary>
        public List<Promotion> Promotions { get; set; } = [];
        public List<SoldVoucher> Vouchers { get; } = [];
        public Dictionary<Guid, Till> Tills { get; } = [];
        public int Receipts { get; set; }

        private readonly Dictionary<(Guid Customer, int Week), int> _bookingsPerWeek = [];

        /// <summary>
        /// Gäste in drei Gruppen (die ältesten Einträge zuerst): Stammgäste (12 % der Gäste, 32 % der Wahlen), regelmäßige Gäste (33 %, 44 %)
        /// und seltene Gäste (55 %, 24 %). Der Zufall wählt nur die Gruppe, innerhalb ist jeder gleich wahrscheinlich.
        /// </summary>
        public Guid Customer()
        {
            var r = Random.NextDouble();
            var regulars = (int)(Customers.Count * 0.12);
            var frequent = (int)(Customers.Count * 0.45);
            var (from, to) = r < 0.32 ? (0, regulars) : r < 0.76 ? (regulars, frequent) : (frequent, Customers.Count);
            return Customers[Random.Next(from, to)];
        }

        /// <summary>
        /// Gast für einen Termin: wie <see cref="Customer"/>, aber höchstens ein Termin je Woche (auch wenn ein Stammgast sonst jeden Tag käme).
        /// Ist die Wahl in dieser Woche schon belegt, wird neu gewürfelt; null, wenn auch nach vielen Versuchen keiner frei ist.
        /// </summary>
        public Guid? CustomerForAppointment(DateTimeOffset start)
        {
            var week = DateOnly.FromDateTime(start.UtcDateTime).DayNumber / 7;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var customer = Customer();
                if (_bookingsPerWeek.TryAdd((customer, week), 1))
                {
                    return customer;
                }
            }

            return null;
        }

        public T Pick<T>(IReadOnlyList<T> items) => items[Random.Next(items.Count)];

        public bool Chance(double probability) => Random.NextDouble() < probability;
    }

    public static async Task RunAsync(IServiceProvider root, Guid tenantId, DateTimeOffset now, DemoDataOptions options, ILogger logger,
        DemoTimings timings, DemoMode mode, CancellationToken ct)
    {
        var clock = new DemoClock(now);
        var started = Stopwatch.GetTimestamp();
        List<DemoLocation> locations;
        Run run;
        await using (var scope = DemoSeeder.Open(root, tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
            locations = await LoadLocationsAsync(db, ct);
            // Gleiches Datum kommt vor; die Namen sind je Spa eindeutig und machen die Reihenfolge (und damit den Zufall) reproduzierbar.
            var customers = await db.Customers.AsNoTracking().OrderBy(c => c.CreatedAt).ThenBy(c => c.LastName).ThenBy(c => c.FirstName).Select(c => c.Id).ToListAsync(ct);
            var articles = await db.Articles.AsNoTracking().OrderBy(a => a.Name)
                .Select(a => new Article(a.Id, a.Name, a.Name == PosDemoData.SaunaDayPass)).ToListAsync(ct);
            run = new Run(new Random(1810), clock, now, customers, articles, timings,
                scope.ServiceProvider.GetRequiredService<IOptions<OnlineBookingOptions>>().Value.PrivacyNoticeVersion)
            {
                Promotions = await db.Promotions.AsNoTracking().Include(p => p.Locations).Include(p => p.Treatments).AsSplitQuery()
                    .OrderBy(p => p.ValidUntil).ThenBy(p => p.Name).ToListAsync(ct),
            };
        }

        var zone = locations[0].Zone;
        var today = LocalTime.LocalDate(now, zone);
        var first = today.AddDays(-7 * options.WeeksBack);
        var last = today.AddDays(7 * options.WeeksAhead);
        using (timings.Measure("7a Dienstplan")) { await AddRosterAsync(root, tenantId, today, now, ct); }

        await using (var scope = DemoSeeder.Open(root, tenantId))
        {
            run.Clock.Now = LocalTime.ToUtc(first, new TimeOnly(8, 0), zone);
            var pos = ActivatorUtilities.CreateInstance<PosService>(scope.ServiceProvider, run.Clock);
            foreach (var (locationId, registerId) in await PosDemoData.CreateRegistersAsync(pos, scope.ServiceProvider.GetRequiredService<AispaDbContext>(), ct))
            {
                run.Tills[locationId] = new Till(registerId);
            }
        }

        // Der Verlauf läuft über die Dienste oder im Speicher; die Kassen und der Dienstplan stehen jetzt in der Datenbank.
        await using IDemoBackend backend = mode == DemoMode.Bulk
            ? await MemoryDemoBackend.CreateAsync(root, tenantId, clock, ct)
            : new ServiceDemoBackend(root, tenantId, clock);

        var appointments = 0;
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            foreach (var location in locations)
            {
                var hours = location.Location.OpeningHours.Where(h => h.Day == date.DayOfWeek).OrderBy(h => h.Opens).ToList();
                if (hours.Count == 0)
                {
                    continue;
                }

                await using var ops = backend.Open();
                List<AppointmentDto> booked;
                using (run.Timings.Measure("7b Buchen (Tag)")) { booked = await BookDayAsync(ops, run, location, date, today, ct); }
                appointments += booked.Count;
                if (date <= today)
                {
                    using (run.Timings.Measure("7c Kassieren (Tag)")) { await SettleDayAsync(ops, run, location, hours, date, booked, ct); }
                }
            }

            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
                Progress(logger, date, appointments, run.Receipts, seconds);
            }
        }

        using (timings.Measure("7d Abschluss (Schreiben)")) { await backend.CompleteAsync(ct); }
    }

    private static async Task<List<DemoLocation>> LoadLocationsAsync(AispaDbContext db, CancellationToken ct)
    {
        var locations = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).OrderBy(l => l.Name).ToListAsync(ct);
        var treatments = await db.Treatments.AsNoTracking().Include(t => t.Rooms).OrderBy(t => t.Name).ToListAsync(ct);
        var access = await db.StaffAccess.AsNoTracking().Where(a => a.Role == StaffRole.Reception || a.Role == StaffRole.Manager).ToListAsync(ct);
        return [.. locations.Select(l => new DemoLocation(
            l,
            LocalTime.Zone(l.TimeZone),
            [.. treatments.Where(t => t.Rooms.Any(r => r.LocationId == l.Id))
                .Select(t => new OfferedTreatment(t.Id, t.Name, t.DurationMinutes, Popular.Contains(t.Name) ? 9 : 3, t.Rooms.Count(r => r.LocationId == l.Id),
                    t.Rooms.Any(r => r.LocationId == l.Id && r.Places >= 2)))],
            access.FirstOrDefault(a => a.Role == StaffRole.Reception && a.LocationIds.Contains(l.Id))?.UserId,
            access.FirstOrDefault(a => a.Role == StaffRole.Manager && a.LocationIds.Contains(l.Id))?.UserId))];
    }

    // ---------- Dienstplan ----------

    /// <summary>Urlaub, Krankenstand, Schulung und ein getauschter Dienst, damit Kalender und Dienstplan nicht nur das Wochenmuster zeigen.</summary>
    private static async Task AddRosterAsync(IServiceProvider root, Guid tenantId, DateOnly today, DateTimeOffset now, CancellationToken ct)
    {
        await using var scope = DemoSeeder.Open(root, tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var employees = await db.Employees.AsNoTracking().Include(e => e.WorkingHours).ToDictionaryAsync(e => e.DisplayName, ct);
        var graz = await db.Locations.AsNoTracking().Where(l => l.Name.StartsWith("Graz")).Select(l => l.Id).FirstAsync(ct);

        void Absent(string name, RosterKind kind, int fromDay, int days, string? note = null)
        {
            var employee = employees[name];
            for (var date = today.AddDays(fromDay); date < today.AddDays(fromDay + days); date = date.AddDays(1))
            {
                if (employee.WorkingHours.Any(w => w.Day == date.DayOfWeek))
                {
                    db.RosterDays.Add(new RosterDay
                    {
                        EmployeeId = employee.Id,
                        Date = date,
                        Note = note,
                        Entries = [new RosterDayEntry { Kind = kind }],
                        UpdatedAt = now.AddDays(Math.Min(fromDay, 0) - 7),
                    });
                }
            }
        }

        Absent("Marco Berger", RosterKind.Vacation, -24, 7, "Sommerurlaub nachgeholt");
        Absent("Elif Demir", RosterKind.Sick, -10, 2);
        Absent("Theresa Lechner", RosterKind.Training, -4, 1, "Fortbildung Lymphdrainage");
        Absent("Daniel Url", RosterKind.Sick, 0, 1, "Krank gemeldet, Termine umbuchen");
        Absent("Siriporn Kaewdee", RosterKind.DayOff, 3, 1, "Zeitausgleich");
        Absent("Lena Huber", RosterKind.Vacation, 14, 7);

        // Jana Wolf hilft an einem Samstag in Graz aus statt in Wien.
        var saturday = Next(DayOfWeek.Saturday, today.AddDays(8));
        db.RosterDays.Add(new RosterDay
        {
            EmployeeId = employees["Jana Wolf"].Id,
            Date = saturday,
            Note = "Aushilfe Graz",
            Entries = [new RosterDayEntry { Kind = RosterKind.Work, LocationId = graz, Start = new TimeOnly(10, 0), End = new TimeOnly(18, 0) }],
            UpdatedAt = now.AddDays(-2),
        });

        // Dienstplan Wien: vor einer Woche veröffentlicht; für in drei Wochen liegt ein Entwurf, der noch freigegeben werden muss.
        var vienna = await db.Locations.AsNoTracking().Where(l => l.Name.StartsWith("Wien")).Select(l => l.Id).FirstAsync(ct);
        db.RosterPublications.Add(new RosterPublication
        {
            LocationId = vienna,
            From = today.AddDays(7),
            Until = today.AddDays(20),
            Days = 7,
            PublishedAt = now.AddDays(-7),
        });
        var training = Next(DayOfWeek.Wednesday, today.AddDays(21));
        db.RosterDrafts.Add(new RosterDraft
        {
            EmployeeId = employees["Theresa Lechner"].Id,
            Date = training,
            LocationId = vienna,
            Note = "Entwurf: Schulung Hot Stone",
            Entries = [new RosterDraftEntry { Kind = RosterKind.Training }],
            UpdatedAt = now.AddDays(-1),
        });
        await db.SaveChangesAsync(ct);
    }

    // ---------- Buchungen ----------

    private static async Task<List<AppointmentDto>> BookDayAsync(IDemoOps ops, Run run, DemoLocation location,
        DateOnly date, DateOnly today, CancellationToken ct)
    {
        var onlineBookings = new List<OnlineBooking>();
        // Buchungen je Tag bei voller Vorlaufzeit: gut zwei Drittel der Dienstzeit sind belegt.
        var size = location.Location.Name.StartsWith("Wien", StringComparison.Ordinal) ? 34
            : location.Location.Name.StartsWith("Salzburg", StringComparison.Ordinal) ? 15 : 12;
        var ahead = date.DayNumber - today.DayNumber;
        var fill = ahead switch { <= 0 => 1.0, <= 2 => 0.9, <= 7 => 0.7, <= 14 => 0.45, <= 21 => 0.28, _ => 0.16 };
        var day = date.DayOfWeek switch { DayOfWeek.Saturday => 1.3, DayOfWeek.Friday => 1.15, DayOfWeek.Monday => 0.8, _ => 1.0 };
        var attempts = (int)Math.Round(size * fill * day);

        // Paare und Spa-Tage zuerst: sie brauchen zwei Personen und Räume gleichzeitig und fänden zwischen den Einzelterminen keinen Platz mehr.
        var booked = new List<AppointmentDto>();
        await BookGroupsAsync(ops, run, location, date, ahead, booked, ct);
        for (var i = 0; i < attempts; i++)
        {
            var treatment = PickTreatment(run, location.Offered);
            // Eine der freien Zeiten, wie sie Rezeption und Online-Buchung anbieten.
            List<AvailableSlot> slots;
            using (run.Timings.Measure("Buchen: freie Zeiten (Kontext laden, Planer)"))
            {
                slots = await ops.FreeSlotsAsync(location.Location.Id, treatment.Id, date, ct);
            }
            if (slots.Count == 0)
            {
                continue;
            }

            var start = run.Pick(slots).Start;
            SetBookingTime(run, start);
            var note = run.Chance(0.06) ? run.Pick(BookingNotes) : null;
            if (run.CustomerForAppointment(start) is not { } customerId)
            {
                continue;
            }

            BookingOutcome bookOutcome;
            using (run.Timings.Measure("Buchen: BookAsync"))
            {
                bookOutcome = await ops.BookAsync(new AppointmentRequest(location.Location.Id, treatment.Id, customerId, start, null, null, null, note), ct);
            }

            if (bookOutcome is not BookingOutcome.Success { Appointment: var appointment })
            {
                continue;
            }

            booked.Add(appointment);
            if (run.Chance(0.22))
            {
                onlineBookings.Add(new OnlineBooking
                {
                    AppointmentId = appointment.Id,
                    CustomerId = appointment.CustomerId,
                    CreatedAt = run.Clock.Now,
                    PrivacyAcceptedAt = run.Clock.Now,
                    PrivacyNoticeVersion = run.PrivacyNoticeVersion,
                    Language = run.Chance(0.1) ? "en" : "de",
                });
            }
        }

        using (run.Timings.Measure("Buchen: Online-Markierung speichern")) { await ops.AddOnlineBookingsAsync(onlineBookings, ct); }

        // Was vorbei ist, wurde erledigt, abgesagt oder versäumt; was gerade läuft, ist eingecheckt.
        var result = new List<AppointmentDto>();
        using var statusTimer = run.Timings.Measure("Buchen: Status, Absage, Check-in");
        foreach (var appointment in booked)
        {
            // Paare und Spa-Tage werden nicht einzeln abgesagt, sonst bliebe ein halbes Paar übrig.
            BookingOutcome outcome;
            if (appointment.End <= run.Now)
            {
                if (appointment.GroupId is null && run.Chance(0.06))
                {
                    run.Clock.Now = Max(appointment.Start.AddHours(-run.Random.Next(4, 60)), appointment.UpdatedAt);
                    outcome = await ops.CancelAsync(appointment.Id, run.Pick(CancelReasons), ct);
                }
                else
                {
                    run.Clock.Now = appointment.End;
                    outcome = await ops.ChangeStatusAsync(appointment.Id,
                        run.Chance(0.025) ? AppointmentStatus.NoShow : AppointmentStatus.Completed, ct);
                }
            }
            else if (appointment.Start <= run.Now)
            {
                run.Clock.Now = appointment.Start.AddMinutes(-5);
                outcome = await ops.ChangeStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, ct);
            }
            else if (appointment.GroupId is null && run.Chance(0.04))
            {
                run.Clock.Now = Max(run.Now.AddHours(-run.Random.Next(1, 48)), appointment.UpdatedAt);
                outcome = await ops.CancelAsync(appointment.Id, run.Pick(CancelReasons), ct);
            }
            else
            {
                outcome = new BookingOutcome.Success(appointment);
            }

            result.Add(outcome is BookingOutcome.Success { Appointment: var changed } ? changed : appointment);
        }

        statusTimer.Dispose();
        // Nachbereitung, die auf den nächsten Behandlungskarten erscheint.
        await ops.AddFollowUpsAsync([.. result.Where(a => a.Status == AppointmentStatus.Completed && run.Chance(0.1))
            .Select(a => new AppointmentFollowUp { AppointmentId = a.Id, Note = run.Pick(FollowUps), UpdatedAt = a.End.AddMinutes(10) })], ct);
        return result;
    }

    /// <summary>
    /// Gemeinsame Buchungen eines Tages: zwei bis vier Paare (zwei Gäste gleichzeitig; das erste in den Paarraum, wo es einen gibt) und
    /// gelegentlich ein Spa-Tag (ein Gast, mehrere Behandlungen nacheinander). Ein Paar oder Spa-Tag wird als Ganzes gebucht oder gar nicht.
    /// </summary>
    private static async Task BookGroupsAsync(IDemoOps ops, Run run, DemoLocation location, DateOnly date, int ahead, List<AppointmentDto> booked, CancellationToken ct)
    {
        var distant = ahead > FullGroupDays;
        var wanted = distant ? run.Random.Next(0, 2) : run.Random.Next(MinPairsPerDay, MaxPairsPerDay + 1);
        var pairRoomPending = location.Offered.Any(t => t.PairRoom);
        // Jede Paarraum-Behandlung wird höchstens einmal versucht, und was an diesem Tag niemand im Dienst kann, kommt nicht mehr dran: Sonst
        // trifft der Zufall bei wenigen Personen im Dienst immer wieder Behandlungen, die nicht gehen, und der Tag bliebe ohne Paar.
        var pairRoomTried = new HashSet<Guid>();
        var unavailable = new HashSet<Guid>();
        // Kombinationen (Behandlung, Partner), für die an diesem Tag keine gemeinsame freie Zeit besteht: Freie Zeiten werden nur weniger, sie
        // kommen nicht wieder. Ohne diese Liste zieht der Zufall bei wenigen Personen im Dienst dieselbe Kombination immer wieder.
        var noSlots = new HashSet<(Guid, Guid)>();
        var pairs = 0;
        var lastFailed = false;
        for (int draw = 0, attempt = 0; draw < MaxGroupDraws && attempt < MaxGroupAttempts && pairs < wanted; draw++)
        {
            var open = location.Offered.Where(t => !unavailable.Contains(t.Id)).ToList();
            // Ohne zwei Personen mit der nötigen Qualifikation im Dienst gibt es keinen Paarraum-Termin; dann geht es mit anderen Paaren weiter.
            List<OfferedTreatment> pairRoomCandidates = pairRoomPending ? [.. open.Where(t => t.PairRoom && !pairRoomTried.Contains(t.Id))] : [];
            pairRoomPending &= pairRoomCandidates.Count > 0;
            // Passt dieselbe Behandlung für beide nicht (z. B. nur eine Person dafür im Dienst), kommen die beiden mit verschiedenen Behandlungen.
            var mixed = lastFailed && !pairRoomPending;
            lastFailed = true;
            List<OfferedTreatment> candidates = pairRoomPending ? pairRoomCandidates : [.. open.Where(t => t.Duration <= MaxPairMinutes && t.Rooms >= 2)];
            if (candidates.Count == 0)
            {
                break;
            }

            var treatment = PickTreatment(run, candidates);
            if (pairRoomPending)
            {
                pairRoomTried.Add(treatment.Id);
            }

            List<OfferedTreatment> partners = mixed ? [.. open.Where(t => t != treatment && t.Duration <= MaxPairMinutes)] : [];
            if (mixed && partners.Count == 0)
            {
                break;
            }

            var partner = mixed ? PickTreatment(run, partners) : treatment;
            if (noSlots.Contains(PairKey(treatment, partner)))
            {
                continue;
            }

            attempt++;
            if (await TryBookPairAsync(ops, run, location, date, treatment, partner, pairRoomPending, unavailable, noSlots, booked, ct))
            {
                pairs++;
                pairRoomPending = false;
                lastFailed = false;
            }
        }

        // Hat der Zufall in den Versuchen kein Paar gefunden, das geht (z. B. wenn nur eine Person die Qualifikation hat und der Zufall die
        // Behandlung vor allem mit anderen Behandlungen paart), geht der Tag die Kombinationen der Reihe nach durch, bis die Mindestzahl erreicht ist.
        if (!distant && pairs < MinPairsPerDay)
        {
            await BookPairsInOrderAsync(ops, run, location, date, MinPairsPerDay - pairs, pairRoomPending, unavailable, noSlots, booked, ct);
        }

        if (run.Chance(distant ? DistantSpaDayChance : SpaDayChance))
        {
            await BookSpaDayAsync(ops, run, location, date, booked, ct);
        }
    }

    /// <summary>
    /// Sucht eine gemeinsame freie Zeit für zwei Gäste und bucht das Paar, wenn es eine gibt (mit <paramref name="sharedRoomOnly"/> nur im Paarraum).
    /// Bleibt die Suche leer, merkt sich der Tag die Kombination in <paramref name="noSlots"/>.
    /// </summary>
    private static async Task<bool> TryBookPairAsync(IDemoOps ops, Run run, DemoLocation location, DateOnly date, OfferedTreatment treatment,
        OfferedTreatment partner, bool sharedRoomOnly, HashSet<Guid> unavailable, HashSet<(Guid, Guid)> noSlots, List<AppointmentDto> booked, CancellationToken ct)
    {
        // Die Gäste stehen erst nach der Zeitwahl fest; die Suche braucht nur zwei verschiedene.
        List<GroupBookingItem> probe = [new(treatment.Id, Guid.NewGuid(), 0), new(partner.Id, Guid.NewGuid(), 0)];
        var slots = await ops.FreeGroupSlotsAsync(location.Location.Id, new GroupAvailabilityRequest(date, probe), ct);
        if (slots.Count == 0)
        {
            noSlots.Add(PairKey(treatment, partner));
            await MarkUnavailableAsync(ops, location, date, [treatment, partner], unavailable, ct);
        }

        var fitting = slots.Where(s => !sharedRoomOnly || s.SharedRoom).ToList();
        if (fitting.Count == 0)
        {
            return false;
        }

        var start = run.Pick(fitting).Start;
        SetBookingTime(run, start);
        if (run.CustomerForAppointment(start) is not { } first || run.CustomerForAppointment(start) is not { } second)
        {
            return false;
        }

        if (await ops.BookGroupAsync(new GroupBookingRequest(location.Location.Id, start, [new(treatment.Id, first, 0), new(partner.Id, second, 0)]), ct)
            is not BookingOutcome.GroupBooked { Result: var pair })
        {
            return false;
        }

        booked.AddRange(pair.Appointments);
        return true;
    }

    /// <summary>
    /// Bucht fehlende Paare nicht nach dem Zufall, sondern geht die Kombinationen in der Reihenfolge des Angebots durch: erst dieselbe Behandlung für
    /// beide (zuerst im Paarraum, solange er fehlt), dann verschiedene. Der Zufall wählt nur noch Zeit und Gäste.
    /// </summary>
    private static async Task BookPairsInOrderAsync(IDemoOps ops, Run run, DemoLocation location, DateOnly date, int missing, bool pairRoomPending,
        HashSet<Guid> unavailable, HashSet<(Guid, Guid)> noSlots, List<AppointmentDto> booked, CancellationToken ct)
    {
        var pool = location.Offered.Where(t => t.Duration <= MaxPairMinutes).ToList();
        var combos = new List<(OfferedTreatment Treatment, OfferedTreatment Partner, bool SharedRoomOnly)>();
        combos.AddRange(pool.Where(t => t.PairRoom).Select(t => (t, t, true)));
        combos.AddRange(pool.Where(t => t.Rooms >= 2).Select(t => (t, t, false)));
        for (var i = 0; i < pool.Count; i++)
        {
            combos.AddRange(pool.Skip(i + 1).Select(p => (pool[i], p, false)));
        }

        foreach (var (treatment, partner, sharedRoomOnly) in combos)
        {
            if (missing == 0)
            {
                return;
            }

            if ((sharedRoomOnly && !pairRoomPending) || unavailable.Contains(treatment.Id) || unavailable.Contains(partner.Id)
                || noSlots.Contains(PairKey(treatment, partner)))
            {
                continue;
            }

            if (await TryBookPairAsync(ops, run, location, date, treatment, partner, sharedRoomOnly, unavailable, noSlots, booked, ct))
            {
                missing--;
                pairRoomPending = false;
            }
        }
    }

    /// <summary>Schlüssel einer Kombination, unabhängig von der Reihenfolge der beiden Behandlungen.</summary>
    private static (Guid, Guid) PairKey(OfferedTreatment treatment, OfferedTreatment partner) =>
        treatment.Id.CompareTo(partner.Id) <= 0 ? (treatment.Id, partner.Id) : (partner.Id, treatment.Id);

    /// <summary>Merkt Behandlungen, für die an diesem Tag auch einzeln keine freie Zeit besteht (niemand im Dienst kann sie).</summary>
    private static async Task MarkUnavailableAsync(IDemoOps ops, DemoLocation location, DateOnly date, IEnumerable<OfferedTreatment> treatments,
        HashSet<Guid> unavailable, CancellationToken ct)
    {
        foreach (var treatment in treatments.DistinctBy(t => t.Id))
        {
            if ((await ops.FreeSlotsAsync(location.Location.Id, treatment.Id, date, ct)).Count == 0)
            {
                unavailable.Add(treatment.Id);
            }
        }
    }

    private static async Task BookSpaDayAsync(IDemoOps ops, Run run, DemoLocation location, DateOnly date, List<AppointmentDto> booked, CancellationToken ct)
    {
        var candidates = location.Offered.Where(t => t.Duration <= MaxSpaDayStepMinutes).ToList();
        var chosen = new List<OfferedTreatment>();
        for (var wanted = Math.Min(run.Random.Next(2, 4), candidates.Count); chosen.Count < wanted;)
        {
            var treatment = PickTreatment(run, candidates);
            if (!chosen.Contains(treatment))
            {
                chosen.Add(treatment);
            }
        }

        var guest = Guid.NewGuid();
        List<GroupBookingItem> probe = [.. chosen.Select((t, step) => new GroupBookingItem(t.Id, guest, step))];
        var slots = await ops.FreeGroupSlotsAsync(location.Location.Id, new GroupAvailabilityRequest(date, probe, SpaDayBreakMinutes), ct);
        if (slots.Count == 0)
        {
            return;
        }

        var start = run.Pick(slots).Start;
        SetBookingTime(run, start);
        if (run.CustomerForAppointment(start) is not { } customerId)
        {
            return;
        }

        var request = new GroupBookingRequest(location.Location.Id, start, [.. chosen.Select((t, step) => new GroupBookingItem(t.Id, customerId, step))],
            SpaDayBreakMinutes);
        if (await ops.BookGroupAsync(request, ct) is BookingOutcome.GroupBooked { Result: var day })
        {
            booked.AddRange(day.Appointments);
        }
    }

    /// <summary>Behandlung nach Gewicht: beliebte kommen öfter dran.</summary>
    private static OfferedTreatment PickTreatment(Run run, IReadOnlyList<OfferedTreatment> candidates)
    {
        var pick = run.Random.Next(candidates.Sum(t => t.Weight));
        return candidates.First(t => (pick -= t.Weight) < 0);
    }

    /// <summary>Stellt die Uhr auf den Zeitpunkt, zu dem die Buchung eingegangen ist: Tage bis Stunden vor dem Termin, spätestens jetzt.</summary>
    private static void SetBookingTime(Run run, DateTimeOffset start)
    {
        var createdAt = start < run.Now
            ? start.AddDays(-run.Random.Next(1, 22)).AddHours(-run.Random.Next(0, 9))
            : run.Now.AddDays(-run.Random.Next(0, 15)).AddHours(-run.Random.Next(0, 10));
        run.Clock.Now = createdAt < run.Now ? createdAt : run.Now;
    }

    // ---------- Kassa ----------

    private static async Task SettleDayAsync(IDemoOps pos, Run run, DemoLocation location, List<LocationOpeningHours> hours,
        DateOnly date, List<AppointmentDto> appointments, CancellationToken ct)
    {
        var till = run.Tills[location.Location.Id];
        var user = location.ReceptionUserId;
        var opens = LocalTime.ToUtc(date, hours[0].Opens, location.Zone);
        var closes = LocalTime.ToUtc(date, hours[^1].Closes, location.Zone);
        var open = (int)(closes - opens).TotalMinutes;
        var vienna = location.Location.Name.StartsWith("Wien", StringComparison.Ordinal);

        var events = new List<(DateTimeOffset At, Func<Task> Run)>();
        foreach (var appointment in appointments.Where(a => a.Status == AppointmentStatus.Completed))
        {
            events.Add((Min(appointment.End.AddMinutes(run.Random.Next(3, 16)), closes.AddMinutes(10)), () => Timed(run, "Kassa: Termin kassieren", () => CheckoutAppointmentAsync(pos, run, till, location, date, appointment, user, ct))));
        }

        var saturday = date.DayOfWeek == DayOfWeek.Saturday;
        for (var i = run.Random.Next(saturday ? 1 : 0, saturday ? 4 : 3); i > 0; i--)
        {
            events.Add((opens.AddMinutes(run.Random.Next(open)), () => Timed(run, "Kassa: Gutschein verkaufen", () => SellVoucherAsync(pos, run, till, location, user, ct))));
        }

        for (var i = run.Random.Next(0, 3); i > 0; i--)
        {
            events.Add((opens.AddMinutes(run.Random.Next(open)), () => Timed(run, "Kassa: Artikelverkauf", () => SellArticlesAsync(pos, run, till, false, user, ct))));
        }

        for (var i = vienna ? run.Random.Next(1, saturday ? 7 : 4) : 0; i > 0; i--)
        {
            events.Add((opens.AddMinutes(run.Random.Next(open)), () => Timed(run, "Kassa: Saunaeintritt", () => SellArticlesAsync(pos, run, till, true, user, ct))));
        }

        // Morgens Wechselgeld bzw. Abschöpfung, abends der Tagesabschluss; heute nur, was schon passiert ist.
        var morning = opens.AddMinutes(-20);
        if (morning <= run.Now)
        {
            using var cashTimer = run.Timings.Measure("Kassa: Wechselgeld, Abschöpfung");
            run.Clock.Now = morning;
            if (!till.Started)
            {
                await pos.AddCashMovementAsync(till.Id, new CashMovementInput(CashMovementKind.Deposit, Float, "Wechselgeld"), user, supervisor: false, ct);
                till.Started = true;
            }
            else if (till.Counted > Float + 50)
            {
                await pos.AddCashMovementAsync(till.Id, new CashMovementInput(CashMovementKind.Withdrawal, till.Counted - Float, "Abschöpfung zur Bank"),
                    location.ManagerUserId ?? user, supervisor: true, ct);
            }
        }

        // Eine Aktion kann die Uhr weiterstellen (Storno und Neuzahlung nach drei Minuten); die nächste Aktion beginnt frühestens dann,
        // sonst bekäme ein späterer Beleg einen früheren Zeitpunkt als sein Vorgänger.
        var last = DateTimeOffset.MinValue;
        foreach (var (at, action) in events.Where(e => e.At <= run.Now).OrderBy(e => e.At))
        {
            run.Clock.Now = Max(at, last);
            await action();
            last = run.Clock.Now;
        }

        var evening = closes.AddMinutes(20);
        if (evening <= run.Now)
        {
            run.Clock.Now = evening;
            using var closingTimer = run.Timings.Measure("Kassa: Tagesabschluss");
            var report = await pos.DayReportAsync(till.Id, null, user, ct);
            var difference = run.Chance(0.08) ? (run.Chance(0.6) ? -1 : 1) * run.Random.Next(10, 600) / 100m : 0m;
            var counted = Math.Max(0, report.Value!.ExpectedCash + difference);
            await pos.DayReportAsync(till.Id, new DayClosingInput(counted, difference == 0 ? null : "Differenz beim Zählen, nicht geklärt"), user, ct);
            till.Counted = counted;
        }
    }

    private static async Task Timed(Run run, string name, Func<Task> action)
    {
        using (run.Timings.Measure(name))
        {
            await action();
        }
    }

    private static async Task CheckoutAppointmentAsync(IDemoOps pos, Run run, Till till, DemoLocation location, DateOnly date, AppointmentDto appointment,
        string? user, CancellationToken ct)
    {
        // Gelegentlich Stammkundenrabatt; dann die Position selbst anlegen statt sie aus dem Termin zu übernehmen.
        var discount = run.Chance(0.05) ? decimal.Round(appointment.Price * 0.1m, 2) : 0m;
        var bill = await pos.CreateBillAsync(new BillInput(till.Id, appointment.CustomerId, discount == 0 ? [appointment.Id] : []), ct);
        if (bill.Value is null)
        {
            return;
        }

        if (discount > 0)
        {
            await pos.AddLineAsync(bill.Value.Id, new BillLineInput(BillLineKind.Appointment, appointment.Id, null, 1, null, discount, null, null, null, null, null), supervisor: true, ct);
        }

        if (run.Chance(0.12))
        {
            await pos.AddLineAsync(bill.Value.Id, Line(run.Pick(run.Articles.Where(a => !a.SaunaPass).ToList()), 1), supervisor: true, ct);
        }

        // Läuft am Tag eine Aktion für diese Behandlung, klickt die Rezeption sie an (ohne Zufall: das tut sie immer, wenn die Aktion passt).
        var promoted = discount == 0 ? await ApplyPromotionAsync(pos, run, bill.Value.Id, location, date, appointment, ct) : 0m;
        var total = await pos.OpenAmountAsync(bill.Value.Id, ct);
        var payments = new List<PaymentInput>();
        SoldVoucher? used = null;
        decimal credit = 0;
        // Gutscheine werden meist erst Wochen nach dem Kauf eingelöst, viele sind noch offen.
        var redeemable = run.Vouchers.Where(v => v.Balance > 0 && v.SoldAt < run.Clock.Now.AddDays(-10)).ToList();
        if (redeemable.FirstOrDefault(v => v.Kind == VoucherKind.SinglePurpose && v.TreatmentId == appointment.TreatmentId) is { } single
            && run.Chance(0.3))
        {
            // Einzweckgutschein: der Server rechnet den Betrag aus der passenden Behandlung.
            (used, credit) = (single, Math.Min(single.Balance, appointment.Price - discount - promoted));
            payments.Add(new PaymentInput(PaymentMethod.Voucher, credit, single.Code, null));
        }
        else if (run.Chance(0.05) && redeemable.FirstOrDefault(v => v.Kind == VoucherKind.MultiPurpose) is { } multi)
        {
            (used, credit) = (multi, Math.Min(multi.Balance, total));
            payments.Add(new PaymentInput(PaymentMethod.Voucher, credit, multi.Code, null));
        }

        payments.AddRange(Pay(run, total - credit));
        var receipt = await CheckoutAsync(pos, run, bill.Value.Id, payments, total, user, ct);
        if (receipt is null)
        {
            return;
        }

        if (used is not null && (receipt.Payments.Any(p => p.Method == PaymentMethod.Voucher) || receipt.Lines.Any(l => l.Amount < 0)))
        {
            used.Balance -= credit;
        }

        // Etwa jeder sechzigste Beleg wird gleich wieder storniert, weil die Zahlungsart falsch erfasst war, und neu bezahlt.
        if (used is null && receipt.Payments is [{ Method: PaymentMethod.Card }] && run.Chance(1.0 / 60))
        {
            run.Clock.Now = run.Clock.Now.AddMinutes(2);
            if ((await pos.VoidAsync(receipt.Id, "Falsche Zahlungsart erfasst, Gast hat bar bezahlt", user, supervisor: true, ct)).Value is not null)
            {
                run.Receipts++;
                run.Clock.Now = run.Clock.Now.AddMinutes(1);
                await CheckoutAsync(pos, run, bill.Value.Id, [new PaymentInput(PaymentMethod.Cash, total, null, null)], total, user, ct);
            }
        }
    }

    /// <summary>
    /// Wendet die erste laufende Aktion an, die zur Behandlung des Termins passt; gibt den Rabatt zurück (0 ohne Aktion oder wenn die Kassa sie ablehnt,
    /// etwa wegen eines Kundenrabatts).
    /// </summary>
    private static async Task<decimal> ApplyPromotionAsync(IDemoOps pos, Run run, Guid billId, DemoLocation location, DateOnly date, AppointmentDto appointment,
        CancellationToken ct)
    {
        var promotion = run.Promotions.FirstOrDefault(p => PromotionRules.IsRunning(p, location.Location.Id, date)
            && (p.Scope == PromotionScope.AllTreatments || (p.Scope == PromotionScope.SelectedTreatments && p.Treatments.Any(t => t.TreatmentId == appointment.TreatmentId))));
        if (promotion is null)
        {
            return 0m;
        }

        var applied = await pos.ApplyPromotionAsync(billId, promotion.Id, ct);
        return applied.Value?.Lines.Sum(l => l.Discount) ?? 0m;
    }

    private static async Task SellVoucherAsync(IDemoOps pos, Run run, Till till, DemoLocation location, string? user, CancellationToken ct)
    {
        var bill = await pos.CreateBillAsync(new BillInput(till.Id, run.Customer(), []), ct);
        if (bill.Value is null)
        {
            return;
        }

        var popular = location.Offered.Where(t => t.Weight > 3).ToList();
        var line = run.Chance(0.35) && popular.Count > 0
            ? new BillLineInput(BillLineKind.Voucher, null, null, 1, null, 0, null, null, null, VoucherKind.SinglePurpose, run.Pick(popular).Id)
            : new BillLineInput(BillLineKind.Voucher, null, null, 1, run.Pick([50m, 80m, 100m, 100m, 120m, 150m, 200m]), 0, null, null, null,
                VoucherKind.MultiPurpose, null);
        var added = await pos.AddLineAsync(bill.Value.Id, line, supervisor: true, ct);
        if (added.Value is null)
        {
            await pos.DeleteBillAsync(bill.Value.Id, ct);
            return;
        }

        var receipt = await CheckoutAsync(pos, run, bill.Value.Id, Pay(run, added.Value.Open), added.Value.Open, user, ct);
        run.Vouchers.AddRange((receipt?.IssuedVouchers ?? []).Select(v => new SoldVoucher(v.Code, v.Kind, v.TreatmentId, v.Balance, run.Clock.Now)));
    }

    private static async Task SellArticlesAsync(IDemoOps pos, Run run, Till till, bool sauna, string? user, CancellationToken ct)
    {
        var bill = await pos.CreateBillAsync(new BillInput(till.Id, run.Chance(0.4) ? run.Customer() : null, []), ct);
        if (bill.Value is null)
        {
            return;
        }

        var article = sauna ? run.Articles.First(a => a.SaunaPass) : run.Pick(run.Articles.Where(a => !a.SaunaPass).ToList());
        var added = await pos.AddLineAsync(bill.Value.Id, Line(article, run.Chance(0.25) ? 2 : 1), supervisor: true, ct);
        if (added.Value is not null)
        {
            await CheckoutAsync(pos, run, bill.Value.Id, Pay(run, added.Value.Open), added.Value.Open, user, ct);
        }
    }

    private static BillLineInput Line(Article article, int quantity) =>
        new(BillLineKind.Article, null, article.Id, quantity, null, 0, null, null, null, null, null);

    /// <summary>Zahlungsarten wie an einer Wiener Rezeption: meist Karte, oft bar, manchmal Sofortüberweisung oder geteilt.</summary>
    private static List<PaymentInput> Pay(Run run, decimal amount)
    {
        if (amount <= 0)
        {
            return [];
        }

        var r = run.Random.NextDouble();
        if (r < 0.58)
        {
            return [new(PaymentMethod.Card, amount, null, $"Terminal {run.Random.Next(100000, 999999)}")];
        }

        if (r < 0.86)
        {
            // Bar meist aufgerundet gegeben; das Rückgeld rechnet die Kassa.
            var given = run.Random.Next(3) switch { 0 => amount, 1 => Math.Ceiling(amount / 10) * 10, _ => Math.Ceiling(amount / 50) * 50 };
            return [new(PaymentMethod.Cash, given, null, null)];
        }

        if (r < 0.93 || amount < 20)
        {
            return [new(PaymentMethod.InstantTransfer, amount, null, "QR-Überweisung")];
        }

        // Rechnung geteilt: ein Teil auf Karte, der Rest bar.
        var card = Math.Round(amount / 2, 0);
        return [new(PaymentMethod.Card, card, null, $"Terminal {run.Random.Next(100000, 999999)}"), new(PaymentMethod.Cash, amount - card, null, null)];
    }

    private static async Task<ReceiptDto?> CheckoutAsync(IDemoOps pos, Run run, Guid billId, List<PaymentInput> payments, decimal open,
        string? user, CancellationToken ct)
    {
        var result = await pos.CheckoutAsync(billId, new CheckoutRequest(null, payments), user, ct);
        if (result.Value is null && open > 0)
        {
            // Sollte nicht vorkommen; wenn doch (z. B. Gutschein schon verbraucht), zahlt der Gast mit Karte.
            result = await pos.CheckoutAsync(billId, new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Card, open, null, null)]), user, ct);
        }

        if (result.Value is not null)
        {
            run.Receipts++;
        }

        return result.Value;
    }

    /// <summary>Der nächste solche Wochentag ab <paramref name="from"/> (einschließlich).</summary>
    public static DateOnly Next(DayOfWeek day, DateOnly from) => from.AddDays(((int)day - (int)from.DayOfWeek + 7) % 7);

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    [LoggerMessage(Level = LogLevel.Information, Message = "Beispieldaten: bis {Date:dd.MM.yyyy} {Appointments} Termine, {Receipts} Belege ({Seconds:0} s)")]
    private static partial void Progress(ILogger logger, DateOnly date, int appointments, int receipts, double seconds);
}
