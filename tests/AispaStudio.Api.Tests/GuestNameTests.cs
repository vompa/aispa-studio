using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Reminders;
using AispaStudio.Api.Retention;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Anrede in Nachrichten an den Gast (ADR 0064): „Frau Mag. Huber“ nach dem Land des Standorts, nie der Rufname.</summary>
public sealed class GuestNameTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Customer Huber() => new()
    {
        FirstName = "Stefanie",
        LastName = "Huber",
        Salutation = CustomerSalutation.Female,
        TitlesBefore = "Mag.",
        PreferredName = "Steffi",
    };

    private static string ReminderMail(GuestName guest, string language) => ReminderTexts.Mail(
        new ReminderTexts.Facts(guest, "Massage", "Montag, 1. März um 10:00 Uhr", "Spa Wien", "https://spa.example/termin/x", language));

    [Fact]
    public void Kundin_mit_Frau_und_Mag_wird_in_Oesterreich_mit_Titel_angesprochen()
    {
        var mail = ReminderMail(GuestName.Of(Huber(), "Europe/Vienna"), "de");

        Assert.StartsWith("Guten Tag Frau Mag. Huber,", mail, StringComparison.Ordinal);
    }

    [Fact]
    public void Derselbe_Titel_Mag_steht_in_Deutschland_nicht_in_der_Anrede()
    {
        var mail = ReminderMail(GuestName.Of(Huber(), "Europe/Berlin"), "de");

        Assert.StartsWith("Guten Tag Frau Huber,", mail, StringComparison.Ordinal);
    }

    [Fact]
    public void Der_Rufname_steht_in_keiner_Nachricht_an_den_Gast()
    {
        var guest = GuestName.Of(Huber(), "Europe/Vienna");

        foreach (var language in new[] { "de", "en", "fr", "it" })
        {
            Assert.DoesNotContain("Steffi", ReminderMail(guest, language), StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Steffi", ConsentTexts.Request("gast@example.at", guest, "Spa Test", "https://spa.example/e", "de").Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Anrede_gilt_der_volle_Name_und_in_anderen_Sprachen_der_Vorname()
    {
        var kim = new Customer { FirstName = "Kim", LastName = "Berger", Salutation = CustomerSalutation.Diverse };
        var guest = GuestName.Of(kim, "Europe/Vienna");

        Assert.StartsWith("Guten Tag Kim Berger,", ReminderMail(guest, "de"), StringComparison.Ordinal);
        Assert.StartsWith("Hello Kim,", ReminderMail(guest, "en"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Terminannahme_antwortet_deutsch_mit_der_Anrede_und_sonst_mit_dem_Vornamen()
    {
        static ReplyRequest Request(string language, string guest) => new(
            language, ReplyCase.Forwarded, guest, "Termin", "", "Spa Test", null, null, null, [], []);
        var assistant = new RuleBasedBookingAssistant();

        var german = await assistant.DraftReplyAsync(Request("de", GuestName.Of(Huber(), "Europe/Vienna").GreetingName), Ct);
        var english = await assistant.DraftReplyAsync(Request("en", "Stefanie Huber"), Ct);

        Assert.StartsWith("Guten Tag Frau Mag. Huber,", german.Body, StringComparison.Ordinal);
        Assert.StartsWith("Hello Stefanie Huber,", english.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Aus_der_Mail_angelegter_Kunde_bekommt_weder_Anrede_noch_Titel_aus_dem_Vorschlag()
    {
        var mail = new InboundMail
        {
            MessageId = "m1",
            From = "gast@example.at",
            To = "spa@example.at",
            Subject = "Termin",
            Body = "",
            GuestFirstName = "Stefanie",
            GuestLastName = "Huber",
            SuggestedSalutation = CustomerSalutation.Female,
            SuggestedTitlesBefore = "Mag.",
        };

        var customer = MailIntakeProcessor.NewCustomer(mail, DateTimeOffset.UnixEpoch);

        Assert.Equal(CustomerSalutation.None, customer.Salutation);
        Assert.Null(customer.TitlesBefore);
        Assert.Equal(("Stefanie", "Huber"), (customer.FirstName, customer.LastName));
    }
}
