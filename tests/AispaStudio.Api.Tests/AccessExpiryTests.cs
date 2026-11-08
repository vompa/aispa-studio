using System.Net.Http.Json;
using AispaStudio.Api.Access;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Vor dem Ende eines befristeten Zugangs bekommen die Person und ihre Leitung einmal Bescheid.</summary>
public sealed class AccessExpiryTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Person_und_Leitung_bekommen_einmal_je_Ende_Bescheid()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var owner = await spa.Client.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var person = await reception.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);
        using var colleague = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id]);
        var other = await colleague.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);
        var scheduler = fixture.Factory.Services.GetRequiredService<AccessExpiryScheduler>();
        var now = DateTimeOffset.UtcNow;

        await spa.PutAsync<StaffMemberDto>($"/api/staff/{person!.Id}",
            new StaffAccessInput(StaffRole.Reception, false, [spa.Location.Id], null, true, now.AddDays(20)), Ct);
        await scheduler.RunOnceAsync(now, Ct);
        Assert.Empty(fixture.Mailbox.To(person.Email));

        await spa.PutAsync<StaffMemberDto>($"/api/staff/{person.Id}",
            new StaffAccessInput(StaffRole.Reception, false, [spa.Location.Id], null, true, now.AddDays(3)), Ct);
        await scheduler.RunOnceAsync(now, Ct);
        await scheduler.RunOnceAsync(now.AddHours(1), Ct);

        var own = await fixture.Mailbox.WaitForAsync(person.Email, m => m.Subject.Contains("endet bald", StringComparison.Ordinal), Ct);
        var lead = await fixture.Mailbox.WaitForAsync(owner!.Email, m => m.Subject.Contains(person.DisplayName, StringComparison.Ordinal), Ct);
        Assert.Contains("bis einschließlich", own.Text, StringComparison.Ordinal);
        Assert.Contains("Rezeption", lead.Text, StringComparison.Ordinal);
        Assert.Single(fixture.Mailbox.To(person.Email));
        // Wer Zugänge nicht verwalten darf, bekommt nichts.
        Assert.Empty(fixture.Mailbox.To(other!.Email));

        // Verlängert, aber wieder bald: neuer Bescheid für das neue Ende.
        await spa.PutAsync<StaffMemberDto>($"/api/staff/{person.Id}",
            new StaffAccessInput(StaffRole.Reception, false, [spa.Location.Id], null, true, now.AddDays(5)), Ct);
        await scheduler.RunOnceAsync(now, Ct);
        await fixture.Mailbox.WaitForAsync(person.Email, _ => fixture.Mailbox.To(person.Email).Count == 2, Ct);
    }
}
