---
art: seite
bereich: Kassa
oberflaeche: windows-app
rollen: rezeption, leitung, inhaber, manager
seite: kassa
stichwoerter: Kassa, Kasse, Kassieren, Rechnung, Beleg, Bon, abrechnen, bezahlen, Zahlung, Registrierkasse, RKSV, TSE, Tagesabschluss, Z-Bericht, Kassenlade, POS, Export, Excel, CSV, Belegliste exportieren, Umsatz exportieren, Gutscheinbestand, checkout, cash register, till, receipt
---
# Seite „Kassa“ in der Windows-App

Auf der Seite „Kassa“ rechnet die Rezeption Termine, Eintritte, Artikel und Gutscheine ab, sieht die Belege des Tages und macht den Tagesabschluss. Steuern, Signatur und Beleg rechnet der Server; die App zeigt an, was er zurückgibt.

Die Seite sehen alle, die Termine buchen dürfen (Inhaber, Standortleitung, Rezeption). Therapeut:innen haben sie nicht in der Navigation.

![Kassa mit offener Rechnung](images/app/kassa.png)

## Kopfzeile {#kopfzeile}

- Drei Bereiche zum Umschalten: **Kassieren**, **Belege** und **Kassenlade & Abschluss**.
- Die Auswahl der Kasse („Kasse WIEN-1“ usw.). Kommt man aus dem Kalender, ist die Kasse des Standorts des Termins gewählt.
- Der Knopf mit den Pfeilen (**Aktualisieren**) lädt die offenen Termine und Rechnungen neu.
- **Exportieren** speichert Listen der gewählten Kasse als Excel oder CSV: Belegliste, Umsatz je Tag, je Zahlart und je Behandlung und Person sowie die Tagesabschlüsse, jeweils für den Belegtag im Bereich **Belege**, dazu den Gutscheinbestand. Den Knopf sehen nur Inhaberin und Geschäftsführung; jeder Export steht im Verlauf.
- Rote Hinweise nennen Fehler, gelbe Hinweise Warnungen der Kasse, z. B. „Signatureinrichtung ausgefallen …“ oder „Kasse außer Betrieb (Schlussbeleg erstellt).“

## Kassieren {#kassieren}

Vier Bereiche nebeneinander:

- **„Heute abzurechnen“**, die Hauptliste ganz links: oben das Suchfeld „Gast, Code oder Artikel“, darunter die Gruppen „Wartet auf Abrechnung“ (fertige Termine, die noch auf keiner Rechnung stehen, mit „fertig seit …“) und „Später heute“ (die übrigen Termine und Eintritte des Tages). Jede Zeile nennt Uhrzeit, Gast, Behandlung, Person, Raum und Preis, ohne Kürzung. „Erledigt“ mit der Zahl der heute bezahlten Termine und Eintritte ist zugeklappt; ein Klick klappt die Gruppe auf, dort lässt sich nur nachsehen. Am Ende stehen die „Geparkten Rechnungen“. Die Liste rollt für sich und bleibt auch mit offener Leiste „KI und Hilfe“ mindestens 380 px breit.
- **Die offene Rechnung** daneben: Gast (ohne Gast „Laufkundschaft“), Positionen mit Steuersatz und Rabatt, je Position die Knöpfe für Rabatt (Prozentzeichen) und **Position entfernen** (×), oben **Parken** und **Verwerfen**, unten **Summe** und „davon offen“. Passt eine laufende Aktion zu Positionen der Rechnung, steht darüber der Vorschlag „Aktion „…“ …“ mit **Anwenden** ([Aktion anwenden](hilfe:kassa-aktion-anwenden)). Ohne Rechnung steht hier nur der Hinweis „Keine offene Rechnung. In „Heute abzurechnen“ einen Gast wählen oder einen Code scannen.“, nach dem Bezahlen der Beleg mit „Bezahlt“.
- **„Artikel und Gutscheine“** neben der Rechnung: „Artikel“ als Kacheln und „Gutschein verkaufen“ mit Wertgutschein, Behandlungsgutschein und Paket. Zu Beginn ist das Panel zugeklappt, eine schmale Leiste mit seinem Namen; ein Klick auf die Leiste klappt es auf, der Pfeil im Kopf wieder zu. Die Kassa merkt sich den Zustand an diesem Arbeitsplatz, auch nach einem Neustart.
- **„Bezahlen“** ganz rechts: je Zahlungsart der Kasse ein Betragsfeld (ein Klick in ein leeres Feld trägt den offenen Betrag ein) mit **Rest**, bei angebundenem Kartenterminal **Am Terminal zahlen**, beim Gutschein das Feld „Gutschein-Code“, das beim Verlassen Restwert und Gültigkeit zeigt. Darunter „Noch offen“ oder „Alles gedeckt“, das Rückgeld und **Bezahlen und Beleg erstellen**. Reicht die Breite nicht für alle vier Bereiche nebeneinander (etwa ein Bildschirm mit 1280 px und offener Leiste „KI und Hilfe“ oder aufgeklapptem Panel), steht „Bezahlen“ unter der Rechnung, und die Seite rollt als Ganzes.

![Kassa mit der Liste Heute abzurechnen](images/app/kassa-heute-abzurechnen.png)

