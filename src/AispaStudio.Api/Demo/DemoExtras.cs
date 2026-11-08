using System.Globalization;
using System.Security.Cryptography;
using AispaStudio.Api.Data;
using AispaStudio.Api.HrFile;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Online;
using AispaStudio.Api.Retention;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Staff;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Was um den Kalender herum dazugehört und sich auf „jetzt“ bezieht: Anamnesebögen, Kundenkonten, Warteliste, online gekaufte
/// Gutscheine, eine Ausfallgebühr in Salzburg, Anzahlungen in Graz und ein Posteingang mit Anfragen per Mail, SMS, WhatsApp, Anruf und Treatwell, die die Terminannahme gleich bearbeitet.
/// </summary>
internal static class DemoExtras
{
    /// <summary>Stammgäste mit Kundenkonto; der Anmeldecode kommt in der Entwicklung im Test-Postfach (Mailpit) an.</summary>
    public static readonly string[] CustomerAccountEmails = ["anna.bauer@example.at", "thomas.leitner@example.at", "julia.novak@example.at"];

    public static async Task AddAsync(IServiceProvider root, Guid tenantId, DateTimeOffset now, CancellationToken ct)
    {
        var random = new Random(1910);
        await using var scope = DemoSeeder.Open(root, tenantId);
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AispaDbContext>();
        var online = services.GetRequiredService<OnlineDbContext>();
        var customers = await db.Customers.AsNoTracking().OrderBy(c => c.CreatedAt).ToListAsync(ct);
        var byEmail = customers.Where(c => c.Email is not null).ToDictionary(c => c.Email!);
        var locations = await db.Locations.AsNoTracking().OrderBy(l => l.Name).ToDictionaryAsync(l => l.Name.Split(' ')[0], ct);
        var treatments = await db.Treatments.AsNoTracking().ToDictionaryAsync(t => t.Name, ct);
        var zone = LocalTime.Zone(locations["Wien"].TimeZone);
        var today = LocalTime.LocalDate(now, zone);

        await AddAnamnesesAsync(services, random, customers, now, ct);
        await AddHrFileAsync(services, db, today, ct);
        await AddBirthdayTodayAsync(db, locations["Wien"], today, zone, ct);

        var accounts = services.GetRequiredService<CustomerAccounts>();
        foreach (var email in CustomerAccountEmails)
        {
            await accounts.FindOrCreateAsync(email, ct);
        }

        // Salzburg verlangt online von neuen Gästen eine Karte: 50 % Ausfallgebühr bei Nichterscheinen oder Absage nach 24 Stunden;
        // ohne Karte binnen 24 Stunden wird der Termin freigegeben.
        var bookingOptions = services.GetRequiredService<IOptions<OnlineBookingOptions>>().Value;
        db.LocationOnlineSettings.Add(new LocationOnlineSettings
        {
            LocationId = locations["Salzburg"].Id,
            MinLeadMinutes = bookingOptions.MinLeadMinutes,
            MaxDaysAhead = bookingOptions.MaxDaysAhead,
            CancellationDeadlineHours = 24,
            SlotStepMinutes = bookingOptions.SlotStepMinutes,
            NoShowFeePercent = 50,
            CardHoldHours = 24,
            NoShowFeeNewCustomersOnly = true,
            CancellationPolicy = "Absage bis 24 Stunden vorher ist kostenlos. Danach oder bei Nichterscheinen verlangen wir von neuen Gästen 50 % des Preises.",
        });

        // Graz nimmt online 30 % Anzahlung (Anzahlungsbeleg der Kassa, beim Kassieren abgezogen); ohne Zahlung binnen 24 Stunden wird
        // der Termin freigegeben. Das Hamam-Ritual ist überall voraus zu bezahlen, wo der Standort Anzahlungen annehmen kann.
        db.LocationOnlineSettings.Add(new LocationOnlineSettings
        {
            LocationId = locations["Graz"].Id,
            MinLeadMinutes = bookingOptions.MinLeadMinutes,
            MaxDaysAhead = bookingOptions.MaxDaysAhead,
            CancellationDeadlineHours = 48,
            SlotStepMinutes = bookingOptions.SlotStepMinutes,
            DepositPercent = 30,
            CardHoldHours = 24,
            CancellationPolicy = "Absage bis 48 Stunden vorher ist kostenlos und die Anzahlung wird erstattet; danach bleibt sie beim Spa.",
        });
        var hamam = await db.Treatments.FirstAsync(t => t.Name == "Hamam-Ritual mit Schaumwaschung", ct);
        hamam.DepositPercent = 100;
        await db.SaveChangesAsync(ct);
        await AddReceptionDepositAsync(services, db, locations["Graz"].Id, now, ct);

        // Warteliste: beliebte Termine am Abend und am Wochenende sind knapp.
        var privacy = bookingOptions.PrivacyNoticeVersion;
        (int Customer, string Location, string Treatment, int From, int Days, DayPart Part)[] waiting =
        [
            (20, "Wien", "Lava-Hot-Stone-Massage (60 Min.)", 1, 10, DayPart.Evening),
            (31, "Wien", "Hamam-Ritual mit Schaumwaschung", 2, 14, DayPart.Any),
            (44, "Wien", "AISPA Signaturmassage", 1, 6, DayPart.Afternoon),
            (57, "Salzburg", "Zirben-Ritual", 3, 21, DayPart.Any),
            (63, "Salzburg", "Klassische Massage", 1, 5, DayPart.Morning),
            (78, "Graz", "Lava-Hot-Stone-Massage (90 Min.)", 2, 12, DayPart.Evening),
        ];
        foreach (var (customer, location, treatment, from, days, part) in waiting)
        {
            online.WaitlistEntries.Add(new WaitlistEntry
            {
                CustomerId = customers[customer].Id,
                LocationId = locations[location].Id,
                TreatmentId = treatments[treatment].Id,
                From = today.AddDays(from),
                Until = today.AddDays(from + days),
                DayPart = part,
                Language = "de",
                CreatedAt = now.AddDays(-random.Next(1, 6)),
                PrivacyAcceptedAt = now.AddDays(-6),
                PrivacyNoticeVersion = privacy,
                Status = WaitlistStatus.Waiting,
            });
        }

        // Zwei online bezahlte Gutscheine; ausgestellt werden sie wie im Betrieb über die Queue an der Kasse in Wien.
        var orders = new[]
        {
            NewOrder(locations["Wien"].Id, byEmail["anna.bauer@example.at"].Id, VoucherKind.MultiPurpose, 120m, null,
                "anna.bauer@example.at", "Mama", "Alles Liebe zum Geburtstag!", now, privacy),
            NewOrder(locations["Salzburg"].Id, byEmail["thomas.leitner@example.at"].Id, VoucherKind.SinglePurpose,
                treatments["Zirben-Ritual"].Price, treatments["Zirben-Ritual"].Id, "thomas.leitner@example.at", "Sabine", null, now, privacy),
        };
        online.VoucherOrders.AddRange(orders);
        await online.SaveChangesAsync(ct);
        var bus = services.GetRequiredService<IMessageBus>();
        foreach (var order in orders)
        {
            await bus.PublishAsync(new IssueOnlineVoucher(order.Id));
        }

        await AddInboxAsync(services, db, byEmail, today, now, ct);
        await AddRebookingAsync(services, db, customers, now, ct);
        await AddStaffRegulationsAsync(db, today, ct);
        await AddStaffDutiesAsync(db, today, ct);
    }

