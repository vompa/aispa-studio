---
art: einstellung
bereich: Schließzeiten
oberflaeche: verwaltung
rollen: leitung, inhaber, manager
seite: verwaltung
stichwoerter: Schließzeit, Schließzeiten, Betriebsurlaub, Betriebsferien, Sonderschließung, Feiertag geschlossen, Brückentag, Ausfall, Wasserschaden, Schaden, Krankheit, Behörde, geschlossen, zugesperrt, Termine absagen, halber Tag, closure, holiday closure, outage, vacation
---
# Schließzeiten

Eine Schließzeit sagt, dass ein Standort in einem Zeitraum zu ist: Betriebsurlaub, ein Feiertag oder eine Sonderschließung (zum Beispiel ein Brückentag) oder ein Ausfall durch einen Schaden (Wasser), Krankheit oder eine Behörde. Sie gilt für den ganzen Standort, nicht für eine Person oder einen Raum; dafür gibt es die Sperrzeit im Kalender ([Pausen, Sperrzeiten und Notizen](hilfe:kalender-pausen-und-notizen)), den Dienstplan und für einen einzelnen Raum für Tage oder Wochen die [Raumsperre](hilfe:raumsperren).

Verwalten dürfen Inhaber, Manager und Standortleitung (Recht „Standorte verwalten“), die Standortleitung nur für ihre Standorte. Lesen können alle mit Zugang zum Kalender, weil Kalender, Meldungen und Assistent die Schließzeiten zeigen. **Termine absagen** darf zusätzlich, wer buchen darf.

## Was eine Schließzeit bewirkt {#wirkung}

- **Keine freien Zeiten für Gäste:** Online-Buchung, „Freie Zeit suchen“, Warteliste und Assistent bieten in der Zeit nichts an. Bei einem halben Tag bleibt der Rest des Tages buchbar, ein Termin muss aber vor der Schließung enden.
- **Die Rezeption trägt trotzdem ein:** An einem Schließtag und an einem Tag ohne Dienst lassen sich Termine, Pausen, Notizen, Sperrzeiten und Sauna-Eintritte ganz normal eintragen. Die Maske **Neuer Termin** und die Antwort nennen nur den Hinweis „Der Standort hat an diesem Tag geschlossen.“; **Termin buchen** bleibt aktiv. Person, Raum und Gerät müssen wie sonst frei sein. Ein halber Tag Schließzeit gilt weiter: ein Termin muss vor der Schließung enden. Paar und Spa-Tag lassen sich an einem Schließtag noch nicht buchen.
- **Kalender:** über allen Spalten liegt ein **Schließband** in der Farbe der Art mit dem Text. In der Kopfzeile steht am Tag der Chip **Geschlossen · Betriebsurlaub**.
- **Bestehende Termine bleiben gebucht.** Nichts wird von selbst abgesagt und niemand benachrichtigt. Die Seitenleiste **Meldungen** zeigt „n Termine in der Schließzeit“ (auch „n Einträge der Warteliste“) mit dem Knopf **Termine absagen …**, gleich wie fern die Schließzeit liegt: als Warnung, solange sie später als in 14 Tagen beginnt, als Fehler, wenn sie läuft oder in 14 Tagen beginnt. Derselbe Knopf steht hier in der Maske: [Termine absagen](hilfe:schliesszeiten-termine-absagen).
![Kalender am Tag eines Betriebsurlaubs: Schließband über allen Spalten und der Chip Geschlossen](images/app/kalender-schliessband.png)

- **Kundenbereich:** Gäste sehen bei der Buchung „Vom … bis … geschlossen“ mit dem Text für Gäste.
- **Assistent:** „Ist am Montag offen?“, „Sind wir im Jänner geschlossen?“ und „Wann ist der nächste Feiertag?“ nennen die Schließzeit. Mehr unter [KI-Assistent fragen](hilfe:ki-assistent-fragen).

## Schließzeiten verwalten {#schliesszeiten}

1. In der Windows-App **Verwaltung** › **Betrieb** › **Schließzeiten** öffnen. Gibt es mehrere Standorte, oben den **Standort** wählen. Aus dem Kalender führt der Chip **Geschlossen · Betriebsurlaub** in der Kopfzeile gleich hierher.
2. Oben den Reiter **Aktuell** (aktiv und geplant) oder **Beendet** wählen. Die Zahl nennt, wie viele Schließzeiten dort stehen.
3. Links eine Schließzeit wählen oder auf **Neue Schließzeit** klicken, rechts die Maske ausfüllen und auf **Speichern** klicken (in der Windows-App auch **Strg+S**). Es erscheint „Gespeichert“.

In der Windows-App gilt zusätzlich:

- **Verwerfen** neben **Speichern** holt den gespeicherten Stand zurück. Wer mit ungespeicherten Änderungen eine andere Schließzeit, einen anderen Reiter, Standort oder Bereich wählt, wird gefragt: „Änderungen speichern?“ mit **Speichern**, **Verwerfen** und **Abbrechen**.
- Ein Fehler steht am Feld, und der Cursor springt ins erste Feld mit einem Fehler.
- Datum und Uhrzeit lassen sich kurz eintippen: „24.12.“, „9“ wird 09:00, „1430“ wird 14:30.

