namespace AispaStudio.LiveTests;

/// <summary>
/// Ablauf der Rezeption: anmelden als Nina Kovač (Wien, Demo-Daten), dann die Themen „Kundin anlegen“ und „Termin buchen“.
/// Gebucht wird für die Kundin des Laufs, ohne sie für eine Kundin aus den Demo-Daten.
/// </summary>
internal static class ReceptionJourney
{
    private const string Treatment = "Gesichtsbehandlung Klassik";
    private const string DemoCustomer = "Ingrid Wallner";

    public static LiveJourney Journey { get; } = new(
        "Rezeption",
        "Anmelden als Nina Kovač (Rezeption Wien)",
        SignIn,
        [
            new("customer", "Kundin anlegen", "Neue Kundin mit Name und Telefon anlegen", CreateCustomer),
            new("appointment", "Termin buchen", $"{Treatment} zur ersten freien Zeit buchen, ohne neue Kundin für {DemoCustomer}", BookAppointment),
        ]);

    private static void SignIn(LiveSession s)
    {
        s.Step("Abgemeldet beginnen", () => SignOutIfSignedIn(s));
        s.Step("Als Nina Kovač (Rezeption Wien) anmelden", () =>
        {
            s.Click("Testkonto");
            s.Click("Nina Kovač");
            s.Click("Als Testkonto anmelden");
            s.Expect("Konto Nina Kovač, Rezeption · AISPA Day Spa GmbH");
        });
        // Am Sonntag hat in den Demo-Daten niemand Dienst.
        s.Step("Kalender auf den nächsten Tag stellen", () => s.Click("Nächster Tag"));
    }

    private static void CreateCustomer(LiveSession s, LiveRunContext context)
    {
        var name = $"Eva Live{context.Stamp}";
        s.Step("Kunden öffnen", () => s.Click(Navigation(s, "Kunden")));
        s.Step("Neue Kundin beginnen", () => s.Click("NewButton"));
        s.Step($"Kundin {name} eintragen", () =>
        {
            s.Type("Vorname", "Eva");
            s.Type("Nachname", $"Live{context.Stamp}");
            s.Type("Telefon", $"+43 660 {context.Stamp}");
        });
        s.Step("Anlegen", () =>
        {
            s.Click("Anlegen");
            // Das Feld trägt den Namen als Wert, nicht als Name; gefunden wird also nur die angelegte Kundin.
            s.Expect(e => e.Properties.Name.ValueOrDefault == name, $"Kundin „{name}“");
        });
        context.CustomerName = name;
    }

    private static void BookAppointment(LiveSession s, LiveRunContext context)
    {
        var customer = context.CustomerName ?? DemoCustomer;
        s.Step("Kalender öffnen", () => s.Click(Navigation(s, "Kalender")));
        s.Step("Neuen Termin beginnen", () =>
        {
            s.Click(s.Find(e => e.Properties.AutomationId.ValueOrDefault == "PART_PrimaryButton"
                                && e.Properties.Name.ValueOrDefault == "Termin",
                "Knopf „+ Termin“"));
            s.Expect("Kundin oder Kunde suchen");
        });
        s.Step($"Kundin {customer} suchen und wählen", () =>
        {
            s.Type("Kundin oder Kunde suchen", customer);
            s.Click(s.Find(e => e.Properties.ClassName.ValueOrDefault is "ListBoxItem" or "Button"
                                && e.Properties.Name.ValueOrDefault?.StartsWith(customer, StringComparison.Ordinal) == true,
                $"Treffer „{customer}“"));
            s.Expect("Ändern");
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
            // Eine Kundin aus den Demo-Daten hat schon Termine; erst die Meldung belegt, dass dieser Lauf gebucht hat.
            s.Expect(e => e.Properties.Name.ValueOrDefault?.StartsWith($"Gebucht: {customer},", StringComparison.Ordinal) == true,
                $"Meldung „Gebucht: {customer}, …“");
            // Die Kalenderkarte heißt „<Zeit> <Kundin>, <Behandlung>, Gebucht“; der Name allein stünde auch im Kundenfeld.
            s.Expect(
                e => e.Properties.ClassName.ValueOrDefault == "AppointmentCard"
                     && e.Properties.Name.ValueOrDefault is { } card
                     && card.Contains($" {customer}, {Treatment}", StringComparison.Ordinal),
                $"Kalenderkarte „{customer}, {Treatment}“");
        });
        // Kassieren braucht „Ist da“, und das geht nur am Tag des Termins; der Lauf bucht für den nächsten Tag.
        s.Report.AddEvent("Kassieren nicht geprüft: braucht „Ist da“ am Tag des Termins, der Lauf bucht für den nächsten Tag.");
    }

    /// <summary>Eintrag der Seitennavigation; „Kalender“ heißt im Kalender auch ein Ansichtsknopf.</summary>
    private static FlaUI.Core.AutomationElements.AutomationElement Navigation(LiveSession s, string page) =>
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
}
