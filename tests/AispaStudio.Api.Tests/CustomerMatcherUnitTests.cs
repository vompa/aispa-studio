using AispaStudio.Api.MailIntake;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Telefonnummern und Namen vergleichbar machen (ADR 0061), ohne Datenbank.</summary>
public sealed class CustomerMatcherUnitTests
{
    [Theory]
    [InlineData("0660 1234567", "AT", "+436601234567")]
    [InlineData("0660/123 45 67", "AT", "+436601234567")]
    [InlineData("+43 660 1234567", "AT", "+436601234567")]
    [InlineData("+43 (0) 660 1234567", "AT", "+436601234567")]
    [InlineData("0043-660-1234567", "DE", "+436601234567")]
    [InlineData("(0660) 123-4567", "AT", "+436601234567")]
    [InlineData("whatsapp:+436601234567", "AT", "+436601234567")]
    [InlineData("436601234567", "CH", "+436601234567")]
    [InlineData("0171 2345678", "DE", "+491712345678")]
    [InlineData("0171 2345678", "AT", "+431712345678")]
    [InlineData("079 123 45 67", "CH", "+41791234567")]
    [InlineData("+41 79 123 45 67", "AT", "+41791234567")]
    [InlineData("0660 1234567", "FR", "+436601234567")]
    [InlineData("0660 1234567", null, "+436601234567")]
    [InlineData("", "AT", "")]
    [InlineData("   ", "AT", "")]
    [InlineData(null, "AT", "")]
    [InlineData("12345", "AT", "")]
    [InlineData("kein Telefon", "AT", "")]
    [InlineData("660 1234567", "AT", "")]
    public void Telefonnummer_wird_zu_E164(string? raw, string? country, string expected) =>
        Assert.Equal(expected, PhoneNumbers.ToE164(raw, country));

    [Theory]
    [InlineData("0660 1234567", "+43 660 1234567", true)]
    [InlineData("+436601234567", "0660-1234567", true)]
    [InlineData("0660 1234567", "0660 1234568", false)]
    public void Verschiedene_Schreibweisen_derselben_Nummer_sind_gleich(string a, string b, bool same) =>
        Assert.Equal(same, PhoneNumbers.ToE164(a, "AT") == PhoneNumbers.ToE164(b, "AT"));

    [Theory]
    [InlineData("0660 123 45 67", 4, "4567")]
    [InlineData("12", 4, "12")]
    public void Letzte_Ziffern(string raw, int count, string expected) => Assert.Equal(expected, PhoneNumbers.LastDigits(raw, count));

    [Theory]
    [InlineData("Müller", "mueller")]
    [InlineData("MUELLER", "mueller")]
    [InlineData("Müller", "mueller")]
    [InlineData("Weiß", "weiss")]
    [InlineData("Strauß-Öhler", "strauss oehler")]
    [InlineData("José  Núñez", "jose nunez")]
    [InlineData("O'Brien", "o brien")]
    [InlineData("  Anna  ", "anna")]
    [InlineData(null, "")]
    public void Namen_werden_zum_Vergleich_vereinheitlicht(string? name, string expected) =>
        Assert.Equal(expected, CustomerMatcher.Fold(name));

    [Theory]
    [InlineData("sonja winkler", "sonja winkler", 0)]
    [InlineData("sonja winkler", "sonja winkller", 1)]
    [InlineData("sonja winkler", "sonia winkler", 1)]
    [InlineData("sonja winkler", "sonja winkelr", 2)]
    [InlineData("anna", "annabelle", 5)]
    public void Tippfehler_Abstand(string a, string b, int distance) =>
        Assert.Equal(distance, CustomerMatcher.Distance(a, b, 5));

    [Fact]
    public void Abstand_bricht_ab_wenn_er_ueber_der_Grenze_liegt() => Assert.Equal(2, CustomerMatcher.Distance("anna", "annabelle", 1));
}
