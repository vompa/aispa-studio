using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Tests;

/// <summary>Namensregeln (ADR 0064): Rufname im Haus, gültiger Name nach außen, Titel in Adresszeile und Anrede je Land.</summary>
public sealed class PersonNameTests
{
    [Fact]
    public void Adresszeile_nennt_alle_Titel_vor_und_nach_dem_gueltigen_Namen()
    {
        Assert.Equal("Frau Mag. Stefanie Huber, MSc",
            PersonNames.AddressLine(CustomerSalutation.Female, "Mag.", "Stefanie", "Huber", "MSc"));
        Assert.Equal("Herr Prof. Dr. Paul Berger, MBA, PhD",
            PersonNames.AddressLine(CustomerSalutation.Male, "Prof. Dr.", "Paul", "Berger", "MBA, PhD"));
        Assert.Equal("Kim Novak", PersonNames.AddressLine(CustomerSalutation.Diverse, null, "Kim", "Novak", null));
    }

    [Theory]
    [InlineData(FiscalCountry.AT, "Mag. Dr.", "MSc", "Frau Dr. Huber")]
    [InlineData(FiscalCountry.AT, "Mag. DI", null, "Frau Mag. Huber")]
    [InlineData(FiscalCountry.AT, "KR Ing.", null, "Frau Ing. Huber")]
    [InlineData(FiscalCountry.AT, null, "MSc", "Frau Huber")]
    [InlineData(FiscalCountry.DE, "Mag. Dr.", "MSc", "Frau Dr. Huber")]
    [InlineData(FiscalCountry.DE, "Prof. Dr.", null, "Frau Prof. Dr. Huber")]
    [InlineData(FiscalCountry.DE, "Mag.", null, "Frau Huber")]
    [InlineData(FiscalCountry.CH, "Dr.", null, "Frau Huber")]
    public void Anrede_nimmt_Titel_nach_dem_Land_des_Standorts(FiscalCountry country, string? before, string? after, string expected)
    {
        Assert.Equal(expected, PersonNames.GreetingName(CustomerSalutation.Female, before, "Stefanie", "Huber", country));
        Assert.DoesNotContain(after ?? "§", PersonNames.Greeting(CustomerSalutation.Female, before, "Stefanie", "Huber", country), StringComparison.Ordinal);
    }

    [Fact]
    public void Anrede_ist_je_Kunde_uebersteuerbar()
    {
        Assert.Equal("Herr Dr. Lang", PersonNames.GreetingName(CustomerSalutation.Male, "Dr.", "Peter", "Lang", FiscalCountry.CH, titleInGreeting: true));
        Assert.Equal("Herr Lang", PersonNames.GreetingName(CustomerSalutation.Male, "Dr.", "Peter", "Lang", FiscalCountry.AT, titleInGreeting: false));
        Assert.Equal("Herr Mag. Lang", PersonNames.GreetingName(CustomerSalutation.Male, "Mag.", "Peter", "Lang", FiscalCountry.DE, titleInGreeting: true));
    }

    [Fact]
    public void Ohne_Frau_oder_Herr_spricht_die_Anrede_mit_vollem_Namen_an()
    {
        Assert.Equal("Guten Tag Mag. Kim Berger", PersonNames.Greeting(CustomerSalutation.Diverse, "Mag.", "Kim", "Berger", FiscalCountry.AT));
        Assert.Equal("Guten Tag Kim Berger", PersonNames.Greeting(CustomerSalutation.None, null, "Kim", "Berger", FiscalCountry.AT));
        Assert.Equal("Guten Tag Eva", PersonNames.Greeting(CustomerSalutation.Female, null, "Eva", "", FiscalCountry.AT));
    }

    [Fact]
    public void Rufname_ersetzt_im_Haus_den_Vornamen_der_gueltige_Name_bleibt()
    {
        Assert.Equal("Steffi Huber", PersonNames.InHouse("Stefanie", "Huber", "Steffi"));
        Assert.Equal("Stefanie Huber", PersonNames.InHouse("Stefanie", "Huber", " "));
        Assert.Equal("Stefanie Huber", PersonNames.Legal("Stefanie", "Huber"));
    }

    [Fact]
    public void Symbol_nur_bei_Frau_und_Herr()
    {
        Assert.Equal("♀", PersonNames.Symbol(CustomerSalutation.Female));
        Assert.Equal("♂", PersonNames.Symbol(CustomerSalutation.Male));
        Assert.Null(PersonNames.Symbol(CustomerSalutation.Diverse));
        Assert.Null(PersonNames.Symbol(CustomerSalutation.None));
    }

    [Fact]
    public void Namen_ohne_Titel_vergleichen_und_Titel_einheitlich_speichern()
    {
        Assert.Equal("Stefanie Huber", PersonNames.WithoutTitles("Mag. Dr. Stefanie Huber, MSc"));
        Assert.Equal("Li Ma", PersonNames.WithoutTitles("Li Ma"));
        Assert.Equal("Mag. Dr.", PersonNames.NormalizeTitles(" Mag.  Dr. ", after: false));
        Assert.Equal("MSc, MBA", PersonNames.NormalizeTitles("MSc MBA", after: true));
        Assert.Null(PersonNames.NormalizeTitles(" ", after: true));
    }
}
