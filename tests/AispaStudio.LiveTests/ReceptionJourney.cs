namespace AispaStudio.LiveTests;

/// <summary>
/// Ablauf der Rezeption: anmelden als Nina Kovač (Wien, Demo-Daten), neue Kundin anlegen und für sie einen Termin buchen.
/// </summary>
internal static class ReceptionJourney
{
    private const string Treatment = "Gesichtsbehandlung Klassik";

    public static int Run(LiveSession s) => s.Run(() =>
    {
        // Name und Telefon je Lauf eindeutig, sonst schlägt die Kundenmaske die Kundin des letzten Laufs als Dublette vor.
        var stamp = DateTime.Now.ToString("MMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var lastName = $"Live{stamp}";

        s.Step("Abgemeldet beginnen", () => SignOutIfSignedIn(s));
        s.Step("Als Nina Kovač (Rezeption Wien) anmelden", () =>
        {
            s.Click("Testkonto");
            s.Click("Nina Kovač");
            s.Click("Als Testkonto anmelden");
            s.Expect("Konto Nina Kovač, Rezeption · AISPA Day Spa GmbH");
        });
        s.Step("Kalender auf den nächsten Tag stellen", () => s.Click("Nächster Tag"));
        s.Step("Kunden öffnen", () => s.Click("Kunden"));
        s.Step("Neue Kundin beginnen", () => s.Click("NewButton"));
        s.Step($"Kundin Eva {lastName} eintragen", () =>
        {
            s.Type("Vorname", "Eva");
            s.Type("Nachname", lastName);
            s.Type("Telefon", $"+43 660 {stamp}");
        });
        s.Step("Anlegen und Termin buchen", () =>
        {
            s.Click("Anlegen und Termin buchen");
            s.Expect("Termin buchen");
        });
        s.Step($"{Treatment} wählen", () => s.Choose("Behandlung", Treatment));
        // Eine feste Uhrzeit hielte nicht: jeder Lauf hinterlässt einen Termin. Die Rezeption sucht deshalb wie im Betrieb freie Zeit.
        s.Step("Freie Zeit suchen", () =>
        {
            s.Click(s.Find(e => e.Properties.Name.ValueOrDefault == "Freie Zeit suchen"
                                && e.Properties.ClassName.ValueOrDefault == "Button"
                                && e.Properties.AutomationId.ValueOrDefault != "FreeTimeButton",
                "Knopf „Freie Zeit suchen“ im Buchungsbereich"));
            s.Expect(e => e.Properties.ClassName.ValueOrDefault == "ListBoxItem", "Erste freie Zeit");
        });
        s.Step("Erste freie Zeit buchen", () =>
        {
            s.Click(s.Find(e => e.Properties.Name.ValueOrDefault?.EndsWith("buchen (Enter)", StringComparison.Ordinal) == true
                                || e.Properties.Name.ValueOrDefault == "Buchen (Enter)",
                "Knopf „Buchen“ unter den freien Zeiten"));
            // Die Kalenderkarte heißt „<Zeit> Eva Live…, <Behandlung>, Gebucht“; der Name allein stünde auch im Kundenfeld.
            s.Expect(
                e => e.Properties.ClassName.ValueOrDefault == "AppointmentCard"
                     && e.Properties.Name.ValueOrDefault is { } card
                     && card.Contains($" Eva {lastName}, {Treatment}", StringComparison.Ordinal),
                $"Kalenderkarte „Eva {lastName}, {Treatment}“");
        });
        // Kassieren braucht „Ist da“, und das geht nur am Tag des Termins; der Lauf bucht für den nächsten Tag (am Sonntag
        // hat in den Demo-Daten niemand Dienst). Das Kassieren kommt mit der Ausbaustufe (ADR 0090).
        s.Report.AddEvent("Kassieren nicht geprüft: braucht „Ist da“ am Tag des Termins, der Lauf bucht für den nächsten Tag.");
    });

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
}
