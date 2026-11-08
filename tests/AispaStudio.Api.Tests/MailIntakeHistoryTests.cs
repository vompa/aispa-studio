using System.Net;
using AispaStudio.Contracts;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>Ä184, Slice 90: Termine der Terminannahme tragen im Verlauf den Kanal der Nachricht.</summary>
public sealed partial class MailIntakeTests
{
    [Fact]
    public async Task SMS_der_Terminannahme_steht_als_Angelegt_per_SMS_mit_Zeitpunkt_im_Verlauf()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var phone = NewPhone();
        var form = new Dictionary<string, string>
        {
            ["MessageSid"] = $"SM{Guid.NewGuid():N}",
            ["From"] = phone,
            ["To"] = "+43676800100",
            ["Body"] = $"Hallo, bitte eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name}. Eva Sommer",
        };

        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        using var anonymous = fixture.CreateClient();
        using var response = await PostTwilioAsync(anonymous, form, Sign(form), spa.Spa.TenantId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mail = await spa.WaitForSenderAsync(phone, Ct);
        Assert.Equal(InboundMailStatus.Booked, mail.Status);

        var created = (await spa.Spa.GetAsync<List<HistoryEntry>>($"/api/appointments/{mail.AppointmentId}/history", Ct))[^1];
        Assert.Equal("Angelegt per SMS", created.Text);
        Assert.InRange(created.At, before, DateTimeOffset.UtcNow.AddSeconds(5));

        // Der Kanal steht in der Zeile selbst (die Terminannahme setzt ihn), nicht nur aus der Quelle des Termins abgeleitet.
        await using var db = new NpgsqlConnection(fixture.ConnectionString);
        await db.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            $$"""SELECT channel FROM sync.history WHERE table_name = 'Appointments' AND operation = 'I' AND row_key = '{"Id": "{{mail.AppointmentId}}"}'::jsonb""", db);
        Assert.Equal("Sms", await command.ExecuteScalarAsync(Ct));
    }
}
