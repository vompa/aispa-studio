---
art: einstellung
bereich: Stammdaten
oberflaeche: verwaltung
rollen: inhaber, manager, leitung
seite: verwaltung
---
# Team

Einstellungen in der Windows-App unter „Verwaltung“ › „Team“: die Personen, die Behandlungen geben, mit Kalenderfarbe, Qualifikationen und Regelarbeitszeit.

In der Windows-App steht die Liste der Personen neben der Personenkarte. Über der Liste stehen **Person hinzufügen** und **Person einladen**. Der Kopf der Karte mit dem Namen und die Leiste der Bereiche bleiben beim Scrollen stehen. Unter dem Kopf stehen die Kacheln mit den Kennzahlen der Person, darunter bis zu drei Bereiche in einer Leiste: **Profil**, **Arbeitszeit** sowie **Lohn und Akte**. Welche zu sehen sind, hängt von den Rechten ab. Anmeldung und Rolle einer Person (Zugang zur Software) sowie ihre Qualifikationen sind Abschnitte im Bereich **Profil**; „Zugänge“ und „Qualifikationen“ gibt es in der App nicht mehr als eigene Einträge.

![Team in der Windows-App](images/app/verwaltung-team.png)

Ändern dürfen Inhaber und Standortleitung. Eine Standortleitung ändert nur Arbeitszeiten an ihren eigenen Standorten; Einträge an anderen Standorten bleiben beim Speichern unverändert.

Gespeichert wird mit „Speichern“. Änderungen gelten für neue Buchungen; gebuchte Termine bleiben unverändert.

In der Windows-App gilt ein **Speichern** unten für die ganze Karte, aus allen Bereichen:

- **Strg+S** speichert wie der Knopf. Unten erscheint kurz „Gespeichert“.
- **Verwerfen** holt den gespeicherten Stand zurück.
- Wer mit ungespeicherten Änderungen eine andere Person wählt oder die Seite verlässt, wird gefragt: „Änderungen speichern?“ mit „Speichern“, „Verwerfen“ und „Abbrechen“.
- Eine neue Person legt **Person hinzufügen** über der Liste an; wer sich anmelden soll, bekommt über **Person einladen** darunter einen Zugang (siehe [Person einladen](hilfe:zugaenge-person-einladen)). Eine Person braucht keinen Zugang, um eingeplant zu werden.

## Wochenplan, Zeiten und Anträge {#ansichten}

Über der Liste schaltet eine Leiste zwischen höchstens fünf Ansichten des Bereichs um. Sie zeigt nur, was die eigene Rolle darf:

- **Personen**: die Liste mit den Personenkarten, für alle, die Stammdaten oder Zugänge verwalten.
- **Wochenplan**: der Dienstplan aller Mitarbeitenden einer Woche, für alle, die den Kalender sehen oder Personalunterlagen lesen. Siehe [Seite „Dienstplan“](hilfe:seite-roster).
- **Zeiten**: Soll und Ist aller Mitarbeitenden einer Woche, für alle, die Standorte verwalten, und lesend für das Backoffice. Siehe [Seite „Zeiten“](hilfe:seite-time).
- **Anträge**: Schaltfläche im **Wochenplan** mit der Zahl der offenen Abwesenheitsanträge aus der Handy-App. Sie öffnet die Anträge als Seitenleiste rechts, für alle, die Standorte verwalten. Siehe [Abwesenheitsantrag genehmigen oder ablehnen](hilfe:dienstplan-antrag-entscheiden).
- **Lohn**: die Monatswerte aller Mitarbeitenden, der Export für die Lohnverrechnung und deren Einstellungen, für alle, die die Organisation verwalten, und lesend für das Backoffice. Die Personalnummer steht auf der Karte der Person im Bereich **Lohn und Akte**. Siehe [Lohnexport für den Monat erstellen](hilfe:lohn-export-erstellen).
- **Personalakte**: Abschnitt im Bereich **Lohn und Akte** auf der Karte einer Person mit Mitarbeitergesprächen, Anmerkungen und Verwarnungen, nur für Inhaber und Manager. Siehe [Personalakte](hilfe:personalakte).
- **Regelungen und Vollmachten**: Abschnitt im Bereich **Lohn und Akte** auf der Karte einer Person mit Hinweisen, Sonderregelungen und Vollmachten, nur für Inhaber und Manager. Die Ansicht **Rechte und Zuständigkeiten** zeigt in der Tabelle **Wer darf was, wer ist zuständig?** alle Vollmachten und Zuständigkeiten auf einen Blick; **+ Person** trägt jemanden ein. Siehe [Regelungen und Vollmachten](hilfe:regelungen-vollmachten).
- **Zuständigkeiten**: Abschnitt im Bereich **Profil** auf der Karte einer Person mit Zuständigkeiten wie Ersthelfer oder Lager und den Zertifikaten mit Ablauf und Scan, nur für Inhaber und Manager. Die Ansicht **Rechte und Zuständigkeiten** zeigt in der Tabelle, wer wofür zuständig ist, darunter was bald abläuft und die Liste der Zuständigkeiten. Siehe [Zuständigkeiten und Zertifikate](hilfe:zustaendigkeiten-zertifikate).

