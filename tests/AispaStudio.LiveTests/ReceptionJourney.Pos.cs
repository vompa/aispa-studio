using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;

namespace AispaStudio.LiveTests;

/// <summary>
/// Themen an der Kassa. Sie sind nach dem Code der Masken und dem Onboarding-Katalog geschrieben, aber noch nicht in einem Lauf
/// geprüft (der Auftraggeber hat die Läufe auf später verschoben); ihre Beschreibung beginnt deshalb mit „Nicht live geprüft.“
/// </summary>
internal static partial class ReceptionJourney
{
    private const string Unchecked = "Nicht live geprüft. ";

    private static LiveTopic[] PosTopics =>
    [
        new("checkout", "Termin kassieren", Unchecked + "Einchecken, Kassieren, zu wenig bar, Rückgeld, Beleg", Checkout, TermNeeds,
            Leaves: "Kassiert den Termin des Laufs und erzeugt einen Beleg; zum Aufräumen die Datenbank neu laden."),
        new("voucher", "Gutschein verkaufen", Unchecked + "Wertgutschein für 50,00 € verkaufen und den Code vom Beleg lesen", SellVoucher,
            Leaves: "Erzeugt einen Gutscheinbeleg; zum Aufräumen die Datenbank neu laden."),
        new("voucher-invalid", "Gutschein-Code ungültig", Unchecked + "Ein erfundener Code meldet „Diesen Gutschein-Code gibt es nicht.“", InvalidVoucher,
            Leaves: "Legt nichts an."),
        new("receipt-void-empty-reason", "Beleg stornieren: Grund fehlt", Unchecked + "Ohne Grund ist „Stornieren“ gesperrt", VoidWithoutReason,
            Leaves: "Legt nichts an."),
        new("day-closing", "Einlage und Tagesabschluss", Unchecked + "Einlage buchen, Bestand zählen, Tagesabschluss durchführen", DayClosing,
            Leaves: "Der Tagesabschluss lässt sich nicht zurücknehmen und beginnt die Kassa ab null; Datenbank danach neu laden."),
    ];

    private static void OpenPos(LiveSession s) => s.Step("Kassa öffnen", () => s.Click(Navigation(s, "Kassa")));

    private static Func<AutomationElement, bool> EnabledButton(string name) =>
        Class("Button", e => e.Properties.Name.ValueOrDefault == name && e.Properties.IsEnabled.ValueOrDefault);

    private static void Checkout(LiveSession s, LiveRunContext context)
    {
        var booked = context.Appointment!;
        RequireToday(booked);
        OpenCalendar(s);
        s.Step("Termin einchecken und „Kassieren“", () =>
        {
            SelectCard(s, booked, "Gebucht");
            s.Expect(Button("Einchecken"), "Knopf „Einchecken“ im Panel");
            Thread.Sleep(700); // Sichtprüfung 09.11.2026: ein zu früher Klick geht ins Leere
            s.Click(Button("Einchecken"), "Knopf „Einchecken“");
            s.Expect(Card(booked, "Eingecheckt"), "Karte mit Status „Eingecheckt“");
            s.Click(Button("Kassieren"), "Knopf „Kassieren“");
            s.Expect(NameContains(booked.Treatment), "Rechnungsposition mit der Behandlung");
        });
        s.Step("Zu wenig bar: „Noch offen“, „Bezahlen“ gesperrt", () =>
        {
            s.Type("Bar gegeben", "1,00");
            s.Expect(NameStarts("Noch offen:"), "Hinweis „Noch offen: …“");
            s.ExpectMissing(EnabledButton("Bezahlen und Beleg erstellen"), "Freigegebener Knopf „Bezahlen und Beleg erstellen“");
        });
        s.Step("Bar mit Rückgeld: „Rückgeld“, „Bezahlen und Beleg erstellen“", () =>
        {
            s.Type("Bar gegeben", "500,00");
            s.Expect(NameStarts("Rückgeld:"), "Hinweis „Rückgeld: …“");
            s.Click(EnabledButton("Bezahlen und Beleg erstellen"), "Freigegebener Knopf „Bezahlen und Beleg erstellen“");
            s.Expect(NameIs("Bezahlt"), "Überschrift „Bezahlt“");
            s.Expect(NameStarts("Beleg Nr."), "Text „Beleg Nr. …“");
        });
    }

