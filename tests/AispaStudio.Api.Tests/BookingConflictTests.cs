using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Konflikte tragen den belegten Zeitraum und nie Uhrzeiten in UTC im Text (Schritt 18, Ä21): die Oberfläche zeigt Ortszeit.</summary>
public sealed class BookingConflictTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sperrzeit_ueber_einem_Termin_nennt_Zeitraum_und_Gast_ohne_UTC_im_Text()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);

        var response = await spa.Client.PostAsJsonAsync("/api/blocks",
            new BlockInput(ResourceKind.Employee, spa.Anna.Id, TestSpa.At(9), TestSpa.At(12), "Schulung"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var rejected = (await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
        Assert.Equal("Zeitraum ist belegt.", rejected.Message);
        var conflict = Assert.Single(rejected.Conflicts);
        Assert.Equal((ResourceKind.Employee, spa.Anna.Id, appointment.Id, false), (conflict.Kind, conflict.ResourceId, conflict.AppointmentId, conflict.IsBlock));
        Assert.Equal((appointment.BlockedFrom, appointment.BlockedUntil), (conflict.From, conflict.Until));
        Assert.StartsWith("Eva ", conflict.ResourceName, StringComparison.Ordinal);
        AssertNoUtcText(rejected);
    }

    [Fact]
    public async Task Sperrzeit_ueber_einer_Sperrzeit_nennt_deren_Grund_und_Zeitraum()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.Client.PostAsJsonAsync("/api/blocks",
            new BlockInput(ResourceKind.Employee, spa.Anna.Id, TestSpa.At(12), TestSpa.At(13), "Mittagspause"), TestSpa.Json, Ct);

        var response = await spa.Client.PostAsJsonAsync("/api/blocks",
            new BlockInput(ResourceKind.Employee, spa.Anna.Id, TestSpa.At(12, 30), TestSpa.At(14), "Schulung"), TestSpa.Json, Ct);

        var rejected = (await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
        var conflict = Assert.Single(rejected.Conflicts);
        Assert.Equal(("Mittagspause", TestSpa.At(12), TestSpa.At(13), true, null), (conflict.ResourceName, conflict.From, conflict.Until, conflict.IsBlock, conflict.AppointmentId));
        AssertNoUtcText(rejected);
    }

    [Fact]
    public async Task Besetzte_Person_beim_Buchen_nennt_die_Belegung_dahinter()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var first = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);

        var response = await spa.BookAsync(TestSpa.At(10, 30), Ct, employee: spa.Anna.Id);

        var rejected = (await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
        var conflict = Assert.Single(rejected.Conflicts);
        Assert.Equal((first.Id, first.BlockedFrom, first.BlockedUntil), (conflict.AppointmentId, conflict.From, conflict.Until));
        AssertNoUtcText(rejected);
    }

    private static void AssertNoUtcText(BookingRejected rejected) =>
        Assert.DoesNotContain("UTC", rejected.Message + string.Concat(rejected.Conflicts.Select(c => c.Reason + c.ResourceName)), StringComparison.Ordinal);
}
