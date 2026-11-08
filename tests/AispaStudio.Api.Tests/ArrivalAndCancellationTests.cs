using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Termine und Absagen im Assistenten (Ä317): wer kommt als Nächstes, wer ist noch nicht da, wer hat heute abgesagt, ob eine Absage jetzt etwas kostet und wer auf der
/// Warteliste steht. Die Stornoregel des Standorts: 50 % Ausfallgebühr bei Absage weniger als 24 Stunden vorher; die Massage kostet 79,00 EUR.
/// </summary>
public sealed class ArrivalAndCancellationTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly NextMonday = TestSpa.Monday.AddDays(7);

    private readonly AnswerKit _kit = new(fixture);

    private static Task<CustomerDto> GuestAsync(TestSpa spa, string first, string last) =>
        spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput(first, last, null, null, null, null), Ct);

    private static Task<LocationOnlineRules> FeeRuleAsync(TestSpa spa) =>
        spa.PutAsync<LocationOnlineRules>($"/api/locations/{spa.Location.Id}/online-rules", new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 50), Ct);

    private static Task<AppointmentDto> CancelAsync(TestSpa spa, AppointmentDto appointment, string reason) =>
        spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/cancel", new CancelAppointmentRequest(reason), Ct);

    /// <summary>Die Absage geschah zu <paramref name="at"/>: der Termin trägt die Zeit seiner letzten Änderung (ohne eigenes Feld für die Absage).</summary>
    private async Task CancelledAtAsync(TestSpa spa, AppointmentDto appointment, DateTimeOffset at)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Appointments.Where(a => a.Id == appointment.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.UpdatedAt, at), Ct);
    }

    private async Task<AssistantBlock> FeeAsync(TestSpa spa, AppointmentDto open, DateTimeOffset now, StaffRole role = StaffRole.Reception, Guid? employeeId = null) =>
        (await _kit.StaffAsync(spa, new AssistantQuestion("fällt eine gebühr an", null, Reference: new AssistantReference(open.Id, null, null)), role: role, now: now,
            employeeId: employeeId)).Block!;

    [Fact]
    public async Task Termin_vor_zehn_Minuten_ohne_Einchecken_steht_bei_noch_nicht_da()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var late = await GuestAsync(spa, "Christian", "Steiner");
        var here = await GuestAsync(spa, "Petra", "Schwarz");
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, customer: late.Id);
        var checkedIn = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Ben.Id, customer: here.Id);
        await spa.SetStatusAsync(checkedIn.Id, AppointmentStatus.CheckedIn, Ct);

        var block = await _kit.AnswerAsync(spa, "wer ist noch nicht da", now: TestSpa.At(10, 10));

        Assert.Equal("1 Gast ist noch nicht eingecheckt.", block.Text);
        var item = Assert.Single(block.Items!);
        Assert.Equal(("Christian Steiner", "10:00", "Massage · Anna · seit 10 Min."), (item.Title, item.Lead, item.Detail));
    }

    [Fact]
    public async Task Wer_kommt_als_naechstes_nennt_den_naechsten_Termin_ab_jetzt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await GuestAsync(spa, "Christian", "Steiner");
        var schwarz = await GuestAsync(spa, "Petra", "Schwarz");
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, customer: schwarz.Id);
        await spa.BookOkAsync(TestSpa.At(14, date: NextMonday), Ct, spa.Ben.Id, customer: schwarz.Id);
        await spa.BookOkAsync(TestSpa.At(10, date: NextMonday), Ct, spa.Anna.Id, customer: steiner.Id);

        var block = await _kit.AnswerAsync(spa, "wer kommt als nächstes");

        Assert.Equal("Als Nächstes im Standort Wien: 2 Termine.", block.Text);
        Assert.Equal(("Christian Steiner", "08.03. 10:00", "Massage · Anna"), (block.Items![0].Title, block.Items[0].Lead, block.Items[0].Detail));
        Assert.Equal("Petra Schwarz", block.Items[1].Title);
    }

    [Fact]
    public async Task Absage_fuenf_Stunden_vorher_kostet_fuenfzig_Prozent_mit_Betrag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await FeeRuleAsync(spa);
        var open = await spa.BookOkAsync(TestSpa.At(14, date: NextMonday), Ct, spa.Ben.Id);

        var block = await FeeAsync(spa, open, TestSpa.At(9, date: NextMonday));

        Assert.Equal("Absage jetzt: 5 Std. vor Beginn, Frist 24 Std. – Gebühr 50 % = 39,50 EUR. Keine Karte hinterlegt.", block.Text);
    }

    [Fact]
    public async Task Absage_vor_der_Frist_ist_kostenlos()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await FeeRuleAsync(spa);
        var open = await spa.BookOkAsync(TestSpa.At(14, date: NextMonday), Ct, spa.Ben.Id);

        var block = await FeeAsync(spa, open, TestSpa.At(8, date: NextMonday.AddDays(-1)));

        Assert.Equal("Bis 24 Std. vorher kostenlos; jetzt noch 30 Std. – keine Gebühr.", block.Text);
    }

    [Fact]
    public async Task Gebuehr_ohne_Kassarecht_nennt_nur_den_Prozentsatz()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await FeeRuleAsync(spa);
        var open = await spa.BookOkAsync(TestSpa.At(14, date: NextMonday), Ct, spa.Ben.Id);

        var block = await FeeAsync(spa, open, TestSpa.At(9, date: NextMonday), StaffRole.Therapist, spa.Ben.Id);

        Assert.Equal("Absage jetzt: 5 Std. vor Beginn, Frist 24 Std. – Gebühr 50 % des Preises. Keine Karte hinterlegt.", block.Text);
    }

    [Fact]
    public async Task Absage_genau_an_der_Frist_ist_noch_kostenlos()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await FeeRuleAsync(spa);
        var open = await spa.BookOkAsync(TestSpa.At(14, date: NextMonday), Ct, spa.Ben.Id);

        var block = await FeeAsync(spa, open, TestSpa.At(14, date: NextMonday.AddDays(-1)));

        Assert.Equal("Bis 24 Std. vorher kostenlos; jetzt noch 24 Std. – keine Gebühr.", block.Text);
    }

    [Fact]
    public async Task Gebuehr_gilt_fuer_den_offenen_Termin_nicht_den_fruehesten_des_Gastes()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await FeeRuleAsync(spa);
        var steiner = await GuestAsync(spa, "Christian", "Steiner");
        await spa.BookOkAsync(TestSpa.At(10, date: NextMonday), Ct, spa.Anna.Id, customer: steiner.Id);
        var open = await spa.BookOkAsync(TestSpa.At(14, date: NextMonday), Ct, spa.Ben.Id, customer: steiner.Id);

        var block = (await _kit.StaffAsync(spa, new AssistantQuestion("Fällt eine Gebühr an, wenn sie jetzt absagt?", null,
            Reference: new AssistantReference(open.Id, null, null)), role: StaffRole.Reception, now: TestSpa.At(9, date: NextMonday))).Block!;

        Assert.Equal("Absage jetzt: 5 Std. vor Beginn, Frist 24 Std. – Gebühr 50 % = 39,50 EUR. Keine Karte hinterlegt.", block.Text);
    }

    [Fact]
    public async Task Termine_heute_nachmittag_zeigt_nur_den_Nachmittag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Ben.Id);

        var answer = await _kit.StaffAsync(spa, new AssistantQuestion("Termine heute nachmittag", null), role: StaffRole.Reception, now: TestSpa.At(8));

        Assert.Equal(["14:00"], answer.Block!.Items!.Select(i => i.Lead));
    }

    [Fact]
    public async Task Absage_innerhalb_der_Frist_ist_als_spaet_markiert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await FeeRuleAsync(spa);
        var steiner = await GuestAsync(spa, "Christian", "Steiner");
        var late = await CancelAsync(spa, await spa.BookOkAsync(TestSpa.At(14, date: NextMonday), Ct, spa.Ben.Id, customer: steiner.Id), "Krank");
        await CancelledAtAsync(spa, late, TestSpa.At(9, date: NextMonday));

        var block = await _kit.AnswerAsync(spa, "wer hat heute abgesagt", now: TestSpa.At(10, date: NextMonday));

        var item = Assert.Single(block.Items!);
        Assert.Equal("abgesagt für Montag, 08.03.2027, 14:00 Uhr · Massage · Grund: Krank · spät", item.Detail);
    }

    [Fact]
    public async Task Wer_hat_heute_abgesagt_nennt_die_heutige_Absage()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var steiner = await GuestAsync(spa, "Christian", "Steiner");
        var schwarz = await GuestAsync(spa, "Petra", "Schwarz");
        var today = await CancelAsync(spa, await spa.BookOkAsync(TestSpa.At(14, date: NextMonday), Ct, spa.Ben.Id, customer: steiner.Id), "Krank");
        var yesterday = await CancelAsync(spa, await spa.BookOkAsync(TestSpa.At(10, date: NextMonday), Ct, spa.Anna.Id, customer: schwarz.Id), "Urlaub");
        await CancelledAtAsync(spa, today, AnswerKit.Now.AddHours(-1));
        await CancelledAtAsync(spa, yesterday, AnswerKit.Now.AddDays(-1));

        var block = await _kit.AnswerAsync(spa, "wer hat heute abgesagt");

        var item = Assert.Single(block.Items!);
        Assert.Equal(("Christian Steiner", "abgesagt für Montag, 08.03.2027, 14:00 Uhr · Massage · Grund: Krank"), (item.Title, item.Detail));
    }

    [Fact]
    public async Task Warteliste_nennt_Gast_und_Wunschzeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var online = spa.OnlineClient();
        var joined = await online.PostAsJsonAsync("/api/online/waitlist", new WaitlistRequest(spa.Location.Id, spa.MassageTreatment.Id, NextMonday,
            NextMonday.AddDays(4), DayPart.Afternoon, new OnlineContact("Wanda", "Warte", $"warte-{Guid.NewGuid():N}@example.at", null), true, "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, joined.StatusCode);

        var block = await _kit.AnswerAsync(spa, "wer steht auf der warteliste");

        var item = Assert.Single(block.Items!);
        Assert.Equal("Wanda Warte", item.Title);
        Assert.StartsWith("Massage · 08.03.–12.03., nachmittags", item.Detail, StringComparison.Ordinal);
    }
}
