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
| Angebot, Behandlung | Grunddaten · Durchführung (Ablauf, Material, Bereitstellen) · Preise und Verkauf · Qualifikationen und Räume. *Geändert im Nachtrag vom 17.11.2026: Übersicht · Grunddaten und Preis · Durchführung · Qualifikationen und Räume.* |
| Zahlen | Fünf Reiter wie bisher. Management und Pflichtberichte sind Abschnitte in „Berichte“. |
| Betrieb | Fünf Masken: Schließzeiten und Raumsperren stehen auf einer Seite. |
| Kassa & Geld, Kontakt | Aufbau bleibt. Proportionen und Verlauf nach den Regeln 3 und 5. |

Der Balken mit den sieben Gruppen bleibt (design.md Abschnitt 6). *Geändert im Nachtrag vom 17.11.2026: Der Balken hat acht Gruppen.*

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

## Nachtrag 17.11.2026: acht Gruppen, Behandlungskarte mit Übersicht

*Status des Nachtrags: umgesetzt (P3); Bestätigung durch den Auftraggeber offen.* Grundlage ist der Entwurf v5 vom 17.11.2026, den der Auftraggeber durchgesehen hat.

**Acht Gruppen statt sieben.** Der Balken der Verwaltung hat in dieser Reihenfolge: Betrieb · Behandlungen · Aktionen · Team · Lager · Kassa & Geld · Kontakt · Zahlen. Die Gruppe „Angebot“ mit den Masken „Behandlungen“ und „Aktionen“ entfällt: Beide haben jetzt eine eigene Gruppe mit je einer Maske und ohne Reiterzeile. Die untere Schiene „Behandlungen · Aktionen“ entfällt damit. Das ersetzt „sieben Gruppen“ in der Entscheidung oben, in design.md Abschnitt 6 und in der Festlegung vom 31.10.2026 (Ä293). Die Adressen bleiben (Regel 7): `masterData/treatments` öffnet „Behandlungen“, `promotions` öffnet „Aktionen“. Die Maske „Aktionen“ selbst bleibt zunächst unverändert; sie zieht in einem eigenen Schritt um.

**Behandlungskarte mit Übersicht.** Die Tabelle „Aufteilung nach dieser Regel“ gilt für die Behandlung so:

| Bereich | Inhalt |
| --- | --- |
| Übersicht | Kennzahl-Kacheln (`StatTile`): Preis, Dauer, Dürfen behandeln, Räume; jede springt in ihren Bereich. Nur für eine gespeicherte Behandlung. |
| Grunddaten und Preis | Name und Kategorie (je halbe Breite), Dauer und Preis (je Viertel), Hinweis für Gäste, „Anamnese prüfen“, „Passt dazu“; darunter eingeklappt „Weitere Angaben“: Preis je Standort, Preise nach Wochentag und Uhrzeit, Ausfallgebühr, Anzahlung. |
| Durchführung | Vor- und Nachbereitung, Vorbereitungsschritte, Material je Termin, „Zum Termin bereitstellen“. |
| Qualifikationen und Räume | Nötige Qualifikationen mit „Wer kann das?“ daneben, Räume und Geräte je Standort. |

„Preise und Verkauf“ ist kein eigener Bereich mehr. Seine Gruppen sind in „Grunddaten und Preis“ aufgegangen; die alten Sprungziele (`preise-und-verkauf`, `preise`, `verkauf`) bleiben gültig und führen dorthin (Regel 7), „preise“ klappt dabei „Weitere Angaben“ auf.

**Weitere Änderungen an der Maske.** „Neue Behandlung“ steht als primäre Schaltfläche fest über der Liste, die Suche darunter; Kategorien lassen sich einklappen und zeigen ihre Anzahl; je Zeile stehen Dauer und Preis rechts. „Nicht mehr angebotene zeigen“ steht als Schalter unter der Liste; solche Behandlungen stehen ausgegraut mit „Nicht angeboten“, ihre Karte lässt sich ansehen, und „Wieder anbieten“ holt sie zurück (API: `includeInactive` an der Liste, `POST /treatments/{id}/reactivate`). „Speichern“ und „Verwerfen“ stehen an der Karte statt in einer Leiste am Fuß der Seite. „Nicht mehr anbieten“ steht im Menü „…“ im Kopf der Karte neben „Verlauf“, mit Rückfrage.

