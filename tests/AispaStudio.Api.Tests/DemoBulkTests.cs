using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Demo;
using AispaStudio.Api.Online;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Pos.Fiscal.Rksv;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Vergleich der beiden Wege, das Beispiel-Spa einzuspielen: über die Dienste (jeder Schritt wie im Betrieb) und im Speicher mit <c>COPY</c>.
/// Beide laufen mit derselben Uhr und demselben Zufall; sie müssen dieselben Termine, Belege, Gutscheine, Abschlüsse und Lagerbewegungen ergeben,
/// eine gültige Belegkette und dieselben Berichte.
/// </summary>
public sealed class DemoBulkTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Einspielen_im_Speicher_ergibt_dieselben_Daten_wie_ueber_die_Dienste()
    {
        var now = TimeProvider.System.GetUtcNow();
        using var servicesHost = await HostAsync();
        using var bulkHost = await HostAsync();

        var seeded = await Task.WhenAll(
            DemoSeeder.SeedAsync(servicesHost.Services, now, new DemoDataOptions { WeeksBack = 1, WeeksAhead = 1, Mode = DemoMode.Services }, NullLogger.Instance, Ct),
            DemoSeeder.SeedAsync(bulkHost.Services, now, new DemoDataOptions { WeeksBack = 1, WeeksAhead = 1, Mode = DemoMode.Bulk }, NullLogger.Instance, Ct));

        // Die Beispiel-Nachrichten verarbeitet die Queue der Terminannahme nach dem Einspielen; verglichen wird erst der fertige Stand.
        await Task.WhenAll(WaitForMailIntakeAsync(servicesHost, seeded[0]), WaitForMailIntakeAsync(bulkHost, seeded[1]));
        var expected = await FingerprintAsync(servicesHost, seeded[0], now);
        var actual = await FingerprintAsync(bulkHost, seeded[1], now);
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var table in expected.Keys.Order())
        {
            Assert.True(expected[table].Count > 0 || table is "Vouchers" or "AppointmentFollowUps", $"{table}: Dienste haben nichts angelegt");
            Assert.Equal(expected[table].Count, actual[table].Count);
            for (var i = 0; i < expected[table].Count; i++)
            {
                Assert.True(expected[table][i] == actual[table][i], $"{table}[{i}]:\nDienste:  {expected[table][i]}\nSpeicher: {actual[table][i]}");
            }
        }

        // Aktionen (ADR 0071): beide Wege legen dieselben zwei an, und die Kassa hat die laufende an Behandlungen ihrer Laufzeit angewendet.
        Assert.Equal(2, expected["Promotions"].Count);
        Assert.Equal(DemoBusinessContacts.Count, expected["BusinessContacts"].Count);
        Assert.Equal(5, expected["BusinessContacts"].Count(c => c.Contains("|Management|", StringComparison.Ordinal)));
        Assert.Contains(expected["Receipts"], r => r.Contains("Aktion Herbstzauber 10 %", StringComparison.Ordinal));
        Assert.Contains(actual["Receipts"], r => r.Contains("Aktion Herbstzauber 10 %", StringComparison.Ordinal));

        // Schließzeiten (ADR 0073): ein Betriebsurlaub in Salzburg in der Zukunft und ein beendeter Ausfall in Wien.
        Assert.Equal(2, expected["Closures"].Count);
        Assert.Contains(expected["Closures"], c => c.StartsWith("Salzburg Altstadt|", StringComparison.Ordinal) && c.Contains("|Vacation|", StringComparison.Ordinal));
        Assert.Contains(expected["Closures"], c => c.StartsWith("Wien Innere Stadt|", StringComparison.Ordinal) && c.Contains("|Outage|", StringComparison.Ordinal));

        // Raumsperre (ADR 0076): der Raum Lotus in Wien ist in den nächsten Tagen wegen Wartung gesperrt, auf beiden Wegen gleich.
        Assert.Single(expected["RoomClosures"]);
        Assert.StartsWith("Wien Innere Stadt|Raum Lotus|", expected["RoomClosures"][0], StringComparison.Ordinal);
        Assert.Contains("|Maintenance|", expected["RoomClosures"][0], StringComparison.Ordinal);
        Assert.NotEqual("0", expected["RoomClosures"][0].Split('|')[^1]);

        // Änderungsverlauf (ADR 0082): jeder Termin ist angelegt, einige sind verschoben, und Storno sowie Nicht erschienen nennen die Rezeption.
        Assert.Equal(expected["Appointments"].Count, expected["History"].Count(h => h.Contains("|I|", StringComparison.Ordinal)));
        Assert.Contains(expected["History"], h => h.Contains("{\"Start\": [", StringComparison.Ordinal));
        Assert.Contains(expected["History"], h => h.Contains("\"NoShow\"]", StringComparison.Ordinal) && h.Contains("|empfang.", StringComparison.Ordinal));

        // Der Abgleich (Schema sync) hat jede eingespielte Zeile der Fachtabellen als Einfügung mit Mandant protokolliert, auch beim COPY.
        await VerifyChangeLogAsync(bulkHost, seeded[1]);

        // Belegkette und Signaturen im Datenerfassungsprotokoll der Kassen, auf beiden Wegen.
        await VerifyChainsAsync(servicesHost, seeded[0]);
        await VerifyChainsAsync(bulkHost, seeded[1]);

        // Berichte rechnen gleich.
        Assert.Equal(await ManagementReportAsync(servicesHost, seeded[0], now), await ManagementReportAsync(bulkHost, seeded[1], now));
    }

    private static async Task WaitForMailIntakeAsync(WebApplicationFactory<Program> host, Guid tenantId)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        while (true)
        {
            await using var scope = host.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
            var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
            if (!await db.InboundMails.AnyAsync(m => m.Status == InboundMailStatus.Received, Ct))
            {
                return;
            }

            Assert.True(DateTimeOffset.UtcNow < deadline, "Die Terminannahme hat die Beispiel-Nachrichten nicht in 2 min verarbeitet.");
            await Task.Delay(200, Ct);
        }
    }

    private async Task<WebApplicationFactory<Program>> HostAsync()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", connection));
        _ = host.Server;
        return host;
    }

    /// <summary>Je Tabelle die Zeilen als Text, ohne Ids und Zufallswerte (Gutscheincodes, Signaturen), sortiert.</summary>
    private static async Task<Dictionary<string, List<string>>> FingerprintAsync(WebApplicationFactory<Program> host, Guid tenantId, DateTimeOffset now)
    {
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var online = scope.ServiceProvider.GetRequiredService<OnlineDbContext>();

        var locations = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, Ct);
        var treatments = await db.Treatments.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, Ct);
        var customers = await db.Customers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => $"{c.FirstName} {c.LastName} {c.Email} {c.Salutation} {c.PreferredName} {c.TitlesBefore} {c.TitlesAfter} {c.Street} {c.PostalCode} {c.City} {c.Country}", Ct);
        var resources = (await db.Employees.AsNoTracking().ToListAsync(Ct)).ToDictionary(e => e.Id, e => e.DisplayName);
        foreach (var room in await db.Rooms.AsNoTracking().ToListAsync(Ct))
        {
            resources[room.Id] = room.Name;
        }

        foreach (var equipment in await db.Equipment.AsNoTracking().ToListAsync(Ct))
        {
            resources[equipment.Id] = equipment.Name;
        }

        var articles = await db.Articles.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Name, Ct);
        var places = await db.StoragePlaces.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, Ct);
        var minimums = await db.StoragePlaceMinimums.AsNoTracking().ToListAsync(Ct);
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Email, Ct);
        string User(string? id) => id is null ? "" : users[id] ?? "";
        var registers = await db.CashRegisters.AsNoTracking().ToDictionaryAsync(r => r.Id, Ct);
        // Was die Queue nach dem Einspielen mit der echten Uhr anlegt (Treatwell-Termine, online gekaufte Gutscheine), gehört nicht zum Verlauf.
        var appointments = await db.Appointments.AsNoTracking().Where(a => a.Source == AppointmentSource.Reception).ToListAsync(Ct);
        string Appointment(Appointment a) => string.Join('|', locations[a.LocationId], treatments[a.TreatmentId], customers[a.CustomerId],
            resources[a.EmployeeId], resources[a.RoomId], a.EquipmentId is { } e ? resources[e] : "", Stamp(a.Start), Stamp(a.End), Stamp(a.BlockedFrom),
            Stamp(a.BlockedUntil), a.Status, Num(a.Price), a.Note, a.CancelReason, a.Source, Stamp(a.CreatedAt), Stamp(a.UpdatedAt));
        var appointmentKeys = appointments.ToDictionary(a => a.Id, Appointment);

        var bills = await db.Bills.AsNoTracking().Include(b => b.Lines).Where(b => b.CreatedAt < now).AsSplitQuery().ToListAsync(Ct);
        var receipts = await db.Receipts.AsNoTracking().Include(r => r.Lines).Include(r => r.Payments).Where(r => r.IssuedAt < now).AsSplitQuery().ToListAsync(Ct);
        var vouchers = await db.Vouchers.AsNoTracking().Where(v => v.CreatedAt < now).ToListAsync(Ct);
        var voucherKeys = vouchers.ToDictionary(v => v.Id, v => string.Join('|', v.Kind, Num(v.InitialValue), treatments.GetValueOrDefault(v.TreatmentId ?? Guid.Empty), v.Units,
            v.ValidUntil, Stamp(v.CreatedAt)));
        var receiptKeys = receipts.ToDictionary(r => r.Id, r => $"{registers[r.RegisterId].Code}#{r.Number}");
        var billKeys = bills.ToDictionary(b => b.Id, b => $"{registers[b.RegisterId].Code}|{(b.CustomerId is { } c ? customers[c] : "")}|{Stamp(b.CreatedAt)}|{b.Lines.Count}");

        var result = new Dictionary<string, List<string>>
        {
            ["Appointments"] = [.. appointmentKeys.Values.Order(StringComparer.Ordinal)],
            // Welche Termine zu einer gemeinsamen Buchung gehören (die Gruppen-Id selbst ist zufällig).
            ["AppointmentGroups"] = [.. appointments.Where(a => a.GroupId is not null).GroupBy(a => a.GroupId)
                .Select(g => string.Join(" + ", g.Select(a => appointmentKeys[a.Id]).Order(StringComparer.Ordinal))).Order(StringComparer.Ordinal)],
            ["ResourceBookings"] = [.. (await db.ResourceBookings.AsNoTracking().ToListAsync(Ct)).Where(b => b.AppointmentId is null || appointmentKeys.ContainsKey(b.AppointmentId.Value)).Select(b => string.Join('|', b.Kind, resources[b.ResourceId], b.Slot,
                Stamp(b.From), Stamp(b.Until), b.AppointmentId is { } a ? appointmentKeys[a] : b.Reason)).Order(StringComparer.Ordinal)],
            ["OnlineBookings"] = [.. (await online.OnlineBookings.AsNoTracking().Where(o => o.CreatedAt < now).ToListAsync(Ct)).Where(o => appointmentKeys.ContainsKey(o.AppointmentId)).Select(o => string.Join('|', appointmentKeys[o.AppointmentId], Stamp(o.CreatedAt),
                Stamp(o.PrivacyAcceptedAt), o.PrivacyNoticeVersion, o.Language, o.CustomerAccountId)).Order(StringComparer.Ordinal)],
            ["AppointmentFollowUps"] = [.. (await db.AppointmentFollowUps.AsNoTracking().ToListAsync(Ct)).Where(f => appointmentKeys.ContainsKey(f.AppointmentId)).Select(f => string.Join('|', appointmentKeys[f.AppointmentId], f.Note,
                Stamp(f.UpdatedAt), User(f.UserId))).Order(StringComparer.Ordinal)],
            ["Bills"] = [.. bills.Select(b => string.Join('|', billKeys[b.Id], b.Status, string.Join(';', b.Lines.OrderBy(l => l.Position).Select(l =>
                string.Join(',', l.Position, l.Kind, l.Description, l.Quantity, Num(l.UnitPrice), Num(l.Discount), l.DiscountNote, Num(l.Amount), Num(l.PaidAmount),
                    l.TaxCategory, Num(l.TaxPercent), l.RevenueAccountId, l.AppointmentId is { } a ? appointmentKeys[a] : "", l.TreatmentId is { } t ? treatments[t] : "",
                    l.ArticleId is { } article ? articles[article] : "", l.VoucherKind, l.VoucherUnits, l.VoucherValidUntil))))).Order(StringComparer.Ordinal)],
            ["Receipts"] = [.. receipts.Select(r => string.Join('|', receiptKeys[r.Id], r.Kind, Stamp(r.IssuedAt), r.BillId is { } b ? billKeys[b] : "",
                r.CustomerId is { } c ? customers[c] : "", Num(r.Total), Num(r.Change), r.VoidsReceiptId is { } v ? receiptKeys[v] : "", r.VoidReason, User(r.CreatedByUserId),
                r.SignatureDeviceFailed, r.FiscalCode is null, string.Join(';', r.Lines.OrderBy(l => l.Position).Select(l => string.Join(',', l.Position, l.Description,
                    l.Quantity, Num(l.Amount), Num(l.Discount), l.DiscountNote, l.TaxCategory, Num(l.TaxPercent), l.RevenueAccountId, l.AppointmentId is { } a ? appointmentKeys[a] : "",
                    l.VoucherId is { } vo ? voucherKeys[vo] : "", l.DepositId))),
                string.Join(';', r.Payments.OrderBy(p => p.Method).ThenBy(p => p.Amount).Select(p => string.Join(',', p.Method, Num(p.Amount), p.Reference,
                    p.VoucherId is { } vp ? voucherKeys[vp] : ""))))).Order(StringComparer.Ordinal)],
            ["Vouchers"] = [.. vouchers.Select(v => string.Join('|', voucherKeys[v.Id], Num(v.Balance), v.Status, v.UnitsLeft, receiptKeys[v.SoldOnReceiptId])).Order(StringComparer.Ordinal)],
            ["CashMovements"] = [.. (await db.CashMovements.AsNoTracking().Where(m => m.At < now).ToListAsync(Ct)).Select(m => string.Join('|', registers[m.RegisterId].Code, m.Kind, Num(m.Amount),
                m.Reason, Stamp(m.At), User(m.UserId))).Order(StringComparer.Ordinal)],
            ["DayClosings"] = [.. (await db.DayClosings.AsNoTracking().Where(d => d.Until < now).ToListAsync(Ct)).Select(d => string.Join('|', registers[d.RegisterId].Code, Stamp(d.From), Stamp(d.Until),
                Num(d.CountedCash), User(d.UserId), WithoutId(d.Report))).Order(StringComparer.Ordinal)],
            ["CashRegisters"] = [.. registers.Values.Select(r => string.Join('|', r.Code, r.IsActive, r.SignatureDeviceFailedSince, r.SiteId is not null))
                .Order(StringComparer.Ordinal)],
            ["StockMovements"] = [.. (await db.StockMovements.AsNoTracking().Where(m => m.At < now).ToListAsync(Ct)).Select(m => string.Join('|', locations[m.LocationId], m.ItemId, m.Kind,
                Num(m.Quantity), Num(m.UnitCost), Stamp(m.At), m.Reason, m.Batch, m.ExpiresOn, m.AppointmentId is { } a ? appointmentKeys[a] : "",
                m.ReceiptId is { } r ? receiptKeys[r] : "", places[m.StoragePlaceId], m.TargetStoragePlaceId is { } t ? places[t] : "")).Order(StringComparer.Ordinal)],
        };

        result["Promotions"] = [.. (await db.Promotions.AsNoTracking().Include(p => p.Locations).Include(p => p.Treatments).AsSplitQuery().ToListAsync(Ct))
            .Select(p => string.Join('|', p.Name, p.GuestText, p.ValidFrom, p.ValidUntil, p.AllLocations, p.DiscountPercent, p.DiscountAmount, p.Scope, p.IsActive,
                p.ArchivedAt is not null, string.Join(',', p.Locations.Select(l => locations[l.LocationId]).Order(StringComparer.Ordinal)),
                string.Join(',', p.Treatments.Select(t => treatments[t.TreatmentId]).Order(StringComparer.Ordinal)))).Order(StringComparer.Ordinal)];

        // Betriebsinformationen (ADR 0074): beide Wege legen dieselben zehn Einträge an.
        result["BusinessContacts"] = [.. (await db.BusinessContacts.AsNoTracking().Include(c => c.Locations).AsSplitQuery().ToListAsync(Ct))
            .Select(c => string.Join('|', c.Category, c.Designation, c.Company, c.ContactPerson, c.Phone, c.Mobile, c.Email, c.Responsibility, c.CustomerNumber, c.Insurer, c.PolicyNumber,
                c.Coverage, c.ClaimsHotline, c.AllLocations, c.Visibility, c.Notes, c.ArchivedAt is not null, string.Join(',', c.Locations.Select(l => locations[l.LocationId]).Order(StringComparer.Ordinal))))
            .Order(StringComparer.Ordinal)];
        // Schließzeiten (ADR 0073) entstehen nach dem Verlauf und gleich auf beiden Wegen.
        result["Closures"] = [.. (await db.Closures.AsNoTracking().ToListAsync(Ct))
            .Select(c => string.Join('|', locations[c.LocationId], c.FromDate, c.FromTime, c.UntilDate, c.UntilTime, c.Kind, c.InternalReason, c.GuestText, c.CreatedByName,
                Stamp(c.CreatedAt))).Order(StringComparer.Ordinal)];
        var roomNames = await db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => $"{locations[r.LocationId]}|{r.Name}", Ct);
        var viennaZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        var roomClosures = new List<string>();
        foreach (var c in await db.RoomClosures.AsNoTracking().ToListAsync(Ct))
        {
            // Wie viele offene Termine im gesperrten Raum liegen: die Meldung „n Termine im gesperrten Raum“ zeigt sie.
            var (from, until) = (c.StartsAt(viennaZone), c.EndsAt(viennaZone));
            var open = await db.Appointments.AsNoTracking().CountAsync(a => a.RoomId == c.RoomId && a.Source == AppointmentSource.Reception
                && (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.CheckedIn) && a.Start < until && from < a.End, Ct);
            roomClosures.Add(string.Join('|', roomNames[c.RoomId], c.FromDate, c.FromTime, c.UntilDate, c.UntilTime, c.Kind, c.Reason, c.CreatedByName, Stamp(c.CreatedAt), open));
        }

        result["RoomClosures"] = [.. roomClosures.Order(StringComparer.Ordinal)];

        // Terminannahme (T-033): jede Beispiel-Nachricht mit Status, Art, Notiz und dem Gast des verknüpften Termins, auf beiden Wegen gleich.
        var guests = await db.Appointments.AsNoTracking().ToDictionaryAsync(a => a.Id, a => customers.GetValueOrDefault(a.CustomerId, "neuer Gast"), Ct);
        result["InboundMails"] = [.. (await db.InboundMails.AsNoTracking().ToListAsync(Ct))
            .Select(m => string.Join('|', m.Channel, m.From, m.Subject, m.Status, m.Kind, m.Note, m.LocationId is { } l ? locations[l] : "",
                m.AppointmentId is { } a ? guests[a] : ""))
            .Order(StringComparer.Ordinal)];

        // Lagerartikel haben je Datenbank eigene Ids; über den Namen vergleichbar machen.
        var items = await db.StockItems.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.Name, Ct);
        result["StockMovements"] = [.. result["StockMovements"].Select(line => ReplaceItem(line, items)).Order(StringComparer.Ordinal)];
        result["StoragePlaces"] = [.. (await db.StoragePlaces.AsNoTracking().ToListAsync(Ct)).Select(p => string.Join('|', locations[p.LocationId], p.Name, p.Description, p.Kind,
            p.RoomId is { } room ? resources[room] : "", p.IsActive, p.Position, string.Join(';', minimums.Where(m => m.StoragePlaceId == p.Id)
                .Select(m => $"{items[m.ItemId]}={Num(m.MinimumQuantity)}").Order(StringComparer.Ordinal)))).Order(StringComparer.Ordinal)];

        // Änderungsverlauf der Termine (ADR 0082): Termin, Art, Zeitpunkt, Person, Kanal und Werte, auf beiden Wegen gleich.
        var history = await db.Database.SqlQuery<string>($"""
            SELECT (h.row_key ->> 'Id') || '#' || h.operation || '|' || (extract(epoch FROM h.changed_at) * 1000)::bigint || '|' || coalesce(u."Email", '')
                   || '|' || h.channel || '|' || coalesce(h.changes::text, '') AS "Value"
              FROM sync.history h LEFT JOIN aispa."AspNetUsers" u ON u."Id" = h.user_id
             WHERE h.tenant_id = {tenantId} AND h.table_name = 'Appointments'
            """).ToListAsync(Ct);
        result["History"] = [.. history.Select(line => line.Split('#', 2)).Where(p => appointmentKeys.ContainsKey(Guid.Parse(p[0])))
            .Select(p => $"{appointmentKeys[Guid.Parse(p[0])]}|{p[1]}").Order(StringComparer.Ordinal)];
        return result;
    }

    private static async Task VerifyChangeLogAsync(WebApplicationFactory<Program> host, Guid tenantId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var tables = new Dictionary<string, Func<Task<int>>>
        {
            ["Appointments"] = () => db.Appointments.CountAsync(Ct),
            ["ResourceBookings"] = () => db.ResourceBookings.CountAsync(Ct),
            ["Bills"] = () => db.Bills.CountAsync(Ct),
            ["BillLines"] = () => db.BillLines.CountAsync(Ct),
            ["Receipts"] = () => db.Receipts.CountAsync(Ct),
            ["ReceiptLine"] = () => db.Set<ReceiptLine>().CountAsync(Ct),
            ["ReceiptPayment"] = () => db.Set<ReceiptPayment>().CountAsync(Ct),
            ["Vouchers"] = () => db.Vouchers.CountAsync(Ct),
            ["CashMovements"] = () => db.CashMovements.CountAsync(Ct),
            ["DayClosings"] = () => db.DayClosings.CountAsync(Ct),
            ["StockMovements"] = () => db.StockMovements.CountAsync(Ct),
        };
        foreach (var (table, count) in tables)
        {
            var rows = await count();
            var inserted = await db.Database.SqlQuery<long>(
                $"SELECT count(*) AS \"Value\" FROM sync.changes WHERE table_schema = 'aispa' AND table_name = {table} AND operation = 'I' AND tenant_id = {tenantId}").SingleAsync(Ct);
            Assert.True(rows > 0, $"{table}: leer");
            Assert.True(inserted >= rows, $"{table}: {rows} Zeilen, aber nur {inserted} Einfügungen im Änderungsprotokoll");
        }
    }

    private static string ReplaceItem(string line, Dictionary<Guid, string> items)
    {
        var parts = line.Split('|');
        parts[1] = items[Guid.Parse(parts[1])];
        return string.Join('|', parts);
    }

    private static string Stamp(DateTimeOffset? value) => value?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? "";

    private static string Num(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Der Bericht eines Tagesabschlusses ohne seine zufällige Id.</summary>
    private static string WithoutId(string report)
    {
        using var json = JsonDocument.Parse(report);
        return string.Join(',', json.RootElement.EnumerateObject().Where(p => p.Name is not ("id" or "registerId")).Select(p => $"{p.Name}={p.Value.GetRawText()}"));
    }

    private static async Task VerifyChainsAsync(WebApplicationFactory<Program> host, Guid tenantId)
    {
        using var client = await SignInAsync(host, DemoAccounts.Email("inhaberin"));
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        foreach (var register in await db.CashRegisters.AsNoTracking().ToListAsync(Ct))
        {
            var dep = await client.GetFromJsonAsync<DepExport>($"/api/pos/registers/{register.Id}/dep", TestSpa.Json, Ct);
            var jws = dep!.Groups[0].Receipts;
            Assert.True(jws.Count > 30, "Beispiel-Belege fehlen");
            for (var i = 0; i < jws.Count; i++)
            {
                var code = VerifyJws(jws[i], dep.Groups[0].Certificate);
                var chain = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(i == 0 ? register.Code : jws[i - 1])), 0, 8);
                Assert.Equal(chain, code.Split('_')[12]);
            }
        }
    }

    private static string VerifyJws(string jws, string certificate)
    {
        var parts = jws.Split('.');
        using var cert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certificate));
        using var key = cert.GetECDsaPublicKey()!;
        Assert.True(key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), RksvCode.FromBase64Url(parts[2]), HashAlgorithmName.SHA256), "Signatur ungültig");
        return Encoding.UTF8.GetString(RksvCode.FromBase64Url(parts[1]));
    }

    private static async Task<string> ManagementReportAsync(WebApplicationFactory<Program> host, Guid tenantId, DateTimeOffset now)
    {
        using var client = await SignInAsync(host, DemoAccounts.Email("inhaberin"));
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var reports = new List<string>();
        foreach (var location in await db.Locations.AsNoTracking().OrderBy(l => l.Name).ToListAsync(Ct))
        {
            var today = LocalTime.LocalDate(now, LocalTime.Zone(location.TimeZone));
            var report = await client.GetFromJsonAsync<ManagementReportDto>(
                $"/api/reports/locations/{location.Id}/management?from={today.AddDays(-7):yyyy-MM-dd}&until={today.AddDays(-1):yyyy-MM-dd}", TestSpa.Json, Ct);
            reports.Add($"{location.Name}: {string.Join(';', report!.Kpis.Select(k => $"{k.Key}={Num(k.Value)}"))}");
        }

        return string.Join('\n', reports);
    }

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> host, string email)
    {
        using var anonymous = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await anonymous.PostAsJsonAsync("/api/auth/dev/login", new DevLogin(email), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        return client;
    }
}
