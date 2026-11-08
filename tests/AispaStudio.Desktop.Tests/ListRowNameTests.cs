using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Namen der Listenzeilen für den Screenreader nach R01 Slice 11 (W-Muster 7): jede Zeile heißt nach ihrem sichtbaren Inhalt in Lesereihenfolge,
/// nicht nach dem Record-Typ („InboxRow { Mail = … }“).
/// </summary>
public sealed class ListRowNameTests
{
    [Fact]
    public void Freie_Zeit_heisst_Tag_Uhrzeit_Person_Raum_und_ihre_Hinweise_wie_angezeigt()
    {
        var row = new FreeSlotRow(null!, "09:00–10:20", "Lena", "#2f6f5e", "Lotus", false, null) { DayText = "Do. 22.10." };
        var full = new FreeSlotRow(null!, "09:00–10:20", "Lena", "#2f6f5e", "Lotus", true, "lässt 30 Minuten Lücke")
        {
            SpeaksLanguage = "spricht Italienisch",
            OutsideWish = "außerhalb der Wunschzeit des Gastes",
            IsWishPerson = true,
        };

        Assert.Equal("Do. 22.10., 09:00–10:20, Lena, Lotus", row.AccessibleName);
        Assert.Equal("09:00–10:20, Lena, Lotus", (row with { DayText = null }).AccessibleName);
        Assert.Equal("09:00–10:20, Lena, Lotus, spricht Italienisch, außerhalb der Wunschzeit des Gastes, lässt 30 Minuten Lücke, passt genau, Wunsch",
            full.AccessibleName);
    }

    [Fact]
    public void Vorschlag_heisst_Nummer_Tag_Uhrzeit_Person_Raum_Behandlung_Grund_und_Preis()
    {
        var proposal = new CalendarProposal(TestData.At(17), TestData.At(18, 20), Guid.NewGuid(), "Hot-Stone-Massage", Guid.NewGuid(), "Lena", Guid.NewGuid(), "Lotus",
            null, 98m, 0, "");
        var row = new ProposalRow(proposal, 2, "17:00–18:20", "Do. 22.10.", "#2f6f5e", "€ 98,00");
        var reasoned = row with { Proposal = proposal with { Reason = "Wunschperson des Gastes" }, OtherDay = null };

        Assert.Equal("2, Do. 22.10., 17:00–18:20, Lena, Lotus · Hot-Stone-Massage, € 98,00", row.AccessibleName);
        Assert.Equal("2, 17:00–18:20, Lena, Lotus · Hot-Stone-Massage, Wunschperson des Gastes, € 98,00", reasoned.AccessibleName);
    }

    [Fact]
    public void Nachricht_heisst_Status_Absender_Kanal_Uhrzeit_Betreff_ohne_KI_Zusammenfassung()
    {
        var call = new InboundMailSummary(Guid.NewGuid(), BookingChannel.Phone, "+43 1 5124477", null, "Anruf (Mailbox)", TestData.At(10, 48),
            InboundMailStatus.NeedsReview, InboundMailKind.Question, ReplyStatus.None, "Fragt nach Gutschein", null);
        var mail = call with { Channel = BookingChannel.Email, From = "eva@example.at", FromName = "Eva Muster", Subject = "Terminanfrage", Status = InboundMailStatus.Proposed };

        Assert.Equal("Prüfen, +43 1 5124477, Anruf, 10:48, Anruf (Mailbox)", new InboxRow(call, "10:48").AccessibleName);
        Assert.Equal("Vorschlag, Eva Muster, 10:48, Terminanfrage", new InboxRow(mail, "10:48").AccessibleName);
    }

    [Fact]
    public void Beleg_heisst_Uhrzeit_Nummer_Storno_Zahlungsart_Betrag_wie_angezeigt()
    {
        var receipt = new ReceiptListItem("Nr. 42", "10:15", "€ 89,00", "Karte", false, false, null!);

        Assert.Equal("10:15, Nr. 42, Karte, € 89,00", receipt.AccessibleName);
        Assert.Equal("10:20, Nr. 43, Storno, Karte, -€ 89,00", (receipt with { Number = "Nr. 43", Time = "10:20", Total = "-€ 89,00", IsVoid = true }).AccessibleName);
        Assert.Equal("10:15, Nr. 42, storniert, Karte, € 89,00", (receipt with { IsVoided = true }).AccessibleName);
    }

    [Fact]
    public void Kundenzeile_der_Kundenseite_heisst_Nachname_Rufname()
    {
        var customer = SpaSample.Customers[0] with { PreferredName = "Gitti" };

        Assert.Equal("Müller, Gitti", Converters.CustomerListName.Convert(customer, typeof(string), null, CultureInfo.InvariantCulture));
    }
}
