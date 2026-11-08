namespace AispaStudio.Api.Tests;

/// <summary>
/// Synthetische Treatwell-Mails (HTML) aus dem Ordner <c>Fixtures</c>. Sie sind erfunden, nicht von Treatwell: das echte Format ist nicht öffentlich.
/// Platzhalter wie <c>{{REFERENCE}}</c> setzt der Test ein.
/// </summary>
internal static class MailFixtures
{
    public const string BookingTable = "synthetic-not-from-treatwell-booking-table.html";
    public const string BookingList = "synthetic-not-from-treatwell-booking-list.html";
    public const string Change = "synthetic-not-from-treatwell-change.html";
    public const string Cancellation = "synthetic-not-from-treatwell-cancellation.html";
    public const string BookingWithInjection = "synthetic-not-from-treatwell-booking-with-injection.html";

    /// <summary>Absender der erfundenen Beispiele: eine Unterdomain von treatwell.com.</summary>
    public const string Sender = "bookings@partner.treatwell.com";

    public static string Load(string name, string reference, string dateText, string treatment = "Hot-Stone-Massage", string location = "Wien Innere Stadt", string staff = "Lena")
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
        return html
            .Replace("{{REFERENCE}}", reference, StringComparison.Ordinal)
            .Replace("{{DATE_TEXT}}", dateText, StringComparison.Ordinal)
            .Replace("{{TREATMENT}}", treatment, StringComparison.Ordinal)
            .Replace("{{LOCATION}}", location, StringComparison.Ordinal)
            .Replace("{{STAFF}}", staff, StringComparison.Ordinal);
    }
}