    /// <summary>
    /// Zuständigkeiten und Zertifikate (R05 Slice 117): Lena Huber ist Ersthelferin, ihr Kurs läuft in 40 Tagen ab (Erinnerung sichtbar); Marco Berger ist Ersthelfer mit abgelaufenem Kurs
    /// (Warnung sichtbar); Elif Demir hat Hygiene mit Vertretung Jana Wolf. Die Zuständigkeit Lager bleibt frei, sonst sähe die Inhaberin im Demo keine Lageraufgaben mehr.
    /// </summary>
    private static async Task AddStaffDutiesAsync(AispaDbContext db, DateOnly today, CancellationToken ct)
    {
        await StaffDutyModel.EnsureDefaultsAsync(db, ct);
        var types = await db.Set<DutyType>().AsNoTracking().Where(t => t.Key != null).ToDictionaryAsync(t => t.Key!.Value, t => t.Id, ct);
        var people = await db.Employees.AsNoTracking()
            .Where(e => e.DisplayName == "Lena Huber" || e.DisplayName == "Marco Berger" || e.DisplayName == "Elif Demir" || e.DisplayName == "Jana Wolf")
            .ToDictionaryAsync(e => e.DisplayName, e => e.Id, ct);
        db.Add(new StaffDuty { EmployeeId = people["Lena Huber"], DutyTypeId = types[DutyTypeKey.FirstAid], ValidFrom = today.AddDays(-365) });
        db.Add(new StaffDuty { EmployeeId = people["Marco Berger"], DutyTypeId = types[DutyTypeKey.FirstAid], ValidFrom = today.AddDays(-365) });
        db.Add(new StaffDuty { EmployeeId = people["Elif Demir"], DutyTypeId = types[DutyTypeKey.Hygiene], ValidFrom = today.AddDays(-180), DeputyId = people["Jana Wolf"] });
        db.Add(new StaffCertificate
        {
            EmployeeId = people["Lena Huber"],
            Name = "Erste-Hilfe-Kurs",
            IssuedOn = today.AddDays(40).AddYears(-2),
            ValidUntil = today.AddDays(40),
            IssuedBy = "Rotes Kreuz",
        });
        db.Add(new StaffCertificate
        {
            EmployeeId = people["Marco Berger"],
            Name = "Erste-Hilfe-Kurs",
            IssuedOn = today.AddDays(-20).AddYears(-2),
            ValidUntil = today.AddDays(-20),
            IssuedBy = "Rotes Kreuz",
        });
        db.Add(new StaffCertificate { EmployeeId = people["Elif Demir"], Name = "Hygieneschulung", IssuedOn = today.AddDays(-180), IssuedBy = "WIFI Wien" });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Regelungen und Vollmachten (R05 Slice 111): Lena Huber mit Sonderregelung und Hinweis als Symbol. Erst nach allen Buchungen, damit die Warnung
    /// den Verlauf nicht stoppt. Nur Vollmachten zur Anzeige: Eine Kassa-Storno-Vollmacht würde die Prüfung im ganzen Demo-Spa scharf schalten.
    /// </summary>
    private static async Task AddStaffRegulationsAsync(AispaDbContext db, DateOnly today, CancellationToken ct)
    {
        var lena = await db.Employees.AsNoTracking().Where(e => e.DisplayName == "Lena Huber").Select(e => e.Id).FirstAsync(ct);
        db.Add(new StaffRule
        {
            EmployeeId = lena,
            Kind = StaffRuleKind.NoAppointmentsAfter,
            ValidFrom = today.AddDays(-14),
            ValidUntil = new DateOnly(today.Year, 12, 31),
            LatestEnd = new TimeOnly(16, 0),
            Reason = "Kinderbetreuung am Nachmittag",
        });
        db.Add(new StaffNote { EmployeeId = lena, Text = "Spricht Englisch und Italienisch; übernimmt gern internationale Gäste.", Visibility = StaffNoteVisibility.Team, ShowAsSymbol = true });
        db.Add(new StaffPower { EmployeeId = lena, Kind = StaffPowerKind.Keys, ValidFrom = today.AddDays(-60), GrantedBy = "Inhaberin" });
        db.Add(new StaffPower { EmployeeId = lena, Kind = StaffPowerKind.Safe, ValidFrom = today.AddDays(-60), ValidUntil = today.AddDays(-1), GrantedBy = "Inhaberin" });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Anzahlung an der Rezeption in Graz: für den nächsten gebuchten Termin bar bezahlt, mit Anzahlungsbeleg wie im Betrieb. Beim
    /// Kassieren der Behandlung zeigt die Kassa „Anzahlung abgezogen“.
    /// </summary>
    private static async Task AddReceptionDepositAsync(IServiceProvider services, AispaDbContext db, Guid locationId, DateTimeOffset now, CancellationToken ct)
    {
        var register = await db.CashRegisters.AsNoTracking().Where(r => r.LocationId == locationId && r.IsActive).OrderBy(r => r.Code).FirstOrDefaultAsync(ct);
        var appointment = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == locationId && a.Status == AppointmentStatus.Booked && a.Start > now.AddDays(2) && a.Price >= 80)
            // Bei gleichem Beginn entscheiden Gast und Behandlung, nie die Kennung: beide Wege des Einspielens müssen denselben Termin wählen (T-033).
            .OrderBy(a => a.Start).ThenBy(a => a.Customer.Email).ThenBy(a => a.Customer.LastName).ThenBy(a => a.Customer.FirstName).ThenBy(a => a.Treatment.Name).FirstOrDefaultAsync(ct);
        if (register is null || appointment is null)
        {
            return;
        }

        var pos = ActivatorUtilities.CreateInstance<Pos.PosService>(services, new DemoClock(now));
        var bill = await pos.CreateBillAsync(new BillInput(register.Id, appointment.CustomerId, []), ct);
        if (bill.Value is null)
        {
            return;
        }

        var amount = decimal.Round(appointment.Price * 0.3m, 2);
        var line = await pos.AddLineAsync(bill.Value.Id, new BillLineInput(BillLineKind.Deposit, appointment.Id, null, 1, amount, 0, null, null, null, null, null), supervisor: false, ct);
        if (line.Value is null)
        {
            await pos.DeleteBillAsync(bill.Value.Id, ct);
            return;
        }

        await pos.CheckoutAsync(bill.Value.Id, new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, amount, null, null)]), null, ct);
    }

    /// <summary>
    /// Erinnerung an die nächste Behandlung: eingeschaltet für Massagen, Gesicht sowie Hände und Füße; jeder dritte Gast mit Mailadresse
    /// hat am Empfang eingewilligt und bestätigt. Der erste Lauf geht wie im Betrieb über den Planer und die Queue.
    /// </summary>
    private static async Task AddRebookingAsync(IServiceProvider services, AispaDbContext db, List<Customer> customers, DateTimeOffset now, CancellationToken ct)
    {
        var days = new Dictionary<string, int> { ["Massagen"] = 42, ["Gesicht"] = 35, ["Hände und Füße"] = 28 };
        db.RebookSettings.Add(new RebookSettings { Enabled = true });
        db.TreatmentRebookIntervals.AddRange((await db.Treatments.AsNoTracking().ToListAsync(ct))
            .Where(t => t.Category is not null && days.ContainsKey(t.Category))
            .Select(t => new TreatmentRebookInterval { TreatmentId = t.Id, Days = days[t.Category!] }));
        db.MarketingConsents.AddRange(customers.Where(c => c.Email is not null).Where((_, i) => i % 3 == 0).Select(c => new MarketingConsent
        {
            CustomerId = c.Id,
            Email = c.Email!,
            Source = MarketingConsents.SourceTeam,
            State = MarketingConsentState.Confirmed,
            RequestedAt = now.AddDays(-60),
            ConfirmedAt = now.AddDays(-60).AddHours(3),
        }));
        await db.SaveChangesAsync(ct);
        await services.GetRequiredService<RebookPlanner>().PlanAsync(now, ct);
    }

    private static VoucherOrder NewOrder(Guid locationId, Guid customerId, VoucherKind kind, decimal amount, Guid? treatmentId, string email,
        string? recipient, string? message, DateTimeOffset now, string privacy) => new()
        {
            TokenHash = SHA256.HashData(RandomNumberGenerator.GetBytes(32)),
            LocationId = locationId,
            CustomerId = customerId,
            Kind = kind,
            Amount = amount,
            Currency = "EUR",
            TreatmentId = treatmentId,
            BuyerEmail = email,
            RecipientName = recipient,
            Message = message,
            Language = "de",
            CreatedAt = now.AddMinutes(-30),
            PrivacyAcceptedAt = now.AddMinutes(-30),
            PrivacyNoticeVersion = privacy,
            Status = VoucherOrderStatus.Paid,
            Provider = "Test",
            ProviderReference = $"test_{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6))}",
            PaidAt = now.AddMinutes(-28),
        };

    /// <summary>
    /// Ein Gast mit Geburtstag am Demo-Tag (Runde 5b, Ä45): der Gast mit dem ersten Termin des Tages in Wien wird heute 42. So zeigen Kalender (Geschenk am Termin), Hover-Karte,
    /// Tagesüberblick und die Frage „Wer hat heute Geburtstag?“ gleich etwas. Läuft nach dem Verlauf, damit der Gast sicher einen Termin heute hat.
    /// </summary>
    private static async Task AddBirthdayTodayAsync(AispaDbContext db, Location vienna, DateOnly today, TimeZoneInfo zone, CancellationToken ct)
    {
        var (start, end) = LocalTime.Day(today, zone);
        var customerId = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == vienna.Id && a.Start >= start && a.Start < end && a.PlaceholderGuest == null && a.Status != AppointmentStatus.Cancelled)
            // Bei gleichem Beginn entscheidet der Name, nie die Kennung: beide Wege des Verlaufs müssen denselben Gast wählen.
            .OrderBy(a => a.Start).ThenBy(a => a.Customer.LastName).ThenBy(a => a.Customer.FirstName).Select(a => (Guid?)a.CustomerId).FirstOrDefaultAsync(ct);
        if (customerId is { } id)
        {
            (await db.Customers.FirstAsync(c => c.Id == id, ct)).BirthDate = today.AddYears(-42);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Anamnesebögen der Stammgäste, über alle drei Wege; wenige mit Angaben, die die Behandlung betreffen.</summary>
    private static async Task AddAnamnesesAsync(IServiceProvider services, Random random, List<Customer> customers, DateTimeOffset now, CancellationToken ct)
    {
        if (!services.GetRequiredService<HealthDataProtector>().IsConfigured)
        {
            return;
        }

        (string Key, string Details)[] findings =
        [
            ("allergies", "Nussöle (Mandel, Macadamia)"), ("allergies", "Lavendel, Duftstoffe allgemein"), ("skin", "Neurodermitis an den Armen"),
            ("circulation", "Niedriger Blutdruck, nach der Behandlung langsam aufstehen"), ("pregnancy", "24. Woche"),
            ("medication", "Blutverdünner (Marcoumar)"), ("surgery", "Knie-OP links vor vier Monaten"),
        ];
        var clock = new DemoClock(now);
        var store = ActivatorUtilities.CreateInstance<AnamnesisStore>(services, clock);
        foreach (var customer in customers.Take(70))
        {
            var found = random.Next(5) == 0 ? findings[random.Next(findings.Length)] : default;
            var answers = AnamnesisForm.Keys
                .Select(key => AnamnesisForm.IsYesNo(key)
                    ? new AnamnesisAnswer(key, key == found.Key, key == found.Key ? found.Details : null)
                    : new AnamnesisAnswer(key, null, random.Next(6) == 0 ? "Bitte nicht zu fest am Nacken." : null))
                .ToList();
            clock.Now = now.AddDays(-random.Next(2, 330)).AddMinutes(-random.Next(600));
            var channel = random.Next(10) switch { < 6 => "Link", < 9 => "Tablet", _ => "Papier" };
            await store.SaveAsync(answers, $"{customer.FirstName} {customer.LastName}", "de", customer.Id, null, channel, ct);
        }
    }

    /// <summary>Personalakte der ersten Person (R05 Slice 110): ein Gespräch mit heute fälligem Folgetermin, eine Anmerkung und eine Verwarnung; ohne Schlüssel keine, wie die Anamnesen.</summary>
    private static async Task AddHrFileAsync(IServiceProvider services, AispaDbContext db, DateOnly today, CancellationToken ct)
    {
        var protector = services.GetRequiredService<HealthDataProtector>();
        if (!protector.IsConfigured)
        {
            return;
        }

        var employeeId = await db.Employees.AsNoTracking().OrderBy(e => e.DisplayName).Select(e => e.Id).FirstAsync(ct);
        HrEntryInput[] inputs =
        [
            new(HrEntryKind.Conversation, today.AddDays(-90), "Ziele für das Jahr besprochen. Wünscht sich mehr Massagen am Vormittag.", HrConversationKind.Annual, "Leitung und Person",
                [new("Kurs Hot Stone buchen", false), new("Dienstplan im Frühling anpassen", true)], today),
            new(HrEntryKind.Note, today.AddDays(-30), "Hat in der Grippewelle zwei Wochen lang zuverlässig vertreten."),
            new(HrEntryKind.Warning, today.AddDays(-200), "Dreimal unentschuldigt zu spät zum Dienst.", IssuedBy: "Leitung", HandedOverInWriting: true),
        ];
        foreach (var input in inputs)
        {
            var entry = new HrEntry { EmployeeId = employeeId, Kind = input.Kind, Payload = [] };
            entry.Apply(input, protector);
            db.Add(entry);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Anfragen der letzten Stunden. Die Terminannahme bearbeitet sie sofort (ohne Schlüssel für Claude mit den Regeln) und
    /// schickt Antworten ins Test-Postfach; was sie nicht sicher versteht, bleibt für die Rezeption liegen.
    /// </summary>
    private static async Task AddInboxAsync(IServiceProvider services, AispaDbContext db, Dictionary<string, Customer> byEmail, DateOnly today,
        DateTimeOffset now, CancellationToken ct)
    {
        var de = CultureInfo.GetCultureInfo("de-AT");
        var tomorrow = today.AddDays(1);
        var saturday = DemoHistory.Next(DayOfWeek.Saturday, today.AddDays(1));
        var nextWeek = today.AddDays(9);
        var bauer = byEmail["anna.bauer@example.at"];
        // Mehrere Termine beginnen oft gleichzeitig, und beide Wege des Einspielens legen sie in anderer Reihenfolge an: ohne Tie-Break über
        // Inhalte sagte je Weg ein anderer Gast ab (T-033).
        var tomorrowAppointment = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.Status == AppointmentStatus.Booked && a.Customer.Email != null && a.Start > now.AddHours(18) && a.Start < now.AddHours(42))
            .OrderBy(a => a.Start).ThenBy(a => a.Customer.Email).ThenBy(a => a.Customer.LastName).ThenBy(a => a.Customer.FirstName).ThenBy(a => a.Treatment.Name).FirstOrDefaultAsync(ct);

        var mails = new List<IncomingMail>
        {
            Mail(bauer.Email!, "Anna Bauer", "Termin Hot Stone", $"""
                Liebes AISPA-Team,

                hätten Sie am {saturday.ToString("dddd, d. MMMM", de)} am Nachmittag Zeit für eine Lava-Hot-Stone-Massage (60 Min.) in Wien?
                Am liebsten wieder bei Lena.

                Liebe Grüße
                Anna Bauer
                """, now.AddHours(-3)),
            Mail("james.miller@example.com", "James Miller", "Massage tomorrow?", $"""
                Hello,

                I'm visiting Vienna this week. Do you have time for a traditional Thai massage (60 min) tomorrow, {tomorrow.ToString("MMMM d", CultureInfo.GetCultureInfo("en-GB"))}, around 5 pm?

                Thanks,
                James Miller
                +44 7700 900123
                """, now.AddHours(-2)),
            Mail("verena.koller@example.at", "Verena Koller", "Gutschein und Parken", """
                Grüß Gott,

                kann man bei Ihnen Gutscheine auch für eine bestimmte Behandlung kaufen, und gibt es in der Nähe des Spas in Salzburg Parkplätze?

                Danke und schöne Grüße
                Verena Koller
                """, now.AddMinutes(-95)),
            Mail("buchungen@treatwell.at", "Treatwell", "Neue Buchung über Treatwell", $"""
                Hallo,

                Sie haben eine neue Buchung über Treatwell erhalten.

                Buchungsnummer: TW-{now:MMdd}-4711
                Salon: Wien Innere Stadt
                Leistung: Klassische Massage
                Datum: {nextWeek.ToString("dddd, d. MMMM yyyy", de)}
                Uhrzeit: 11:00 Uhr
                Kunde: Miriam Hollaus
                Telefon: +43 660 5550123
                E-Mail: miriam.hollaus@example.at

                Ihr Treatwell-Team
                """, now.AddMinutes(-70)),
        };
        if (tomorrowAppointment is { Customer.Email: { } email } appointment)
        {
            var local = TimeZoneInfo.ConvertTime(appointment.Start, LocalTime.Zone("Europe/Vienna"));
            mails.Add(Mail(email, $"{appointment.Customer.FirstName} {appointment.Customer.LastName}", "Absage Termin morgen", $"""
                Hallo,

                leider muss ich meinen Termin morgen um {local:HH:mm} Uhr ({appointment.Treatment.Name}) absagen, ich bin krank.
                Ich melde mich wieder, sobald es mir besser geht.

                Viele Grüße
                {appointment.Customer.FirstName} {appointment.Customer.LastName}
                """, now.AddMinutes(-40)));
        }

        mails.Add(Message(BookingChannel.Sms, "+43 676 5550198", "Petra Haas",
            "Hallo, gibt es morgen ab 17 Uhr noch eine Rückenmassage in Wien? LG Petra Haas", now.AddMinutes(-25)));
        mails.Add(Message(BookingChannel.WhatsApp, "+49 151 5550147", "Tobias Berger",
            $"Hi! Habt ihr am {saturday:dd.MM.} in Salzburg Zeit für eine klassische Massage, egal wann? Danke, Tobias", now.AddMinutes(-15)));
        mails.Add(Message(BookingChannel.Phone, "+43 664 5550172", null,
            "Anruf: Guten Tag, hier spricht Ingrid Lang. Ich hätte gerne nächste Woche eine Pediküre in Wien, am besten Dienstag Vormittag. Bitte um Rückruf.",
            now.AddMinutes(-8)));

        var ingestion = services.GetRequiredService<MailIngestion>();
        foreach (var mail in mails)
        {
            await ingestion.IngestAsync(mail, ct);
        }
    }

    private static IncomingMail Mail(string from, string name, string subject, string body, DateTimeOffset received) =>
        new($"<{Guid.NewGuid():N}@demo.aispa.test>", from, name, "termine@aispa.test", subject, body, received, SenderVerified: true);

    private static IncomingMail Message(BookingChannel channel, string from, string? name, string text, DateTimeOffset received) =>
        new($"<{Guid.NewGuid():N}@demo.aispa.test>", PhoneNumbers.Normalize(from), name, "+43100000000", MailIntakeProcessor.ChannelName(channel), text,
            received, SenderVerified: true, channel);
}
