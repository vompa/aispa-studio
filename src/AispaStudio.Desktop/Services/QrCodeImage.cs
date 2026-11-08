using Avalonia.Media.Imaging;
using QRCoder;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Zeichnet den Zahlungstext der Sofortüberweisung als QR-Code (Ä297). Der Inhalt ist genau der Text des Servers; die App ergänzt und rechnet nichts.
/// Der Code ist schwarz auf weißem, deckendem Grund mit Ruhezone, damit er in hellem und dunklem Schema scanbar bleibt.
/// </summary>
public static class QrCodeImage
{
    private const int PixelsPerModule = 8;

    /// <summary>PNG des Codes mit Fehlerkorrektur M, wie im Web.</summary>
    public static byte[] Png(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(PixelsPerModule);
    }

    public static Bitmap Create(string payload)
    {
        using var stream = new MemoryStream(Png(payload));
        return new Bitmap(stream);
    }
}
