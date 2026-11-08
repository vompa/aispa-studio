using FlaUI.Core.AutomationElements;

namespace AispaStudio.LiveTests;

internal static partial class ReceptionJourney
{
    private static LiveTopic[] CustomerTopics =>
    [
        new("customer-search", "Kundin suchen und Karte lesen", "Katharina Moser nach Name und Telefon suchen, Karte lesen, unbekannten Namen suchen", SearchCustomer,
            Leaves: "Legt nichts an."),
        new("customer-search-alias", "Suche nach Rufname und mit Titel", "„Kathi“ findet Katharina Moser, „Mag. Leitner“ findet Thomas Leitner", SearchCustomerAlias,
            Leaves: "Legt nichts an."),
        new("customer", "Kundin anlegen", "Neue Kundin mit Name und Telefon anlegen", CreateCustomer),
        new("customer-duplicate", "Kundin anlegen: Dublette", "Katharina Moser eintippen, „Schon vorhanden?“, „Diese Kundin nehmen“", DuplicateCustomer,
            Leaves: "Legt nichts an."),
        new("customer-minimal", "Kundin anlegen: nur Nachname", "Nur ein Nachname, kein Telefon: Anlegen geht, keine Kopierknöpfe", MinimalCustomer),
        new("customer-empty", "Kundin anlegen: Pflichtfeld leer", "Leere Maske: Anlegen ist gesperrt", EmptyCustomer,
            Leaves: "Legt nichts an."),
        new("customer-phone-format", "Telefonnummer wird ins Landesformat gebracht", "„0664 1234567“ wird beim Verlassen des Feldes zu „+43 664 1234567“", PhoneFormat,
            Leaves: "Legt nichts an."),
        new("customer-badmail", "Kundin anlegen: E-Mail ohne @", "Anlegen mit „keine-adresse“ zeigt die rote Meldung", BadMail,
            Leaves: "Legt nichts an."),
    ];

    private static void OpenCustomers(LiveSession s) => s.Step("Kunden öffnen", () => s.Click(Navigation(s, "Kunden")));

    private static void SearchFor(LiveSession s, string query) => s.Type("Kunden suchen", query);

    private static Func<AutomationElement, bool> CustomerRow(string lastName) => Class("ListBoxItem", NameContains(lastName));

    private static void SearchCustomer(LiveSession s, LiveRunContext context)
    {
        OpenCustomers(s);
        s.Step("Katharina Moser nach dem Namen suchen und die Karte öffnen", () =>
        {
            SearchFor(s, "Katharina Moser");
            s.Click(s.Find(CustomerRow("Moser"), "Trefferzeile „Moser“"));
            // Die Karte nennt den Rufnamen im Kopf („Kathi Moser“) und den vollen Namen mit Anrede darunter.
            s.Expect(Class("TextBlock", NameIs("Frau Katharina Moser")), "Karte „Frau Katharina Moser“");
        });
        s.Step("Die Karte zeigt Telefon, Stammgast-Stern und Besuchsstatistik", () =>
        {
            s.Expect(Button("Telefon +43 664 1234002 kopieren"), "Kopierknopf zur Telefonnummer");
            s.Expect(NameStarts("Stammgast"), "Stern „Stammgast“");
            s.Expect(Class("TextBlock", NameContains("Besuche in 12 Monaten")), "Besuchsstatistik");
        });
        s.Step("Dieselbe Kundin über das Telefon finden", () =>
        {
            SearchFor(s, "+43 664 1234002");
            s.Expect(CustomerRow("Moser"), "Trefferzeile „Moser“ zur Telefonnummer");
        });
        s.Step("Unbekannter Name: keine Kundin, aber „Neue Kundin anlegen“", () =>
        {
            SearchFor(s, "Xyzzy");
            s.Expect("Keine Kundin gefunden.");
            s.Expect(Class("Button", NameContains("Xyzzy")), "Knopf „Neue Kundin „Xyzzy“ anlegen“");
        });
    }

    private static void SearchCustomerAlias(LiveSession s, LiveRunContext context)
    {
        OpenCustomers(s);
        s.Step("Rufname „Kathi“ findet Katharina Moser", () =>
        {
            SearchFor(s, "Kathi");
            s.Expect(CustomerRow("Moser"), "Trefferzeile „Moser“ zum Rufnamen");
        });
        s.Step("Mit Titel „Mag. Leitner“ wird Thomas Leitner gefunden", () =>
        {
            SearchFor(s, "Mag. Leitner");
            s.Expect(CustomerRow("Leitner"), "Trefferzeile „Leitner“ trotz Titel im Suchtext");
        });
    }

    /// <summary>Der Hinweistext der offenen Maske „Neue Kundin / neuer Kunde“; die Maske selbst ist ein Border ohne Eintrag im Automationsbaum.</summary>
    private static readonly Func<AutomationElement, bool> MaskHint = Class("TextBlock", NameIs("Nur ein Name ist Pflicht."));

