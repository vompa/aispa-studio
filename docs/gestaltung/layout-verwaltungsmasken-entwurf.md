# Layout der Verwaltungsmasken: Entwurf

> **Entwurf vom 26.11.2026. Nicht freigegeben, nicht umsetzen.**
> Der Auftraggeber sieht sich jede Maske einzeln an und entscheidet je Maske, welche Regel gilt.
> Verbindlich bleiben [design.md](../design.md) und [masken.md](masken.md). Dieses Dokument ändert keine Regel.

## 1. Woher der Entwurf kommt

Der Entwurf verallgemeinert das Mockup zum Management-Bericht mit KI-Auswertung:
[mockups/management-bericht-ki-auswertung.html](mockups/management-bericht-ki-auswertung.html).
Das Mockup zeigt eine Maske. Ob eine Regel auf eine andere Maske passt, ist für keine andere Maske geprüft.

Freigegeben zur Umsetzung ist nur der Management-Bericht selbst, als eigener Auftrag.

## 2. Regeln

Jede Regel nennt die Vorgabe, den Grund und den Bezug zum Brief.

### L1. Der Kopf steht in einer Zeile

- Der Titel steht am Anfang der Zeile, die Aktionen am Ende.
- Alle Ausgaben (Drucken, PDF, Mail, Export) stehen in einem Menü **Ausgabe**.
- Höchstens ein Knopf im Kopf ist primär.

Grund: Im Management-Bericht stehen heute acht Bedienelemente in einem umbrechenden Band. Was Filter, was Aktion und was Einstellung ist, lässt sich nicht erkennen.

Bezug: design.md Abschnitt 6, Punkt 8 (Kopfzeile des Dienstplans mit Menü „Ausgabe“, T-101). L1 überträgt diese Kopfzeile auf andere Masken.

### L2. Einstellungen sind keine Aktionen

- Ein Schalter, der dauerhaft gilt (zum Beispiel „Wochen- und Monatsbericht per Mail“), steht nicht zwischen den Filtern.
- Gehört er zu einer Ausgabe, steht er im Menü **Ausgabe** unter einem Trenner.
- Sonst steht er hinter „Einrichtung“.

Grund: Ein Kästchen neben dem Zeitraum liest sich wie ein Filter für die angezeigten Zahlen.

Bezug: masken.md R15 („Einrichtung“).

### L3. Die Filter stehen in einer Reihe

- Jedes Feld trägt seine Beschriftung über dem Feld.
- Eine Schnellwahl zeigt immer einen Wert. Weicht der Zeitraum von jeder Vorgabe ab, steht dort „Eigener Zeitraum“.
- Zusatzangaben (Vergleichszeitraum, Hinweis auf den laufenden Tag) stehen als eine Zeile in der Stufe „Klein“ neben oder unter den Feldern, nicht als eigene Absätze.

Grund: Ein leeres Feld mit dem Platzhalter „Schnellwahl“ sagt nicht, welcher Zeitraum gilt.

Bezug: design.md Abschnitt 6, Punkt 7 (Filterzeile der Berichte in einer Reihe).

### L4. Kennzahlen stehen im Raster, die Veränderung als Pille

- Kacheln stehen im Raster mit Mindestbreite 160 und Abstand 12 und füllen die Breite.
- Die Veränderung steht als Pille mit Pfeil und Zahl: ok-soft und ok für besser, warn-soft und warn für schlechter. Der Pfeil trägt die Richtung, die Farbe ist nie das einzige Zeichen.
- Der Wert der Vorperiode steht darunter in der Stufe „Klein“.
- Eine einzelne Kachel trägt keine Farbfläche und kein einzelnes Symbol, solange die anderen keines tragen.
- Eine Veränderung eines Anteils steht in Prozentpunkten, nicht in Prozent.

Grund: Im Bericht trägt heute eine von vielen Kacheln ein Warnzeichen. Warum gerade diese, ist nicht erkennbar.

Bezug: design.md 5.5. Das Raster steht dort schon; die Pille ist neu (siehe Abschnitt 3).

### L5. Gleichrangige kurze Abschnitte stehen nebeneinander

- Zwei Tabellen oder Listen derselben Art und ähnlicher Länge stehen gleich breit nebeneinander.
- Unter einer Mindestbreite stehen sie untereinander.