Die Rezeption und Therapeutinnen sehen nur den **Wochenplan**, ohne Personenliste und ohne Karten. Gibt es nur eine Ansicht, fehlt die Leiste.

Ein Klick auf einen Namen im **Wochenplan** öffnet die Karte der Person im Bereich **Arbeitszeit** beim Dienstplan, in den **Zeiten** im Bereich **Arbeitszeit** bei den Zeiten. Der Dienstplan und die Zeiten einer Person stehen dort nur für diese Person. Zum Planen eines Tages: [Dienst für einen Tag planen](hilfe:dienstplan-tag-planen); zum Nachtragen einer Zeit: [Arbeitszeit nachtragen](hilfe:zeiten-nachtragen).

Ein geöffneter Tag im Dienstplan oder in den Zeiten hat seine eigene Leiste **Speichern** / **Verwerfen**. Die Fußleiste der Karte erscheint im Bereich **Arbeitszeit** erst, wenn die **Regelarbeitszeit**, die Stammdaten, Qualifikationen oder der Zugang geändert sind; die Regelarbeitszeit wird mit den Stammdaten gespeichert. **Strg+S** und die Rückfrage „Änderungen speichern?“ gelten für beides.

## Suche {#suche}

Das Suchfeld „Name oder E-Mail“ über der Liste filtert die Personen beim Tippen. Bei mehr als einem Standort stehen darunter **Alle** und die Standorte als Auswahl; ein Standort zeigt die Personen, die dort arbeiten oder deren Zugang dort gilt. Jede Zeile nennt unter dem Namen die Rolle, z. B. „Rezeption“. Ein Kennzeichen am Zeilenende steht nur bei einer Abweichung: **Gesperrt**, **Abgelaufen**, **bis 19.12.2026** bei einer Befristung (**Endet bald, bis …** in den letzten sieben Tagen), **Eingeladen** oder **Ausgeschieden**. Wer aktiv und unbefristet angemeldet ist, hat kein Kennzeichen. Personen ohne Zugang tragen in der Liste kein Kennzeichen; **Nur ohne Zugang** unter der Liste zeigt nur sie, und der Kopf ihrer Karte nennt **Ohne Zugang**.

## Ausgeschiedene zeigen {#ausgeschiedene-zeigen}

Das Kästchen „Ausgeschiedene zeigen“ nimmt Personen in die Liste, die aus dem Team genommen wurden; sie tragen das Kennzeichen „Ausgeschieden“. Ihre Karte ist nur lesbar und sagt „Ausgeschieden: Die Angaben sind nur lesbar.“ Eine ausgeschiedene Person lässt sich nicht wieder aufnehmen. Ausgeschiedene sehen Inhaber, Manager und Standortleitung, die Rezeption nicht.

Ungespeicherte Änderungen fragen beim Wechsel der Zeile nach, auch ein angefangenes Formular „Person einladen“ oder „Zugang anlegen“. **Speichern** versendet dort nichts: dafür **Einladung senden** wählen oder die Eingaben verwerfen.

Scheitert nur der Zugang, bleiben Stammdaten und Qualifikationen gespeichert. Die Meldung nennt beides.

## Person {#person}