Der Zustand wird gerechnet: **Geplant** beginnt später, **Aktiv** läuft gerade, **Beendet** ist vorbei. Zwei Schließzeiten desselben Standorts dürfen sich nicht überschneiden. **Löschen** gibt es nur, solange wegen der Schließzeit keine Termine abgesagt wurden; danach bleibt sie als Verlauf stehen.

## Zeitraum {#zeitraum}

**Von** und **Bis** sind Kalendertage am Standort, beide Tage zählen mit; das Ende darf nicht vor dem Beginn liegen, höchstens 366 Tage. Mit **Nur ein Teil der Tage (mit Uhrzeit)** kommen **Ab Uhrzeit** (am ersten Tag) und **Bis Uhrzeit** (am letzten Tag) dazu. So entsteht ein halber Tag, zum Beispiel „ab 13:00 Uhr“, wenn ein Schaden mittags auffällt. Ohne Uhrzeit gilt der erste Tag ab 0:00 Uhr und der letzte bis zum Ende des Tages.

## Art {#art}

- **Betriebsurlaub**: geplante Schließung, meist lange im Voraus.
- **Feiertag oder Sonderschließung**: ein Tag, an dem das Spa zu hat, obwohl der Wochenplan offen wäre (Brückentag, Betriebsfeier). Gesetzliche Feiertage schließen den Standort nicht von selbst; wer an einem Feiertag zusperrt, legt hier eine Schließzeit an.
- **Ausfall (Schaden, Krankheit, Behörde)**: ungeplant, zum Beispiel Wasserschaden oder Anordnung der Behörde.

Die Art bestimmt die Farbe des Schließbands, den Standardtext für Gäste und die Vorlage der Absage.

## Texte {#texte}

- **Grund für das Team**: höchstens 200 Zeichen, zum Beispiel „Wasserschaden im Keller, Trocknung“. Sehen nur Mitarbeitende, nie Gäste.
- **Text für Gäste**: ein kurzer Satz, höchstens 200 Zeichen, zum Beispiel „Wegen eines Wasserschadens vorübergehend geschlossen.“ Er steht im Kundenbereich, beim Assistenten und als Grund in der Absage. Leer: ein Standardsatz je Art („Wegen Betriebsurlaub geschlossen.“).

Unter den Feldern steht, wer die Schließzeit wann angelegt hat.

## Protokoll der Absagen {#protokoll}

Wurden wegen der Schließzeit Termine abgesagt, zeigt die Maske unten das **Protokoll der Absagen** (in der Windows-App eine Zeile zum Aufklappen): je Termin (und je benachrichtigtem Eintrag der Warteliste) der Gast, die Zeit, der Kanal (Mail, SMS oder „kein Kontakt“) mit dem Kürzel der Sprache, eine retournierte Anzahlung, der Zustand (gesendet, wird gesendet, nicht gesendet, zurückgenommen, fehlgeschlagen), wer abgesagt hat und wann. „KI“ steht dabei, wenn der Text mit KI umformuliert wurde. Eine Nachricht, die noch auf ihre Wartezeit wartet, zeigt „Nachricht an den Gast geht um 14:32 raus.“ mit den Knöpfen **Jetzt senden** und **Nicht senden** (siehe unten).

## Wartezeit vor der Nachricht an den Gast {#wartezeit}

Sagt das Team Termine ab, geht die Nachricht an den Gast nicht sofort raus, sondern erst nach einer **Wartezeit**. So lässt sich ein Versehen noch zurücknehmen, bevor der Gast etwas erfährt. Die Wartezeit gilt je Standort und steht oben auf der Seite im Feld **Wartezeit vor der Nachricht an den Gast (Minuten)**: 0 bis 60, vorgegeben sind 10. Mit **Wartezeit speichern** übernehmen Sie den Wert. Bei 0 geht jede Nachricht sofort.

- **Beginnt der Termin in weniger als 2 Stunden,** geht die Nachricht sofort raus, denn dann hilft dem Gast kein Warten mehr.
- **Gäste, die selbst absagen oder umbuchen,** bekommen ihre Nachricht immer sofort. Die Wartezeit gilt nur für Absagen, die das Team auslöst.
- Der **Termin ist sofort abgesagt** und eine Anzahlung geht sofort zurück. Nur die Nachricht wartet.
- Im Protokoll der Absagen steht bei einer wartenden Nachricht **„Nachricht an den Gast geht um 14:32 raus.“** Mit **Jetzt senden** geht sie gleich raus, mit **Nicht senden** nie. Danach zeigt der Zustand „gesendet“ oder „nicht gesendet (zurückgenommen)“. Eine gesendete Nachricht lässt sich nicht mehr zurücknehmen.

> Hinweis: **Nicht senden** nimmt nur die Nachricht zurück. Der Termin bleibt abgesagt, und der Gast erfährt nichts davon. Rufen Sie ihn in diesem Fall an.
