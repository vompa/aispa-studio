using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using SkiaSharp;

namespace AispaStudio.Desktop.Tests;

/// <summary>Druckdatei, Papierbogen und Verkleinern des Fotos (Runde R05, Slice 127: Ä300).</summary>
public sealed class AnamnesisPrintTests : IDisposable
{
    private static readonly AnamnesisFormDefinition Form = new("2", "fr", "Intro", "Accord", [new("skin", AnamnesisQuestionKind.YesNo, "Peau ?", "Lesquels ?", true)]);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"aispa-print-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        private readonly List<(TimerCallback Callback, object? State)> _timers = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _timers.Add((callback, state));
            return new NoTimer();
        }

        public void FireAll() => _timers.ForEach(t => t.Callback(t.State));

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>Links rot, rechts blau, damit eine falsche Drehrichtung auffällt.</summary>
    private static byte[] Jpeg(int width, int height, byte[]? exif = null)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Blue);
        using (var canvas = new SKCanvas(bitmap))
        using (var red = new SKPaint { Color = SKColors.Red })
        {
            canvas.DrawRect(0, 0, width / 2, height, red);
        }

        using var encoded = bitmap.Encode(SKEncodedImageFormat.Jpeg, 80);
        var bytes = encoded.ToArray();
        return exif is null ? bytes : [.. bytes[..2], .. exif, .. bytes[2..]];
    }

    /// <summary>EXIF-Abschnitt mit der Angabe Orientation (3: 180°, 6: 90° im Uhrzeigersinn, 8: 90° dagegen).</summary>
    private static byte[] ExifRotated(byte orientation)
    {
        byte[] tiff = [(byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8, 0, 1, 0x01, 0x12, 0, 3, 0, 0, 0, 1, 0, orientation, 0, 0, 0, 0, 0, 0, 0, 0];
        byte[] header = [(byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0];
        var length = header.Length + tiff.Length + 2;
        return [0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. header, .. tiff];
    }

    [Fact]
    public void Druckdatei_wird_nach_der_Frist_geloescht()
    {
        var time = new ManualTime();

        var file = PrintFile.Write("<p>Bogen</p>", time, _folder);

        Assert.Equal("<p>Bogen</p>", File.ReadAllText(file.LocalPath));
        time.FireAll();
        Assert.False(File.Exists(file.LocalPath));
    }

    [Fact]
    public void Druckdatei_raeumt_alte_Reste_beim_naechsten_Druck_weg()
    {
        Directory.CreateDirectory(_folder);
        var old = Path.Combine(_folder, "rest.html");
        File.WriteAllText(old, "alt");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        var recent = Path.Combine(_folder, "offen.html");
        File.WriteAllText(recent, "neu");

        PrintFile.Write("<p>Bogen</p>", new ManualTime(), _folder);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public void Papierbogen_nennt_Sprache_und_Vorgabe_der_API_und_maskiert_Namen()
    {
        var sheet = new AnamnesisPaperSheet(Form, "Eva <script>", "Wien", "Massage", new DateTimeOffset(2026, 10, 19, 8, 30, 0, TimeSpan.Zero), "Europe/Vienna",
            "https://spa.example.test/#/anamnese/x", DateTimeOffset.UnixEpoch);

        var html = PaperSheetDocument.Html(sheet);

        Assert.Contains("Questionnaire de santé", html, StringComparison.Ordinal);
        Assert.Contains("Peau ?", html, StringComparison.Ordinal);
        Assert.Contains("Lesquels ?", html, StringComparison.Ordinal);
        Assert.Contains("☐ Oui", html, StringComparison.Ordinal);
        Assert.Contains("10:30", html, StringComparison.Ordinal);
        Assert.Contains("Eva &lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Eva <script>", html, StringComparison.Ordinal);
        Assert.Contains("data:image/png;base64,", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Foto_wird_auf_2000_Pixel_und_JPEG_verkleinert()
    {
        using var large = new SKBitmap(4000, 1000);
        using var png = large.Encode(SKEncodedImageFormat.Png, 100);

        var result = PhotoShrinker.Shrink(png.ToArray());

        using var codec = SKCodec.Create(SKData.CreateCopy(result));
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        Assert.Equal(2000, codec.Info.Width);
        Assert.Equal(500, codec.Info.Height);
    }

    [Theory]
    [InlineData(6, 1000, 2000, 500, 100)]
    [InlineData(8, 1000, 2000, 500, 1900)]
    [InlineData(3, 2000, 1000, 1900, 500)]
    public void Foto_wird_nach_der_EXIF_Angabe_aufgerichtet(byte orientation, int width, int height, int redX, int redY)
    {
        var result = PhotoShrinker.Shrink(Jpeg(2400, 1200, ExifRotated(orientation)));

        using var bitmap = SKBitmap.Decode(result);
        Assert.Equal(width, bitmap.Width);
        Assert.Equal(height, bitmap.Height);
        Assert.True(bitmap.GetPixel(redX, redY).Red > 200);
    }

    [Fact]
    public void Kleines_JPEG_bleibt_unveraendert()
    {
        var photo = Jpeg(100, 50);

        Assert.Same(photo, PhotoShrinker.Shrink(photo));
    }

    [Fact]
    public void Ungueltiges_Bild_bleibt_unveraendert_fuer_die_Pruefung_des_Servers()
    {
        var photo = "kein Foto"u8.ToArray();

        Assert.Same(photo, PhotoShrinker.Shrink(photo));
    }
}
