# 0084. Alle Masken nativ in der Windows-App, Web nur für Dashboard und Handy

- **Status:** angenommen
- **Datum:** 29.10.2026
- **Entschieden von:** Auftraggeber (Vorgabe und Entscheidungen E1–E14 der Runde R05), Ausgestaltung Claude

## Kontext

Auftraggeber, 29.10.2026: „ich möchte das alle masken jetzt windows native masken zur eingabe verwaltung usw sind, im web wird nur noch ein dashboard für die übersicht ( pausenraum ) oder ähnlich, ein webapp für handy mitarbeiter mit grundfunktionen, und einen handy webapp für die kunden zur terminkontrolle gestaltet“. Dazu am selben Tag: „auch sollte das personal ( team ) zusammengehalten werden für die verwaltung, und nicht teile verstreut auf mehrere verwaltungsmasken“.

Bisher ist der Empfang nativ (Kalender, Kunden, Posteingang, Kassa, Geräte), die Verwaltung dagegen die Web-Team-Oberfläche im eingebetteten Browser ([ADR 0053](0053-verwaltung-in-der-windows-app-eingebettet.md)). Das Personal ist auf mehrere Seiten und Reiter verteilt: Zugänge, Team und Qualifikationen in den Stammdaten, Dienstplan, Zeiten und Lohn. In der Runde R05 hat der Auftraggeber die offenen Punkte E1 bis E14 entschieden.

## Optionen

| Option | Bewertung |
| --- | --- |
| Verwaltung eingebettet lassen (ADR 0053) | eine Oberfläche für Browser und App, aber zwei Techniken in der App, Grenzen des WebView2 bei Tastatur, Fokus und Überlagerungen, Verwaltung nur mit Verbindung |
| Verwaltung nativ, daneben weiter eine Web-Verwaltung | Verwalten im Browser und am Mac möglich, aber jede Maske zweimal bauen, prüfen und dokumentieren |
| Alle Masken nativ, Web nur für Dashboard, Mitarbeiter-App und Kunden-App | gewählt |

## Entscheidung

1. **Jede Maske zur Eingabe und Verwaltung läuft nativ in der Windows-App.** Die Web-Team-Oberfläche und die eingebettete Verwaltung entfallen. Welche Seite und welcher Reiter in welcher Slice nativ wird, steht im [Masken-Inventar](../umsetzung/masken-inventar.md).
2. **Das Web liefert für das Spa nur noch drei Apps**, alle zuschaltbar nach [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md):
   - Dashboard für den Pausenraum: nur lesen; Tagesleiste je Person, freie Räume und nächste Termine mit Initialen; nie Gesundheitsdaten.
   - Handy-App für Mitarbeitende aus „Mein Tag“: Plan, nächster Termin, begonnen und beendet melden, Abwesenheit beantragen, offline lesen. Inhaberin und Managerin sehen dazu eine lesende Seite mit Kennzahlen des Tages. Zeiterfassung, Diensttausch und eigene Zertifikate gibt es dort nicht.
   - Handy-App für Kunden aus dem Kundenbereich: Termine ansehen, verschieben und absagen, Kalendereintrag, Gutscheine, Anamnese vorab.

   Push-Hinweise sind zurückgestellt. Die Betreiberseite für den Betrieb der Plattform bleibt; sie gehört nicht zur Verwaltung eines Spas.
3. **Hilfe und Handbuch bleiben im eingebetteten Browser** ([ADR 0079](0079-eine-hilfe-fuer-windows-app-und-web.md)); WebView2 bleibt nur dafür.
4. **Das Team steht an einer Stelle.** Personendaten ändert nur der Bereich „Team“ der Verwaltung, mit Liste und Personenkarte. Zur Karte gehören Stammdaten, Zugang und Rolle, Qualifikationen, Dienstplan und Abwesenheit, Zeiten und Lohn. Dazu kommen Personalakte, Hinweise, Sonderregelungen, Vollmachten, Zuständigkeiten, Zertifikate und Verlauf. Andere Masken zeigen Personendaten nur und springen in die Karte.
5. **Verwalten von zu Hause geht über dieselbe Windows-App** mit Verbindung zum Server des Spas bzw. zum Cloud-Betrieb, nicht im Einzelplatz und nicht am Mac. Eine Web-Verwaltung oder Fernwartung ist kein vorgesehener Weg. Stand der Voraussetzungen:
   - **Erreichbarkeit (Ä196):** Die App findet einen Server nur über die Umgebungsvariable `AISPA_API_URL`; eine Eingabe in der App gibt es nur für den Haupt-PC im eigenen Netz. Slice 118 baut die Eingabe der Server-Adresse.
   - **Erreichbarkeit im eigenen Netz (Ä197):** Der Haupt-PC ([ADR 0049](0049-mehrplatz-im-eigenen-netz.md)) lauscht auf allen Netzwerkkarten, das Programm gibt ihn aber nicht nach außen frei und öffnet keine Ports am Router. Von zu Hause geht es im Server-Betrieb oder über ein VPN des Spas; eine Portweiterleitung empfehlen wir nicht. Das Handbuch beschreibt das (Slice 118).
   - **TLS:** Im Server-Betrieb prüft die App das Zertifikat wie ein Browser. Der Zertifikats-Pin gilt nur für den Haupt-PC im eigenen Netz. Hier fehlt nichts.
   - **Anmeldung:** wie im Spa ([ADR 0007](0007-anmeldung-passkeys-identity.md)); hier fehlt nichts.
   - **Verlorenes Gerät (Ä198):** „Zugang sperren“ beendet die Anmeldung schon heute. Der Offline-Stand bleibt aber auf dem Gerät, verschlüsselt mit DPAPI und an das Windows-Konto gebunden. Das Handbuch rät deshalb zu einem Windows-Kennwort und zur Laufwerksverschlüsselung (Slice 118).
