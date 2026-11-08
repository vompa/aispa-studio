using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Telefonnummern im Kundenstamm (R07 Slice 140): jede Schreibweise wird beim Speichern zu „+43 664 1234567“; was sich nicht
/// als Nummer lesen lässt, bleibt wie getippt.
/// </summary>
public sealed class CustomerPhoneTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("0664 1234567", "+43 664 1234567")]
    [InlineData("0664/1234567", "+43 664 1234567")]
    [InlineData("+43 664 1234567", "+43 664 1234567")]
    [InlineData("0043 664 1234567", "+43 664 1234567")]
    [InlineData("+43 (0) 664 123 45 67", "+43 664 1234567")]
    [InlineData("436641234567", "+43 664 1234567")]
    [InlineData(" 0664-123 45 67 ", "+43 664 1234567")]
    [InlineData("01 5123456", "+43 1 5123456")]
    public async Task Given_eine_Nummer_in_jeder_Schreibweise_When_man_anlegt_Then_speichert_die_API_das_einheitliche_Format(string typed, string expected)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PostAsJsonAsync("/api/customers", new CustomerInput("Anna", $"Gruber{Guid.NewGuid():N}", null, typed, null, null), TestSpa.Json, Ct);
        var created = await TestSpa.ReadAsync<CustomerDto>(response, Ct);

        Assert.Equal(expected, created.Phone);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("+43 660 123")]
    public async Task Eine_nicht_lesbare_Nummer_bleibt_wie_getippt(string typed)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PostAsJsonAsync("/api/customers", new CustomerInput("Anna", $"Gruber{Guid.NewGuid():N}", null, typed, null, null), TestSpa.Json, Ct);
        var created = await TestSpa.ReadAsync<CustomerDto>(response, Ct);

        Assert.Equal(typed, created.Phone);
    }

    [Fact]
    public async Task Auch_das_Aendern_der_Stammdaten_vereinheitlicht_die_Nummer()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PutAsJsonAsync($"/api/customers/{spa.Customer.Id}",
            new CustomerInput(spa.Customer.FirstName, spa.Customer.LastName, null, "0664 1234567", null, null), TestSpa.Json, Ct);
        var changed = await TestSpa.ReadAsync<CustomerDto>(response, Ct);

        Assert.Equal("+43 664 1234567", changed.Phone);
    }

    [Theory]
    [InlineData("0664 1234567", "AT", "+43 664 1234567")]
    [InlineData("0171 1234567", "DE", "+49 171 1234567")]
    [InlineData("079 1234567", "CH", "+41 79 1234567")]
    [InlineData("+44 20 7946 0958", "AT", "+442079460958")]
    public void Format_gruppiert_nach_der_Landesvorwahl(string typed, string country, string expected) =>
        Assert.Equal(expected, PhoneNumbers.Format(typed, country));

    [Fact]
    public void Format_gibt_bei_unlesbarer_Nummer_leer_zurueck() => Assert.Equal("", PhoneNumbers.Format("abc", "AT"));
}
