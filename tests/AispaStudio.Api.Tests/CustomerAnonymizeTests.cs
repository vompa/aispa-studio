using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Online;
using AispaStudio.Api.Reporting;
using AispaStudio.Api.Retention;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Sync;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Gast anonymisieren und weich löschen: Der Gast verliert alle Klardaten (Name, Kontakt, Notizen, Anamnese, Online-Zugang), Termine und Belege bleiben als
/// „Gelöschter Gast“, die Belegkopie der Rechnungsanschrift bleibt wegen BAO und RKSV, der Änderungsverlauf behält Zeilen ohne Werte. Die Vollständigkeitsprobe
/// sucht nach dem Anonymisieren in allen Text- und JSON-Spalten der Schemas aispa, online, health und sync nach Nachname und Nummer des Gastes.
/// </summary>
public sealed partial class CustomerAnonymizeTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Ein vergangener Montag: der Termin liegt nicht in der Zukunft und blockiert das Anonymisieren nicht.</summary>
    private static readonly DateOnly Past = new(2026, 10, 5);

    private const string Placeholder = "Gelöschter Gast";

    // ---------- Vollständigkeitsprobe ----------

    [Fact]
    public async Task Vollstaendigkeitsprobe_nur_die_Belegkopie_der_Rechnungsanschrift_bleibt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var seed = await SeedGuestAsync(spa);

        // Der Aufbau trägt: ohne Anonymisieren steht der Gast in sehr vielen Tabellen.
        var before = await HitsAsync(fixture.ConnectionString, seed.Token, seed.PhoneTail);
        Assert.True(before.Count >= 20, $"nur {before.Count} Tabellen mit Treffern vor dem Anonymisieren: {string.Join(", ", before)}");

        var response = await spa.Client.PostAsync($"/api/customers/{seed.Guest.Id}/anonymize", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await HitsAsync(fixture.ConnectionString, seed.Token, seed.PhoneTail);
        Assert.Equal(["aispa.ReceiptRecipients"], after);

        // Der Gast selbst ist der Platzhalter, ohne jeden Wert; Rabatt, Vorlieben und Zugehörigkeit zur Kasse sind weg.
        var guest = await spa.GetAsync<CustomerDto>($"/api/customers/{seed.Guest.Id}", Ct);
        Assert.Equal(("Gelöschter", "Gast", Placeholder), (guest.FirstName, guest.LastName, guest.InHouseName));
        Assert.NotNull(guest.DeletedAt);
        Assert.Equal((null, null, null, null, null, null, null, null, null, null),
            (guest.Email, guest.Phone, guest.BirthDate, guest.Note, guest.Billing, guest.Discount, guest.Booking, guest.Address, guest.PreferredName, guest.Pronouns));
        Assert.Equal((CustomerSalutation.None, null, null, null), (guest.Salutation, guest.TitlesBefore, guest.TitlesAfter, guest.Language));

        // Zeilen, die ganz verschwinden: Konto samt Anmeldedaten, Links, offene Warteliste, Anamnese, Flags, weitere Mails, Einwilligung und Erinnerungen.
        foreach (var (sql, name) in new[]
        {
            ($"""SELECT count(*) FROM online."CustomerAccounts" WHERE "CustomerId" = '{seed.Guest.Id}'""", "Konten"),
            ($"""SELECT count(*) FROM online."CustomerAccountLogins" WHERE "ProviderKey" LIKE '%{seed.Token}%'""", "externe Anmeldungen"),
            ($"""SELECT count(*) FROM online."AccessLinks" WHERE "CustomerId" = '{seed.Guest.Id}'""", "Links"),
            ($"""SELECT count(*) FROM online."WaitlistEntries" WHERE "CustomerId" = '{seed.Guest.Id}' AND "Status" = 'Waiting'""", "offene Warteliste"),
            ($"""SELECT count(*) FROM health."AnamnesisRecords" WHERE "CustomerId" = '{seed.Guest.Id}'""", "Anamnesebögen"),
            ($"""SELECT count(*) FROM health."AnamnesisScans" WHERE "CustomerId" = '{seed.Guest.Id}'""", "Scans"),
            ($"""SELECT count(*) FROM aispa."CustomerFlags" WHERE "CustomerId" = '{seed.Guest.Id}'""", "Kennzeichen"),
            ($"""SELECT count(*) FROM aispa."CustomerEmails" WHERE "CustomerId" = '{seed.Guest.Id}'""", "weitere Mailadressen"),
            ($"""SELECT count(*) FROM aispa."CustomerNameChanges" WHERE "CustomerId" = '{seed.Guest.Id}'""", "Namensänderungen"),
            ($"""SELECT count(*) FROM aispa."CustomerPreferences" WHERE "CustomerId" = '{seed.Guest.Id}'""", "Vorlieben"),
            ($"""SELECT count(*) FROM aispa."MarketingConsents" WHERE "CustomerId" = '{seed.Guest.Id}'""", "Einwilligungen"),
            ($"""SELECT count(*) FROM aispa."RebookReminders" WHERE "CustomerId" = '{seed.Guest.Id}'""", "Erinnerungen"),
        })
        {
            Assert.True(await ScalarAsync<long>(fixture.ConnectionString, sql) == 0, $"{name} sind noch da");
        }
    }

    [Fact]
    public async Task Termine_und_Belege_bleiben_als_Geloeschter_Gast_und_der_Verlauf_behaelt_Zeilen_ohne_Werte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var seed = await SeedGuestAsync(spa);
        var before = await spa.GetAsync<ActivityPage>($"/api/locations/{spa.Location.Id}/history", Ct);
        Assert.Contains(before.Items, i => i.Text.Contains($"Allergie {seed.Token}", StringComparison.Ordinal) || i.Values.Any(v => v.New?.Contains(seed.Token, StringComparison.Ordinal) == true));

        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.PostAsync($"/api/customers/{seed.Guest.Id}/anonymize", null, Ct)).StatusCode);

        var appointments = await spa.GetAsync<List<AppointmentDto>>($"/api/customers/{seed.Guest.Id}/appointments", Ct);
        Assert.Equal(new[] { seed.PaidId, seed.CancelledId }.Order(), appointments.Select(a => a.Id).Order());
        Assert.All(appointments, a => Assert.Null(a.Note));
        var receipt = await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{seed.ReceiptId}", Ct);
        Assert.Equal($"Firma {seed.Token} GmbH", receipt.Recipient!.Name);

        var page = await spa.GetAsync<ActivityPage>($"/api/locations/{spa.Location.Id}/history", Ct);
        var cancelled = Assert.Single(page.Items, i => i.Kind == ActivityKind.Cancelled && i.AppointmentId == seed.CancelledId);
        Assert.Equal("Storniert von Test Person", cancelled.Text);
        Assert.DoesNotContain(cancelled.Values, v => v.Label == "Grund");
        Assert.Contains(page.Items, i => i.Kind == ActivityKind.Booked && i.AppointmentId == seed.PaidId);
        Assert.All(page.Items.Where(i => i.CustomerId == seed.Guest.Id), i => Assert.Equal(Placeholder, i.CustomerName));
        Assert.DoesNotContain(page.Items, i => i.Text.Contains(seed.Token, StringComparison.Ordinal)
            || i.Values.Any(v => (v.Old ?? "").Contains(seed.Token, StringComparison.Ordinal) || (v.New ?? "").Contains(seed.Token, StringComparison.Ordinal)));

        // Der Verlauf des Gastes behält die Zeilen (wer, wann, was), aber keine Werte und keine Notiz-Änderung.
        var rows = await ScalarAsync<long>(fixture.ConnectionString,
            $"""SELECT count(*) FROM sync.history WHERE customer_id = '{seed.Guest.Id}'""");
        Assert.True(rows >= 5, $"nur {rows} Verlaufszeilen");
        Assert.Equal(0L, await ScalarAsync<long>(fixture.ConnectionString,
            $"""SELECT count(*) FROM sync.history WHERE customer_id = '{seed.Guest.Id}' AND table_name IN ('Customers', 'CustomerEmails') AND changes IS NOT NULL"""));
        Assert.Equal(0L, await ScalarAsync<long>(fixture.ConnectionString,
            $"""SELECT count(*) FROM sync.history WHERE customer_id = '{seed.Guest.Id}' AND table_name = 'Appointments' AND (changes ? 'Note' OR changes ? 'CancelReason')"""));
        Assert.Equal(0L, await ScalarAsync<long>(fixture.ConnectionString,
            $"""SELECT count(*) FROM sync.history WHERE customer_id = '{seed.Guest.Id}' AND table_name = 'Appointments' AND operation = 'U' AND changes = jsonb_build_object()"""));
    }

    // ---------- Rechte und Sperren ----------

    [Fact]
    public async Task Nur_Inhaber_und_Geschaeftsfuehrung_duerfen_Rezeption_und_Therapeutin_bekommen_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.GeneralManager, [spa.Location.Id]);
        var path = $"/api/customers/{spa.Customer.Id}/anonymize";

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync(path, null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await therapist.PostAsync(path, null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PostAsync($"/api/customers/{Guid.NewGuid()}/anonymize", null, Ct)).StatusCode);
        Assert.Null((await spa.GetAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Ct)).DeletedAt);

        Assert.Equal(HttpStatusCode.NoContent, (await manager.PostAsync(path, null, Ct)).StatusCode);
        Assert.NotNull((await spa.GetAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Ct)).DeletedAt);

        var again = await spa.Client.PostAsync(path, null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("bereits anonymisiert", await again.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_kuenftiger_Termin_und_eine_offene_Rechnung_sperren_mit_Text()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = $"/api/customers/{spa.Customer.Id}/anonymize";
        var upcoming = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", RegisterInput(spa), Ct);
        _ = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, spa.Customer.Id, []), Ct);

        var blocked = await spa.Client.PostAsync(path, null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var rejected = (await blocked.Content.ReadFromJsonAsync<CustomerAnonymizeRejected>(TestSpa.Json, Ct))!;
        Assert.Equal(["1 künftiger Termin", "1 offene Rechnung"], rejected.Open);
        Assert.Contains("Bitte zuerst", rejected.Message, StringComparison.Ordinal);
        Assert.Null((await spa.GetAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Ct)).DeletedAt);
        Assert.Equal("Muster", (await spa.GetAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Ct)).LastName[..6]);

        // Storniert ist der Termin kein Hindernis mehr, die offene Rechnung weiter.
        _ = await spa.PostAsync<AppointmentDto>($"/api/appointments/{upcoming.Id}/cancel", new CancelAppointmentRequest(null), Ct);
        var stillOpen = await spa.Client.PostAsync(path, null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stillOpen.StatusCode);
        Assert.Equal(["1 offene Rechnung"], (await stillOpen.Content.ReadFromJsonAsync<CustomerAnonymizeRejected>(TestSpa.Json, Ct))!.Open);
    }

    [Fact]
    public async Task Eine_laufende_oder_fehlgeschlagene_Rueckerstattung_sperrt_weil_ihre_Mail_die_Adresse_braucht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var seed = await SeedGuestAsync(spa);
        var path = $"/api/customers/{seed.Guest.Id}/anonymize";

        foreach (var status in new[] { OnlineRefundStatus.Pending, OnlineRefundStatus.Failed })
        {
            await SetRefundStatusAsync(spa, seed.ReceiptId, status);
            var blocked = await spa.Client.PostAsync(path, null, Ct);
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            Assert.Equal(["1 offene Rückerstattung"], (await blocked.Content.ReadFromJsonAsync<CustomerAnonymizeRejected>(TestSpa.Json, Ct))!.Open);
        }

        await SetRefundStatusAsync(spa, seed.ReceiptId, OnlineRefundStatus.Refunded);
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.PostAsync(path, null, Ct)).StatusCode);
    }

    private async Task SetRefundStatusAsync(TestSpa spa, Guid receiptId, OnlineRefundStatus status)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var online = scope.ServiceProvider.GetRequiredService<OnlineDbContext>();
        _ = await online.Refunds.Where(r => r.ReceiptId == receiptId).ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, status), Ct);
    }

    // ---------- Aufbau ----------

    private sealed record Seed(CustomerDto Guest, string Token, string PhoneTail, Guid PaidId, Guid CancelledId, Guid ReceiptId);

    /// <summary>
    /// Ein Gast mit eindeutigem Nachnamen und eindeutiger Nummer, der in jeder Tabelle vorkommt, die Personenbezug hat: Stammdaten, Kontaktdaten, Termine mit
    /// Notizen, Kassabeleg mit Rechnungsanschrift, Anfragen, Anamnese, Online-Konto, Bestellungen und Verlauf.
    /// </summary>
    private async Task<Seed> SeedGuestAsync(TestSpa spa)
    {
        var token = $"Zyxwvutanon{Guid.NewGuid():N}"[..27];
        var tail = RandomNumberGenerator.GetInt32(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);
        var email = $"{token}@example.at";
        var phone = $"+43 664 {tail}";
        var created = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Eva", token, email, phone, new DateOnly(1980, 5, 17), $"Notiz {token}"), Ct);
        var path = $"/api/customers/{created.Id}";
        // Namensänderung (alter Name im Protokoll), Rufname, Titel, Anschrift und Pronomen.
        var guest = await spa.PutAsync<CustomerDto>(path, new CustomerInput("Evelyn", token, email, phone, new DateOnly(1980, 5, 17), $"Notiz {token}", null, "en",
            CustomerSalutation.Female, $"Evi{token}", "Mag.", "MSc", null, new CustomerAddress($"Gasse {token}", "1010", "Wien", "AT"), "sie/ihr"), Ct);
        _ = await spa.PutAsync<CustomerDto>($"{path}/billing", new CustomerBilling($"Firma {token} GmbH", $"Ring {token}\n1010 Wien", "ATU12345678"), Ct);
        _ = await spa.PutAsync<CustomerDto>($"{path}/discount", new CustomerDiscountInput(10m), Ct);
        _ = await spa.PutAsync<CustomerDto>($"{path}/booking-preferences", new CustomerBookingPreferences(spa.Anna.Id, false, [DayOfWeek.Monday]), Ct);
        (await spa.Client.PutAsJsonAsync($"{path}/reception-note", new CustomerReceptionNoteInput($"zahlt bar {token}"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        _ = await spa.PostAsync<List<CustomerEmailDto>>($"{path}/emails", new CustomerEmailInput($"zweit.{token}@example.at"), Ct);

        // Termin in der Vergangenheit, bezahlt, mit Notiz, Rechnungsanschrift und Anamnese.
        var paid = await spa.BookInThePastAsync(TestSpa.At(10, 0, Past), Ct, spa.Anna.Id, spa.RoomA.Id, customer: guest.Id);
        _ = await spa.PutAsync<AppointmentDto>($"/api/appointments/{paid.Id}/note", new AppointmentNoteRequest($"Allergie {token}"), Ct);
        _ = await spa.SetStatusAsync(paid.Id, AppointmentStatus.CheckedIn, Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", RegisterInput(spa), Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [paid.Id]), Ct);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 79m, null, null)]), Ct);
        _ = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/recipient", new InvoiceRecipient($"Firma {token} GmbH", $"Ring {token}\n1010 Wien", "ATU12345678"), Ct);

        // Stornierter Termin mit Grund und Notiz: der Verlauf soll „Storniert von …“ behalten, den Grund nicht.
        var cancelled = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id, spa.RoomB.Id, guest.Id);
        _ = await spa.PutAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/note", new AppointmentNoteRequest($"Bitte ruhig {token}"), Ct);
        _ = await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest($"krank {token}"), Ct);

        // Kennzeichen zuletzt: „Nicht mehr buchen“ sperrt das Buchen.
        _ = await spa.PostAsync<List<CustomerFlagDto>>($"{path}/flags", new CustomerFlagInput(CustomerFlagKind.DoNotBook, null, $"offen {token}"), Ct);

        // Konto im Kundenbereich über die Anmeldung per Code (verknüpft sich über die Mailadresse mit dem Gast; der Code bleibt als SignInCode liegen).
        using (await SignInAsync(spa, email))
        {
        }

        await SeedDirectAsync(spa, guest, token, phone, paid.Id, cancelled.Id, receipt.Id);
        return new Seed(guest, token, tail, paid.Id, cancelled.Id, receipt.Id);
    }

    /// <summary>Tabellen, die sich über die Oberfläche nur umständlich füllen lassen: direkt über die Kontexte.</summary>
    private async Task SeedDirectAsync(TestSpa spa, CustomerDto guest, string token, string phone, Guid paidId, Guid cancelledId, Guid receiptId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var online = scope.ServiceProvider.GetRequiredService<OnlineDbContext>();
        var now = DateTimeOffset.UtcNow;
        var ordinary = $"{token}@example.at";

        db.AppointmentSeries.Add(new AppointmentSeries
        {
            LocationId = spa.Location.Id,
            CustomerId = guest.Id,
            TreatmentId = spa.MassageTreatment.Id,
            IntervalWeeks = 2,
            StartTime = new TimeOnly(10, 0),
            EmployeeId = spa.Anna.Id,
            Problem = $"Serie {token}",
            CreatedAt = now,
        });
        db.AreaVisits.Add(new AreaVisit
        {
            LocationId = spa.Location.Id,
            RoomId = spa.RoomA.Id,
            CustomerId = guest.Id,
            Persons = 1,
            From = TestSpa.At(12, 0, Past),
            Until = TestSpa.At(13, 0, Past),
            Note = $"Sauna {token}",
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.AppointmentFollowUps.Add(new AppointmentFollowUp { AppointmentId = paidId, Note = $"Nachsorge {token}", UpdatedAt = now });
        db.CustomerPreferences.Add(new CustomerPreference { CustomerId = guest.Id, Scent = $"Lavendel {token}", Other = $"Musik {token}", UpdatedAt = now });
        db.MarketingConsents.Add(new MarketingConsent { CustomerId = guest.Id, Email = ordinary, Source = "Team", RequestedAt = now });
        db.SentDocuments.Add(new AispaStudio.Api.Documents.SentDocument
        {
            Kind = AispaStudio.Api.Documents.SentDocument.AppointmentConfirmationKind,
            TargetId = paidId,
            To = ordinary,
            Subject = $"Terminbestätigung für {token}",
            SentAt = now,
        });
        db.RebookReminders.Add(new RebookReminder
        {
            CustomerId = guest.Id,
            TreatmentId = spa.MassageTreatment.Id,
            AfterAppointmentId = paidId,
            Text = $"Liebe Eva {token}, wollen Sie wieder kommen?",
            CreatedAt = now,
        });
        var closure = new Closure { LocationId = spa.Location.Id, FromDate = Past, UntilDate = Past, CreatedAt = now };
        db.Closures.Add(closure);
        db.ClosureNotices.Add(new ClosureNotice
        {
            ClosureId = closure.Id,
            AppointmentId = cancelledId,
            Subject = $"Absage {token}",
            Text = $"Liebe Eva {token}, wir müssen absagen.",
            CreatedAt = now,
        });
        // Die Anfrage des Gastes selbst, und die Anfrage eines anderen Absenders, die den Gast nur als Kandidaten nennt.
        db.InboundMails.Add(new InboundMail
        {
            MessageId = $"m-{Guid.NewGuid():N}",
            From = ordinary,
            FromName = token,
            To = "spa@example.at",
            Subject = $"Termin {token}",
            Body = $"Hier ist {token}, Telefon {phone}",
            ReceivedAt = now,
            Summary = $"Anfrage von {token}",
            Note = $"Rückruf {token}",
            CustomerId = guest.Id,
            CustomerConfirmed = true,
            GuestFirstName = "Eva",
            GuestLastName = token,
            GuestPhone = phone,
            GuestEmail = ordinary,
            SuggestedTitlesBefore = "Mag.",
            ReplyTo = ordinary,
            ReplySubject = $"Re: {token}",
            ReplyBody = $"Liebe Frau {token}",
            TranslatedSubject = $"Date {token}",
            TranslatedBody = $"Hello {token}",
            ReplyTranslation = $"Dear {token}",
            ReplyHistory = [$"Entwurf für {token}"],
            WishNote = $"lieber morgens {token}",
            CustomerCandidates = [new CustomerCandidateDto(guest.Id, $"Eva {token}", 1, "gleiche Mailadresse")],
        });
        db.InboundMails.Add(new InboundMail
        {
            MessageId = $"m-{Guid.NewGuid():N}",
            From = "andere@example.at",
            To = "spa@example.at",
            Subject = "Frage",
            Body = "Wer ist das?",
            ReceivedAt = now,
            CustomerCandidates = [new CustomerCandidateDto(guest.Id, $"Eva {token}", 3, "ähnlicher Name"), new CustomerCandidateDto(Guid.NewGuid(), "Rita Beispiel", 4, "ähnlicher Name")],
        });
        await db.SaveChangesAsync(Ct);

        var account = await online.Users.SingleAsync(a => a.CustomerId == guest.Id, Ct);
        online.UserLogins.Add(new IdentityUserLogin<Guid> { LoginProvider = "Google", ProviderKey = $"g-{token}", ProviderDisplayName = "Google", UserId = account.Id });
        online.AccessLinks.Add(new AccessLink
        {
            TokenHash = RandomNumberGenerator.GetBytes(32),
            Purpose = AccessLinkPurpose.Anamnesis,
            CustomerId = guest.Id,
            AppointmentId = paidId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(1),
        });
        online.WaitlistEntries.Add(new WaitlistEntry
        {
            CustomerId = guest.Id,
            LocationId = spa.Location.Id,
            TreatmentId = spa.MassageTreatment.Id,
            From = Past,
            Until = Past.AddDays(7),
            Language = "de",
            CreatedAt = now,
            PrivacyAcceptedAt = now,
            PrivacyNoticeVersion = "1",
        });
        online.VoucherOrders.Add(new VoucherOrder
        {
            TokenHash = RandomNumberGenerator.GetBytes(32),
            LocationId = spa.Location.Id,
            CustomerId = guest.Id,
            Amount = 50m,
            Currency = "EUR",
            BuyerEmail = ordinary,
            RecipientName = token,
            Message = $"Alles Gute {token}",
            Language = "de",
            CreatedAt = now,
            PrivacyAcceptedAt = now,
            PrivacyNoticeVersion = "1",
            Provider = "Test",
        });
        online.Deposits.Add(new DepositOrder
        {
            AppointmentId = paidId,
            LocationId = spa.Location.Id,
            CustomerId = guest.Id,
            Email = ordinary,
            Language = "de",
            Currency = "EUR",
            Provider = "Test",
            AcceptedAt = now,
        });
        online.CardGuarantees.Add(new CardGuarantee
        {
            AppointmentId = cancelledId,
            LocationId = spa.Location.Id,
            CustomerId = guest.Id,
            Email = ordinary,
            Language = "de",
            Currency = "EUR",
            Provider = "Test",
        });
        online.Refunds.Add(new OnlineRefund
        {
            ReceiptId = receiptId,
            SourceId = Guid.NewGuid(),
            LocationId = spa.Location.Id,
            Provider = "Test",
            PaymentReference = "pay-1",
            Currency = "EUR",
            Email = ordinary,
            Language = "de",
            RequestedAt = now,
            Status = OnlineRefundStatus.Refunded,
            RefundedAt = now,
        });
        online.AnamnesisRecords.Add(new AnamnesisRecord
        {
            CustomerId = guest.Id,
            AppointmentId = paidId,
            FormVersion = "1",
            Channel = "Link",
            SubmittedAt = now,
            ConsentGivenAt = now,
            Payload = RandomNumberGenerator.GetBytes(64),
        });
        online.AnamnesisScans.Add(new AnamnesisScan
        {
            CustomerId = guest.Id,
            AppointmentId = paidId,
            UploadedAt = now,
            UploadedBy = "tester",
            ContentType = "image/jpeg",
            Language = "de",
            Image = RandomNumberGenerator.GetBytes(64),
        });
        await online.SaveChangesAsync(Ct);
    }

    private static CashRegisterInput RegisterInput(TestSpa spa) =>
        new(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null);

    /// <summary>Meldet sich im Kundenbereich mit Code an (wie die Buchungsseite) und gibt den Client mit dem Kunden-Cookie zurück.</summary>
    private async Task<HttpClient> SignInAsync(TestSpa spa, string email)
    {
        using var anonymous = spa.OnlineClient();
        // Bei einer zweiten Anmeldung mit derselben Adresse liegt der alte Code schon im Postfach: nur eine neue Mail zählt.
        var earlier = fixture.Mailbox.To(email);
        var request = await anonymous.PostAsJsonAsync("/api/online/auth/code", new SignInCodeRequest(email, "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Ihr Anmeldecode", StringComparison.Ordinal) && !earlier.Contains(m), Ct);
        var verify = await anonymous.PostAsJsonAsync("/api/online/auth/code/verify",
            new SignInCodeVerification(email, SixDigits().Match(mail.Subject).Value), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return spa.OnlineClient(string.Join("; ", verify.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
    }

    // ---------- Probe ----------

    /// <summary>Tabellen (<c>schema.Tabelle</c>), in deren Text-, varchar- und jsonb-Spalten einer der Suchwerte vorkommt; sortiert, ohne Wiederholung.</summary>
    internal static async Task<List<string>> HitsAsync(string connectionString, params string[] terms)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        var columns = new List<(string Schema, string Table, string Column)>();
        await using (var list = new NpgsqlCommand(
            """
            SELECT c.table_schema, c.table_name, c.column_name FROM information_schema.columns c
              JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name AND t.table_type = 'BASE TABLE'
             WHERE c.table_schema IN ('aispa', 'online', 'health', 'sync') AND c.data_type IN ('text', 'character varying', 'jsonb')
             ORDER BY 1, 2, 3
            """, connection))
        await using (var reader = await list.ExecuteReaderAsync(Ct))
        {
            while (await reader.ReadAsync(Ct))
            {
                columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        var hits = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (schema, table, column) in columns)
        {
            if (hits.Contains($"{schema}.{table}"))
            {
                continue;
            }

            foreach (var term in terms)
            {
                await using var probe = new NpgsqlCommand($"SELECT EXISTS (SELECT 1 FROM \"{schema}\".\"{table}\" WHERE \"{column}\"::text ILIKE @pattern)", connection);
                probe.Parameters.AddWithValue("pattern", $"%{term}%");
                if ((bool)(await probe.ExecuteScalarAsync(Ct))!)
                {
                    hits.Add($"{schema}.{table}");
                    break;
                }
            }
        }

        return [.. hits];
    }

    internal static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(Ct))!;
    }

    [GeneratedRegex(@"\d{6}")]
    private static partial Regex SixDigits();
}

/// <summary>
/// Abgleich (Muster <see cref="SyncTests"/>): Der Einzelplatz anonymisiert einen Gast, den die zentrale Seite schon kennt. Auf der Gegenseite ist der Gast dann
/// ebenfalls anonymisiert, und ihr Verlauf ist bereinigt, weil der verzögerte Trigger dort nach den Leer-Updates derselben Transaktion läuft.
/// </summary>
[Collection(typeof(SyncAlone))]
public sealed class CustomerAnonymizeSyncTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Gegenseite_ist_anonymisiert_und_ihr_Verlauf_bereinigt()
    {
        var localDb = await fixture.CreateDatabaseAsync(Ct);
        var centralDb = await fixture.CreateDatabaseAsync(Ct);
        await using var local = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", localDb);
            b.UseSetting("Hosting:Standalone", "true");
        });
        await using var central = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", centralDb));
        _ = central.Server;

        using var anonymous = local.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var setup = await anonymous.PostAsJsonAsync("/api/auth/standalone/setup",
            new StandaloneSetup("Spa am See", "AT", "Gmunden", "Eva Huber", "eva", "seeblick-2026"), TestSpa.Json, Ct);
        setup.EnsureSuccessStatusCode();
        using var team = local.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        team.DefaultRequestHeaders.Add("Cookie", string.Join("; ", setup.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));

        var token = $"Zyxwvutanon{Guid.NewGuid():N}"[..27];
        var tail = RandomNumberGenerator.GetInt32(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);
        var created = await (await team.PostAsJsonAsync("/api/customers",
                new CustomerInput("Lea", token, $"{token}@example.at", $"+43 664 {tail}", null, $"Notiz {token}"), TestSpa.Json, Ct))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<CustomerDto>(TestSpa.Json, Ct);
        (await team.PostAsJsonAsync($"/api/customers/{created!.Id}/emails", new CustomerEmailInput($"zweit.{token}@example.at"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        (await team.PutAsJsonAsync($"/api/customers/{created.Id}", new CustomerInput("Lena", token, $"{token}@example.at", $"+43 664 {tail}", null, $"Notiz {token} neu"), TestSpa.Json, Ct))
            .EnsureSuccessStatusCode();

        var runner = new SyncRunner(localDb, centralDb, "gmunden-empfang");
        _ = await runner.RunOnceAsync(Ct);
        // Die zentrale Seite kennt den Gast mit allen Werten; ihr Verlauf zeigt sie (Gegenprobe der Probe).
        Assert.Equal(token, await CustomerAnonymizeTests.ScalarAsync<string>(centralDb, $"""SELECT "LastName" FROM aispa."Customers" WHERE "Id" = '{created.Id}'"""));
        Assert.Contains("aispa.Customers", await CustomerAnonymizeTests.HitsAsync(centralDb, token, tail));

        var anonymized = await team.PostAsync($"/api/customers/{created.Id}/anonymize", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, anonymized.StatusCode);
        var pulled = await runner.RunOnceAsync(Ct);
        Assert.True(pulled.Pushed >= 2, $"nur {pulled.Pushed} Zeilen gesendet");

        Assert.Equal(("Gast", true), (await CustomerAnonymizeTests.ScalarAsync<string>(centralDb, $"""SELECT "LastName" FROM aispa."Customers" WHERE "Id" = '{created.Id}'"""),
            await CustomerAnonymizeTests.ScalarAsync<bool>(centralDb, $"""SELECT "DeletedAt" IS NOT NULL FROM aispa."Customers" WHERE "Id" = '{created.Id}'""")));
        Assert.Equal(0L, await CustomerAnonymizeTests.ScalarAsync<long>(centralDb, $"""SELECT count(*) FROM aispa."CustomerEmails" WHERE "CustomerId" = '{created.Id}'"""));
        Assert.Empty(await CustomerAnonymizeTests.HitsAsync(centralDb, token, tail));
        Assert.Empty(await CustomerAnonymizeTests.HitsAsync(localDb, token, tail));
        var centralRows = await CustomerAnonymizeTests.ScalarAsync<long>(centralDb, $"""SELECT count(*) FROM sync.history WHERE customer_id = '{created.Id}'""");
        Assert.True(centralRows >= 1, "der Verlauf der zentralen Seite hat keine Zeile mehr");
        Assert.Equal(0L, await CustomerAnonymizeTests.ScalarAsync<long>(centralDb,
            $"""SELECT count(*) FROM sync.history WHERE customer_id = '{created.Id}' AND table_name IN ('Customers', 'CustomerEmails') AND changes IS NOT NULL"""));
    }
}
