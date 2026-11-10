using System.Text;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Exports;

namespace AispaStudio.Api.Tests;

/// <summary>iCal 2.0 (RFC 5545) für den Gast und den privaten Kalender einer Person: TZID des Standorts, Umbruch, stabile UID; der eigene Leser liest die Datei zurück.</summary>
public sealed class ICalWriterTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly Guid Id = new("0193b1c2-0000-7000-8000-000000000002");
    private static readonly DateTimeOffset Stamp = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Montag, 1. März 2027, 14:00 Wien (Winterzeit, UTC+1).</summary>
    private static ICalEvent Event() => new(Id, TestSpa.At(14), TestSpa.At(14, 50), "Klassische Massage bei Spa Wien", "Ringstraße 1, 1010 Wien",
        "Hinweis: 10 Minuten vorher da sein;\nbequeme Kleidung", "https://spa.example/kunden", TimeSpan.FromHours(24));

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [Fact]
    public void VCALENDAR_mit_PRODID_VTIMEZONE_und_VEVENT_mit_TZID_des_Standorts()
    {
        var bytes = ICalWriter.Write([Event()], Vienna, Stamp);
        SampleFiles.Save("Termin_2027-03-01_1400.ics", bytes);
        var lines = Text(bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("BEGIN:VCALENDAR", lines[0]);
        Assert.Contains("VERSION:2.0", lines);
        Assert.Contains("PRODID:-//AISPA Studio//DE", lines);
        Assert.Contains("BEGIN:VTIMEZONE", lines);
        Assert.Contains("TZID:Europe/Vienna", lines);
        Assert.Contains("DTSTART;TZID=Europe/Vienna:20270301T140000", lines);
        Assert.Contains("DTEND;TZID=Europe/Vienna:20270301T145000", lines);
        Assert.Contains("DTSTAMP:20261109T120000Z", lines);
        Assert.Contains($"UID:{Id}@aispa-studio", lines);
        Assert.Contains("SUMMARY:Klassische Massage bei Spa Wien", lines);
        Assert.Contains("LOCATION:Ringstraße 1\\, 1010 Wien", lines);
        Assert.Contains("DESCRIPTION:Hinweis: 10 Minuten vorher da sein\\;\\nbequeme Kleidung", lines);
        Assert.Contains("URL:https://spa.example/kunden", lines);
        Assert.Contains("BEGIN:VALARM", lines);
        Assert.Contains("TRIGGER:-PT24H", lines);
        Assert.Contains("ACTION:DISPLAY", lines);
        Assert.Equal("END:VCALENDAR", lines[^1]);
        // Sommerzeit-Regel aus der Zeitzone, damit Outlook die Zeiten ohne Rückfrage richtig legt.
        Assert.Contains("BEGIN:DAYLIGHT", lines);
        Assert.Contains("TZOFFSETTO:+0200", lines);
        Assert.Contains("RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU", lines);
    }

    [Fact]
    public void Der_eigene_Leser_liest_die_Datei_zurueck_und_die_UID_bleibt_stabil()
    {
        var first = ICalWriter.Write([Event()], Vienna, Stamp);
        var second = ICalWriter.Write([Event()], Vienna, Stamp.AddDays(1));

        var parsed = IcsParser.Parse(Text(first), new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 31), Vienna);
        Assert.True(parsed.IsCalendar);
        var read = Assert.Single(parsed.Events);
        Assert.Equal((new DateOnly(2027, 3, 1), new TimeOnly(14, 0), new TimeOnly(14, 50)), (read.Start, read.StartTime, read.EndTime));
        Assert.Equal("Klassische Massage bei Spa Wien", read.Summary);
        Assert.Equal(Text(first).Split("\r\n").Single(l => l.StartsWith("UID:", StringComparison.Ordinal)),
            Text(second).Split("\r\n").Single(l => l.StartsWith("UID:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Lange_Zeilen_sind_nach_75_Oktetten_umgebrochen_und_ohne_Alarm_fehlt_VALARM()
    {
        var text = Text(ICalWriter.Write([Event() with { Description = string.Concat(Enumerable.Repeat("Ö", 200)), Alarm = null }], Vienna, Stamp));
        var lines = text.Split("\r\n");

        Assert.All(lines, line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, $"{Encoding.UTF8.GetByteCount(line)} Oktette"));
        Assert.DoesNotContain("BEGIN:VALARM", lines);
        Assert.Contains(lines, l => l.StartsWith(' '));
    }

    [Fact]
    public void Sommerzeit_Termin_traegt_die_Ortszeit_nicht_UTC()
    {
        var summer = Event() with { Start = TestSpa.At(14, 0, new DateOnly(2027, 7, 5)), End = TestSpa.At(15, 0, new DateOnly(2027, 7, 5)) };
        var lines = Text(ICalWriter.Write([summer], Vienna, Stamp)).Split("\r\n");

        Assert.Contains("DTSTART;TZID=Europe/Vienna:20270705T140000", lines);
        Assert.Contains("DTEND;TZID=Europe/Vienna:20270705T150000", lines);
    }
}
