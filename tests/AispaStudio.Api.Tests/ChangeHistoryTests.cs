using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>Ä184, Slice 90 (ADR 0082): Verlauf eines Termins aus dem Änderungs-Trigger mit Person, Kanal, Zeitpunkt und alten und neuen Werten.</summary>
public sealed class ChangeHistoryTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string HistoryPath(Guid appointmentId) => $"/api/appointments/{appointmentId}/history";

    [Fact]
    public async Task Lena_verschiebt_von_14_auf_15_30_und_der_Verlauf_nennt_sie_mit_Zeitpunkt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);
        using var lena = await StaffAsync(spa, "Lena", StaffRole.Reception);

        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var moved = await lena.PostAsJsonAsync($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(15, 30), spa.Anna.Id, null, null), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var history = await spa.GetAsync<List<HistoryEntry>>(HistoryPath(appointment.Id), Ct);
        Assert.Equal("Verschoben von Lena: 14:00 → 15:30", history[0].Text);
        Assert.InRange(history[0].At, before, DateTimeOffset.UtcNow.AddSeconds(5));
        // Neueste oben; das Anlegen durch die Inhaberin (Testkonto „Test Person“) steht darunter.
        Assert.Equal("Angelegt von Test Person", history[^1].Text);
        Assert.True(history[0].At >= history[^1].At);
    }

    [Fact]
    public async Task Storno_nennt_wer_storniert_hat_und_wann()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        using var lena = await StaffAsync(spa, "Lena", StaffRole.Reception);

        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var cancelled = await lena.PostAsJsonAsync($"/api/appointments/{appointment.Id}/cancel", new CancelAppointmentRequest("Gast krank"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        var latest = (await spa.GetAsync<List<HistoryEntry>>(HistoryPath(appointment.Id), Ct))[0];
        Assert.Equal("Storniert von Lena: Gast krank", latest.Text);
        Assert.InRange(latest.At, before, DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Notiz_und_Nicht_erschienen_stehen_mit_Person_im_Verlauf_und_ein_fremder_Termin_ist_nicht_gefunden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        _ = await spa.PutAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/note", new AppointmentNoteRequest("Lavendelöl"), Ct);
        _ = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/status", new AppointmentStatusChange(AppointmentStatus.NoShow), Ct);

        var texts = (await spa.GetAsync<List<HistoryEntry>>(HistoryPath(appointment.Id), Ct)).Select(e => e.Text).ToList();
        Assert.Equal(["Als nicht erschienen markiert von Test Person", "Notiz geändert von Test Person", "Angelegt von Test Person"], texts);

        var other = await TestSpa.CreateAsync(fixture, Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync(HistoryPath(appointment.Id), Ct)).StatusCode);
    }

    [Fact]
    public async Task Was_der_Kundenbereich_schreibt_steht_als_online_ohne_Person_im_Verlauf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        using (ChangeActor.Use(ChangeChannel.Online))
        {
            scope.ServiceProvider.GetRequiredService<Tenancy.TenantContext>().Use(spa.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<Data.AispaDbContext>();
            var entity = await db.Appointments.FindAsync([appointment.Id], Ct);
            entity!.ConfirmedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(Ct);
        }

        Assert.Equal("Vom Gast bestätigt (online)", (await spa.GetAsync<List<HistoryEntry>>(HistoryPath(appointment.Id), Ct))[0].Text);
    }

    [Theory]
    [InlineData("/api/online/me/appointments", null, "Online")]
    [InlineData("/api/appointments", "benutzer-1", "Team")]
    [InlineData("/api/channels/twilio/sms", null, "Job")]
    public void Person_und_Kanal_kommen_aus_der_Anfrage(string path, string? userId, string channel)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        if (userId is not null)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));
        }

        Assert.Equal((Enum.Parse<ChangeChannel>(channel), userId), ChangeActor.Resolve(http));
        Assert.Equal((ChangeChannel.Job, (string?)null), ChangeActor.Resolve(null));
        using (ChangeActor.Use(ChangeChannel.Treatwell))
        {
            Assert.Equal((ChangeChannel.Treatwell, (string?)null), ChangeActor.Resolve(http));
        }
    }

    [Fact]
    public async Task Aenderung_an_einer_Tabelle_im_Schema_health_hat_keine_Werte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var recordId = Guid.NewGuid();
        await using var db = await OpenAsync();
        await ExecuteAsync(db, $"SELECT set_config('{ChangeHistory.UserSetting}', 'benutzer-1', false), set_config('{ChangeHistory.ChannelSetting}', 'Team', false)");
        await ExecuteAsync(db, $"""
            INSERT INTO health."AnamnesisRecords" ("Id", "CustomerId", "FormVersion", "Channel", "SubmittedAt", "ConsentGivenAt", "Payload")
            VALUES ('{recordId}', '{spa.Customer.Id}', 'v1', 'Tablet', now(), now(), '\x01'::bytea)
            """);
        await ExecuteAsync(db, $"""UPDATE health."AnamnesisRecords" SET "FormVersion" = 'v2', "Payload" = '\x02'::bytea WHERE "Id" = '{recordId}'""");

        var rows = await ListAsync(db, $$"""
            SELECT operation || '|' || coalesce(user_id, '') || '|' || channel || '|' || coalesce(changes::text, 'ohne Werte') || '|' || coalesce(customer_id::text, '')
              FROM sync.history WHERE table_schema = 'health' AND table_name = 'AnamnesisRecords' AND row_key = '{"Id": "{{recordId}}"}'::jsonb
             ORDER BY seq
            """);
        Assert.Equal([$"I|benutzer-1|Team|ohne Werte|{spa.Customer.Id}", $"U|benutzer-1|Team|ohne Werte|{spa.Customer.Id}"], rows);
    }

    [Fact]
    public async Task Eintraege_aelter_als_drei_Jahre_loescht_der_Loeschjob()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var now = DateTimeOffset.UtcNow;
        await using var db = await OpenAsync();
        var key = $$"""{"Id": "{{appointment.Id}}"}""";
        await ExecuteAsync(db, $"""
            INSERT INTO sync.history (table_schema, table_name, row_key, tenant_id, operation, changed_at, channel)
            VALUES ('aispa', 'Appointments', '{key}'::jsonb, '{spa.TenantId}', 'D', '{now.AddYears(-3).AddDays(-1):O}', 'Team'),
                   ('aispa', 'Appointments', '{key}'::jsonb, '{spa.TenantId}', 'U', '{now.AddYears(-3).AddDays(1):O}', 'Team')
            """);

        await fixture.Factory.Services.GetRequiredService<HistoryCleanup>().RunOnceAsync(now, Ct);

        var left = await ListAsync(db, $"""
            SELECT operation FROM sync.history WHERE table_name = 'Appointments' AND row_key = '{key}'::jsonb ORDER BY changed_at
            """);
        // Der knapp jüngere Eintrag und das Anlegen von eben bleiben, der ältere ist weg.
        Assert.Equal(["U", "I"], left);
    }

    [Fact]
    public async Task Gast_loeschen_loescht_den_Verlauf_seiner_Termine()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var guest = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Lea", $"Weg{Guid.NewGuid():N}", null, null, null, null), Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, customer: guest.Id);
        var kept = await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Anna.Id);
        _ = await spa.PutAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/note", new AppointmentNoteRequest("Bitte ruhig"), Ct);

        await using var db = await OpenAsync();
        Assert.Equal(2, await CountAsync(db, appointment.Id));

        // Gast löschen: zuerst seine Termine (Restrict am Gast), dann ihn selbst.
        await using (var tx = await db.BeginTransactionAsync(Ct))
        {
            await ExecuteAsync(db, $"""DELETE FROM aispa."ResourceBookings" WHERE "AppointmentId" = '{appointment.Id}'""");
            await ExecuteAsync(db, $"""DELETE FROM aispa."Appointments" WHERE "Id" = '{appointment.Id}'""");
            await ExecuteAsync(db, $"""DELETE FROM aispa."Customers" WHERE "Id" = '{guest.Id}'""");
            await tx.CommitAsync(Ct);
        }

        Assert.Equal(0, await CountAsync(db, appointment.Id));
        Assert.Equal(0L, await ScalarAsync<long>(db, $"SELECT count(*) FROM sync.history WHERE customer_id = '{guest.Id}'"));
        Assert.Equal(1, await CountAsync(db, kept.Id));
    }

    /// <summary>Weiteres Konto im Spa mit eigenem Anzeigenamen (wie „Lena“ an der Rezeption).</summary>
    private async Task<HttpClient> StaffAsync(TestSpa spa, string name, StaffRole role)
    {
        using var client = fixture.CreateClient(spa.Cookie);
        var response = await client.PostAsJsonAsync("/api/auth/dev/register",
            new DevRegistration($"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@aispa.test", name, role, [spa.Location.Id]), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        return fixture.CreateClient(string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
    }

    private static async Task<int> CountAsync(NpgsqlConnection db, Guid appointmentId) =>
        (int)await ScalarAsync<long>(db, $$"""SELECT count(*) FROM sync.history WHERE table_name = 'Appointments' AND row_key = '{"Id": "{{appointmentId}}"}'::jsonb""");

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var db = new NpgsqlConnection(fixture.ConnectionString);
        await db.OpenAsync(Ct);
        return db;
    }

    private static async Task ExecuteAsync(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        return (T)(await command.ExecuteScalarAsync(Ct))!;
    }

    private static async Task<List<string>> ListAsync(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = new List<string>();
        while (await reader.ReadAsync(Ct))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }
}
