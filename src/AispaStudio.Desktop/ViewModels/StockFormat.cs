using System.Globalization;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Zahlen, Daten und Namen im Lager wie <c>stockFormat.ts</c> der Web-Maske: deutsche Schreibweise, Eingabe mit Komma oder Punkt.</summary>
public static class StockFormat
{
    // Feste Trennzeichen statt de-AT, weil Windows und ICU unterschiedliche Tausendertrennzeichen liefern (wie Money).
    private static readonly NumberFormatInfo German = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = ".", NegativeSign = "-" };

    /// <summary>Menge mit bis zu drei Nachkommastellen: „2,5“, „1.250“.</summary>
    public static string Quantity(decimal value) => value.ToString("#,##0.###", German);

    /// <summary>Menge zum Bearbeiten ohne Tausendertrenner („1250“, „1,25“), damit <see cref="Parse"/> sie unverändert zurückliest.</summary>
    public static string Plain(decimal value) => value.ToString("0.###", German);

    /// <summary>Einstandspreis zum Bearbeiten: die vier Stellen, die die API führt.</summary>
    public static string Cost(decimal value) => value.ToString("0.####", German);

    public static string Date(DateOnly date) => date.ToString("d.M.yyyy", CultureInfo.InvariantCulture);

    public static string Kind(StockMovementKind kind) => kind switch
    {
        StockMovementKind.GoodsReceipt => "Wareneingang",
        StockMovementKind.Consumption => "Verbrauch",
        StockMovementKind.Sale => "Verkauf",
        StockMovementKind.Count => "Inventur",
        StockMovementKind.Correction => "Korrektur",
        StockMovementKind.Transfer => "Umbuchung",
        StockMovementKind.Refill => "Nachfüllen",
        _ => kind.ToString(),
    };

    public static string PlaceKind(StoragePlaceKind kind) => kind switch
    {
        StoragePlaceKind.Main => "Hauptlager",
        StoragePlaceKind.Room => "Raum",
        StoragePlaceKind.Sales => "Verkauf",
        _ => "Sonstiger Ort",
    };

    public static string OrderChannel(SupplierOrderChannel channel) => channel switch
    {
        SupplierOrderChannel.Email => "Mail",
        SupplierOrderChannel.Phone => "Telefon",
        SupplierOrderChannel.Portal => "Portal",
        _ => "Vertreter",
    };

    public static string OrderStatus(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "Entwurf",
        PurchaseOrderStatus.Ordered => "bestellt",
        PurchaseOrderStatus.PartiallyReceived => "teilweise geliefert",
        PurchaseOrderStatus.Received => "geliefert",
        _ => "storniert",
    };

    /// <summary>Zahl aus einer Eingabe mit Komma oder Punkt; <c>null</c>, wenn es keine ist (leer eingeschlossen).</summary>
    public static decimal? Parse(string? text)
    {
        var cleaned = (text ?? "").Replace(" ", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal).Replace(',', '.').Replace('−', '-');
        return cleaned.Length > 0 && decimal.TryParse(cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>Eine leere Eingabe zählt als 0, wie im Formular „Material anlegen“; sonst wie <see cref="Parse"/>.</summary>
    public static decimal? ParseOrZero(string? text) => string.IsNullOrWhiteSpace(text) ? 0m : Parse(text);
}
