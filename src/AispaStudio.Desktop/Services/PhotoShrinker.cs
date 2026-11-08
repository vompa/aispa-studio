using SkiaSharp;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Verkleinert ein Foto vor dem Hochladen auf JPEG mit höchstens 2000 px Kantenlänge, wie die Web-Oberfläche: Das reicht, um Handschrift zu lesen, und bleibt unter der Grenze von 5 MB.
/// Alles geschieht im Speicher, nie über eine Datei. Lässt sich ein Bild nicht lesen, geht das Original an den Server, der das Format prüft.
/// </summary>
public static class PhotoShrinker
{
    private const int MaxSide = 2000;
    private const int KeepBelowBytes = 3_000_000;
    private const int JpegQuality = 85;

    public static byte[] Shrink(byte[] photo)
    {
        using var data = SKData.CreateCopy(photo);
        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            return photo;
        }

        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null)
        {
            return photo;
        }

        var scale = Math.Min(1d, (double)MaxSide / Math.Max(decoded.Width, decoded.Height));
        if (scale >= 1d && codec.EncodedFormat == SKEncodedImageFormat.Jpeg && photo.Length < KeepBelowBytes)
        {
            return photo;
        }

        var width = (int)Math.Round(decoded.Width * scale);
        var height = (int)Math.Round(decoded.Height * scale);
        var sideways = codec.EncodedOrigin is SKEncodedOrigin.RightTop or SKEncodedOrigin.LeftBottom;
        using var target = new SKBitmap(sideways ? height : width, sideways ? width : height);
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.White);
            Upright(canvas, codec.EncodedOrigin, target);
            using var image = SKImage.FromBitmap(decoded);
            canvas.DrawImage(image, new SKRect(0, 0, width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        }

        using var encoded = target.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return encoded?.ToArray() ?? photo;
    }

    /// <summary>Handyfotos tragen die Drehung nur als Angabe in der Datei; der Browser beachtet sie, Skia beim Dekodieren nicht.</summary>
    private static void Upright(SKCanvas canvas, SKEncodedOrigin origin, SKBitmap target)
    {
        switch (origin)
        {
            case SKEncodedOrigin.RightTop:
                canvas.Translate(target.Width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(target.Width, target.Height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, target.Height);
                canvas.RotateDegrees(270);
                break;
        }
    }
}
