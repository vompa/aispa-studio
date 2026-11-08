using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Geburtstag eines Gastes im Kalender (Runde 5b, Ä45): der Server rechnet (<see cref="GuestBirthday"/>), die Oberfläche setzt nur Worte und Symbole daraus zusammen.
/// Symbol am Eintrag nur am Tag selbst, Hover-Karte und Panel auch drei Tage davor und danach, Tagesüberblick nur heute, Kundenkarte mit Alter und nächstem Geburtstag.
/// </summary>
public sealed class BirthdayTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static AppointmentDto For(int hour, EmployeeDto employee, string name) => TestData.Appointment(hour, employee) with { CustomerId = Guid.NewGuid(), CustomerName = name };

    private static CalendarDay DayWith(params (AppointmentDto Appointment, GuestBirthday? Birthday)[] guests) =>
        TestData.Day([.. guests.Select(g => g.Appointment)]) with
        {
            Guests = [.. guests.Select(g => new CalendarGuest(g.Appointment.CustomerId, 3, 2, 0, null, null, Birthday: g.Birthday))],
        };

    private static List<CalendarItem> Items(CalendarDay day) => [.. DayLayout.Build(day, TestData.Resources, byRoom: false, Vienna).Columns.SelectMany(c => c.Items)];

    private static GuestBirthday Today(int? age = 42) => new(TestData.Monday, 0, age, age);

    // ---------- Texte ----------

    [Fact]
    public void Hover_Karte_und_Panel_nennen_heute_mit_Alter_und_drei_Tage_davor_und_danach_nur_das_Datum()
    {
        Assert.Equal("Geburtstag heute · 42", BirthdayText.Line(Today()));
        Assert.Equal("Geburtstag heute", BirthdayText.Line(Today(null)));
        Assert.Equal("Geburtstag am 04.03.", BirthdayText.Line(new GuestBirthday(new(2027, 3, 4), 3, 37, 36)));
        Assert.Equal("Geburtstag am 26.02.", BirthdayText.Line(new GuestBirthday(new(2027, 2, 26), -3, 37, 37)));
        Assert.Null(BirthdayText.Line(null));
    }

    [Fact]
    public void Die_Kundenkarte_nennt_Alter_und_Abstand_zum_naechsten_Geburtstag()
    {
        Assert.Equal("45 Jahre · Nächster Geburtstag in 12 Tagen", BirthdayText.Card(new GuestBirthday(new(2027, 3, 13), 12, 46, 45)));
        Assert.Equal("45 Jahre · Nächster Geburtstag morgen", BirthdayText.Card(new GuestBirthday(new(2027, 3, 2), 1, 46, 45)));
        Assert.Equal("Geburtstag heute · 46", BirthdayText.Card(Today(46)));
        Assert.Equal("Nächster Geburtstag in 12 Tagen", BirthdayText.Card(new GuestBirthday(new(2027, 3, 13), 12, null, null)));
        Assert.Null(BirthdayText.Card(null));
    }

    // ---------- Eintrag, Hover-Karte ----------

    [Fact]
    public void Das_Geschenk_am_Eintrag_erscheint_nur_am_Tag_selbst_und_nur_bei_Gaesten_mit_Geburtsdatum()
    {
        var today = For(10, TestData.Anna, "Brigitte Müller");
        var near = For(11, TestData.Anna, "Hannah Schmid");
        var before = For(12, TestData.Anna, "Dora Danach");
        var none = For(13, TestData.Anna, "Gerda Ohne");
        var items = Items(DayWith((today, Today()), (near, new GuestBirthday(new(2027, 3, 3), 2, 37, 36)), (before, new GuestBirthday(new(2027, 2, 26), -3, 37, 37)), (none, null)));

        bool Celebrates(AppointmentDto a) => items.Single(i => i.Appointment!.Id == a.Id).BirthdayToday;
        Assert.True(Celebrates(today));
        Assert.False(Celebrates(near));
        Assert.False(Celebrates(before));
        Assert.False(Celebrates(none));
    }

    [Fact]
    public void Die_Hover_Karte_nennt_den_Geburtstag_heute_mit_Alter_und_in_den_drei_Tagen_davor_und_danach_das_Datum()
    {
        var today = For(10, TestData.Anna, "Brigitte Müller");
        var near = For(11, TestData.Anna, "Hannah Schmid");
        var far = For(12, TestData.Anna, "Fritz Fern");
        var items = Items(DayWith((today, Today()), (near, new GuestBirthday(new(2027, 3, 3), 2, 37, 36)), (far, null)));

        HoverCard CardOf(AppointmentDto a) => items.Single(i => i.Appointment!.Id == a.Id).Hover!;
        Assert.Equal("Geburtstag heute · 42", CardOf(today).Birthday);
        Assert.Contains("Geburtstag heute · 42", CardOf(today).ToText().Split('\n'));
        Assert.Equal("Geburtstag am 03.03.", CardOf(near).Birthday);
        Assert.Null(CardOf(far).Birthday);
        Assert.DoesNotContain("Geburtstag", CardOf(far).ToText(), StringComparison.Ordinal);
        // Das Alter steht nur in der Karte, nie am Eintrag.
        Assert.DoesNotContain("42", items.Single(i => i.Appointment!.Id == today.Id).Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_Gast_ohne_Namen_hat_kein_Symbol_weil_die_Kennzeichen_der_buchenden_Person_nicht_an_seinen_Termin_gehoeren()
    {
        var placeholder = For(10, TestData.Anna, "Brigitte Müller") with { PlaceholderGuest = 2 };

        var item = Items(DayWith((placeholder, Today()))).Single();

        Assert.False(item.BirthdayToday);
        Assert.Null(item.Hover!.Birthday);
    }

    // ---------- Tagesüberblick ----------

    [Fact]
    public void Der_Tagesueberblick_nennt_Geburtstage_mit_der_ersten_Uhrzeit_des_Tages_mehrere_mit_Punkt_getrennt()
    {
        var brigitte = For(10, TestData.Anna, "Brigitte Müller") with { Start = TestData.At(10, 30), End = TestData.At(11, 20) };
        var later = brigitte with { Id = Guid.NewGuid(), Start = TestData.At(14), End = TestData.At(14, 50) };
        var hannah = For(9, TestData.Anna, "Hannah Schmid");
        var cancelled = For(12, TestData.Anna, "Absage Abgesagt") with { Status = AppointmentStatus.Cancelled };
        var nearOnly = For(13, TestData.Anna, "Emil Vorher");
        var day = DayWith((brigitte, Today()), (hannah, Today()), (cancelled, Today()), (nearOnly, new GuestBirthday(new(2027, 3, 3), 2, 37, 36)));
        day = day with { Appointments = [.. day.Appointments, later] };

        Assert.Equal("Geburtstag heute: Hannah Schmid (09:00) · Brigitte Müller (10:30)", DayLayout.BirthdayLine(day, Vienna));
        Assert.Equal("Geburtstag heute: Hannah Schmid (09:00)", DayLayout.BirthdayLine(day with { Appointments = [hannah, cancelled] }, Vienna));
        Assert.Null(DayLayout.BirthdayLine(day with { Appointments = [nearOnly, cancelled] }, Vienna));
        Assert.Null(DayLayout.BirthdayLine(TestData.Day(), Vienna));
    }
}
