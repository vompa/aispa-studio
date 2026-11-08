using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace AispaStudio.LiveTests;

internal static partial class ReceptionJourney
{
    private static LiveTopic[] AccountTopics =>
    [
        new("signin", "Abmelden und wieder anmelden", "Kontoknopf, Abmelden, Anmeldung sichtbar, wieder anmelden", SignOutAndIn,
            Leaves: "Legt nichts an."),
        new("signin-other-account", "Anderes Konto: Lisa Hofer", "Als Lisa Hofer anmelden (befristetes Konto), danach wieder als Nina Kovač", OtherAccount,
            Leaves: "Legt nichts an."),
        new("overview", "Tagesüberblick und Kalender lesen", "Tagesüberblick, Räume und Team, Tageswechsel, Suche, Tastenhilfe", Overview,
            Leaves: "Legt nichts an."),
    ];

    private static void SignOutAndIn(LiveSession s, LiveRunContext context)
    {
        s.Step("Kontoknopf öffnen und abmelden", () =>
        {
            s.Click(s.Find(NameStarts("Konto Nina Kovač, Rezeption"), "Kontoknopf"));
            s.Click("Abmelden");
        });
        s.Step("Anmeldung ist wieder zu sehen", () => s.Expect("Als Testkonto anmelden"));
        SignInAs(s, "Nina Kovač");
        TomorrowStep(s);
    }

    private static void OtherAccount(LiveSession s, LiveRunContext context)
    {
        s.Step("Abmelden", () =>
        {
            s.Click(s.Find(NameStarts("Konto Nina Kovač"), "Kontoknopf"));
            s.Click("Abmelden");
            s.Expect("Als Testkonto anmelden");
        });
        SignInAs(s, "Lisa Hofer");
        s.Step("Kalender öffnen: das befristete Konto darf buchen", () =>
        {
            s.Click(Navigation(s, "Kalender"));
            s.Expect(Button("Heute"), "Knopf „Heute“ im Kalender");
        });
        s.Step("Wieder als Nina Kovač anmelden", () =>
        {
            s.Click(s.Find(NameStarts("Konto Lisa Hofer"), "Kontoknopf"));
            s.Click("Abmelden");
            s.Expect("Als Testkonto anmelden");
        });
        SignInAs(s, "Nina Kovač");
        TomorrowStep(s);
    }

    private static void Overview(LiveSession s, LiveRunContext context)
    {
        s.Step("Kalender öffnen", () => s.Click(Navigation(s, "Kalender")));
        s.Step("Tagesüberblick öffnen: Termine, Eingecheckt, Noch erwartet", () =>
        {
            s.Click("OverviewChip");
            s.Expect(NameStarts("Termine, "), "Zahl „Termine, n“ im Tagesüberblick");
            s.Expect(NameStarts("Eingecheckt, "), "Zahl „Eingecheckt, n“ im Tagesüberblick");
            s.Expect(NameStarts("Noch erwartet, "), "Zahl „Noch erwartet, n“ im Tagesüberblick");
            LiveSession.Press(VirtualKeyShort.ESCAPE);
        });
        s.Step("Ansicht Räume: Spalte „Raum Lavendel“", () =>
        {
            s.Click("HeadRooms");
            s.Expect(NameContains("Raum Lavendel"), "Spalte „Raum Lavendel“");
        });
        s.Step("Ansicht Team: die Raumspalte verschwindet", () =>
        {
            s.Click("HeadTeam");
            s.ExpectMissing(NameContains("Raum Lavendel"), "Raumspalte „Raum Lavendel“");
        });
        s.Step("Vortag, Nächster Tag, Heute: Titel „Heute“", () =>
        {
            s.Click("Vortag");
            s.Click("Nächster Tag");
            s.Click(Button("Heute"), "Knopf „Heute“");
            s.Expect(Class("TextBlock", NameStarts("Heute")), "Tagestitel „Heute …“");
            s.Click("Nächster Tag");
        });
        s.Step("Suche mit Strg+F: Treffer oder „Keine Treffer“", () =>
        {
            LiveSession.FocusCalendar();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Keyboard.Type("Moser");
            s.Expect(e => e.Properties.Name.ValueOrDefault is { } name
                          && (name.Contains("Moser", StringComparison.Ordinal) || name == "Keine Treffer"),
                "Trefferliste der Suche oder „Keine Treffer“");
            LiveSession.Press(VirtualKeyShort.ESCAPE);
        });
        s.Step("Tastenhilfe über „Alle Tasten“ öffnen und schließen", () =>
        {
            s.Click(Class("Button", NameStarts("Alle Tasten")), "Knopf „Alle Tasten“");
            s.Expect("Tasten im Kalender");
            s.Click("Tastenhilfe schließen");
            s.ExpectMissing("Tasten im Kalender");
        });
    }
}
