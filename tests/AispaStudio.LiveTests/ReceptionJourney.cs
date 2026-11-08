using FlaUI.Core.AutomationElements;

namespace AispaStudio.LiveTests;

/// <summary>
/// Ablauf der Rezeption: anmelden als Nina Kovač (Wien, Demo-Daten), dann die Themen des Onboarding-Katalogs
/// (<c>docs/umsetzung/live-tests-rezeption-onboarding.md</c>). Gearbeitet wird mit der Kundin und dem Termin des Laufs oder mit
/// den festen Stammgästen der Demo-Daten; jedes Thema lässt den Kalender auf dem nächsten Tag zurück, wie der Vorlauf ihn hinterlässt.
/// Die Reihenfolge der Themen ist fest; <c>day-closing</c> steht zuletzt.
/// </summary>
internal static partial class ReceptionJourney
{
    /// <summary>Behandlung des Laufs; Wien bietet sie an; die Gesichtsbehandlungen waren nach vielen Läufen ausgebucht.</summary>
    private const string Treatment = "Maniküre mit Lack";

    /// <summary>Stammgast der Demo-Daten (Demo/DemoCustomers.cs), den der Lauf für Termine ohne eigene Kundin nimmt.</summary>
    private const string DemoCustomer = "Anna Bauer";

    /// <summary>Zweiter Stammgast für Fälle, in denen zwei verschiedene Gäste nötig sind.</summary>
    private const string OtherDemoCustomer = "Thomas Leitner";

    public static LiveJourney Journey { get; } = new(
        "Rezeption",
        "Anmelden als Nina Kovač (Rezeption Wien)",
        SignIn,
        [
            .. AccountTopics,
            .. CustomerTopics,
            .. AppointmentTopics,
            .. PosTopics,
        ]);

    private static void SignIn(LiveSession s)
    {
        s.Step("Abgemeldet beginnen", () => SignOutIfSignedIn(s));
        SignInAs(s, "Nina Kovač");
        TomorrowStep(s);
    }

    /// <summary>Am Sonntag hat in den Demo-Daten niemand Dienst; der Lauf arbeitet deshalb mit dem nächsten Tag.</summary>
    private static void TomorrowStep(LiveSession s) =>
        s.Step("Kalender auf den nächsten Tag stellen", () =>
        {
            // Die App merkt sich den zuletzt gezeigten Tag; „Heute“ macht den Ausgangspunkt eindeutig.
            s.Click(Button("Heute"), "Knopf „Heute“");
            s.Click("Nächster Tag");
            s.Report.AddEvent($"Der Lauf arbeitet am {LiveSession.TextOf(s.Find("DateLine"))}.");
        });

    private static void SignInAs(LiveSession s, string person) =>
        s.Step($"Als {person} (Rezeption) anmelden", () =>
        {
            s.Click("Testkonto");
            s.Click(person);
            s.Click("Als Testkonto anmelden");
            s.Expect(e => e.Properties.Name.ValueOrDefault?.StartsWith($"Konto {person}, Rezeption", StringComparison.Ordinal) == true,
                $"Kontoknopf „Konto {person}, Rezeption …“");
        });

    /// <summary>Eintrag der Seitennavigation; „Kalender“ heißt im Kalender auch ein Ansichtsknopf.</summary>
    private static AutomationElement Navigation(LiveSession s, string page) =>
        s.Find(e => e.Properties.ClassName.ValueOrDefault == "ListBoxItem"
                    && e.Properties.Name.ValueOrDefault?.StartsWith(page, StringComparison.Ordinal) == true,
            $"Navigation „{page}“");

    private static void SignOutIfSignedIn(LiveSession s)
    {
        // Die App merkt sich die Anmeldung; je nach Ladezeit erscheint zuerst die Anmeldung oder der Kontoknopf.
        var first = s.Find(e => e.Properties.Name.ValueOrDefault is { } name
                                && (name == "Als Testkonto anmelden" || name.StartsWith("Konto ", StringComparison.Ordinal)),
            "Anmeldung oder Kontoknopf");
        if (first.Properties.Name.ValueOrDefault == "Als Testkonto anmelden")
        {
            return;
        }

        s.Click(first);
        s.Click("Abmelden");
        s.Expect("Als Testkonto anmelden");
    }

    // Kurzformen für Suchbedingungen; Elemente werden immer an Klasse und Name festgemacht, nie am Namen allein.
    private static Func<AutomationElement, bool> NameIs(string name) => e => e.Properties.Name.ValueOrDefault == name;

    private static Func<AutomationElement, bool> NameStarts(string text) =>
        e => e.Properties.Name.ValueOrDefault?.StartsWith(text, StringComparison.Ordinal) == true;

    private static Func<AutomationElement, bool> NameContains(string text) =>
        e => e.Properties.Name.ValueOrDefault?.Contains(text, StringComparison.Ordinal) == true;

    private static Func<AutomationElement, bool> Class(string className, Func<AutomationElement, bool> also) =>
        e => e.Properties.ClassName.ValueOrDefault == className && also(e);

    private static Func<AutomationElement, bool> Button(string name) => Class("Button", NameIs(name));

    /// <summary>Die Kalenderkarte des Termins, mit dem Status am Ende („Gebucht“, „Eingecheckt“ …); ohne Status trifft jede.</summary>
    private static Func<AutomationElement, bool> Card(BookedAppointment appointment, string? status = null) =>
        Class("AppointmentCard", e => e.Properties.Name.ValueOrDefault is { } card
                                      && card.Contains(appointment.CardPrefix, StringComparison.Ordinal)
                                      && (status is null || card.EndsWith($", {status}", StringComparison.Ordinal)));
}
