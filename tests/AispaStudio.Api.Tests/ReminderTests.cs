using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake.Channels;
using AispaStudio.Api.Reminders;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

public sealed class ReminderTests(ApiFixture fixture)
{
    [Fact]
    public async Task Sends_first_and_second_reminder_by_mail_once_each()
    {
        var ct = TestContext.Current.CancellationToken;
        var (spa, customer) = await SpaWithGuestAsync(ct);
        var appointment = await BookAsync(spa, customer, ct);

        Assert.Equal(0, await PlanAsync(spa, appointment.Start.AddHours(-49), ct));
        Assert.Equal(1, await PlanAsync(spa, appointment.Start.AddHours(-47), ct));
        Assert.Equal(0, await PlanAsync(spa, appointment.Start.AddHours(-46), ct));

        var first = await fixture.Mailbox.WaitForAsync(customer.Email!, m => m.Subject.StartsWith("Erinnerung:", StringComparison.Ordinal), ct);
        Assert.Contains("Massage", first.Subject, StringComparison.Ordinal);
        Assert.Contains("Montag, 1. März um 10:00 Uhr", first.Text, StringComparison.Ordinal);
        Assert.Contains("/buchen/", first.Text, StringComparison.Ordinal);
        Assert.Contains("#/termin/", first.Text, StringComparison.Ordinal);
        Assert.Contains("Bitte bestätigen Sie kurz, dass Sie kommen", first.Text, StringComparison.Ordinal);

        Assert.Equal(1, await PlanAsync(spa, appointment.Start.AddMinutes(-90), ct));
        var reminders = await WaitForStatesAsync(spa, appointment.Id, 2, ct);
        Assert.Equal([1, 2], reminders.Select(r => r.Stage));
        Assert.All(reminders, r => Assert.Equal(ReminderChannel.Email, r.Channel));
        Assert.Equal(2, fixture.Mailbox.To(customer.Email!).Count(m => m.Subject.StartsWith("Erinnerung:", StringComparison.Ordinal)));
    }

    /// <summary>R05 Slice 96: Die native Maske speichert nur; geplant wird nach dem gespeicherten Stand, auch für schon gebuchte Termine.</summary>
    [Fact]
    public async Task Changing_the_first_reminder_from_24_to_48_hours_plans_it_48_hours_before()
    {
        var ct = TestContext.Current.CancellationToken;
        var (spa, customer) = await SpaWithGuestAsync(ct);
        await spa.PutAsync<ReminderSettingsDto>("/api/reminders/settings", new ReminderSettingsDto(true, 24, 0, ReminderChannel.Email, null, "de"), ct);
        var appointment = await BookAsync(spa, customer, ct);
        Assert.Equal(0, await PlanAsync(spa, appointment.Start.AddHours(-47), ct));

        await spa.PutAsync<ReminderSettingsDto>("/api/reminders/settings", new ReminderSettingsDto(true, 48, 0, ReminderChannel.Email, null, "de"), ct);

        Assert.Equal(1, await PlanAsync(spa, appointment.Start.AddHours(-47), ct));
        Assert.Equal(0, await PlanAsync(spa, appointment.Start.AddHours(-23), ct));
        var reminder = Assert.Single(await WaitForStatesAsync(spa, appointment.Id, 1, ct));
        Assert.Equal(1, reminder.Stage);
    }

    [Fact]
    public async Task Late_booking_gets_only_the_reminder_that_was_still_ahead()
    {
        var ct = TestContext.Current.CancellationToken;
        var (spa, customer) = await SpaWithGuestAsync(ct);
        var appointment = await BookAsync(spa, customer, ct);
        await SetCreatedAtAsync(spa, appointment.Id, appointment.Start.AddHours(-30), ct);

        // 48 Stunden vorher war der Termin noch nicht gebucht, also keine erste Erinnerung.
        Assert.Equal(0, await PlanAsync(spa, appointment.Start.AddHours(-29), ct));
        Assert.Equal(1, await PlanAsync(spa, appointment.Start.AddHours(-1), ct));
        var reminders = await WaitForStatesAsync(spa, appointment.Id, 1, ct);
        Assert.Equal(2, Assert.Single(reminders).Stage);
    }

