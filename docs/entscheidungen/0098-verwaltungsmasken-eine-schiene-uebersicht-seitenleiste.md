# 0098. Verwaltungsmasken: eine Schiene, Übersicht zuerst, Verlauf als Seitenleiste

- **Status:** angenommen
- **Datum:** 16.11.2026
- **Entschieden von:** Auftraggeber (Richtung am Beispiel der Personenkarte, Auftrag für alle Verwaltungsmasken, Werte des Briefs bleiben), Regeln und Aufteilung Claude nach ADR 0019

## Kontext

Der Auftraggeber fand die Personenkarte „nicht mehr zeitgemäß mit den vielen Reitern“. Die Zählung im Code (Stand `dev` vom 16.11.2026, Rolle Inhaberin) zeigt, wo sich Reiter stapeln:

| Bereich | Reiter unter dem Balken | Größte Schiene |
| --- | --- | --- |
| Team | 7 Ansichten, darunter 10 Reiter der Personenkarte | 10 |
| Lager | 9 Reiter, dazu ein Buchungspanel mit 4 | 9 |
| Angebot | 2 Masken, die Behandlung mit 7 Abschnittsreitern | 7 |
| Zahlen | 5 Reiter, „Berichte“ mit 2 Unterreitern | 5 |
| Betrieb | 6 Masken | 6 |
| Kassa & Geld | 4 Masken | 4 |
| Kontakt | 3 Masken | 3 |

Dazu kommen drei Beobachtungen:

- Die Schiene der Personenkarte bricht als einzige um, gegen design.md 5.4.
- „Zeiten“, „Lohn“ und „Zuständigkeiten“ gibt es zweimal: als Ansicht des Teams und als Reiter der Karte.
- Die Kundenkarte kommt seit Slice 141 ohne Reiter aus (Abschnitte auf einer Fläche, design.md 5.5).

Der Auftraggeber hat den Vorschlag für die Personenkarte gutgeheißen (Übersicht, vier Bereiche, Verlauf als Seitenleiste) und am 16.11.2026 beauftragt, alle Verwaltungsmasken so zu modernisieren. Frühere Festlegungen zu Reitern gelten dafür nicht mehr. Farben, Schrift, Abstände und Radien aus [design.md](../design.md) bleiben.

## Optionen

| Option | Bewertung |
| --- | --- |
| Reiter behalten, Schiene scrollt oder bekommt ein Menü „Mehr“ | Behebt nur das Umbrechen. Zehn Ziele bleiben zehn Ziele, die Hälfte davon verborgen. Verworfen. |
| Alles auf einer langen Seite ohne Reiter | Passt für die Kundenkarte (Anzeige). Die Personenkarte hat zehn Formulare mit eigenem Speichern; eine Seite wäre mehrere Bildschirme lang. Verworfen. |
| **Wenige Bereiche mit Abschnitten, Übersicht als Einstieg, Seltenes in Seitenleisten** | Höchstens fünf Ziele je Schiene, die häufigen Fragen ohne Klick beantwortet, nichts entfällt. Gewählt. |

## Entscheidung

Für jede Maske der Verwaltung in der Windows-App gilt:

1. **Eine Schiene, höchstens fünf Einträge.** Unter dem Balken der Verwaltung steht je Seite höchstens eine Reiterschiene. Sie hat höchstens fünf Einträge und bricht nie um. Was darüber hinausgeht, wird zusammengelegt, ein Abschnitt oder eine Seitenleiste.
2. **Karten haben höchstens vier Bereiche, der erste ist eine Übersicht.** Eine Karte neben einer Liste (Person, Behandlung) beginnt mit einer Übersicht aus Kennzahl-Kacheln (design.md 5.5) und Sprüngen in die Bereiche. In einem Bereich stehen die Gruppen untereinander (masken.md R1), Seltenes eingeklappt (R2).
3. **Verlauf ist eine Seitenleiste.** Eine Schaltfläche „Verlauf“ im Kopf der Karte öffnet die Seitenleiste (design.md 5.12). Einen Reiter „Verlauf“ gibt es nicht.
4. **Einrichtung steht getrennt vom Alltag.** Selten geänderte Stammdaten eines Bereichs (Lagerorte, Lieferanten) liegen hinter „Einrichtung“, nicht in der Schiene.
5. **Die Breite wird genutzt.** Haupt- und Nebeninhalt stehen 3 : 2 (masken.md R14). Tabellen und Wochenraster nutzen die volle Breite; die Höchstbreite gilt nur für Formulargruppen.
6. **Filter statt Zustandsreiter.** „Aktuell / Beendet“ ist ein Kontrollkästchen „Beendete zeigen“, wie „Ausgeschiedene zeigen“ im Team.
7. **Sprungziele bleiben gültig.** Jede bisherige Adresse (Seite, Reiter, Abschnitt) bleibt als Alias erhalten und führt an den neuen Ort.
8. **Nichts entfällt.** Jede Funktion bleibt erreichbar; sie wechselt nur den Ort. Rechte ändern sich nicht.

Aufteilung nach dieser Regel:

| Bereich | Neu |
| --- | --- |
| Team, Ansichten | Personen · Wochenplan · Zeiten · Lohn · Rechte und Zuständigkeiten. „Anträge“ ist eine Seitenleiste im Wochenplan. |
| Team, Personenkarte | Übersicht · Profil (Stammdaten, Zugang und Rolle, Qualifikationen, Zuständigkeiten) · Arbeitszeit (Dienstplan und Abwesenheit, Urlaubskonto, Zeiten) · Lohn und Akte (Lohn, Personalakte, Regelungen und Vollmachten). |
| Lager | Übersicht (mit Verbrauch) · Bestand (mit Chargen und Bewegungen) · Bestellen · Inventur. Lagerorte und Lieferanten unter „Einrichtung“. |
| Angebot, Behandlung | Grunddaten · Durchführung (Ablauf, Material, Bereitstellen) · Preise und Verkauf · Qualifikationen und Räume. |
| Zahlen | Fünf Reiter wie bisher. Management und Pflichtberichte sind Abschnitte in „Berichte“. |
| Betrieb | Fünf Masken: Schließzeiten und Raumsperren stehen auf einer Seite. |
| Kassa & Geld, Kontakt | Aufbau bleibt. Proportionen und Verlauf nach den Regeln 3 und 5. |

Der Balken mit den sieben Gruppen bleibt (design.md Abschnitt 6).

Neue gemeinsame Bausteine der Windows-App: `StatTile` (Kennzahl-Kachel nach design.md 5.5) und `HistoryPanel` (Seitenleiste „Verlauf“ nach design.md 5.12 mit den vorhandenen Verlaufsabschnitten).

## Begründung

- Eine Schiene mit fünf Einträgen lässt sich mit einem Blick erfassen. Zehn Einträge in zwei Zeilen muss man lesen.
- Die Übersicht beantwortet, was am häufigsten gefragt wird (nächster Dienst, Resturlaub, offene Anträge), ohne dass jemand einen Reiter sucht.
- Der Verlauf ist kein eigener Inhalt, sondern die Geschichte aller Bereiche. Als Seitenleiste steht er neben dem, worauf er sich bezieht.
- Die Kundenkarte zeigt seit Slice 141, dass Abschnitte auf einer Fläche ruhiger wirken als gerahmte Karten und Reiter.
- Die Werte bleiben, damit Kalender, Kassa, Web und Verwaltung weiter gleich aussehen.

## Abgelöst

- design.md 5.4, „Alltag vor Verwaltung“ mit Trenner und neun Reitern im Lager (R07 Slice 143, Ä353): ersetzt durch die Regeln 1 und 4.
- design.md Abschnitt 6: „Team und Lager … behalten darunter ihre eigenen Reiter“ (Ä293) und „Management und Pflichtberichte als Unterreiter“ (Ä352): ersetzt durch Regel 1.
- Rundenakte R07, Ä351 („Unterreiter statt langem Scrollen“ im Lager): Die Unterteilung bleibt, aber als vier Reiter mit Sichten und Seitenleisten.
- [ADR 0084](0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md): Die Liste dessen, was zur Personenkarte gehört, gilt weiter. Sie legt keine Reiter mehr fest.
- masken.md R5a nennt „Unterreiter“ als ein Mittel für die Tiefe nach Rolle. Das bleibt erlaubt, solange Regel 1 eingehalten ist.

## Folgen und Stand

- Die Umstellung läuft in sieben Schritten, je eigener Pull Request: Grundlagen, Lager, Behandlung, Zahlen, Betrieb mit Kassa & Geld und Kontakt, Personenkarte, Ansichten des Teams. Bis ein Bereich umgestellt ist, beschreibt design.md dort den alten Stand.
- Schritt 1 (dieser ADR, Regel R15 in masken.md, die beiden Bausteine) ändert keine bestehende Maske.
- Die Übersicht der Personenkarte braucht einen neuen Endpunkt, der die Kennzahlen am Server rechnet. Er entsteht mit der Personenkarte.
- Das Handbuch nennt Reiter in rund 30 Themen zum Team. Jeder Schritt zieht seine Themen und Bilder im selben Pull Request nach ([ADR 0056](0056-handbuch-mit-ablaeufen-und-bildern-als-grundlage-der-ki-hilfe.md)).
- Nicht geprüft ist, ob Liste, Karte und Seitenleiste „Verlauf“ beim Mindestfenster von 1100 Breite neben der Seitenleiste „KI und Hilfe“ Platz haben. Das misst der Schritt Personenkarte; passt es nicht, liegt der Verlauf über der Karte.

## Quellen

- Rückmeldung des Auftraggebers vom 16.11.2026 mit Bild der Personenkarte, Auftrag vom selben Tag
- [design.md](../design.md) 5.4, 5.5, 5.12, Abschnitt 6; [masken.md](../gestaltung/masken.md) R1, R2, R5a, R14, R15
- [ADR 0067](0067-masken-und-meldungen-nach-material-3-und-fluent-2.md), [ADR 0069](0069-fluent-2-als-grundlage-der-windows-app.md), [ADR 0084](0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md), [ADR 0094](0094-proportionen-von-masken-drittel-und-goldener-schnitt.md)