6. **Übergang:** Bis eine Slice ihren Bereich nativ gebaut hat, öffnet die App ihn weiter in der eingebetteten Verwaltung an derselben Stelle. Eine Slice ersetzt ihren Bereich vollständig, mit denselben Feldern, Regeln und Hilfetexten, und bringt keine neuen Felder mit. Slice 103 entfernt danach die eingebettete Verwaltung und die Web-Team-Oberfläche.
7. **Geschäftslogik bleibt in der API.** Fristen, Gebühren, Genehmigungen, Erinnerungen, Kennzahlen und Lagerbuchungen rechnet nur der Server; Oberflächen zeigen an.

## Begründung

Am Empfang und in der Verwaltung sitzt dieselbe Person am selben Windows-PC; eine Technik für alle Masken heißt ein Verhalten für Tastatur, Fokus, Meldungen und Screenreader (Fluent 2, [ADR 0069](0069-fluent-2-als-grundlage-der-windows-app.md)) und eine Testart. Das Web kann, was die App nicht kann: auf dem Handy und auf einem Bildschirm ohne Bedienung laufen. Eine zweite Web-Verwaltung für den Fernzugriff würde jede Maske verdoppeln; die Windows-App mit Serververbindung erreicht dasselbe mit einer Fassung.

## Folgen und Stand

- Runde R05 setzt das in den Slices 93 bis 118 um; Reihenfolge der Masken nach Häufigkeit im Alltag (Behandlungen, Team, Dienstplan, Standorte, dann der Rest).
- `web/src/maskInventory.test.ts` hält das Inventar gegen die Seiten der Web-Team-Oberfläche, bis Slice 103 sie entfernt. Seit Slice 103 prüft er nur noch den ADR-Index; die Sprungziele prüfen `ViewTests.JumpTargets` und `FieldMaskInventoryTests`.
- Neue Verwaltungsfunktionen werden ab jetzt nur noch nativ gebaut; die Web-Team-Oberfläche bekommt nur noch Fehlerbehebungen, bis ihr Bereich nativ ist.
- Die Gestaltungsregeln für Web-Oberflächen (`fluent2-web`, `modern-web-guidance`) gelten weiter für Dashboard, Handy-Apps, Kundenbereich und Hilfe.
- Nicht verwalten kann, wer weder Windows-PC noch Verbindung zum Server hat (Mac, Einzelplatz von unterwegs); das ist so gewollt.

## Nachtrag 01.11.2026 (Slice 103)

Ergänzt Punkt 5 und Punkt 6; der ursprüngliche Text bleibt.

5. **Anmeldung im Server-Betrieb über die Seite „Anmelden“.** „Mein Tag“ lässt sich abschalten (Ruling des Auftraggebers, 31.10.2026); ohne die Handy-App fehlte im Server-Betrieb eine Web-Seite für die Anmeldung. Die Seite `/anmelden/` bietet deshalb die Anmeldung mit Passkey, **Code für die Windows-App**, das Annehmen einer **Einladung**, **Passkey hinzufügen** und **Neues Spa registrieren**; im Einzelplatz meldet sie mit Benutzername und Passwort an, in der Entwicklung bietet sie die Testkonten. Verwaltet wird weiter nur in der Windows-App; die Seite verwaltet nichts.
6. **Übergang abgeschlossen.** Slice 103 hat die eingebettete Verwaltung und die Web-Team-Oberfläche entfernt. Jeder Sprung in die Verwaltung öffnet eine native Maske; bei fehlendem Recht oder unbekanntem Ziel zeigt die App die erste erlaubte Maske mit einem Hinweis. Die API liefert nur noch die Apps aus; die Wurzel leitet nach `/anmelden/`.

## Nachtrag 07.11.2026 (Erklärseite)

Ergänzt Punkt 7; der ursprüngliche Text bleibt.

7. **Erklärseite „Über AISPA Studio“ (`/ueber/`).** Das Web liefert zusätzlich eine statische Seite ohne Anmeldung, ohne Skript und ohne Masken. Sie erklärt Laien die Betriebsarten, was im Spa, im Rechenzentrum und bei anderen Firmen läuft, und zeigt die Browser-Fassung nur als Ausblick (Versuch, nicht verfügbar). Aufbau nach der Erklärseite von OPCAgentSample, Farben und Schrift aus dem Brief. Die Hilfe-Seite verlinkt sie mit `/ueber/index.html`, damit sie auch im lokalen Ordner der Windows-App (Einzelplatz) aufgeht. Verwaltet wird dort nichts; die Regel „alle Masken nativ“ bleibt.

## Quellen

- Rundenakte `work/rounds/R05-native-masks-web-apps.md` (Rulings E1–E14)
- [Masken-Inventar](../umsetzung/masken-inventar.md)
- [ADR 0053](0053-verwaltung-in-der-windows-app-eingebettet.md) (ersetzt), [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), [ADR 0059](0059-zum-start-nur-die-windows-app.md), [ADR 0079](0079-eine-hilfe-fuer-windows-app-und-web.md)