**Begründung.** „Behandlungen“ und „Aktionen“ sind zwei Dinge mit eigenem Alltag (Katalog und Werbung); als Reiter einer Gruppe „Angebot“ lag die Aktion einen Klick tiefer als nötig. Das Anlegen lag als letzte Listenzeile unter dem sichtbaren Bereich, die erste Zeile war angeschnitten: Die Übersicht der Behandlungskarte und der feste Knopf beheben beides.

## Nachtrag 17.11.2026: Schließzeiten und Raumsperren in einer Liste

*Status des Nachtrags: umgesetzt (P2); Bestätigung durch den Auftraggeber offen.* Grundlage ist der Entwurf v5 vom 17.11.2026 (Abschnitt 2), den der Auftraggeber durchgesehen hat.

Die Zeile „Betrieb: Schließzeiten und Raumsperren stehen auf einer Seite“ galt zuerst als zwei gestapelte Abschnitte mit eigener Liste und eigenem Formular. Sie gilt jetzt als **eine** Liste:

- Schließzeiten des ganzen Standorts und Raumsperren stehen in einer Liste nach Beginn, kommende zuerst, Vergangenes eingeklappt („Vergangene (n)“, Regel 6). Die Art ist ein Kennzeichen an der Zeile („Standort“, „Raum“); ein Filter über der Liste zeigt nur eine Art.
- „Neue Schließzeit“ steht fest über der Liste. Das Formular öffnet in einer Seitenleiste (Regel 3 gilt sinngemäß für jedes Formular einer Liste), bei einem neuen Eintrag mit der Wahl „Ganzer Standort“ oder „Einzelner Raum“. Die Formulare beider Arten bleiben getrennt, weil sich die Felder unterscheiden.
- Die Wartezeit vor der Nachricht an den Gast ist eine selten geänderte Einstellung und liegt hinter „Einrichtung“ (Regel 4), nicht mehr als Karte über der Liste.
- Ohne Eintrag steht statt der Liste der Leerzustand mit den ersten Aktionen. Rückmeldungen nach Speichern und Löschen sind ein Toast (meldungen.md M1) statt „Gelöscht.“ als Balken.
- Die Liste kommt aus **einem** Aufruf der API: `GET /api/locations/{id}/closure-overview?scope=Upcoming|Past|All` liefert beide Arten mit Zustand und Zahl offener Termine, sortiert wie die Liste, dazu die Zahlen der kommenden und vergangenen Einträge. Es sind neue Typen, die bestehenden Wege bleiben unverändert (ADR 0085 §3); Anlegen, Ändern und Löschen laufen weiter über die Wege je Art.
- Alte Adressen bleiben (Regel 7): `masterData/closures` öffnet die Seite, `masterData/roomClosures` öffnet sie mit einem neuen Formular „Einzelner Raum“ (Raum und Zeitraum des Sprungs vorbelegt).

## Nachtrag 17.11.2026: Personenkarte und Teamliste (P5)

*Status des Nachtrags: umgesetzt (P5); Bestätigung durch den Auftraggeber offen.* Grundlage ist der Entwurf v5 vom 17.11.2026, Abschnitt „Personenkarte“.

**Regelarbeitszeit unter „Arbeitszeit“.** Die Tabelle „Aufteilung nach dieser Regel“ gilt für die Personenkarte so: Der Bereich **Arbeitszeit** hat jetzt drei Abschnitte: Dienstplan und Abwesenheit (mit Urlaubskonto und Kalenderdatei) · **Regelarbeitszeit** · Zeiten. Die Regelarbeitszeit (früher „Wochenarbeitszeiten“ im Profil) ist die Grundlage des Dienstplans und steht deshalb dort. Sie wird weiter mit „Speichern“ zusammen mit den Stammdaten gespeichert (`PUT api/employees/{id}`), nichts am Server ändert sich. Der neue Abschnitt heißt für Sprungziele `regelarbeitszeit`; `team#wochenarbeitszeiten` und die Abschnitte der bisherigen Adressen bleiben gültig (Regel 7). Eine neue und eine ausgeschiedene Person haben den Bereich ebenfalls (die ausgeschiedene nur lesbar).