Grund: „Umsatz nach Erlöskonto“ und „Umsatz nach Zahlungsart“ beantworten dieselbe Frage aus zwei Sichten und brauchen heute zwei Bildschirmhöhen.

Bezug: masken.md R14 (gleich breit nur bei Gleichrangigem).

### L6. Eine Seitenleiste mit Ergebnis gibt dem Ergebnis den Platz

- Vor dem Ausführen zeigt die Seitenleiste das Formular.
- Nach dem Ausführen klappt das Formular zu einer Zeile mit dem gewählten Wert und dem Link „Ändern“ ein.
- Das Ergebnis füllt die Höhe und rollt allein.
- Die Aktion zum Wiederholen steht fest im Fuß.

Grund: Im KI-Panel belegen Schwerpunkt, Frage und Knopf nach der Auswertung die halbe Höhe; das Ergebnis muss man rollen.

Bezug: design.md 5.12 (Seitenleiste, ein primärer Knopf), masken.md R5 (Aktion fest im Fuß).

### L7. Eine KI-Ausgabe ist gegliedert und führt zur Zahl

- Die Ausgabe trägt die Pille „KI“ und die Hinweiszeile.
- Sie ist gegliedert: Kernaussagen, Auffälligkeiten, Maßnahmen.
- Eine Auffälligkeit ist eine Karte mit der Aussage, einer gekennzeichneten Vermutung (Pille „Vermutung“) und den Kennzahlen, auf die sie sich stützt.
- Die Kennzahlen stehen als Chips. Ein Klick auf einen Chip markiert die Kachel im Bericht.
- Die Ausgabe nennt den Vergleich so wie der Bericht („Vorperiode“, nicht „Vorjahr“).

Grund: Die KI rechnet nicht. Wer eine Aussage liest, muss die Zahl dazu sofort finden und eine Vermutung von einer Feststellung unterscheiden können.

Bezug: design.md 5.28 (KI-Kennzeichnung, Wortlaut in `Services/AiLabels.cs`).

### L8. Ein Leerzustand ist eine Zeile

- Ein leerer Abschnitt zeigt die Überschrift und einen Satz, keine Spaltenköpfe.

Bezug: design.md 5.7, Masken-Inventar P10c. Schon umgesetzt; L8 steht hier nur, damit das Mockup vollständig beschrieben ist.

## 3. Abgleich mit dem Brief

| Regel | Stand | Anmerkung |
| --- | --- | --- |
| L1 | Erweiterung | Der Brief regelt die Kopfzeile mit Menü „Ausgabe“ nur für den Dienstplan. |
| L2 | Erweiterung | R15 kennt „Einrichtung“; ein Schalter im Menü „Ausgabe“ ist neu. |
| L3 | zum Teil geregelt | Die Reihe steht im Brief. Neu sind „Eigener Zeitraum“ und die Zusatzangaben als eine Zeile. |
| L4 | zum Teil geregelt, eine offene Frage | Das Raster mit Mindestbreite 160 steht in 5.5; der Management-Bericht nutzt heute eine feste Breite von 200. Die Pille ist neu: 5.5 nennt den Vergleich in der Stufe „Klein“ in fg-2. |
| L5 | geregelt | R14 deckt die Regel. |
| L6 | Erweiterung | 5.12 und R5 regeln Aufbau und Fuß; das Einklappen des Formulars ist neu. |
| L7 | zum Teil geregelt, eine offene Frage | 5.28 regelt Pille und Hinweiszeile. Neu sind die Gliederung, die Pille „Vermutung“ und die Chips. |
| L8 | geregelt | Keine Änderung. |

## 4. Offene Fragen an den Auftraggeber

