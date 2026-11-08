using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Hover-Karte eines Termins: Kopf in drei Zeilen (Name mit ♀/♂ und Etikett, Behandlung, Zeit · Dauer · Person · Raum), oranges Band für alles,
/// was Handeln verlangt, Kennzeichen nur für die übrigen Zeichen, Besuche einmal unter „Gast“, Ablauf in einer Zeile.
/// </summary>
public sealed class HoverCardTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static CalendarItem Build(AppointmentDto appointment, CalendarGuest guest)
    {
        var day = TestData.Day(appointment) with { Guests = [guest] };
        return DayLayout.Build(day, TestData.Resources, byRoom: false, Vienna).Columns[0].Items.Single();
    }

    [Fact]
    public void Kopf_zeigt_Name_mit_Anrede_Etikett_Behandlung_und_eine_Zeile_mit_Zeit_Dauer_Person_und_Raum()
    {
        var appointment = TestData.Appointment(10, TestData.Anna) with { CustomerName = "Steffi Huber" };
        var guest = new CalendarGuest(appointment.CustomerId, 136, 136, 0, TestData.Monday.AddDays(-1), null, Salutation: CustomerSalutation.Female);

        var item = Build(appointment, guest);
        var card = item.Hover!;

        Assert.Equal("Steffi Huber", card.Title);
        Assert.Equal("♀", card.Symbol);
        Assert.Equal("Stammgast", card.Tag);
        Assert.Equal("Massage", card.Treatment);
        Assert.Equal("Mo. 1.3. · 10:00–10:50 · 50 Min. · Anna · Raum Zeder", card.When);
        Assert.Equal(("Mo. 1.3. · 10:00–10:50", "50 Min.", "Anna", "Raum Zeder"), (card.DayTime, card.Duration, card.Person, card.Room));
        Assert.Equal("♀", item.Symbol);
        Assert.StartsWith("Steffi Huber ♀ · Stammgast\nMassage\nMo. 1.3. · 10:00–10:50", item.HoverText, StringComparison.Ordinal);
        // Der Stammgast sagt es im Etikett: keine Zeile „Besuche“ (Ä158), und die Zahl steht nirgends in der Karte.
        Assert.DoesNotContain(card.Guest, f => f.Label == "Besuche");
        Assert.Equal(Controls.Icons.Female, card.SymbolIcon);
        Assert.DoesNotContain(card.Flags, f => f.Kind is GuestFlagKind.Regular or GuestFlagKind.New);
        Assert.DoesNotContain("136", item.HoverText, StringComparison.Ordinal);
    }

    [Fact]
    public void Divers_und_ohne_Angabe_zeigen_kein_Symbol_der_Erstbesuch_ist_ein_Etikett()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var item = Build(appointment, new CalendarGuest(appointment.CustomerId, 0, 0, 0, null, null, Salutation: CustomerSalutation.Diverse));

        Assert.Null(item.Hover!.Symbol);
        Assert.Null(item.Symbol);
        Assert.Equal("Erstbesuch", item.Hover.Tag);
        Assert.DoesNotContain(item.Hover.Guest, f => f.Label == "Besuche");
    }

    [Fact]
    public void Alles_was_Handeln_verlangt_steht_im_Band_oben_die_uebrigen_Zeichen_unter_Kennzeichen()
    {
        var appointment = TestData.Appointment(10, TestData.Anna) with { LateMinutes = 15 };
        var guest = new CalendarGuest(appointment.CustomerId, 4, 2, 3, TestData.Monday.AddDays(-20), "Allergie gegen Nussöl",
            new CustomerBookingPreferences(TestData.Ben.Id, true, Gender: TherapistGender.Male),
            Flags: [CustomerFlagKind.DoNotBook, CustomerFlagKind.InformManagement, CustomerFlagKind.MoreTime], Language: "en");

        var card = Build(appointment, guest).Hover!;

        Assert.Contains("Gast kommt 10:15", card.Alerts);
        Assert.Contains(card.Alerts, a => a.Contains("Allergie gegen Nussöl", StringComparison.Ordinal) && a.Contains("3× nicht erschienen", StringComparison.Ordinal));
        Assert.Contains(card.Alerts, a => a.StartsWith("Nicht mehr buchen", StringComparison.Ordinal));
        Assert.Contains(card.Alerts, a => a.StartsWith("Leitung informieren", StringComparison.Ordinal));
        Assert.Contains(card.Alerts, a => a.StartsWith("Feste Therapeut:in ist Ben", StringComparison.Ordinal));
        Assert.Equal([GuestFlagKind.MoreTime, GuestFlagKind.Language], card.Flags.Select(f => f.Kind));
    }

    [Fact]
    public void Wunsch_Therapeut_abweichend_steht_im_Band_passend_nur_unter_Wuensche()
    {
        var anna = TestData.Anna with { Gender = TherapistGender.Female };
        var resources = TestData.Resources with { Employees = [anna, TestData.Ben] };
        var appointment = TestData.Appointment(10, anna);
        var day = TestData.Day(appointment) with
        {
            Guests = [new CalendarGuest(appointment.CustomerId, 1, 1, 0, null, null, new CustomerBookingPreferences(Gender: TherapistGender.Male))],
        };

        var card = DayLayout.Build(day, resources, byRoom: false, Vienna).Columns[0].Items.Single().Hover!;

        Assert.Contains(card.Alerts, a => a.StartsWith("Wünscht einen Therapeuten", StringComparison.Ordinal));
        Assert.False(card.HasFlags);
    }

    [Fact]
    public void Ablauf_in_einer_Zeile_Hinweis_darunter_seltene_Angaben_nur_wenn_vorhanden()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var day = TestData.Day(appointment) with
        {
            Guests = [new CalendarGuest(appointment.CustomerId, 2, 2, 0, TestData.Monday.AddDays(-7), null)],
            Preparations = [new CalendarPreparation(TestData.MassageTreatment.Id, "Öl anwärmen")],
        };

        var item = DayLayout.Build(day, TestData.Resources, byRoom: false, Vienna).Columns[0].Items.Single();

        // Der übliche Ablauf steht in den Streifen des Kalenders, nicht in der Karte (Ä158).
        Assert.Equal("Vorb. 5 · Nachb. 10 · Raumpflege 10 Min.", item.Hover!.Flow);
        Assert.False(item.Hover.HasFlow);
        Assert.Equal("Öl anwärmen", item.Hover.Preparation);
        Assert.Empty(item.Hover.Appointment);
        Assert.False(item.Hover.HasFlags);
    }

    [Fact]
    public void Die_neue_Karte_ist_rund_ein_Drittel_kuerzer()
    {
        // Typischer Termin mit Stammgast, fester Therapeutin, Vorlieben, Ablauf und Notiz: in der alten Gliederung 13 Zeilen.
        var appointment = TestData.Appointment(10, TestData.Anna) with { Note = "Rücken" };
        var guest = new CalendarGuest(appointment.CustomerId, 12, 6, 0, TestData.Monday.AddDays(-14), null,
            new CustomerBookingPreferences(TestData.Anna.Id, true, [DayOfWeek.Monday]), "Druck: fest · wenig Gespräch", Salutation: CustomerSalutation.Male);
        var day = TestData.Day(appointment) with
        {
            Guests = [guest],
            Preparations = [new CalendarPreparation(TestData.MassageTreatment.Id, "Öl anwärmen")],
        };

        var lines = DayLayout.Build(day, TestData.Resources, byRoom: false, Vienna).Columns[0].Items.Single().HoverText!.Split('\n');

        Assert.True(lines.Length <= 9, string.Join(" | ", lines));
    }

    [Fact]
    public void Given_ein_Gast_ohne_Ausfaelle_When_die_Karte_aufgeht_Then_fehlt_die_Zeile_Nicht_erschienen()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var card = Build(appointment, new CalendarGuest(appointment.CustomerId, 3, 2, 0, TestData.Monday.AddDays(-9), null)).Hover!;

        Assert.DoesNotContain(card.Alerts, a => a.Contains("nicht erschienen", StringComparison.Ordinal));
        Assert.DoesNotContain(card.Guest, f => f.Label == "Nicht erschienen");
        Assert.DoesNotContain("nicht erschienen", card.ToText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Given_ein_Gast_mit_einem_Ausfall_When_die_Karte_aufgeht_Then_steht_er_als_Warnung_im_Band_ohne_Etikett_und_Wert()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var card = Build(appointment, new CalendarGuest(appointment.CustomerId, 3, 2, 1, TestData.Monday.AddDays(-9), null)).Hover!;

        Assert.Contains("1× nicht erschienen", card.Alerts);
        Assert.DoesNotContain(card.Guest, f => f.Label == "Nicht erschienen");
    }

    [Fact]
    public void Besuche_stehen_nur_bei_Gaesten_ohne_Etikett()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);

        var card = Build(appointment, new CalendarGuest(appointment.CustomerId, 3, 2, 0, TestData.Monday.AddDays(-9), null)).Hover!;

        Assert.Null(card.Tag);
        Assert.Equal(["2 in 12 Monaten · zuletzt 20.02.2027"], card.Guest.Where(f => f.Label == "Besuche").Select(f => f.Value));
    }

    [Fact]
    public void Der_Ablauf_steht_nur_bei_geaenderten_Zeiten_oder_Einwirkzeit()
    {
        var plain = TestData.Appointment(10, TestData.Anna);
        var changed = plain with { PreparationMinutes = 15, BlockedFrom = TestData.At(9, 45) };
        var day = TestData.Day(changed);

        var card = DayLayout.Build(day with { Guests = [new CalendarGuest(changed.CustomerId, 3, 2, 0, null, null)] }, TestData.Resources, byRoom: false, Vienna)
            .Columns[0].Items.Single().Hover!;

        Assert.True(card.HasFlow);
    }

    [Fact]
    public void Person_normal_in_der_Teamansicht_und_fett_in_der_Raumansicht()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var day = TestData.Day(appointment);

        var team = DayLayout.Build(day, TestData.Resources, byRoom: false, Vienna).Columns[0].Items.Single().Hover!;
        var rooms = DayLayout.Build(day, TestData.Resources, byRoom: true, Vienna).Columns.SelectMany(c => c.Items).Single().Hover!;

        Assert.True(team.PersonNormal);
        Assert.False(team.PersonStrong);
        Assert.True(rooms.PersonStrong);
        Assert.False(rooms.PersonNormal);
    }
}
