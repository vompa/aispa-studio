using AispaStudio.Api.Assistant.Answers;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Slice 136 (Ä318): die Regeln erkennen Fragen zu Personal, Räumen und Lager ohne KI. Gegenproben halten die bestehenden Themen, die dieselben Wörter nutzen
/// („wann kommt steiner“, „welcher Raum ist jetzt frei“, „was kostet Hot Stone“). „Heute“ ist Mittwoch, der 3. März 2027.
/// </summary>
public sealed class StaffRoomStockIntentTests
{
    private static readonly DateOnly Today = new(2027, 3, 3);

    [Theory]
    [InlineData("wann hat sophie pause", BusinessTopic.PersonBreak, "sophie", null)]
    [InlineData("Wann geht Sophie heute heim?", BusinessTopic.PersonBreak, "Sophie", null)]
    [InlineData("wer ist heute ersthelfer", BusinessTopic.DutyHolders, null, "firstaid")]
    [InlineData("Wer ist für die Kassa zuständig?", BusinessTopic.DutyHolders, null, "cash")]
    [InlineData("wer ist für das lager zuständig", BusinessTopic.DutyHolders, null, "stock")]
    [InlineData("wann ist der paarraum morgen frei", BusinessTopic.RoomFreeTimes, null, "free")]
    [InlineData("ist der hamam morgen gesperrt", BusinessTopic.RoomFreeTimes, null, "closure")]
    [InlineData("haben wir das arganöl noch", BusinessTopic.StockLevel, null, null)]
    [InlineData("wie viel bodylotion haben wir", BusinessTopic.StockLevel, null, null)]
    [InlineData("was ist bald aus", BusinessTopic.StockLow, null, null)]
    [InlineData("was muss bestellt werden", BusinessTopic.StockLow, null, null)]
    [InlineData("wann kommt die lieferung", BusinessTopic.OpenOrders, null, null)]
    [InlineData("was ist bestellt", BusinessTopic.OpenOrders, null, null)]
    public void Fragen_zu_Personal_Raeumen_und_Lager_haben_ihr_Thema(string question, BusinessTopic topic, string? subject, string? detail)
    {
        var intent = BusinessIntents.Detect(question, Today);

        Assert.Equal(topic, intent?.Topic);
        if (subject is not null)
        {
            Assert.Equal(subject, intent!.Subject);
        }

        Assert.Equal(detail, intent!.Detail);
    }

    [Theory]
    [InlineData("welcher raum ist jetzt frei", BusinessTopic.RoomFree)]
    [InlineData("was kostet die hot stone", BusinessTopic.TreatmentInfo)]
    [InlineData("was kostet die gesichtscreme", BusinessTopic.TreatmentInfo)]
    [InlineData("wann kommt steiner", BusinessTopic.GuestNextVisit)]
    [InlineData("ist elif morgen da", BusinessTopic.PersonAvailable)]
    [InlineData("ist die sauna voll", BusinessTopic.AreaLoad)]
    [InlineData("wer ist für die heizung zuständig", BusinessTopic.BusinessContacts)]
    [InlineData("wie viele termine haben wir noch", BusinessTopic.RemainingAppointments)]
    public void Gegenproben_bleiben_bei_ihrem_Thema(string question, BusinessTopic topic)
    {
        var intent = BusinessIntents.Detect(question, Today);

        Assert.Equal(topic, intent?.Topic);
    }

    /// <summary>Fragen, die dieselben Wörter nutzen, aber bei Handbuch oder einem anderen Thema bleiben (Durchsicht der Slice 136).</summary>
    [Theory]
    [InlineData("Wie lege ich einen Mindestbestand an?")]
    [InlineData("Wie kann ich Arganöl nachbestellen?")]
    [InlineData("bis wann hat das spa heute offen")]
    [InlineData("bis wann ist die sauna offen")]
    [InlineData("haben wir noch jemanden auf der warteliste")]
    [InlineData("wie viele no-shows haben wir diesen monat")]
    [InlineData("haben wir diese woche noch geburtstage")]
    [InlineData("haben wir im dezember noch feiertage")]
    [InlineData("haben wir von herrn steiner noch den fragebogen")]
    [InlineData("ist die zufahrt gesperrt")]
    [InlineData("ist der parkplatz gesperrt")]
    [InlineData("wann ist im zeitraum märz noch etwas frei")]
    [InlineData("wie lange dauert die hot stone massage")]
    public void Andere_Fragen_gehen_nicht_an_die_neuen_Themen(string question)
    {
        var topic = BusinessIntents.Detect(question, Today)?.Topic;

        Assert.False(topic is BusinessTopic.PersonBreak or BusinessTopic.DutyHolders or BusinessTopic.RoomFreeTimes or BusinessTopic.StockLevel
            or BusinessTopic.StockLow or BusinessTopic.OpenOrders, $"{question} → {topic}");
    }

    [Fact]
    public void Haben_wir_am_8_Dezember_offen_fragt_nach_Oeffnung_und_Feiertag()
    {
        var intent = BusinessIntents.Detect("haben wir am 8. dezember offen", Today);

        Assert.Equal(BusinessTopic.OpeningHours, intent?.Topic);
        Assert.Equal(new DateOnly(2027, 12, 8), intent!.From);
    }
}