    [Fact]
    public async Task Cancelled_appointment_gets_no_reminder_and_a_moved_one_gets_a_new_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var (spa, customer) = await SpaWithGuestAsync(ct);
        var cancelled = await BookAsync(spa, customer, ct);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest("Krank"), ct);
        Assert.Equal(0, await PlanAsync(spa, cancelled.Start.AddHours(-47), ct));

        var moved = await BookAsync(spa, customer, ct, hour: 14);
        Assert.Equal(1, await PlanAsync(spa, moved.Start.AddHours(-47), ct));
        await WaitForStatesAsync(spa, moved.Id, 1, ct);
        var later = await spa.PostAsync<AppointmentDto>($"/api/appointments/{moved.Id}/move",
            new MoveAppointmentRequest(moved.Start.AddHours(2), null, null, null), ct);

        // Neuer Beginn: die erste Erinnerung kommt für die neue Zeit noch einmal.
        Assert.Equal(1, await PlanAsync(spa, later.Start.AddHours(-47), ct));
        var reminders = await WaitForStatesAsync(spa, moved.Id, 2, ct);
        Assert.All(reminders, r => Assert.Equal("Sent", r.State));
    }

    [Fact]
    public async Task Reminder_for_an_outdated_start_is_skipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var (spa, customer) = await SpaWithGuestAsync(ct);
        var appointment = await BookAsync(spa, customer, ct);
        var reminderId = Guid.CreateVersion7();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
            db.AppointmentReminders.Add(new AppointmentReminder
            {
                Id = reminderId,
                AppointmentId = appointment.Id,
                Stage = 1,
                AppointmentStart = appointment.Start.AddHours(-3),
                Channel = ReminderChannel.Email,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            await scope.ServiceProvider.GetRequiredService<ReminderSender>().SendAsync(reminderId, ct);
        }

        var reminder = Assert.Single(await spa.GetAsync<List<AppointmentReminderDto>>($"/api/appointments/{appointment.Id}/reminders", ct));
        Assert.Equal("Skipped", reminder.State);
        Assert.Empty(fixture.Mailbox.To(customer.Email!));
    }

    [Fact]
    public async Task WhatsApp_uses_the_approved_template_and_falls_back_to_sms_without_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var (spa, customer) = await SpaWithGuestAsync(ct);
        var phone = PhoneNumbers.Normalize(customer.Phone!);

        var missing = await spa.Client.PutAsJsonAsync("/api/reminders/settings",
            new ReminderSettingsDto(true, 48, 2, ReminderChannel.WhatsApp, null, "de"), TestSpa.Json, ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var settings = await spa.PutAsync<ReminderSettingsDto>("/api/reminders/settings",
            new ReminderSettingsDto(true, 24, 0, ReminderChannel.WhatsApp, " termin_erinnerung ", "de_AT"), ct);
        Assert.Equal("termin_erinnerung", settings.WhatsAppTemplate);
        Assert.Equal(settings, await spa.GetAsync<ReminderSettingsDto>("/api/reminders/settings", ct));

        var appointment = await BookAsync(spa, customer, ct);
        Assert.Equal(1, await PlanAsync(spa, appointment.Start.AddHours(-23), ct));
        await WaitForStatesAsync(spa, appointment.Id, 1, ct);
        var message = Assert.Single(fixture.Texts.To(phone));
        Assert.Equal(BookingChannel.WhatsApp, message.Channel);
        Assert.Equal("termin_erinnerung", message.Template!.Name);
        Assert.Equal("de_AT", message.Template.Language);
        Assert.Equal(customer.FirstName, message.Template.Parameters[0]);
        Assert.Equal("Massage", message.Template.Parameters[1]);
        Assert.Contains("#/termin/", message.Template.Parameters[4], StringComparison.Ordinal);

        // SMS bevorzugt: ohne Telefonnummer geht es per Mail.
        await spa.PutAsync<ReminderSettingsDto>("/api/reminders/settings", new ReminderSettingsDto(true, 24, 0, ReminderChannel.Sms, null, "de"), ct);
        var noPhone = await spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Ida", $"Ohne{Guid.NewGuid():N}", $"ida-{Guid.NewGuid():N}@example.at", null, null, null), ct);
        var second = await BookAsync(spa, noPhone, ct, hour: 14);
        Assert.Equal(1, await PlanAsync(spa, second.Start.AddHours(-23), ct));
        Assert.Equal(ReminderChannel.Email, Assert.Single(await WaitForStatesAsync(spa, second.Id, 1, ct)).Channel);
    }

    [Fact]
    public async Task Settings_need_the_organization_right_and_reminders_stay_in_their_spa()
    {
        var ct = TestContext.Current.CancellationToken;
        var (spa, customer) = await SpaWithGuestAsync(ct);
        var appointment = await BookAsync(spa, customer, ct);
        var other = await TestSpa.CreateAsync(fixture, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.CreateClient().GetAsync("/api/reminders/settings", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"/api/appointments/{appointment.Id}/reminders", ct)).StatusCode);

        // Planen im anderen Spa sieht den Termin nicht.
        Assert.Equal(0, await PlanAsync(other, appointment.Start.AddHours(-47), ct));
    }

    [Fact]
    public void Twilio_sends_templates_as_content_sid_with_numbered_variables()
    {
        var fields = TwilioProvider.Fields(
            new OutgoingText(BookingChannel.WhatsApp, "+43660123", "Erinnerung", new TextTemplate("HXabc", "de", ["Eva", "Massage"])),
            whatsApp: true, "+43111");
        Assert.Equal("whatsapp:+43660123", fields["To"]);
        Assert.Equal("HXabc", fields["ContentSid"]);
        Assert.Equal("""{"1":"Eva","2":"Massage"}""", fields["ContentVariables"]);
        Assert.False(fields.ContainsKey("Body"));

        var sms = TwilioProvider.Fields(new OutgoingText(BookingChannel.Sms, "+43660123", "Erinnerung"), whatsApp: false, "+43111");
        Assert.Equal("Erinnerung", sms["Body"]);
        Assert.False(sms.ContainsKey("ContentSid"));
    }

    private async Task<(TestSpa Spa, CustomerDto Customer)> SpaWithGuestAsync(CancellationToken ct)
    {
        var spa = await TestSpa.CreateAsync(fixture, ct);
        var suffix = Guid.NewGuid().ToString("N");
        var customer = await spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Lena", $"Erinnerung{suffix}", $"lena-{suffix}@example.at",
                $"+43 660 {Random.Shared.Next(1_000_000, 9_999_999)}", null, null), ct);
        return (spa, customer);
    }

    private static async Task<AppointmentDto> BookAsync(TestSpa spa, CustomerDto customer, CancellationToken ct, int hour = 10) =>
        await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(hour), ct, customer: customer.Id), ct);

    private async Task<int> PlanAsync(TestSpa spa, DateTimeOffset now, CancellationToken ct)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        return await scope.ServiceProvider.GetRequiredService<ReminderPlanner>().PlanAsync(now, ct);
    }

    private async Task SetCreatedAtAsync(TestSpa spa, Guid appointmentId, DateTimeOffset createdAt, CancellationToken ct)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Appointments.Where(a => a.Id == appointmentId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.CreatedAt, createdAt), ct);
    }

    /// <summary>Versand läuft über die Queue: warten, bis so viele Erinnerungen nicht mehr in der Warteschlange stehen.</summary>
    private static async Task<List<AppointmentReminderDto>> WaitForStatesAsync(TestSpa spa, Guid appointmentId, int count, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var reminders = await spa.GetAsync<List<AppointmentReminderDto>>($"/api/appointments/{appointmentId}/reminders", timeout.Token);
            if (reminders.Count == count && reminders.All(r => r.State != "Queued"))
            {
                return reminders;
            }

            await Task.Delay(50, timeout.Token);
        }
    }
}