### Suchfeld „Gast, Code oder Artikel“ {#suche}

- Beim Öffnen der Kassa und bei der Rückkehr auf den Reiter „Kassieren“ (von „Belege“ oder „Kassenlade & Abschluss“) steht der Cursor im Suchfeld.
- Tippen filtert die Liste nach Gast, Behandlung, Person oder Raum; ab zwei Buchstaben stehen passende Artikel darunter unter „Artikel“.
- **Pfeil nach unten** wählt die erste Zeile der Liste, die Pfeiltasten wählen weiter, **Enter** setzt die gewählte Zeile auf die Rechnung. **Esc** oder **Pfeil nach oben** auf der ersten Zeile führt zurück ins Suchfeld. Passt keine Zeile, führt **Pfeil nach unten** zu den Artikeln; dort wählen die Pfeiltasten weiter und **Enter** setzt den Artikel auf die Rechnung.
- **Enter** im Suchfeld sucht den Text als Code; eine passende Zeile oder ein passender Artikel kommt nur gewählt auf die Rechnung (Klick, Pfeil nach unten oder Tab, dann Enter). Ein Handscanner tippt den Code wie eine Tastatur und schließt mit Enter ab; der Artikel kommt mit Menge 1 auf die Rechnung.

## Belege {#belege}

Links „Belege heute“ dieser Kasse, neueste zuerst (mit den Pfeilen daneben zeigt die Liste den Tag davor oder danach), mit Uhrzeit, Nummer, Zahlungsarten und Betrag. Stornobelege tragen „Storno“, stornierte Belege „storniert“. Rechts der gewählte Beleg mit **Drucken**, „Rechnung mit Empfänger“ und „Beleg stornieren“. Bei einem online bezahlten Beleg steht darüber eine Karte mit dem Weg des Geldes und dem Stand der Erstattung, siehe [Beleg stornieren](hilfe:kassa-beleg-stornieren#online).

## Kassenlade und Abschluss {#abschluss}

Links der „Zwischenbericht seit dem letzten Abschluss“: Umsatz, Zahlungsarten, Steuern, Erlöskonten, Bargeld in der Lade und Soll-Bestand. Rechts „Tagesabschluss“ mit dem gezählten Bestand und „Einlage oder Entnahme“.

## Was hier nicht geht {#grenzen}

- Ohne Verbindung zum Server kann nicht kassiert werden; die App sagt das oben im gelben Hinweis.
- Kassen, Artikel, Rabattgrenze und Barzahlungsrabatt richtet die Leitung unter „Verwaltung“ ein, siehe [Kassa-Einstellungen](hilfe:kassa).
- Eine Rechnung auf einzelne Positionen aufteilen (Teilbeträge) gibt es nicht; dafür die Positionen auf getrennte Rechnungen setzen. Das Datenerfassungsprotokoll (DEP) gibt es unter **Verwaltung** › **Zahlen** › **Berichte** › **Pflichtberichte**.
- Die Belegliste zeigt nur den heutigen Tag.

## Rechte und Eingaben {#rechte}

- **Entnahme** sehen nur Standortleitung und Inhaber; für die Rezeption heißt der Kasten „Einlage“ und zeigt keine Reiter, es gibt nur die Einlage.
- Stornieren dürfen Standortleitung und Inhaber jeden Beleg; die Rezeption nur eigene Belege bis 15 Minuten nach dem Kassieren.
- Beträge mit Komma oder Punkt eingeben, z. B. „12,50“ oder „12.50“. Ein Betrag, der sich nicht lesen lässt oder mehr als zwei Nachkommastellen hat, bekommt beim Verlassen des Feldes einen Hinweis. Ein lesbarer Betrag gilt schon beim Tippen: Ein Knopf wie **Wertgutschein verkaufen** lässt sich gleich drücken, ohne das Feld zu verlassen. Solange ein Betrag nicht lesbar ist, sind **Bezahlen und Beleg erstellen** und die anderen Knöpfe zum Bezahlen, Buchen und Abschließen gesperrt.
- Ist noch keine Kasse eingerichtet, führt die Einrichtung über **Verwaltung** › **Kassa & Geld** › **Kassa**.

## Abläufe {#ablaeufe}

- [Termin kassieren](hilfe:kassa-termin-kassieren)
- [Artikel verkaufen](hilfe:kassa-artikel-verkaufen)
- [Eintritt abrechnen](hilfe:kassa-eintritt-verkaufen)
- [Gutschein verkaufen](hilfe:kassa-gutschein-verkaufen)
- [Gutschein einlösen](hilfe:kassa-gutschein-einloesen)
- [Rabatt geben](hilfe:kassa-rabatt-geben)
- [Bezahlen: bar, Karte, Terminal, Überweisung, geteilt](hilfe:kassa-bezahlen)
- [Rechnung parken](hilfe:kassa-rechnung-parken)
- [Rechnung mit Empfänger (Firmenrechnung)](hilfe:kassa-rechnung-mit-empfaenger)
- [Belege ansehen und drucken](hilfe:kassa-beleg-drucken)
- [Beleg stornieren](hilfe:kassa-beleg-stornieren)
- [Einlage oder Entnahme buchen](hilfe:kassa-einlage-entnahme)
- [Tagesabschluss](hilfe:kassa-tagesabschluss)