**Kopf und Schiene stehen fest.** Der Kopf der Karte (Initialen, Name, „Rolle · Standort“, Kennzeichen, „Verlauf“, Menü „⋯“, Fragezeichen) und die Schiene der Bereiche liegen außerhalb des Rollbereichs; nur die Bereiche darunter rollen. Das Menü „⋯“ hat „Zugang sperren“ bzw. „Zugang freigeben“ (nur bei einer Person mit Zugang) und „Aus dem Team nehmen …“. „Aus dem Team nehmen“ steht damit nicht mehr als Knopf unten neben „Speichern“ (ADR 0099, Regel 2). „Zugang sperren“ schaltet den Schalter „Zugang aktiv“; gesperrt ist der Zugang erst mit „Speichern“, wie jede Änderung der Karte.

**Jeder Abschnitt ist eine Karte.** Im Bereich Profil stehen „Stammdaten“ und „Zugang und Rolle“ gleich breit nebeneinander (gleichrangig, masken.md R14; unter 860 Breite untereinander), darunter „Qualifikationen“ und „Zuständigkeiten“ in voller Breite. Qualifikationen, Standorte und Sprachen sind Auswahl-Chips (gewählt: weiche Akzentfläche mit Haken), die Zuständigkeiten bleiben die vollständige Liste mit Formular statt einer Zusammenfassung mit „Ändern“, weil ein zweiter Weg dorthin ein zweites Formular brauchte. „Weitere Angaben“ (Zuordnung zu einer Person im Team, Befristung) ist eingeklappt und offen, wenn der Zugang befristet ist (R2). Der Entwurf zeigt Vorname, Nachname und Telefon; die Karte hat sie nicht (die Person hat einen vollständigen Namen und einen Namen im Haus, die E-Mail steht beim Zugang), und es entstehen keine neuen Felder.

**Schalter in einer Maske mit „Speichern“.** design.md 5.23 verlangt Schalter nur für sofort wirkende Einstellungen. Der Entwurf des Auftraggebers zeigt auf der Personenkarte Schalter für „Zugang aktiv“, „Berichte mit Beträgen“, „Auswertungen ohne Beträge im Assistenten“ und „Alle Standorte“ statt Häkchen mit doppelter Beschriftung. Das gilt hier als benannte Ausnahme: Die Schalter wirken erst mit „Speichern“, die Beschriftung steht einmal am Schalter, der Hinweis darunter. Andere Masken mit „Speichern“ behalten Kontrollkästchen.

**Teamliste.** „Person hinzufügen“ (primär) und „Person einladen“ bzw. „Zugang anlegen“ stehen fest über der Liste, nicht mehr als letzte Zeilen darin (ADR 0099, Regel 1); darunter die Suche und bei mehr als einem Standort „Alle“ und je Standort als Auswahl. Die Zeile nennt Name und Rolle; ein Kennzeichen am Zeilenende steht nur bei einer Abweichung: Gesperrt, Abgelaufen, „bis 19.12.2026“ (Befristung), Eingeladen, Ausgeschieden. Personen ohne Zugang zeigt der Filter „Nur ohne Zugang“ unter der Liste statt eines Kennzeichens in jeder Zeile (Auftraggeber, 17.11.2026); der Kopf der Karte nennt „Ohne Zugang“. Ein Standort zeigt die Personen, die dort Arbeitszeit haben, oder deren Zugang dort gilt. Eine leere Liste nennt „Person hinzufügen“ als erste Aktion. Alles rechnet die Liste aus Daten, die die Karte ohnehin lädt; es gibt keinen neuen Endpunkt.

**Begründung.** Die Regelarbeitszeit gehört zur Planung, nicht zu den Stammdaten; im Profil lag sie unter Sprachen und wurde übersehen. Ein fester Kopf hält Name und Menü im Bild, während man durch die Bereiche rollt. Das Kennzeichen nur bei Abweichungen entlastet die Liste: Wer normal angemeldet ist, braucht keinen Zusatz.

## Quellen

- Rückmeldung des Auftraggebers vom 16.11.2026 mit Bild der Personenkarte, Auftrag vom selben Tag
- [design.md](../design.md) 5.4, 5.5, 5.12, Abschnitt 6; [masken.md](../gestaltung/masken.md) R1, R2, R5a, R14, R15
- [ADR 0067](0067-masken-und-meldungen-nach-material-3-und-fluent-2.md), [ADR 0069](0069-fluent-2-als-grundlage-der-windows-app.md), [ADR 0084](0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md), [ADR 0094](0094-proportionen-von-masken-drittel-und-goldener-schnitt.md)
