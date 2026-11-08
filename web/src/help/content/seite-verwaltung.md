---
art: seite
bereich: Verwaltung
oberflaeche: windows-app
rollen: inhaber, manager, leitung
seite: verwaltung
stichwoerter: Verwaltung, Admin, Administration, Einstellungen, Stammdaten, Zugänge, Dienstplan, Zeiten, Lager, Kosten, Buchhaltung, Lohn, Backoffice, settings, admin area, wo finde ich
---
# Seite „Verwaltung“ in der Windows-App

Unter „Verwaltung“ liegen in der Windows-App alle Verwaltungen: Betrieb, Angebot, Team mit Dienstplan und Zeiten, Lager, Kassa und Geld, Kontakt und Zahlen. Alle Masken sind Teil der App; einen Browser braucht die Verwaltung nicht. Sie arbeitet mit dem Zugang, mit dem man in der App angemeldet ist.

## So kommt man hin {#hinkommen}

1. Links in der Leiste auf **Verwaltung** klicken (über **Hilfe**, unter den Empfangsbereichen).
2. Der Balken zeigt die Gruppen, die der eigene Zugang sehen darf: **Betrieb**, **Angebot**, **Team**, **Lager**, **Kassa & Geld**, **Kontakt** und **Zahlen**.
3. Eine Gruppe anklicken. Hat sie mehrere Masken, erscheinen sie als Reiter, z. B. unter **Betrieb** die **Firmendaten** und die **Schließzeiten**. Beim Öffnen ist die erste erlaubte Maske gewählt; kehrt man zu einer Gruppe zurück, steht wieder die zuletzt gewählte Maske da.

![Verwaltung in der Windows-App](images/app/verwaltung.png)

Ein Tastenkürzel für die Verwaltung gibt es nicht. Der Balken ist mit der Tabulatortaste ein Halt: Die Pfeiltasten gehen zum nächsten Eintrag, Enter oder die Leertaste öffnet ihn, F6 springt zwischen der Leiste der App, Balken, Reitern und Maske. Reicht die Breite des Fensters nicht, stehen die übrigen Einträge im Menü **Mehr**; ist dort einer gewählt, zeigt das Menü ihn mit einem Punkt.

## Welche Bereiche wer sieht {#bereiche}

Der Balken zeigt nur, was die eigene Rolle darf; eine Gruppe ohne erlaubte Maske fehlt. Die Reihenfolge ist immer dieselbe:

- **Betrieb**: Firmendaten nur Inhaber; Standorte & Räume, Schließzeiten, Raumsperren, Kalender-Abos und Handbuch auch die Standortleitung. Siehe [Einrichtung des Spas](hilfe:seite-masterdata).
- **Angebot**: Behandlungen und Aktionen für Inhaber und Standortleitung.
- **Team**: alle Rollen mit einer der Ansichten; Personen, Wochenplan, Zeiten und Anträge schaltet eine Leiste über der Fläche um. Die Personenkarte hat die Reiter Stammdaten, Zugang und Rolle, Qualifikationen, Dienstplan und Abwesenheit, Zeiten und Verlauf. „Zugänge“, „Qualifikationen“, „Dienstplan“ und „Zeiten“ gibt es in der App nicht mehr als eigene Bereiche. Siehe [Team](hilfe:team), [Zugänge](hilfe:zugaenge), [Seite „Dienstplan“](hilfe:seite-roster) und [Seite „Zeiten“](hilfe:seite-time).
  - **Wochenplan**: alle Rollen; planen dürfen nur Inhaber und Standortleitung.
  - **Zeiten**: Inhaber und Standortleitung ändern, das Backoffice liest.
  - **Lohn**: nur Inhaber, als Reiter der Personenkarte. Siehe [Lohn](hilfe:lohn).
- **Lager**: Inhaber, Standortleitung und Rezeption; die Rezeption sieht den Bestand nur an. Siehe [Seite „Lager“](hilfe:seite-stock).
- **Kassa & Geld**: Kassa und Zahlungen nur Inhaber. Kosten und Buchhaltung für Inhaber und Backoffice, für andere Rollen mit dem einzeln vergebenen Recht „Berichte mit Beträgen“. Siehe [Seite „Kosten“](hilfe:seite-costs) und [Buchhaltung](hilfe:buchhaltung).
- **Kontakt**: Erinnerungen, Postfach & Nummern und KI nur Inhaber.
- **Zahlen**: Berichte und Betriebsinfo für alle Rollen, jede mit ihren eigenen Auswertungen. Siehe [Seite „Berichte“](hilfe:seite-reports).

Eine Standortleitung sieht in jedem Bereich nur ihre eigenen Standorte. Was eine Rolle darf, steht unter [Rollen und was sie dürfen](hilfe:zugaenge#rollen).

## Wenn eine Maske nicht aufgeht {#nicht-erreichbar}

- „Diese Maske lässt sich mit der eigenen Rolle nicht öffnen. Das Recht vergibt die Leitung im Bereich „Team“.“: Man ist über einen Link, etwa aus einer Antwort des Assistenten, zu einer Maske gesprungen, die die eigene Rolle nicht sieht. Die App zeigt stattdessen die erste Maske, die die Rolle öffnen darf, mit diesem Hinweis über der Maske. Der Hinweis lässt sich schließen und verschwindet mit dem nächsten Wechsel.
- „Diese Stelle gibt es in der Windows-App nicht. Die passende Maske oben im Balken der Verwaltung wählen.“: Das Ziel des Links gibt es nicht. Auch dann öffnet die erste erlaubte Maske.
- Hat der Zugang gar keine Maske, bleibt die App auf der aktuellen Seite und zeigt unten die Meldung „Diese Maske braucht ein Recht, das die Leitung vergibt“ oder „Diese Stelle gibt es in der Windows-App nicht“. Die Rolle prüft die Person mit Verwaltungsrecht unter **Verwaltung** › **Team**, Reiter **Zugang und Rolle**, siehe [Rollen und was sie dürfen](hilfe:zugaenge#rollen).
- Ohne Verbindung zum Server lassen sich die Masken nicht speichern. Kalender, Kunden und Vormerken gehen weiter.

Links auf fremde Seiten, z. B. die Einrichtung bei Stripe, öffnen sich im normalen Browser, nicht in der App. Kalender, Anfragen, Geräte und Anmeldung haben eigene Einträge in der linken Leiste. Wer Code für die Windows-App oder einen Passkey braucht, findet die Seite „Anmelden“ im Browser: [Seite „Anmelden“ im Browser](hilfe:anmelden-web).

## Abläufe {#ablaeufe}

- [Person einladen](hilfe:zugaenge-person-einladen)
- [Behandlung anlegen](hilfe:stammdaten-behandlung-anlegen)
- [Dienstplan für einen Tag planen](hilfe:dienstplan-tag-planen)
- [Urlaub oder Krankenstand eintragen](hilfe:dienstplan-abwesenheit-eintragen)
- [Arbeitszeit nachtragen](hilfe:zeiten-nachtragen)
- [Wareneingang buchen](hilfe:lager-wareneingang-buchen)
- [Vollkosten einer Behandlung ansehen](hilfe:kosten-vollkosten-ansehen)
