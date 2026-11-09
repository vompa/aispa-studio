using System.Text;
using AispaStudio.Api.Exports;

namespace AispaStudio.Api.Tests;

/// <summary>vCard 4.0 (RFC 6350) für Outlook, Apple und Google: Pflichtfelder, Umbruch langer Zeilen, Sonderzeichen; die Datei wird zeilenweise zurückgelesen.</summary>
public sealed class VCardWriterTests
{
    private static readonly Guid Id = new("0193b1c2-0000-7000-8000-000000000001");

    private static VCardContact Contact() => new(Id, "Mag.", "Anna-Maria", "Müller, Öhlinger", "Steffi", "MSc", "anna@beispiel.at", "+43 664 1234567",
        new DateOnly(1985, 3, 14), "Ringstraße 1", "1010", "Wien", "AT", "Zahlt mit Firmenkarte;\nKommt oft zu spät", new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));

    private static List<string> Lines(byte[] bytes) => [.. Encoding.UTF8.GetString(bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)];

    [Fact]
    public void Pflichtfelder_und_Sonderzeichen_nach_RFC_6350()
    {
        var bytes = VCardWriter.Write([Contact()]);
        SampleFiles.Save("Kontakt_Mueller.vcf", bytes);
        var lines = Lines(bytes);

        Assert.Equal("BEGIN:VCARD", lines[0]);
        Assert.Equal("VERSION:4.0", lines[1]);
        Assert.Equal("END:VCARD", lines[^1]);
        Assert.Contains("FN:Mag. Anna-Maria Müller\\, Öhlinger MSc", lines);
        Assert.Contains("N:Müller\\, Öhlinger;Anna-Maria;;Mag.;MSc", lines);
        Assert.Contains("NICKNAME:Steffi", lines);
        Assert.Contains("TEL;TYPE=cell;VALUE=uri:tel:+436641234567", lines);
        Assert.Contains("EMAIL:anna@beispiel.at", lines);
        Assert.Contains("BDAY:19850314", lines);
        Assert.Contains("ADR;TYPE=home:;;Ringstraße 1;Wien;;1010;AT", lines);
        Assert.Contains("NOTE:Zahlt mit Firmenkarte\\;\\nKommt oft zu spät", lines);
        Assert.Contains($"UID:urn:uuid:{Id}", lines);
        Assert.Contains("REV:20261109T120000Z", lines);
        Assert.Contains("PRODID:-//AISPA Studio//DE", lines);
    }

    [Fact]
    public void Lange_Zeilen_werden_nach_75_Oktetten_umgebrochen_und_lassen_sich_zusammensetzen()
    {
        var longNote = string.Concat(Enumerable.Repeat("Öl ", 60)).TrimEnd();
        var bytes = VCardWriter.Write([Contact() with { Note = longNote }]);
        var raw = Encoding.UTF8.GetString(bytes).Split("\r\n");

        Assert.All(raw, line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, $"{Encoding.UTF8.GetByteCount(line)} Oktette: {line}"));
        var folded = raw.SkipWhile(l => !l.StartsWith("NOTE:", StringComparison.Ordinal)).TakeWhile((l, i) => i == 0 || l.StartsWith(' ')).ToList();
        Assert.True(folded.Count > 1);
        var joined = string.Concat(folded.Select((l, i) => i == 0 ? l : l[1..]));
        Assert.Equal("NOTE:" + longNote, joined);
    }

    [Fact]
    public void Leere_Felder_fehlen_und_mehrere_Kontakte_stehen_hintereinander()
    {
        var bare = new VCardContact(Guid.NewGuid(), null, "", "Huber", null, null, null, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        var lines = Lines(VCardWriter.Write([Contact(), bare]));

        Assert.Equal(2, lines.Count(l => l == "BEGIN:VCARD"));
        Assert.Equal(2, lines.Count(l => l == "END:VCARD"));
        Assert.Contains("FN:Huber", lines);
        Assert.Contains("N:Huber;;;;", lines);
        Assert.Single(lines, l => l.StartsWith("EMAIL:", StringComparison.Ordinal));
        Assert.Single(lines, l => l.StartsWith("BDAY:", StringComparison.Ordinal));
        Assert.Single(lines, l => l.StartsWith("NICKNAME:", StringComparison.Ordinal));
    }
}