    /// <summary>Die App merkt sich, ob „Artikel und Gutscheine“ offen ist; nur aufklappen, wenn „Wert (EUR)“ fehlt.</summary>
    private static void OpenSales(LiveSession s)
    {
        if (s.TryFind("Wert (EUR)", TimeSpan.FromMilliseconds(800)) is null)
        {
            s.Click("SalesColumnHeader");
        }
    }

    private static void SellVoucher(LiveSession s, LiveRunContext context)
    {
        OpenPos(s);
        s.Step("„Artikel und Gutscheine“ aufklappen, „Gutschein verkaufen“", () =>
        {
            OpenSales(s);
            s.Type("Wert (EUR)", "50,00");
            s.Click(Button("Wertgutschein verkaufen"), "Knopf „Wertgutschein verkaufen“");
        });
        s.Step("Bar bezahlen: Beleg mit „Gutschein XXXX-XXXX-XXXX“, Code lesen", () =>
        {
            Thread.Sleep(1200); // Sichtprüfung 09.11.2026: nach „Wertgutschein verkaufen“ baut die Kassa neu auf
            s.Type("Bar gegeben", "50,00");
            LiveSession.Press(VirtualKeyShort.TAB);
            s.Click(EnabledButton("Bezahlen und Beleg erstellen"), "Freigegebener Knopf „Bezahlen und Beleg erstellen“");
            var line = s.Read(NameStarts("Gutschein "), "Belegzeile „Gutschein …“");
            var code = System.Text.RegularExpressions.Regex.Match(line, "[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}");
            context.VoucherCode = code.Success ? code.Value : throw new InvalidOperationException($"Kein Gutscheincode in „{line}“.");
        });
    }

    private static void InvalidVoucher(LiveSession s, LiveRunContext context)
    {
        OpenPos(s);
        s.Step("Ungültigen Code eintragen: „Diesen Gutschein-Code gibt es nicht.“", () =>
        {
            OpenSales(s);
            s.Type("Gutschein-Code", "ABCD-EFGH-2345");
            LiveSession.Press(VirtualKeyShort.TAB);
            s.Expect(NameContains("Diesen Gutschein-Code gibt es nicht"), "Meldung „Diesen Gutschein-Code gibt es nicht.“");
        });
    }

    private static void VoidWithoutReason(LiveSession s, LiveRunContext context)
    {
        OpenPos(s);
        s.Step("Reiter „Belege“: ohne Grund ist „Stornieren“ gesperrt", () =>
        {
            s.Click(Class("TabButton", NameIs("Belege")), "Reiter „Belege“");
            s.ExpectMissing(EnabledButton("Stornieren"), "Freigegebener Knopf „Stornieren“");
        });
    }

    private static void DayClosing(LiveSession s, LiveRunContext context)
    {
        OpenPos(s);
        s.Step("Reiter „Kassenlade & Abschluss“: Einlage von 100,00 € buchen", () =>
        {
            s.Click(Class("TabButton", NameIs("Kassenlade & Abschluss")), "Reiter „Kassenlade & Abschluss“");
            s.Type("Betrag (EUR)", "100,00");
            s.Type("Grund", "Wechselgeld");
            s.Click(Button("Buchen"), "Knopf „Buchen“");
        });
        s.Step("Tagesabschluss durchführen", () =>
        {
            s.Type("Gezählter Bestand (EUR)", "800,00"); // Pflichtfeld seit der Sichtprüfung 09.11.2026; bewusst nicht der Soll-Bestand, um die Differenz zu sehen
            LiveSession.Press(VirtualKeyShort.TAB);
            s.Click(Button("Tagesabschluss durchführen"), "Knopf „Tagesabschluss durchführen“");
            s.Expect(NameContains("Tagesabschluss gespeichert"), "Meldung „Tagesabschluss gespeichert“");
        });
    }
}
