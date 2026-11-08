using AispaStudio.Desktop.Controls;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Die Leser der Eingabefelder (Design-Brief 5.17 bis 5.20) als Tabellen: gültige Eingaben mit Anzeigeformat, Fehlertexte, leere Eingabe.
/// Heute ist in allen Datumstests der 21.10.2026.
/// </summary>
public sealed class InputParserTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    // Uhrzeit

    [Theory]
    [InlineData("9", "09:00")]
    [InlineData("09", "09:00")]
    [InlineData("0", "00:00")]
    [InlineData("930", "09:30")]
    [InlineData("0930", "09:30")]
    [InlineData("1415", "14:15")]
    [InlineData("9:30", "09:30")]
    [InlineData("9.30", "09:30")]
    [InlineData("9,30", "09:30")]
    [InlineData("09:30", "09:30")]
    [InlineData("9:5", "09:05")]
    [InlineData("23:59", "23:59")]
    [InlineData("  9:30 ", "09:30")]
    public void Uhrzeit_wird_tolerant_gelesen_und_als_HHmm_angezeigt(string input, string display)
    {
        var result = TimeInputParser.Parse(input);
        Assert.True(result.IsValid);
        Assert.False(result.IsEmpty);
        Assert.Equal(display, TimeInputParser.Format(result.Value!.Value));
    }

    [Theory]
    [InlineData("24", TimeInputParser.RangeError)]
    [InlineData("9:60", TimeInputParser.RangeError)]
    [InlineData("25:00", TimeInputParser.RangeError)]
    [InlineData("2460", TimeInputParser.RangeError)]
    [InlineData("abc", TimeInputParser.FormatError)]
    [InlineData("9:", TimeInputParser.FormatError)]
    [InlineData(":30", TimeInputParser.FormatError)]
    [InlineData("12345", TimeInputParser.FormatError)]
    [InlineData("9:300", TimeInputParser.FormatError)]
    [InlineData("9:30:00", TimeInputParser.FormatError)]
    [InlineData("9 30", TimeInputParser.FormatError)]
    public void Ungueltige_Uhrzeit_ergibt_einen_Fehlertext_statt_einer_Ausnahme(string input, string error)
    {
        var result = TimeInputParser.Parse(input);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
        Assert.Null(TimeInputParser.TryParse(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Leere_Uhrzeit_ist_keine_Angabe_und_kein_Fehler(string? input)
    {
        var result = TimeInputParser.Parse(input);
        Assert.True(result.IsEmpty);
        Assert.True(result.IsValid);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("09:30", 5, "09:35")]
    [InlineData("09:30", -1, "09:29")]
    [InlineData("09:30", 60, "10:30")]
    [InlineData("23:58", 5, "23:59")]
    [InlineData("00:02", -5, "00:00")]
    public void Uhrzeit_Schritte_begrenzen_ohne_Umlauf_ueber_Mitternacht(string start, int minutes, string expected) =>
        Assert.Equal(expected, TimeInputParser.Format(TimeInputParser.Step(TimeInputParser.TryParse(start), minutes)));

    [Fact]
    public void Uhrzeit_Schritt_ohne_Wert_beginnt_beim_Minimum_sonst_um_neun()
    {
        Assert.Equal("09:00", TimeInputParser.Format(TimeInputParser.Step(null, 5)));
        Assert.Equal("08:00", TimeInputParser.Format(TimeInputParser.Step(null, 5, new TimeOnly(8, 0))));
    }

    [Fact]
    public void Uhrzeit_Schritt_haelt_Grenzen_ein() =>
        Assert.Equal("10:00", TimeInputParser.Format(TimeInputParser.Step(new TimeOnly(9, 58), 5, new TimeOnly(8, 0), new TimeOnly(10, 0))));

    // Datum

    [Theory]
    [InlineData("21102026", "21.10.2026")]
    [InlineData("211026", "21.10.2026")]
    [InlineData("21.10.26", "21.10.2026")]
    [InlineData("21.10.2026", "21.10.2026")]
    [InlineData("21.10.", "21.10.2026")]
    [InlineData("21.10", "21.10.2026")]
    [InlineData("2110", "21.10.2026")]
    [InlineData("1.2.26", "01.02.2026")]
    [InlineData("1.2.2027", "01.02.2027")]
    [InlineData("21/10/2026", "21.10.2026")]
    [InlineData("21-10-2026", "21.10.2026")]
    [InlineData("29.02.2028", "29.02.2028")]
    [InlineData("01.01.80", "01.01.1980")]
    [InlineData("01.01.27", "01.01.2027")]
    public void Datum_wird_tolerant_gelesen_und_als_TTMMJJJJ_angezeigt(string input, string display)
    {
        var result = DateInputParser.Parse(input, Today);
        Assert.True(result.IsValid);
        Assert.Equal(display, DateInputParser.Format(result.Value!.Value));
    }

    [Theory]
    [InlineData("30.02.2026", "Den 30.02.2026 gibt es nicht.")]
    [InlineData("31.04.2026", "Den 31.04.2026 gibt es nicht.")]
    [InlineData("29.02.2026", "Den 29.02.2026 gibt es nicht.")]
    [InlineData("32.01.2026", "Den 32.01.2026 gibt es nicht.")]
    [InlineData("21.13.2026", "Den 21.13.2026 gibt es nicht.")]
    [InlineData("00.10.2026", "Den 00.10.2026 gibt es nicht.")]
    [InlineData("abc", DateInputParser.FormatError)]
    [InlineData("21.10.2", DateInputParser.FormatError)]
    [InlineData("21.10.202", DateInputParser.FormatError)]
    [InlineData("21..10", DateInputParser.FormatError)]
    [InlineData("12345", DateInputParser.FormatError)]
    [InlineData("1.2.3.4", DateInputParser.FormatError)]
    public void Ungueltiges_Datum_ergibt_einen_Fehlertext(string input, string error)
    {
        var result = DateInputParser.Parse(input, Today);
        Assert.Equal(error, result.Error);
        Assert.Null(DateInputParser.TryParse(input, Today));
    }

    [Fact]
    public void Leeres_Datum_ist_keine_Angabe() => Assert.True(DateInputParser.Parse("  ", Today).IsEmpty);

    [Theory]
    [InlineData("14.03.85", "14.03.1985")]
    [InlineData("01.01.30", "01.01.1930")]
    [InlineData("21.10.26", "21.10.2026")]
    [InlineData("22.10.26", "22.10.1926")]
    public void Geburtstag_liegt_nie_in_der_Zukunft(string input, string display) =>
        Assert.Equal(display, DateInputParser.Format(DateInputParser.Parse(input, Today, new DateRules(NotInFuture: true)).Value!.Value));

    [Fact]
    public void Geburtstag_mit_Jahr_aus_der_Zukunft_ist_ein_Fehler() =>
        Assert.Equal("Das Datum liegt am 21.10.2026 oder früher.", DateInputParser.Parse("22.10.2026", Today, new DateRules(NotInFuture: true)).Error);

    [Fact]
    public void Datum_ohne_Jahr_ist_nur_erlaubt_wenn_das_Feld_es_zulaesst() =>
        Assert.Equal(DateInputParser.YearMissingError, DateInputParser.Parse("21.10.", Today, new DateRules(RequireYear: true)).Error);

    [Fact]
    public void Datum_ausserhalb_der_Grenzen_nennt_die_Grenzen()
    {
        var rules = new DateRules(new DateOnly(2026, 10, 22), new DateOnly(2027, 10, 21));
        Assert.Equal("Das Datum liegt zwischen 22.10.2026 und 21.10.2027.", DateInputParser.Parse("21.10.2026", Today, rules).Error);
        Assert.Equal("Das Datum liegt zwischen 22.10.2026 und 21.10.2027.", DateInputParser.Parse("22.10.2027", Today, rules).Error);
        Assert.True(DateInputParser.Parse("22.10.2026", Today, rules).IsValid);
    }

    [Theory]
    [InlineData(1, 0, 0, "22.10.2026")]
    [InlineData(-7, 0, 0, "14.10.2026")]
    [InlineData(0, 1, 0, "21.11.2026")]
    [InlineData(0, 0, 1, "21.10.2027")]
    [InlineData(0, 0, -1, "21.10.2025")]
    public void Datum_Schritte_in_Tagen_Monaten_und_Jahren(int days, int months, int years, string expected) =>
        Assert.Equal(expected, DateInputParser.Format(DateInputParser.Step(Today, Today, days, months, years)));

    [Fact]
    public void Datum_Schritt_ohne_Wert_setzt_den_heutigen_Tag() => Assert.Equal(Today, DateInputParser.Step(null, Today, 1));

    [Fact]
    public void Datum_Schritt_haelt_die_Grenzen_ein()
    {
        var rules = new DateRules(Maximum: new DateOnly(2026, 10, 25));
        Assert.Equal(new DateOnly(2026, 10, 25), DateInputParser.Step(new DateOnly(2026, 10, 24), Today, 7, rules: rules));
    }

    // Betrag

    [Theory]
    [InlineData("12", "12,00")]
    [InlineData("12,5", "12,50")]
    [InlineData("12.5", "12,50")]
    [InlineData("12.50", "12,50")]
    [InlineData("12,50", "12,50")]
    [InlineData("1.234,56", "1.234,56")]
    [InlineData("1234", "1.234,00")]
    [InlineData("1.234", "1.234,00")]
    [InlineData("1234,5", "1.234,50")]
    [InlineData("0,5", "0,50")]
    [InlineData(",5", "0,50")]
    [InlineData("12,", "12,00")]
    [InlineData("0", "0,00")]
    [InlineData("12 €", "12,00")]
    [InlineData("€ 12,50", "12,50")]
    [InlineData("1 234,50", "1.234,50")]
    public void Betrag_wird_gelesen_und_mit_zwei_Nachkommastellen_angezeigt(string input, string display)
    {
        var result = MoneyInputParser.Parse(input);
        Assert.True(result.IsValid);
        Assert.Equal(display, MoneyInputParser.Format(result.Value!.Value));
    }

    [Theory]
    [InlineData("12,505", MoneyInputParser.FractionError)]
    [InlineData("12,500", MoneyInputParser.FractionError)]
    [InlineData("0,001", MoneyInputParser.FractionError)]
    [InlineData("abc", MoneyInputParser.FormatError)]
    [InlineData("1,2,3", MoneyInputParser.FormatError)]
    [InlineData("12.5.5", MoneyInputParser.FormatError)]
    [InlineData("1.23,50", MoneyInputParser.FormatError)]
    [InlineData("12-5", MoneyInputParser.FormatError)]
    [InlineData("-5", MoneyInputParser.NegativeError)]
    public void Betrag_wird_nie_stumm_gerundet_und_Unlesbares_ist_ein_Fehler(string input, string error) =>
        Assert.Equal(error, MoneyInputParser.Parse(input).Error);

    [Fact]
    public void Leerer_Betrag_ist_keine_Angabe_und_nicht_null()
    {
        Assert.True(MoneyInputParser.Parse("").IsEmpty);
        Assert.Null(MoneyInputParser.Parse("").Value);
        Assert.Equal(0m, MoneyInputParser.Parse("0").Value);
    }

    [Fact]
    public void Negativer_Betrag_ist_nur_bei_Storno_und_Gutschrift_erlaubt()
    {
        var rules = new MoneyRules(AllowNegative: true);
        Assert.Equal(-5m, MoneyInputParser.Parse("-5", rules).Value);
        Assert.Equal(-12.5m, MoneyInputParser.Parse("-12,5", rules).Value);
    }

    [Fact]
    public void Betrag_mit_Grenzen_nennt_die_Grenzen() =>
        Assert.Equal("Der Betrag liegt zwischen 0,00 und 100,00.", MoneyInputParser.Parse("100,01", new MoneyRules(Minimum: 0m, Maximum: 100m)).Error);

    [Theory]
    [InlineData("10", 1, "11")]
    [InlineData("10", -20, "0")]
    [InlineData("10,5", -0.1, "10,4")]
    public void Betrag_Schritte_begrenzen_bei_null(string start, double delta, string expected) =>
        Assert.Equal(decimal.Parse(expected, GermanNumbersForTests), MoneyInputParser.Step(MoneyInputParser.TryParse(start), (decimal)delta));

    // Zahl

    private static readonly NumberRules Minutes = new(0, 120);

    [Theory]
    [InlineData("5", "5")]
    [InlineData("0", "0")]
    [InlineData("120", "120")]
    [InlineData(" 45 ", "45")]
    public void Zahl_wird_gelesen_und_angezeigt(string input, string display)
    {
        var result = NumberInputParser.Parse(input, Minutes);
        Assert.True(result.IsValid);
        Assert.Equal(display, NumberInputParser.Format(result.Value!.Value, Minutes));
    }

    [Theory]
    [InlineData("121", "Der Wert liegt zwischen 0 und 120.")]
    [InlineData("-1", NumberInputParser.FormatError)]
    [InlineData("abc", NumberInputParser.FormatError)]
    [InlineData("5+5", NumberInputParser.FormatError)]
    [InlineData("12,5", NumberInputParser.WholeError)]
    public void Ungueltige_Zahl_ergibt_einen_Fehlertext_mit_den_Grenzen(string input, string error) =>
        Assert.Equal(error, NumberInputParser.Parse(input, Minutes).Error);

    [Fact]
    public void Zahl_mit_Nachkommastellen_und_Tausenderpunkt()
    {
        var rules = new NumberRules(0, 999999, 3);
        Assert.Equal(1234.5m, NumberInputParser.Parse("1.234,5", rules).Value);
        Assert.Equal("1.234,5", NumberInputParser.Format(1234.5m, rules));
        Assert.Equal("Höchstens 3 Nachkommastellen.", NumberInputParser.Parse("1,2345", rules).Error);
    }

    [Fact]
    public void Minuszeichen_nur_bei_einem_Minimum_unter_null()
    {
        Assert.Equal(-5m, NumberInputParser.Parse("-5", new NumberRules(-10, 10)).Value);
        Assert.True(NumberInputParser.AllowedCharacters(new NumberRules(-10, 10)).Contains('-', StringComparison.Ordinal));
        Assert.False(NumberInputParser.AllowedCharacters(Minutes).Contains('-', StringComparison.Ordinal));
    }

    [Fact]
    public void Leere_Zahl_ist_keine_Angabe_und_nicht_null() => Assert.True(NumberInputParser.Parse("", Minutes).IsEmpty);

    [Theory]
    [InlineData(5, 5, 10)]
    [InlineData(118, 5, 120)]
    [InlineData(3, -5, 0)]
    public void Zahl_Schritte_begrenzen_ohne_Umlauf(int start, int delta, int expected) =>
        Assert.Equal(expected, NumberInputParser.Step(start, delta, Minutes));

    private static readonly System.Globalization.CultureInfo GermanNumbersForTests = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
}