1. **Warnzeichen an einer Kennzahl (L4).** Der Brief sagt beim Lager: Eine Zahl über 0, die etwas zu tun bedeutet, trägt das Warnzeichen. L4 nimmt das Zeichen aus der Kachel. Vorschlag: Das Warnzeichen bleibt für Zahlen, die eine Aufgabe bedeuten (Lager, offene Anfragen); eine Veränderung zur Vorperiode trägt nur die Pille.
2. **Ort der KI-Hinweiszeile (L7).** 5.28 verlangt die Hinweiszeile unter jeder Ausgabe, mit „Warum?“. Das Mockup zeigt einen Hinweis im Fuß der Seitenleiste und kein „Warum?“. Vorschlag: Die Hinweiszeile bleibt wie in 5.28; der Fuß trägt nur **Neu auswerten**.
3. **Farbe der Pille „schlechter“ (L4).** Das Mockup nutzt warn, nicht danger. Ein Rückgang ist ein Hinweis, kein Fehler.
4. **Wann eine Regel verbindlich wird.** Sobald der Auftraggeber eine Regel freigibt, wandert sie in masken.md oder design.md und bekommt, wenn sie vom Brief abweicht, einen ADR.

## 5. Masken zum einzelnen Ansehen

Die Liste stammt aus dem [Masken-Inventar](../umsetzung/masken-inventar.md). Keine dieser Masken ist für diesen Entwurf angesehen worden. Die Spalte „Kommt in Frage“ nennt nur, welche Regeln nach der Art der Maske zu prüfen sind; sie sagt nichts über den heutigen Zustand.

| Gruppe | Maske | Kommt in Frage | Stand |
| --- | --- | --- | --- |
| Zahlen | Berichte › Management | L1 bis L8 | Mockup liegt vor, Umsetzung eigener Auftrag |
| Zahlen | Berichte › Pflichtberichte | L1, L3, L5, L8 | noch nicht angesehen |
| Zahlen | Tagesstart | L4, L8 | noch nicht angesehen |
| Zahlen | Mein Tag | L4, L8 | noch nicht angesehen |
| Zahlen | Hygiene | L1, L3, L8 | noch nicht angesehen |
| Zahlen | Betriebsinfo | L1, L3 | noch nicht angesehen |
| Lager | Übersicht | L4, L5, offene Frage 1 | noch nicht angesehen |
| Lager | Bestand (Material, Chargen, Bewegungen) | L1, L3 | noch nicht angesehen |
| Lager | Bestellen | L1, L6 | noch nicht angesehen |
| Lager | Inventur | L1, L3 | noch nicht angesehen |
| Lager | Einrichtung (Lagerorte, Lieferanten) | L2 | noch nicht angesehen |
| Team | Personen und Personenkarte | L1 | noch nicht angesehen |
| Team | Wochenplan | L1 (Vorbild der Regel), L6 | noch nicht angesehen |
| Team | Zeiten | L1, L3 | noch nicht angesehen |
| Team | Rechte und Zuständigkeiten | L5 | noch nicht angesehen |
| Betrieb | Firmendaten | keine erkennbar | noch nicht angesehen |
| Betrieb | Standorte & Räume | L1 | noch nicht angesehen |
| Betrieb | Schließzeiten | L1, L6 | noch nicht angesehen |
| Betrieb | Kalender-Abos | L1 | noch nicht angesehen |
| Betrieb | Handbuch | L1 | noch nicht angesehen |
| Behandlungen | Behandlungen | L1, L4 (Übersicht) | noch nicht angesehen |
| Aktionen | Aktionen | L1, L3 | noch nicht angesehen |
| Kassa & Geld | Kassa | L2 | noch nicht angesehen |
| Kassa & Geld | Zahlungen | L2 | noch nicht angesehen |
| Kassa & Geld | Kosten | L4, L5 | noch nicht angesehen |
| Kassa & Geld | Buchhaltung | L1, L3 | noch nicht angesehen |
| Kontakt | Erinnerungen | L2, L5 | noch nicht angesehen |
| Kontakt | Postfach & Nummern | L2 | noch nicht angesehen |
| Kontakt | KI | L2, L7 | noch nicht angesehen |

Kalender, Posteingang und Kassa (Verkauf) sind keine Verwaltungsmasken und stehen nicht in der Liste.

## 6. Vorgehen je Maske

1. Die Maske als Inhaberin öffnen und ein Bild machen.
2. Die Regeln aus der Spalte „Kommt in Frage“ gegen das Bild prüfen.
3. Dem Auftraggeber einen Vorschlag mit Mockup zeigen. Was wegfällt oder sich einschränkt, steht ausdrücklich dabei.
4. Erst nach der Freigabe umsetzen, als eigene Aufgabe in der Aufgabenliste.