Oben auf der Karte einer Person stehen fünf Kacheln mit dem nächsten Dienst, den Stunden dieser Woche, dem Resturlaub, den offenen Anträgen und den Zertifikaten. Sie bleiben in jedem Bereich sichtbar. Ein Klick auf eine Kachel springt in den passenden Bereich; **Offene Anträge** öffnet den **Wochenplan** mit der Seitenleiste **Anträge**. Darunter hat die Karte bis zu drei Bereiche:

- **Profil**: **Stammdaten** und **Zugang und Rolle** nebeneinander (bei schmalem Fenster untereinander), darunter **Qualifikationen** und **Zuständigkeiten**. Jeder Abschnitt ist eine eigene Karte; Ein und Aus sind Schalter, Qualifikationen, Standorte und Sprachen Auswahl-Chips.
- **Arbeitszeit**: **Dienstplan und Abwesenheit** mit Urlaubskonto und Kalenderdatei, darunter die **Regelarbeitszeit** und die **Zeiten**.
- **Lohn und Akte**: **Lohn**, **Personalakte** sowie **Regelungen und Vollmachten**.

Der Kopf der Karte zeigt die Initialen, den Namen, darunter „Rolle · Standort“ und ein Kennzeichen wie **Zugang aktiv** oder **Gesperrt**. Rechts stehen die Schaltfläche **Verlauf**, das Menü **⋯** und das Fragezeichen. **Verlauf** öffnet eine Seitenleiste mit den Änderungen an der Person, siehe [Verlauf](hilfe:team#verlauf). Das Menü **⋯** hat **Zugang sperren** (bzw. **Zugang freigeben**, nur bei einer Person mit Zugang) und **Aus dem Team nehmen …**, siehe [Aus dem Team nehmen](hilfe:team#aus-dem-team-nehmen).

### Vollständiger Name {#name}

Der vollständige Name der Person, z. B. „Anna Berger“ oder „Siriporn Kaewdee“. Er steht in der Teamliste, in Lohn und Export, bei Zugängen und in Einladungen. Pflichtfeld; ohne Namen lässt sich nicht speichern.

### Name im Haus {#name-im-haus}

Wie das Team die Person im Alltag nennt, z. B. „Siri“ für „Siriporn Kaewdee“. Hilfreich bei langen oder schwer auszusprechenden Namen. Freiwillig, höchstens 40 Zeichen. Leer heißt: der vollständige Name gilt auch im Haus.

- Der Name im Haus ersetzt den vollständigen Namen im Kalender (Spaltenkopf, Kreis mit den Initialen, Hover-Karte), in „Mein Tag“, im Tagesstart, bei „Freie Zeit suchen“, bei der KI-Anfrage im Kalender und im Posteingang.
- Die KI-Anfrage und der Posteingang erkennen die Person unter beiden Namen: „bei Siri“ und „bei Siriporn“ meinen dieselbe Person.
- Lohn, Export, Zugänge, Einladungen und Belege verwenden immer den **vollständigen Namen**.
- Die Teamliste, der Wochenplan und die Zeiten in der Verwaltung zeigen weiter den vollständigen Namen.

### Farbe im Kalender {#farbe}

Farbe der Person im Kalender. Standard für eine neue Person ist ein dunkles Grün. Gespeichert wird sie als Farbcode in der Form `#rrggbb`. Am besten bekommt jede Person eine eigene, gut unterscheidbare Farbe.

## Qualifikationen {#qualifikationen}

Auswahl-Chips für alles, was die Person ausführen darf; ein gewählter Chip trägt einen Haken. In der Windows-App ist das der Abschnitt **Qualifikationen** im Bereich **Profil** der Karte. Dort legt **Qualifikationen verwalten** neue Qualifikationen an und benennt sie um, siehe [Qualifikationen](hilfe:qualifikationen).

- Eine Behandlung wird nur mit einer Person geplant, die alle nötigen Qualifikationen der Behandlung hat. Das gilt für Kalender, Online-Buchung und KI-Terminannahme.
- Die Online-Buchung bietet eine Behandlung an einem Standort nur an, wenn dort mindestens eine Person mit Wochenarbeitszeit an diesem Standort alle nötigen Qualifikationen hat.
- Die Vollkosten je Behandlung rechnen mit dem Durchschnitt der Stundensätze aller Personen, die die Behandlung ausführen dürfen.

## Sprachen {#sprachen}

Auswahl-Chips für jede Sprache außer Deutsch, in der die Person eine Behandlung führen kann. Deutsch setzt das Programm bei allen voraus.

- Im Kalender zeigt die Hover-Karte eines Gastes mit eigener Sprache, ob die Person am Termin sie spricht.
- Die Sprache des Gastes steht in der Kundenkarte, siehe [Sprachen und Quelle](hilfe:sprachen-und-quelle).

## Therapeutin oder Therapeut {#therapeutin}

Feld „Für Gäste mit Wunsch“: **Therapeutin**, **Therapeut** oder **keine Angabe** (Standard). Es dient nur dazu, den Wunsch eines Gastes zu erfüllen.

- Wünscht ein Gast eine Therapeutin oder einen Therapeuten (Kundenkarte › „Termine am liebsten“), schlagen Kalender, Online-Buchung und KI-Terminannahme nur passende Personen vor. Personen mit **keine Angabe** gelten als passend.
- Bucht die Rezeption ausdrücklich bei jemand anderem, geht das; der Kalender markiert den Termin dann.
- Siehe [Termine am liebsten](hilfe:kunden-termine-am-liebsten#therapeutin).

## Regelarbeitszeit {#wochenarbeitszeiten}

Die regelmäßige Arbeitszeit der Person, in der Windows-App der Abschnitt **Regelarbeitszeit** im Bereich **Arbeitszeit** der Karte: eine Zeile je Arbeitsblock mit Tag, Von, Bis und Standort. Unter den Zeilen steht, an welchen Wochentagen die Person frei hat („Frei: Samstag, Sonntag“). Sie wird mit **Speichern** zusammen mit den Stammdaten gespeichert. „Arbeitszeit hinzufügen“ fügt den nächsten Wochentag ohne Eintrag hinzu, mit Standort und Zeiten der letzten Zeile (bei der ersten Zeile 09:00 bis 17:00 Uhr). So ist eine ganze Woche schnell erfasst. „Entfernen“ (in der Windows-App **Arbeitszeit entfernen**) löscht eine Zeile.

Regeln beim Speichern:

- Der Beginn muss vor dem Ende liegen.
- An einem Tag sind mehrere Zeilen möglich, z. B. vormittags in Wien und nachmittags in Salzburg, oder eine geteilte Schicht. Sie dürfen sich aber nicht überschneiden, auch nicht an verschiedenen Standorten.

Wirkung:

- Die Person ist zu diesen Zeiten am gewählten Standort buchbar. Für die ganze Bindung eines Termins, also Vorbereitung, Behandlung und Nachbereitung, muss sie im Dienst sein.
- Steht für einen bestimmten Tag etwas im Dienstplan, gilt für diesen Tag der Dienstplan statt der Wochenarbeitszeit.
- Im Kalender eines Standorts erscheint, wer dort Wochenarbeitszeit hat oder laut Dienstplan dort eingeteilt ist.
- Ohne Arbeitszeit an einem Standort ist die Person dort nur über den Dienstplan buchbar.

## Aus dem Team nehmen {#aus-dem-team-nehmen}

Nimmt die Person aus der Planung, z. B. bei einem Austritt. Das geht nur, wenn sie keine künftigen Termine mehr hat; sonst erscheint, wie viele es sind. Diese zuerst verschieben, einer anderen Person geben oder stornieren. Vergangene Termine und Berichte behalten die Person. In der Windows-App steht der Weg im Menü **⋯** im Kopf der Karte als **Aus dem Team nehmen …**; er fragt vorher: „Aus dem Team nehmen?“

## Verlauf {#verlauf}

Die Schaltfläche **Verlauf** im Kopf der Karte öffnet in der Windows-App eine Seitenleiste. Sie zeigt, wer Zugang, Rolle, Standorte, Sperre oder Befristung einer Person wann geändert hat, und, wer ihren Dienstplan und ihre Abwesenheiten geändert hat ([Verlauf des Dienstplans](hilfe:dienstplan-verlauf)). Sie sehen nur Inhaber und Manager; für alle anderen fehlt die Schaltfläche. Esc oder das Kreuz schließt die Seitenleiste. Hat die Karte nur einen Bereich, zeigt sie keine Leiste. Mehr dazu: [Verlauf eines Zugangs](hilfe:zugaenge-verlauf).
