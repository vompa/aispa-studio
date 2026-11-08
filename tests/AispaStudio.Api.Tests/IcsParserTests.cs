using System.Net;
using AispaStudio.Api.CalendarSubscriptions;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Der schlanke ICS-Leser der Kalender-Abos (ADR 0075): ganztägige und zeitgebundene Einträge, Zeitzonen, Wiederholungen, Ausnahmen, kaputte und zu große Dateien.
/// Ohne Datenbank und ohne Netz.
/// </summary>
public sealed class IcsParserTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly Until = new(2028, 12, 31);

    private static FeedParseResult Parse(string body, DateOnly? from = null, DateOnly? until = null) =>
        IcsParser.Parse($"BEGIN:VCALENDAR\r\nVERSION:2.0\r\n{body}END:VCALENDAR\r\n", from ?? From, until ?? Until, Vienna);

    private static string Event(string lines) => $"BEGIN:VEVENT\r\n{lines}\r\nEND:VEVENT\r\n";

    [Fact]
    public void Ganztaegig_mit_exklusivem_Ende_wird_zum_letzten_Tag_einschliesslich()
    {
        var result = Parse(Event("DTSTART;VALUE=DATE:20261026\r\nDTEND;VALUE=DATE:20261027\r\nSUMMARY:Nationalfeiertag"));
        var e = Assert.Single(result.Events);
        Assert.Equal((new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 26), "Nationalfeiertag", true), (e.Start, e.EndInclusive, e.Summary, e.AllDay));

        var range = Assert.Single(Parse(Event("DTSTART;VALUE=DATE:20261027\r\nDTEND;VALUE=DATE:20261102\r\nSUMMARY:Herbstferien")).Events);
        Assert.Equal((new DateOnly(2026, 10, 27), new DateOnly(2026, 11, 1)), (range.Start, range.EndInclusive));

        // Ohne DTEND ist es ein einzelner Tag.
        var single = Assert.Single(Parse(Event("DTSTART;VALUE=DATE:20261101\r\nSUMMARY:Allerheiligen")).Events);
        Assert.Equal(single.Start, single.EndInclusive);
    }

    [Fact]
    public void Zeitgebundene_Eintraege_werden_in_die_Zeitzone_des_Standorts_umgerechnet()
    {
        // 23:30 UTC am 24.12. ist 00:30 am 25.12. in Wien (Winterzeit).
        var utc = Assert.Single(Parse(Event("DTSTART:20261224T233000Z\r\nDTEND:20261225T003000Z\r\nSUMMARY:Messe")).Events);
        Assert.Equal((new DateOnly(2026, 12, 25), new TimeOnly(0, 30), new TimeOnly(1, 30), false), (utc.Start, utc.StartTime, utc.EndTime, utc.AllDay));

        // Sommerzeit: 10:00 in New York (UTC-4) ist 16:00 in Wien.
        var zoned = Assert.Single(Parse(Event("DTSTART;TZID=America/New_York:20260715T100000\r\nDTEND;TZID=America/New_York:20260715T110000\r\nSUMMARY:Anruf")).Events);
        Assert.Equal((new TimeOnly(16, 0), new TimeOnly(17, 0)), (zoned.StartTime, zoned.EndTime));

        // Schwebend gilt als Ortszeit des Standorts.
        var floating = Assert.Single(Parse(Event("DTSTART:20260301T090000\r\nDURATION:PT2H\r\nSUMMARY:Schulung")).Events);
        Assert.Equal((new TimeOnly(9, 0), new TimeOnly(11, 0)), (floating.StartTime, floating.EndTime));
    }

    [Fact]
    public void Wiederholung_jaehrlich_wird_nur_im_Horizont_ausgeschrieben()
    {
        // Ein Feiertag am 1. Mai, der seit 2020 jedes Jahr wiederkehrt.
        var result = Parse(Event("DTSTART;VALUE=DATE:20200501\r\nDTEND;VALUE=DATE:20200502\r\nRRULE:FREQ=YEARLY\r\nSUMMARY:Staatsfeiertag"));
        Assert.Equal([new DateOnly(2026, 5, 1), new DateOnly(2027, 5, 1), new DateOnly(2028, 5, 1)], result.Events.Select(e => e.Start));

        // Mit COUNT zählen auch die Vorkommen vor dem Horizont (2020, 2021, 2022 ... drei Stück sind schon vorbei).
        var counted = Parse(Event("DTSTART;VALUE=DATE:20240501\r\nRRULE:FREQ=YEARLY;COUNT=3\r\nSUMMARY:Dreimal"));
        Assert.Equal([new DateOnly(2026, 5, 1)], counted.Events.Select(e => e.Start));

        var until = Parse(Event("DTSTART;VALUE=DATE:20260501\r\nRRULE:FREQ=YEARLY;UNTIL=20271231T000000Z\r\nSUMMARY:Bis 2027"));
        Assert.Equal([new DateOnly(2026, 5, 1), new DateOnly(2027, 5, 1)], until.Events.Select(e => e.Start));
    }

    [Fact]
    public void Wiederholung_mit_Wochentag_Ordnungszahl_Monat_und_Intervall()
    {
        // Zweiter Montag im Mai, jährlich.
        var second = Parse(Event("DTSTART;VALUE=DATE:20260511\r\nRRULE:FREQ=YEARLY;BYMONTH=5;BYDAY=2MO\r\nSUMMARY:Muttermontag"));
        Assert.Equal([new DateOnly(2026, 5, 11), new DateOnly(2027, 5, 10), new DateOnly(2028, 5, 8)], second.Events.Select(e => e.Start));

        // Letzter Freitag im Monat, vierteljährlich (INTERVAL=3), in einem Jahr.
        var last = Parse(Event("DTSTART;VALUE=DATE:20260130\r\nRRULE:FREQ=MONTHLY;INTERVAL=3;BYDAY=-1FR\r\nSUMMARY:Teamtag"), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        Assert.Equal([new DateOnly(2026, 1, 30), new DateOnly(2026, 4, 24), new DateOnly(2026, 7, 31), new DateOnly(2026, 10, 30)], last.Events.Select(e => e.Start));

        // Wöchentlich montags und mittwochs, vier Mal.
        var weekly = Parse(Event("DTSTART;VALUE=DATE:20260105\r\nRRULE:FREQ=WEEKLY;BYDAY=MO,WE;COUNT=4\r\nSUMMARY:Kurs"));
        Assert.Equal([new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 7), new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 14)], weekly.Events.Select(e => e.Start));

        // Letzter Tag des Monats (BYMONTHDAY=-1), täglich eingeschränkt.
        var monthEnd = Parse(Event("DTSTART;VALUE=DATE:20260131\r\nRRULE:FREQ=MONTHLY;BYMONTHDAY=-1;COUNT=3\r\nSUMMARY:Monatsende"));
        Assert.Equal([new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31)], monthEnd.Events.Select(e => e.Start));
    }

    [Fact]
    public void Ausnahmen_Absagen_und_geaenderte_Vorkommen()
    {
        var excluded = Parse(Event("DTSTART;VALUE=DATE:20260501\r\nRRULE:FREQ=YEARLY\r\nEXDATE;VALUE=DATE:20270501\r\nSUMMARY:Ohne 2027"));
        Assert.Equal([new DateOnly(2026, 5, 1), new DateOnly(2028, 5, 1)], excluded.Events.Select(e => e.Start));

        Assert.Empty(Parse(Event("DTSTART;VALUE=DATE:20261026\r\nSTATUS:CANCELLED\r\nSUMMARY:Abgesagt")).Events);

        // Das geänderte Vorkommen ersetzt das ursprüngliche.
        var moved = Parse(
            Event("UID:abc\r\nDTSTART;VALUE=DATE:20260501\r\nRRULE:FREQ=YEARLY\r\nSUMMARY:Fest")
            + Event("UID:abc\r\nRECURRENCE-ID;VALUE=DATE:20270501\r\nDTSTART;VALUE=DATE:20270502\r\nSUMMARY:Fest verschoben"));
        Assert.Equal([new DateOnly(2026, 5, 1), new DateOnly(2027, 5, 2), new DateOnly(2028, 5, 1)], moved.Events.OrderBy(e => e.Start).Select(e => e.Start));
        Assert.DoesNotContain(moved.Events, e => e.Start == new DateOnly(2027, 5, 1));
    }

    [Fact]
    public void Gefaltete_Zeilen_Maskierung_und_Parameter_am_Titel()
    {
        var result = Parse(Event("DTSTART;VALUE=DATE:20261224\r\nSUMMARY;LANGUAGE=DE:Heiliger\r\n abend\\, halber Tag\r\nDESCRIPTION:Gesetzlicher Feiertag\\nHalbtägig"));
        var e = Assert.Single(result.Events);
        Assert.Equal("Heiligerabend, halber Tag", e.Summary);
        Assert.Equal("Gesetzlicher Feiertag\nHalbtägig", e.Description);

        // Ein Doppelpunkt im Parameterwert in Anführungszeichen trennt nicht.
        var quoted = Assert.Single(Parse(Event("DTSTART;VALUE=DATE:20261001\r\nX-FOO;BAR=\"a:b\":x\r\nSUMMARY:Ok")).Events);
        Assert.Equal("Ok", quoted.Summary);

        // Alarme innerhalb eines Termins stören nicht.
        var alarm = Assert.Single(Parse(Event("DTSTART;VALUE=DATE:20261001\r\nSUMMARY:Mit Alarm\r\nBEGIN:VALARM\r\nTRIGGER:-PT15M\r\nSUMMARY:Alarm\r\nEND:VALARM")).Events);
        Assert.Equal("Mit Alarm", alarm.Summary);
    }

    [Fact]
    public void Eintraege_ausserhalb_des_Horizonts_fehlen()
    {
        var result = Parse(Event("DTSTART;VALUE=DATE:20250101\r\nSUMMARY:Alt") + Event("DTSTART;VALUE=DATE:20291231\r\nSUMMARY:Zu spät")
            + Event("DTSTART;VALUE=DATE:20261201\r\nDTEND;VALUE=DATE:20270115\r\nSUMMARY:Reicht ins Jahr"));
        Assert.Equal(["Reicht ins Jahr"], result.Events.Select(e => e.Summary));
        Assert.Equal(new DateOnly(2026, 1, 1), FeedReader.Horizon(new DateOnly(2026, 10, 22)).From);
        Assert.Equal(new DateOnly(2028, 12, 31), FeedReader.Horizon(new DateOnly(2026, 10, 22)).Until);
    }

    [Fact]
    public void Kaputte_Dateien_sind_kein_Kalender_und_kaputte_Eintraege_werden_gemeldet()
    {
        Assert.False(IcsParser.Parse("<html><body>Zugriff verweigert</body></html>", From, Until, Vienna).IsCalendar);
        Assert.False(IcsParser.Parse("", From, Until, Vienna).IsCalendar);

        var broken = Parse(Event("SUMMARY:Ohne Beginn") + Event("DTSTART;VALUE=DATE:20261026\r\nSUMMARY:Gut") + Event("DTSTART:nicht-lesbar\r\nSUMMARY:Kaputt"));
        Assert.True(broken.IsCalendar);
        Assert.Equal(["Gut"], broken.Events.Select(e => e.Summary));
        Assert.Contains(broken.Warnings, w => w.Contains("2 Einträge", StringComparison.Ordinal));

        // Eine Wiederholungsregel, die der Leser nicht kennt, übernimmt den Eintrag einmal und sagt es.
        var unknown = Parse(Event("DTSTART;VALUE=DATE:20260101\r\nRRULE:FREQ=YEARLY;BYSETPOS=1;BYDAY=MO\r\nSUMMARY:Unklar"));
        Assert.Single(unknown.Events);
        Assert.Contains(unknown.Warnings, w => w.Contains("nicht unterstützt", StringComparison.Ordinal));
    }

    [Fact]
    public void Zu_viele_Eintraege_werden_abgeschnitten_und_gemeldet()
    {
        var many = Parse(Event("DTSTART;VALUE=DATE:20260101\r\nRRULE:FREQ=DAILY\r\nSUMMARY:Täglich"), until: new DateOnly(2045, 12, 31));
        Assert.Equal(IcsParser.MaxEvents, many.Events.Count);
        Assert.Contains(many.Warnings, w => w.Contains("Mehr als", StringComparison.Ordinal));
    }

    [Fact]
    public void Die_Feiertagskalender_von_Google_werden_nach_Beschreibung_eingeordnet()
    {
        Assert.Equal((EntryScope.Holiday, (string?)null), EntryClassifier.Classify("Gesetzlicher Feiertag"));
        Assert.Equal((EntryScope.Holiday, (string?)null), EntryClassifier.Classify("Gesetzlicher Feiertag\nHalbtägiger Feiertag"));
        Assert.Equal((EntryScope.Holiday, (string?)null), EntryClassifier.Classify(null));
        Assert.Equal((EntryScope.Observance, (string?)null), EntryClassifier.Classify("Gedenktag\nWenn Sie Gedenktage ausblenden möchten"));
        Assert.Equal((EntryScope.Observance, (string?)null), EntryClassifier.Classify("Gedenktag in Burgenland, Kärnten, Vorarlberg"));
        Assert.Equal((EntryScope.Regional, (string?)"Baden-Württemberg, Bayern, Sachsen-Anhalt"),
            EntryClassifier.Classify("Feiertag in Baden-Württemberg, Bayern, Sachsen-Anhalt\nWenn Sie ..."));
    }

    [Fact]
    public void CSV_mit_Datum_und_Titel_oder_von_bis_und_Titel()
    {
        var csv = CsvCalendarParser.Parse("Datum;Titel\n26.10.2026;Nationalfeiertag\n2026-12-24;Heiligabend\n1.11.2026;Allerheiligen\n", From, Until);
        Assert.Equal([("Nationalfeiertag", new DateOnly(2026, 10, 26)), ("Heiligabend", new DateOnly(2026, 12, 24)), ("Allerheiligen", new DateOnly(2026, 11, 1))],
            csv.Events.Select(e => (e.Summary, e.Start)));

        var range = CsvCalendarParser.Parse("Von,Bis,Titel\n27.10.2026,01.11.2026,Herbstferien\n", From, Until);
        var herbst = Assert.Single(range.Events);
        Assert.Equal((new DateOnly(2026, 10, 27), new DateOnly(2026, 11, 1)), (herbst.Start, herbst.EndInclusive));

        var broken = CsvCalendarParser.Parse("Datum;Titel\nkein Datum;Falsch\n26.10.2026;\n27.10.2026;Gut\n01.01.2030;Zu spät\n", From, Until);
        Assert.Equal(["Gut"], broken.Events.Select(e => e.Summary));
        Assert.Equal(2, broken.Warnings.Count);
        Assert.False(CsvCalendarParser.Parse("Das ist keine Liste", From, Until).IsCalendar);
        Assert.False(CsvCalendarParser.Parse("", From, Until).IsCalendar);
    }

    [Theory]
    [InlineData("webcal://calendar.example.org/feiertage.ics", true, "https://calendar.example.org/feiertage.ics")]
    [InlineData("https://calendar.example.org/a.ics", true, "https://calendar.example.org/a.ics")]
    [InlineData("", true, "")]
    [InlineData("  ", true, "")]
    [InlineData("http://calendar.example.org/a.ics", false, "")]
    [InlineData("ftp://calendar.example.org/a.ics", false, "")]
    [InlineData("https://user:geheim@calendar.example.org/a.ics", false, "")]
    [InlineData("https://calendar.example.org:8443/a.ics", false, "")]
    [InlineData("https://calendar.example.org/a.ics?token=abc123", false, "")]
    [InlineData("https://calendar.example.org/a.ics?x=1&apikey=abc", false, "")]
    [InlineData("https://calendar.google.com/calendar/ical/x/private-0123456789abcdef0123456789abcdef/basic.ics", false, "")]
    [InlineData("https://localhost/a.ics", false, "")]
    [InlineData("https://intranet/a.ics", false, "")]
    [InlineData("https://drucker.local/a.ics", false, "")]
    [InlineData("https://127.0.0.1/a.ics", false, "")]
    [InlineData("https://192.168.1.20/a.ics", false, "")]
    [InlineData("https://169.254.169.254/latest/meta-data", false, "")]
    [InlineData("https://[::1]/a.ics", false, "")]
    [InlineData("kein link", false, "")]
    public void Adressen_nur_https_ohne_Zugangsdaten_und_ohne_interne_Ziele(string input, bool valid, string normalized)
    {
        Assert.Equal(valid, SubscriptionUrl.TryNormalize(input, out var result, out var error));
        Assert.Equal(valid, error is null);
        Assert.Equal(normalized, result);
    }

    [Fact]
    public void Google_und_OpenHolidays_Adressen_der_Voreinstellungen_sind_gueltig()
    {
        foreach (var (_, _, url) in CalendarFeedSources.Presets)
        {
            Assert.True(SubscriptionUrl.TryNormalize(url, out _, out var error), error);
        }

        foreach (var source in CalendarFeedSources.Suggestions(2026).Where(s => s.Id.StartsWith("schulferien", StringComparison.Ordinal)))
        {
            Assert.True(SubscriptionUrl.TryNormalize(source.Url, out _, out var error), error);
        }
    }

    [Theory]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("100.64.1.1", true)]
    [InlineData("192.168.0.1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("216.58.206.78", false)]
    [InlineData("::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("fd12:3456::1", true)]
    [InlineData("::ffff:10.0.0.1", true)]
    [InlineData("2a00:1450:4001:81b::200e", false)]
    public void Interne_Adressen_werden_beim_Verbinden_abgelehnt(string address, bool internalAddress) =>
        Assert.Equal(internalAddress, SubscriptionUrl.IsInternal(IPAddress.Parse(address)));

    [Fact]
    public void Der_Stand_des_Abrufs_steht_in_Worten_nach_Ortszeit()
    {
        var today = new DateOnly(2027, 3, 3);

        string Text(DateTimeOffset? at) => HolidayCalendar.AsOfText(at, today, Vienna);

        Assert.Equal("noch nicht abgerufen", Text(null));
        Assert.Equal("Stand heute", Text(new DateTimeOffset(2027, 3, 3, 6, 0, 0, TimeSpan.Zero)));
        // 23:30 UTC am 2. März ist schon der 3. März in Wien.
        Assert.Equal("Stand heute", Text(new DateTimeOffset(2027, 3, 2, 23, 30, 0, TimeSpan.Zero)));
        Assert.Equal("Stand gestern", Text(new DateTimeOffset(2027, 3, 2, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal("Stand vor 3 Tagen", Text(new DateTimeOffset(2027, 2, 28, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal("Stand 16.02.2027", Text(new DateTimeOffset(2027, 2, 16, 12, 0, 0, TimeSpan.Zero)));
    }
}