    private static void StartNewCustomer(LiveSession s) =>
        s.Step("Neue Kundin beginnen", () =>
        {
            s.Click("NewButton");
            s.Expect(MaskHint, "Die Maske „Neue Kundin“");
        });

    private static void CreateCustomer(LiveSession s, LiveRunContext context)
    {
        var name = $"Eva Live{context.Stamp}";
        OpenCustomers(s);
        StartNewCustomer(s);
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

    private static void DuplicateCustomer(LiveSession s, LiveRunContext context)
    {
        OpenCustomers(s);
        StartNewCustomer(s);
        s.Step("Name einer Stammkundin eintippen: „Schon vorhanden?“", () =>
        {
            s.Type("Nachname", "Moser");
            s.Type("Vorname", "Katharina");
            s.Expect("Schon vorhanden?");
            s.Expect(Class("TextBlock", NameIs("Moser, Kathi")), "Vorschlag „Moser, Kathi“ unter „Schon vorhanden?“");
        });
        s.Step("„Diese Kundin nehmen“: Maske zu, Karte der Stammkundin offen", () =>
        {
            // Unter „Schon vorhanden?“ stehen alle Mosers; der Knopf muss zur Zeile von Katharina Moser („Moser, Kathi“) gehören.
            var suggestion = s.Find(Class("TextBlock", NameIs("Moser, Kathi")), "Vorschlag „Moser, Kathi“");
            s.Click(LiveSession.FindInRow(suggestion, Button("Diese Kundin nehmen"), "Knopf „Diese Kundin nehmen“"));
            s.ExpectMissing(MaskHint, "Die Maske „Neue Kundin“");
            s.Expect(Class("TextBlock", NameIs("Frau Katharina Moser")), "Karte „Frau Katharina Moser“");
        });
    }

    private static void MinimalCustomer(LiveSession s, LiveRunContext context)
    {
        var name = $"Live{context.Stamp}n";
        OpenCustomers(s);
        StartNewCustomer(s);
        s.Step("Nur den Nachnamen eintragen: Anlegen ist freigegeben", () =>
        {
            s.Type("Nachname", name);
            s.Expect(Class("Button", e => e.Properties.Name.ValueOrDefault == "Anlegen" && e.Properties.IsEnabled.ValueOrDefault),
                "Freigegebener Knopf „Anlegen“");
        });
        s.Step("Anlegen: Karte ohne Telefon-Kopierknopf", () =>
        {
            s.Click(Button("Anlegen"), "Knopf „Anlegen“");
            s.Expect(Class("TextBlock", NameContains(name)), $"Kartenkopf „{name}“");
            s.ExpectMissing(Class("Button", NameStarts("Telefon ")), "Kopierknopf „Telefon … kopieren“");
        });
    }

    private static void EmptyCustomer(LiveSession s, LiveRunContext context)
    {
        OpenCustomers(s);
        StartNewCustomer(s);
        s.Step("Leere Maske: „Anlegen“ ist gesperrt", () =>
        {
            s.Expect(Button("Anlegen"), "Knopf „Anlegen“");
            s.ExpectMissing(Class("Button", e => e.Properties.Name.ValueOrDefault == "Anlegen" && e.Properties.IsEnabled.ValueOrDefault),
                "Freigegebener Knopf „Anlegen“");
        });
        s.Step("Abbrechen schließt die Maske", () =>
        {
            s.Click("Abbrechen");
            s.ExpectMissing(MaskHint, "Die Maske „Neue Kundin“");
        });
    }

    private static void PhoneFormat(LiveSession s, LiveRunContext context)
    {
        OpenCustomers(s);
        StartNewCustomer(s);
        s.Step("Telefon „0664 1234567“ eintippen und das Feld verlassen", () =>
        {
            s.Type("Telefon", "0664 1234567");
            s.Click("Nachname");
        });
        s.Step("Das Feld zeigt „+43 664 1234567“", () =>
        {
            var phone = LiveSession.TextOf(s.Find("Telefon"));
            if (phone != "+43 664 1234567")
            {
                throw new InvalidOperationException($"Telefon steht auf „{phone}“, erwartet „+43 664 1234567“.");
            }

            s.Click("Abbrechen");
        });
    }

    private static void BadMail(LiveSession s, LiveRunContext context)
    {
        OpenCustomers(s);
        StartNewCustomer(s);
        s.Step("Nachname und E-Mail ohne @ eintragen", () =>
        {
            s.Type("Nachname", $"Live{context.Stamp}m");
            s.Type("E-Mail", "keine-adresse");
        });
        s.Step("Anlegen: „Keine gültige E-Mail-Adresse.“", () =>
        {
            s.Click(Button("Anlegen"), "Knopf „Anlegen“");
            s.Expect(NameContains("Keine gültige E-Mail-Adresse"), "Fehlermeldung „Keine gültige E-Mail-Adresse.“");
        });
        s.Step("Abbrechen", () => s.Click("Abbrechen"));
    }
}
