# Mehrsprachige Oberfläche: Entscheidungsvorlage mit Aufwand

Stand: 27.10.2026, Runde R03, Slice 80 (Befund Ä103). Der Auftraggeber entscheidet; gebaut ist nichts davon. Texte nach [texte.md](../gestaltung/texte.md). Alle Zahlen stammen aus dem Bestand von `dev` (Stand `62738970`) und sind im Abschnitt „Wie gezählt wurde“ nachzählbar.

## Festlegung vom 31.10.2026

Auftraggeber: Die Mehrsprachigkeit läuft soweit möglich über die API, der Standard in der App ist Deutsch. Das gilt vor den Abschnitten unten, wo sie abweichen. Die Einzelheiten sind eine Auslegung dieser Vorgabe und werden im ADR der Slice 1 festgelegt:

- **Die API liefert die Wörterbücher.** Web und Windows-App holen sie über einen gemeinsamen Endpunkt je Sprache. So bekommen auch künftige Oberflächen dieselben Texte, und eine neue Sprache oder ein korrigierter Text braucht nur eine neue API-Version. Auch der Einzelplatz nutzt diesen Weg, denn dort läuft die API lokal ([ADR 0041](../entscheidungen/0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md)).
- **Texte aus der API folgen der Sprache der Anfrage.** Das betrifft Fehlermeldungen, Hinweise und Antworten an die Oberflächen (Slice 13).
- **Deutsch ist in die App eingebaut.** Deutsch kommt ohne Abruf aus. Es gilt als Standard und als Rückfall, wenn die API noch nicht erreichbar ist oder ein Schlüssel fehlt. Texte vor der Verbindung (Startbildschirm, Verbindungsfehler) bleiben deshalb die Ausnahme vom Weg über die API.
- **Weg B bleibt.** Die Wörterbücher entstehen weiter im Build und werden geprüft; sie liegen danach in der API statt im Paket der App. Pflichttexte, Belege und Fiskaltexte bleiben wie in Abschnitt 6 geregelt.
- **Masken sind nativ.** Seit [ADR 0084](../entscheidungen/0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md) entstehen alle Masken in der Windows-App. Aussagen unten zur Verwaltung im WebView2 und die Aufteilung der Slices 6 bis 12 sind damit überholt, die Zahlen und die Slice-Schätzung sind noch nicht neu gerechnet.

## 1. Empfehlung

**Die Ressourcen entstehen automatisch, aber im Build der Entwicklung und nicht beim ersten Start auf dem Rechner des Spas.**

- **Eine Quelle, zwei Formate.** Ein Katalog mit den deutschen Texten und stabilen Schlüsseln ist die einzige Quelle. Ein Werkzeug erzeugt daraus die Wörterbücher für die Web-Oberflächen und für die Windows-App. Die Mischung aus Windows-App und Web löst damit das gemeinsame Format, nicht der Zeitpunkt der Erzeugung.
- **Erzeugen mit KI, prüfen vor der Auslieferung.** Das Werkzeug übersetzt nur neue und geänderte Schlüssel mit einem Glossar. Das Ergebnis steht als Datei im Repository, der Pull Request zeigt jede Änderung, und die CI prüft Vollständigkeit, Platzhalter und Glossar. Eine Installation startet nie mit einem ungeprüften Text.
- **Pflichttexte nie von der KI.** Kassa, Belege, Steuer, Impressum, Datenschutz, Einwilligungen, Anamnese und die KI-Kennzeichnung tragen im Katalog das Kennzeichen `mandatory`. Das Werkzeug übersetzt sie nicht. Ohne namentliche Freigabe bleiben sie Deutsch. Die Regel in `CLAUDE.md` gilt für Pflichtzahlen; die Vorlage dehnt sie auf Pflichttexte aus.
- **Mit Englisch anfangen, nur für die Team-Oberflächen.** Der Kundenbereich und die Nachrichten an Gäste sprechen schon Deutsch, Englisch, Französisch und Italienisch. Offen sind die Windows-App, die Verwaltung im Web und die Hilfe.
- **Aufwand: 16 Slices bis Englisch in Windows-App und Web, 18 mit Hilfe und Handbuchbildern.** Die Spanne liegt zwischen 13 und 22 Slices: sieben Umstellungs-Slices ergeben 16, zehn ergeben 19; Zusatzaufwand bei Layout, Assistent und Formaten bis 22. Die größte Unsicherheit ist die Umstellung der rund 4.300 Textstellen (Abschnitt 7).
- **Die Mischform (Weg C) kommt später**, falls ein Spa eine Sprache braucht, die das Produkt nicht ausliefert. Sie baut auf Weg B auf und braucht ihn zuerst.

Fünf Entscheidungen sind offen (Abschnitt 8): Weg, Sprachen, Hilfe und Bilder, Freigabe der Pflichttexte, Sprache je Benutzerin.

## 2. Was „mehrsprachig“ heißt

Fünf Bereiche zeigen Text. Zwei davon sind schon mehrsprachig.

| Bereich | Stand heute | Sprache entscheidet |
| --- | --- | --- |
| Kundenbereich (`web/src/booking`) | Deutsch, Englisch, Französisch, Italienisch, eigene Wörterbücher mit denselben Schlüsseln | Link vom Empfang, gespeicherte Wahl, Browser; Rückfall Deutsch |
| Nachrichten an Gäste (Mails, SMS, WhatsApp, Absagen, Erinnerungen, Antworten der Terminannahme) | vier Sprachen, Vorlagen im Code der API | `Customer.Language` des Gastes; fehlt sie oder gibt es keine Vorlage, geht die Nachricht auf Deutsch (Ä52) |
| Windows-App (Empfang: Kalender, Kunden, Anfragen, Kassa, Geräte, Abgleich, Sicherung, Netz) | nur Deutsch, fest in AXAML und C# | keine Wahl vorhanden |
| Verwaltung und Team-Oberfläche im Web | nur Deutsch, fest in TSX | keine Wahl vorhanden; `lang="de"` fest in allen fünf `index.html` |
| Hilfe (Web-Modul `/help/`, in der App im WebView2) | nur Deutsch, Markdown | keine Wahl vorhanden |

Zwei Besonderheiten bestimmen den Aufwand:

- **Die App zeigt Web-Seiten.** Die Verwaltung läuft in der Windows-App im WebView2 ([ADR 0053](../entscheidungen/0053-verwaltung-in-der-windows-app-eingebettet.md)), die Hilfe ebenso ([ADR 0079](../entscheidungen/0079-eine-hilfe-fuer-windows-app-und-web.md)). Ein Wörterbuch für das Web bedient deshalb schon einen Teil der App. Nativ bleiben nur AXAML und C# des Empfangs. Die App gibt dem eingebetteten Browser bereits Parameter in der Adresse mit (`oberflaeche`, `rolle`); die Sprache kann denselben Weg nehmen.
- **Daten sind kein Oberflächentext.** Behandlungsnamen, Hinweise vom Spa und Beschreibungen schreibt der Betrieb selbst. Sie gehören nicht zu dieser Vorlage. Die Felder `Languages` am Personal („Sprachen, in denen die Person behandeln kann“) und `Language` am Gast sind keine Oberflächensprache.

## 3. Bestand in Zahlen

Die Spalte „Art der Zahl“ sagt, ob die Zahl eine Ober- oder Untergrenze ist. Ein Text ist eine Textstelle im Quelltext, nicht ein verschiedener Satz.

| Bereich | Bestand | Art der Zahl |
| --- | --- | --- |
| AXAML der Windows-App | 26 Dateien, 916 Textattribute (705 verschiedene, 3.729 Wörter): `Text` 327, `Content` 195, `ToolTip.Tip` 135, `Label` 111, `AutomationProperties.Name` 73, `PlaceholderText` 53, `Title` 9, `Header` 9, `Message` 3, `HelpText` 1 | Näherung; nur feste Texte, keine Bindungen |
| C# der Windows-App | 185 Dateien, 1.365 Satz-Literale (1.255 verschiedene, 7.023 Wörter); die meisten in `DayLayout.cs` (142), `CalendarViewModel.Keyboard.cs` (74), `PosViewModel.cs` (70), `InboxViewModel.cs` (56) | Obergrenze; enthält auch Logtexte und Ausnahmemeldungen. Zusammengesetzte Sätze (`$"…"`) zählen als ein Literal |
| Web, Team-Oberfläche (ohne Kundenbereich und Hilfe) | 108 TSX- und 34 TS-Dateien, 975 Textknoten und 597 Textattribute (1.033 verschiedene, 4.182 Wörter), dazu 444 Satz-Literale im Code (Meldungen, Beschriftungen in Modellen) | Näherung; die 444 sind eine Untergrenze, weil nur Sätze mit deutschen Signalwörtern zählen |
| Summe Team-Oberflächen | 3.853 Textstellen (916 + 1.365 + 975 + 597), mit den 444 Literalen im Web rund 4.300; rund 15.000 Wörter ohne diese Literale | Näherung, enthält die Obergrenze aus C# |
| Formate | 112 Stellen mit `CultureInfo` oder `de-AT` in 52 Dateien der Windows-App, 75 Stellen mit `toLocale`, `Intl` oder `de-AT` im Web (ohne Tests und Kundenbereich), 155 Stellen mit `CultureInfo` oder `de-AT` in der API | Anzahl Fundstellen |
| Hilfe | 189 Themen, rund 99.000 Wörter, rund 730.000 Zeichen; 165 Handbuchbilder (127 Windows-App, 38 Web), zusammen 10,4 MB | genau |
| API, Texte im Code | 289 Dateien (ohne Demo-Daten und Migrationen), 2.938 Satz-Literale; die meisten in `Online` (462), `Assistant` (457), `Scheduling` (392), `MailIntake` (388), `Pos` (255) | Obergrenze; enthält Gästevorlagen in vier Sprachen, Prompts, Logs |
| API, Fehlerantworten | 588 Aufrufe (`Results.Problem`, `BadRequest`, `Conflict`, `NotFound`, `throw new …Exception`) in 91 Dateien; 87 verschiedene Meldungstexte stehen direkt im Aufruf | Untergrenze; Texte aus Hilfsfunktionen und Konstanten fehlen |
| API, OpenAPI-Beschreibungen | 361 `WithSummary`-Aufrufe (nach texte.md T21 deutsch) | genau; richtet sich an Entwickler und bleibt Deutsch |
| Kundenbereich | 264 Schlüssel, davon 38 Funktionen mit Platzhaltern; Wörter je Sprache: Deutsch 2.047, Englisch 2.119, Italienisch 2.130, Französisch 2.570 (+26 % gegenüber Deutsch); 24 Code-Dateien. TypeScript prüft, dass kein Schlüssel fehlt | genau für Schlüssel, Näherung für Wörter |
| Nachrichten an Gäste in der API | 20 Dateien nennen `GuestLanguage` oder `OnlineLanguage` (`grep -rlE "GuestLanguage\|OnlineLanguage" src/AispaStudio.Api --include=*.cs`, mit Migrationen und Demo-Daten ausgenommen); die größten Vorlagen: `ClosureText.cs` (428 Zeilen), `OnlineMails.cs` (29 Treffer auf `"fr"`), `RuleBasedBookingAssistant.cs` (9) | genau für Dateien |
| Sprache je Gast | `Customer.Language` (leer heißt Deutsch; 18 Kürzel zur Auswahl in `GuestLanguages`), `GuestLanguage.Normalize` fällt auf Deutsch zurück | genau |
| Kennzeichen „EN“ am Termin | Der Dialog „Termine absagen“ zeigt je Termin Kürzel und Sprachname des Gastes (`CancelClosureDialog.cs`, `closuresModel.ts`) und fasst sie zusammen („Nachrichten nach Sprache: 2 Deutsch, 1 Englisch.“) | genau |
| Tests, die deutschen Text prüfen | 59 Web-Testdateien mit `getByText`, `getByLabelText`, `getByRole` oder `findBy…`; in den Tests der Windows-App prüfen viele Assertions Satztexte (nicht gezählt) | Näherung |

Die Hilfe ist der größte Posten an Wörtern (99.065 gegenüber rund 15.000 in den Oberflächen), die Oberflächen sind der größte Posten an Stellen, die ein Mensch ändern muss.

## 4. Gemeinsame Lösung (Entwurf)

### 4.1 Eine Quelle der Schlüssel

Ein Katalog `i18n/de.json` im Wurzelordner enthält jeden Text einmal, mit Schlüssel, deutschem Text, Hinweis für die Übersetzung und Kennzeichen:

```json
"calendar.keyboard.undo": {
  "text": "Rückgängig",
  "note": "Eintrag in der Tastenleiste, höchstens 14 Zeichen",
  "area": "app",
  "mandatory": false
}
```

- **Schlüssel** folgen dem Bereich (`calendar.`, `pos.`, `admin.staff.`), englisch und klein, wie alle Namen im Code.
- **Platzhalter** heißen `{name}`; Mehrzahl hat zwei Formen (`one`, `other`). Das reicht für Deutsch, Englisch, Französisch und Italienisch. Eigene kleine Formatierer in TypeScript und C# lesen dieselbe Schreibweise. Gemeinsame Testvektoren sichern, dass beide dasselbe liefern. Ein Paket für ICU MessageFormat wäre ein neues Paket und braucht Freigabe des Auftraggebers; der Entwurf kommt ohne aus.
- **Die Sprachliste steht einmal.** Heute steht sie an mindestens sieben Stellen (Ä141).
- **Kundenbereich und Gästenachrichten** wandern nicht sofort um. Der Kundenbereich hat ein funktionierendes, vom Compiler geprüftes Verfahren; er zieht erst nach, wenn der neue Katalog sich bewährt hat.

### 4.2 Erzeugung der Ressourcen

| Ausgabe | Format | Weg |
| --- | --- | --- |
| Web (Team-Oberfläche, Verwaltung, Hilfe-Oberfläche) | `web/src/i18n/<sprache>.json`, nachgeladen je Sprache (Bundle-Budget bleibt: nur die gewählte Sprache lädt) | Vite lädt die Datei; `useT()` gibt `t('schlüssel', { werte })` zurück |
| Windows-App | eingebettete Ressource `Strings.<sprache>.json`, dazu eine erzeugte C#-Klasse mit den Schlüsseln als Konstanten | Markup-Erweiterung `{loc:T …}` in AXAML, `T.Key` in C#; Sprachwechsel zur Laufzeit ohne Neustart |
| WebView2 in der App | Sprache als Parameter in der Adresse, wie `oberflaeche` und `rolle` | das Web lädt sein eigenes Wörterbuch |

.NET bringt mit `.resx` und `ResourceManager` ein eigenes Verfahren ohne Paket mit. Es braucht Satellitenassemblys und bindet die Sprache an die Kultur des Prozesses. Das Wörterbuch als JSON hat denselben Aufbau wie im Web und wechselt ohne Neustart; der ADR in Slice 1 legt das fest.

### 4.3 Laufzeit

- **Sprache je Benutzerin**, gespeichert am Konto; Vorbelegung aus Betriebssystem bzw. Browser. Der Standort hat daneben sein Land und seine Währung (siehe 4.4).
- **Rückfall je Schlüssel auf Deutsch**, nie ein leerer Text und nie der Schlüssel. Die Hilfe fällt je Thema zurück.
- `lang` im `<html>` folgt der Sprache (heute fest `de`, Ä142).

### 4.4 Formate und Pflichtzahlen

Datum, Zahl und Währung der Oberfläche folgen der gewählten Sprache. Belege, Tagesabschluss, Exporte (DSFinV-K) und alle Pflichtzahlen folgen dem Land des Standorts, nicht der Sprache der Benutzerin. Eine englischsprachige Person sieht auf dem Bildschirm `1,234.50`, der Beleg trägt weiter `1.234,50 €` (Rechtsfrage zu Belegen: Abschnitt 6). Die 342 Fundstellen für Formate sind eine eigene Slice (Nr. 14 in Abschnitt 7).

### 4.5 Tests und Pflege

- **Vollständigkeit:** jede Sprache hat jeden Schlüssel; Platzhalter und Mehrzahlformen stimmen mit dem Deutschen überein.
- **Kein deutscher Text im Code:** ein Prüftest wie `findingsMarked.test.ts` sucht neue deutsche Literale in AXAML, C# und TSX außerhalb des Katalogs, mit Ausnahmeliste.
- **Gleicher Text wie vorher:** beim Umstellen entsteht der deutsche Katalogtext aus dem alten Literal; ein Test vergleicht ihn mit dem Stand vor der Umstellung. Die 59 Web-Testdateien und die Tests der Windows-App laufen weiter auf Deutsch.
- **Pseudosprache** (Texte um 40 % länger, mit Akzenten) in den Bildtests von Kalender, Kassa und Tastenleiste, damit Abschneiden und Überlauf auffallen. Französisch ist im Kundenbereich schon 26 % länger als Deutsch.
- **Glossar** nach [texte.md](../gestaltung/texte.md) T12 (Kassa, Gast, Behandlung, Termin, Standort, Person) je Zielsprache; die CI prüft, dass die Glossarwörter vorkommen, wo der deutsche Text sie hat.
- **Pflege:** Wer einen deutschen Text ändert, ändert den Katalog. Das Werkzeug erzeugt nur die geänderten Schlüssel neu; Schlüssel mit Freigabe (`reviewed`) bleiben, bis jemand sie öffnet.

## 5. Drei Wege im Vergleich

- **Weg A: beim ersten Start auf dem Rechner des Spas.** Die App (im Mehrplatz der Haupt-PC) fragt beim ersten Start, oder wenn jemand eine neue Sprache wählt, eine KI nach den Übersetzungen und legt die Wörterbücher lokal ab.
- **Weg B: beim Build mit Prüfung vor der Auslieferung.** Die Entwicklung erzeugt die Wörterbücher, prüft sie und liefert sie mit der Version aus.
- **Weg C: Mischform.** Weg B liefert Englisch und weitere freigegebene Sprachen aus. Zusätzlich darf ein Spa auf eigenen Wunsch weitere Sprachen auf seinem Rechner erzeugen lassen, mit Kennzeichen „maschinell, ungeprüft“ und ohne Pflichttexte.

| Kriterium | A: erster Start | B: Build | C: Mischform |
| --- | --- | --- | --- |
| Wo und wann entsteht der Text | auf jedem Rechner, einmal beim Start | im Repository, bei jeder Änderung der Quelle | wie B, dazu auf dem Rechner bei Bedarf |
| Voraussetzung beim Spa | Internet und ein KI-Schlüssel; der Einzelplatz hat keinen KI-Schlüssel. Eine lokale KI ([ADR 0057](../entscheidungen/0057-lokale-ki-optional-mit-ollama.md)) wäre möglich, ihre Übersetzungsgüte ist nicht gemessen | keine | nur für Zusatzsprachen |
| Prüfung vor der Auslieferung | nicht möglich; die Texte entstehen erst danach | Pull Request mit Diff, CI-Prüfungen, muttersprachliche Durchsicht | für ausgelieferte Sprachen wie B, für Zusatzsprachen nicht |
| Gleiche Texte in jeder Installation | nein; jede Installation und jede Modellversion kann anders übersetzen | ja | für ausgelieferte Sprachen ja |
| Reproduzierbare Fehler im Support | nein; ein Fehler bei einem Spa lässt sich kaum nachstellen | ja | wie B für ausgelieferte Sprachen |
| Automatische Tests und Handbuchbilder in der Sprache | nicht möglich, weil der Text zur Testzeit fehlt | möglich | möglich für ausgelieferte Sprachen |
| Datenschutz | nur feste Texte gehen hinaus, aber vom Rechner des Spas aus; jeder Betrieb braucht ein eigenes Konto beim KI-Anbieter und trägt die Kosten | nur feste Texte gehen hinaus, aus der Entwicklung | wie B, dazu wie A für Zusatzsprachen |
| Pflichttexte | die App müsste jeden Pflichttext einzeln sperren; jede Lücke fiele erst beim Spa auf | im Katalog gekennzeichnet, vom Werkzeug übersprungen, CI prüft die Freigabe | wie B |
| Mehrplatz | der Haupt-PC erzeugt, die anderen holen die Dateien; ein zusätzlicher Abgleich | die Dateien kommen mit der Version | zusätzlicher Abgleich nur für Zusatzsprachen |
| Web-Browser-Nutzer | der Server müsste für sie erzeugen | gleiche Dateien im Web-Build | wie B |
| Neue Sprache | sofort, ohne neue Version | braucht eine Version | sofort, aber nur ungeprüft |
| Aufwand | mittel für das Werkzeug, hoch für Abgleich, Fehlerbehandlung und Support | mittel; das Werkzeug läuft in der Entwicklung | B plus ein Aufwand wie A |
| Hauptrisiko | ungeprüfter Text in der Kassa und im Tagesbetrieb, nicht nachstellbar | neue Sprache wartet auf die nächste Version | Zusatzsprachen mit schlechter Güte unter dem Namen des Produkts |

**Zur Frage „beim ersten Start“.** Die Vorgabe des Auftraggebers lautet: Ressourcen nur automatisiert erstellen, wegen der Mischung aus Windows-App und Web. Weg B erfüllt das: Niemand schreibt oder pflegt Übersetzungen von Hand, ein Werkzeug erzeugt sie für beide Formate aus einer Quelle. Gegen den Zeitpunkt „erster Start“ spricht nicht die Mischung, sondern die fehlende Prüfung: Ein Text, der erst beim Spa entsteht, kann keine Entwicklung, keine CI und keine muttersprachliche Person vorher gesehen haben. Das Problem der Mischung aus App und Web liegt im Format, und das löst die gemeinsame Quelle.

## 6. Datenschutz, Pflichttexte und Kosten

### Datenschutz

- Das Werkzeug sendet nur den Katalog (feste Texte mit Platzhaltern) und das Glossar. Der Katalog enthält nie Werte: Namen, Beträge und Termine bleiben Platzhalter. Hinweise im Feld `note` sind frei von Beispielen mit echten Daten; ein Test prüft den Katalog auf Muster wie Mailadressen, Telefonnummern und Beträge.
- Es gehen keine Gästedaten, keine Bildschirmfotos und keine Daten aus Datenbanken an einen Dienst.
- Hilfetexte können Beispielnamen enthalten. Vor dem ersten Durchlauf scannt das Werkzeug die 189 Themen darauf.
- Unabhängig davon schickt die Terminannahme schon heute den Text eingehender Gästemails zur Übersetzung an die KI (`MailIntakeProcessor.TranslateAsync`). Das ist ein anderer Weg mit Gästedaten; der Vertrag mit dem Anbieter ist laut [ADR 0008](../entscheidungen/0008-ki-schlaegt-vor-mensch-bestaetigt.md) noch offen. Diese Vorlage ändert daran nichts.
- Weg A verlagert das Senden auf den Rechner des Spas. Datenschutzrechtlich ändert das wenig, solange nur feste Texte hinausgehen; es verlangt aber ein KI-Konto und einen Schlüssel in jedem Betrieb.

### Pflichttexte

Die KI erzeugt keine Texte, aus denen Pflichten folgen. Im Katalog tragen sie `mandatory: true`:

- Kassa, Belege, Tagesabschluss, Steuer- und Fiskaltexte (RKSV, TSE, DSFinV-K);
- Impressum, Datenschutzhinweis, Einwilligungen (Marketing, Erinnerung), Bedingungen für Anzahlung, Ausfallgebühr und Gutscheine;
- Anamnesebogen und Einwilligung dazu (Gesundheitsdaten, Schema `health`);
- die Kennzeichnung von KI-Nachrichten (AI Act Art. 50), die heute von Hand in vier Sprachen im Code steht.

Das Werkzeug überspringt diese Schlüssel. Die Übersetzung schreibt eine Person, die namentlich freigibt (`reviewed` mit Name und Datum). Fehlt sie, zeigt die Oberfläche Deutsch. Diese Vorlage prüft nicht, ob ein Beleg in einer anderen Sprache als Deutsch rechtlich zulässig ist; solange das ungeprüft ist, bleiben Belege und Fiskaltexte Deutsch.

### Kosten

Die Rechnung nutzt Beispielpreise, die für diese Vorlage **nicht neu abgerufen** wurden: 3 USD je Million Eingabe-Tokens und 15 USD je Million Ausgabe-Tokens. Die Tokenzahlen sind aus den Zeichen geschätzt (etwa 3,5 Zeichen je Token), nicht gemessen.

| Umfang je Sprache | Tokens (Ein- und Ausgabe je) | Kosten für einen Durchlauf |
| --- | --- | --- |
| Oberflächen (rund 15.000 Wörter) | etwa 35.000 | etwa 0,60 USD |
| Hilfe (rund 730.000 Zeichen) | etwa 210.000 | etwa 3,80 USD |

Mit zwei bis drei Prüfläufen und Nachbesserungen bleibt eine Sprache unter 20 USD. Spätere Änderungen kosten Cent-Beträge, weil nur geänderte Schlüssel laufen. **Die Tokenkosten spielen kaum eine Rolle.** Teuer sind die muttersprachliche Durchsicht (rund 15.000 Wörter Oberfläche; Hilfe ein Vielfaches davon), die Umstellung der Quelltexte und die Pflege der Glossare. Für die Durchsicht gibt es keine Zahl; sie hängt von der Person ab.

Weitere Kosten: Jede Hilfesprache bringt 165 Handbuchbilder (etwa 10 MB) in das Paket, wenn die Bilder die Sprache zeigen sollen. Die Hilfe ohne eigene Bilder nutzt die deutschen.

## 7. Aufwandsschätzung in Slices

Die Größen S, M und L entsprechen denen der Befundtabelle. Als Maß dient die bisherige Arbeit: Slice 60 stellte 78 Felder in 15 Dateien um. Bei 450 bis 600 Textstellen je Umstellungs-Slice braucht der Bestand 7 bis 10 Slices; der Plan setzt 7 an (Nr. 6 bis 12). Das ist eine Schätzung, keine Messung.

| Nr. | Inhalt | Größe | Modell | nach |
| --- | --- | --- | --- | --- |
| 1 | Entwurf und ADR: Katalogformat, Schlüsselnamen, Platzhalter und Mehrzahl, Sprachliste, Pflichttext-Liste, Glossar, Format der Wörterbücher in der App | M | opus | Entscheidung des Auftraggebers |
| 2 | Kern Web: gemeinsamer Lader, `useT()` für die Team-Oberfläche (aus dem Kundenbereich verallgemeinert), Sprachwahl je Benutzerin, Rückfall, `lang`, Pseudosprache | L | opus | 1 |
| 3 | Kern Windows-App: Lader, Markup-Erweiterung für AXAML, Wechsel zur Laufzeit, Sprache an den WebView2, gemeinsame Testvektoren | L | opus | 1 |
| 4 | Erzeugungswerkzeug (Weg B): Katalog lesen, nur geänderte Schlüssel an die KI, Glossar, Status je Schlüssel, Pflichttexte überspringen, CI-Prüfungen | L | opus | 1 |
| 5 | Prüftests und Extraktionshilfe: Scan „kein deutscher Text außerhalb des Katalogs“, Skript, das Schlüssel vorschlägt | M | opus | 2, 3 |
| 6–9 | Windows-App umstellen in vier Teilen: Kalender (`DayLayout`, `CalendarViewModel.*`), Kunden/Posteingang/Anfragen, Kassa/Lager/Geräte/Abgleich/Sicherung, Einstellungen/Anmeldung/Netz/Rest | 4 × M | sonnet | 3, 5 |
| 10–12 | Web umstellen in drei Teilen: Verwaltung (`admin`), Kassa/Lager/Kosten/Berichte, Rest (Anmeldung, Assistent, Posteingang, Mein Tag) | 3 × M | sonnet | 2, 5 |
| 13 | API: Fehlerantworten und Texte an die Oberflächen (Fehlercode mit Parametern oder Sprache aus dem Header), Antworten des Assistenten | L | opus | 1 |
| 14 | Formate nach Sprache und Standort (342 Fundstellen), Belege bleiben beim Land des Standorts | M | opus | 6–12 |
| 15 | Layout- und Bildtests in Pseudosprache, Englisch und Französisch; Anpassung fester Breiten | M | sonnet | 6–12 |
| 16 | Erste Sprache einspielen: Englisch erzeugen, muttersprachlich durchsehen, Pflichttexte freigeben | M | opus | 4, 14 |
| 17 | Hilfe je Sprache (optional): Ordner je Sprache, Suche, Rückfall je Thema, Werkzeug für Markdown; `textRules` bleiben deutsch | L | opus | 4 |
| 18 | Handbuchbilder je Sprache (optional): Skripte mit Sprache, 165 Bilder, Paketgröße | M | sonnet | 16, 17 |

**Summe:** 16 Slices für Englisch in beiden Oberflächen, 18 mit Hilfe und Bildern. Jede weitere Sprache kostet danach eine Slice (Erzeugen, Durchsehen, Layout) und mit Hilfe eine zweite.

**Reihenfolge.** 1 zuerst. Danach laufen 2, 3 und 4 nebeneinander (andere Dateien). Die Umstellungen 6–9 laufen nacheinander, weil der Kalender gemeinsame Dateien hat; 10–12 laufen parallel dazu, weil das Web andere Dateien hat. 13 hängt nur an 1. 14 und 15 folgen den Umstellungen, 16 schließt ab.

**Unsicherheit.** Die Spanne von 13 bis 22 Slices hat diese Gründe:

- **Zusammengesetzte Sätze.** Viele C#-Texte entstehen aus Teilen (`$"…"`, Verkettung, Mehrzahl). Sie lassen sich nicht 1:1 als Schlüssel übernehmen; jeder muss als ganzer Satz mit Platzhaltern neu geschrieben werden. Ob das auf 10 % oder 40 % der Texte zutrifft, ist nicht gezählt.
- **Streuung der Texte.** 142 Texte in `DayLayout.cs` zeigen, dass Texte auch in Rechenklassen stehen, nicht nur in Ansichten.
- **Layout.** Feste Breiten und Mindestbreiten in Kalender, Tastenleiste und Knöpfen sind nicht gezählt. Englisch und Italienisch sind im Kundenbereich nur 4 % länger als Deutsch, Französisch 26 %.
- **Assistent und Prompts.** 457 Satz-Literale in `Assistant` mischen Antworttexte und Prompts; ob die Antworten in der Sprache der Benutzerin kommen sollen, ist offen.
- **Parallele Arbeit.** Jede Umstellung berührt Dateien, die andere Slices ändern. In R03 kollidierten Slices schon bei Protokoll und Stand-Seite. Am besten läuft die Umstellung zwischen zwei Runden ohne parallele Oberflächen-Slices.

## 8. Offene Entscheidungen für den Auftraggeber

1. **Weg:** Weg B (Build) bestätigen, oder Weg A (erster Start) trotz der Gründe in Abschnitt 5 wählen?
2. **Sprachen:** Englisch zuerst, Französisch und Italienisch für die Schweiz danach? Andere?
3. **Umfang:** Nur Oberflächen (16 Slices) oder auch Hilfe und Handbuchbilder (18 Slices)?
4. **Pflichttexte:** Wer gibt sie je Sprache frei, und wer klärt, ob Belege in einer anderen Sprache zulässig sind?
5. **Sprache der Oberfläche:** je Benutzerin (empfohlen) oder je Standort?

## 9. Beim Zählen aufgefallen

Diese Funde sind nicht behoben; sie stehen als Befunde in der Rundenakte R03.

- **Ä140:** Der Kommentar zu `Language` in `OnlineContracts.cs` (Zeile 62) sagt, andere Sprachen bekämen Englisch. Der Code fällt auf Deutsch zurück (`GuestLanguage.Fallback`, Ä52).
- **Ä141:** Die Liste der Sprachen steht mehrfach: `GuestLanguage.Supported`, `languageNames` und `locales` in `i18n.tsx`, `OnlineLanguage.Culture`, `AnamnesisForm`, die Reihenfolge in `CancelClosureDialog.cs` (Zeile 323) und in `closuresModel.ts` (Zeile 201). Eine neue Sprache berührt mindestens sieben Stellen.
- **Ä142:** `lang="de"` steht fest in allen fünf `index.html`, und Datum und Zahlen hängen an 342 Stellen an fester Kultur, ohne Bezug zur Sprache.

## 10. Wie gezählt wurde

Alle Befehle laufen im Wurzelordner des Repositorys. Die Zahlen mit „Näherung“ kommen aus dem Zählskript im Anhang (`python count-ui-texts.py`, Python 3; die Regeln stehen im Skript). Die genauen Zahlen lassen sich so prüfen:

| Zahl | Befehl |
| --- | --- |
| 26 AXAML-Dateien | `find src/AispaStudio.Desktop -name '*.axaml' -not -path '*/obj/*' \| wc -l` |
| 185 C#-Dateien der Windows-App | `find src/AispaStudio.Desktop -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' \| wc -l` |
| 189 Hilfethemen | `ls web/src/help/content \| wc -l` |
| 127 und 38 Handbuchbilder | `ls web/src/help/images/app \| wc -l` und `ls web/src/help/images/web \| wc -l` |
| rund 99.000 Wörter der Hilfe | `cat web/src/help/content/*.md \| wc -w` (99.067; das Skript zählt 99.065) |
| 264 Schlüssel im Kundenbereich | Zeilen mit zwei Leerzeichen und `name:` im Block `const de = {` von `web/src/booking/i18n.tsx` (das Skript zählt sie) |
| 361 OpenAPI-Beschreibungen | `grep -rho '\.WithSummary(' src/AispaStudio.Api --include=*.cs \| wc -l` |
| 342 Format-Fundstellen (112 + 155 + 75) | `grep -rEn "CultureInfo\|de-AT" src/AispaStudio.Desktop --include=*.cs --exclude-dir=obj --exclude-dir=bin \| wc -l` (112); dasselbe für `src/AispaStudio.Api` mit `--exclude-dir=Migrations --exclude-dir=Demo` (155); `grep -rEn "toLocale\|Intl\\.\|de-AT" web/src --include=*.ts --include=*.tsx --exclude-dir=node_modules --exclude-dir=booking \| grep -v "\\.test\\." \| wc -l` (75) |
| `lang="de"` in fünf Seiten | `grep -n 'lang=' web/index.html web/*/index.html` |
| 59 Web-Testdateien mit deutschem Text | `grep -rlE "getBy(Text\|LabelText\|Role)\|findBy(Text\|Role)" web/src --include=*.test.tsx \| wc -l` |

Die Zählweise des Skripts im Einzelnen:

- **AXAML:** feste Werte (kein `{` am Anfang) der Attribute `Text`, `Content`, `Header`, `Title`, `PlaceholderText`, `Watermark`, `Label`, `Description`, `Message`, `Hint`, `Subtitle`, `ToolTip.Tip`, `AutomationProperties.Name` und `.HelpText`, mit mindestens einem Wort aus drei Buchstaben.
- **C#:** Zeichenketten (auch `$"…"` und `@"…"`) mit mindestens zwei Wörtern und einem Leerzeichen; ohne Zeilen, die mit `//` oder `[` beginnen, ohne Pfade, Adressen und Namespaces. Das ist eine Obergrenze für Oberflächentexte.
- **TSX:** Text zwischen `>` und `<` ohne Klammern und Zuweisungen, dazu die Attribute `aria-label`, `title`, `placeholder`, `label`, `hint`, `alt`, `aria-description`, `summary`, `description`. Ausgeschlossen sind Tests, `booking/` und `help/`.
- **Satz-Literale im Web:** Zeichenketten mit mindestens zwei Wörtern, einem Leerzeichen und einem deutschen Signalwort oder Umlaut. Das ist eine Untergrenze.
- **API:** wie C#, ohne `Demo/` und `Data/Migrations/`.
- **Nicht gezählt:** Texte in Bildern, Datenbankinhalte und Texte der Betreiber-Oberfläche in `web/betreiber` außer dem, was unter `web/src` liegt.

### Anhang: Zählskript

Das Skript liest nur Quelltexte und gibt JSON auf der Standardausgabe aus. Zum Nachlaufen als `count-ui-texts.py` speichern; es gehört nicht ins Repository.

```python
import os, re, json
from collections import Counter

L = 'A-Za-zÄÖÜäöüß'
SKIP_DIRS = {'bin', 'obj', 'node_modules', 'Migrations', '.vite', 'dist'}


def files(root, exts, exclude=lambda p: False):
    for d, ds, fs in os.walk(root):
        ds[:] = [x for x in ds if x not in SKIP_DIRS]
        for f in fs:
            if f.endswith(exts):
                p = os.path.join(d, f).replace('\\', '/')
                if not exclude(p):
                    yield p


def read(p):
    return open(p, encoding='utf-8-sig').read()


out = {}

# AXAML: Textattribute mit festem Text
axaml_attr = re.compile(r'\b(Text|Content|Header|Title|PlaceholderText|Watermark|Label|Description|Message|Hint|Subtitle|ToolTip\.Tip|AutomationProperties\.Name|AutomationProperties\.HelpText)="([^"{][^"]*)"')
ax = list(files('src/AispaStudio.Desktop', ('.axaml',)))
cnt = Counter(); n = 0; words = 0; uniq = set()
for p in ax:
    for m in axaml_attr.finditer(read(p)):
        v = m.group(2)
        if len(re.findall('[' + L + ']{2,}', v)) >= 1 and re.search('[' + L + ']{3,}', v):
            n += 1; cnt[m.group(1)] += 1; uniq.add(v); words += len(v.split())
out['axaml_files'] = len(ax)
out['axaml_texts'] = n
out['axaml_unique'] = len(uniq)
out['axaml_words'] = words
out['axaml_by_attr'] = dict(cnt.most_common())

cs_lit = re.compile(r'[$@]*"((?:[^"\\\r\n]|\\.)*)"')


def cs_texts(root, exclude=lambda p: False, depth=3):
    total = 0; uniq = set(); words = 0; per = Counter()
    for p in files(root, ('.cs',), exclude):
        for line in read(p).splitlines():
            s = line.strip()
            if s.startswith('//') or s.startswith('['):
                continue
            for m in cs_lit.finditer(line):
                v = m.group(1)
                if len(re.findall('[' + L + ']{2,}', v)) >= 2 and ' ' in v and not re.search(r'[/\\]\w+\.\w+|^\w+://|^[a-z]+(\.[a-z]+)+$', v):
                    total += 1; uniq.add(v); words += len(v.split())
                    parts = p.split('/')
                    per[parts[depth] if len(parts) > depth + 1 else p] += 1
    return total, len(uniq), words, per


t, u, w, per = cs_texts('src/AispaStudio.Desktop')
out['desktop_cs_files'] = len(list(files('src/AispaStudio.Desktop', ('.cs',))))
out['desktop_cs_literals'] = t; out['desktop_cs_unique'] = u; out['desktop_cs_words'] = w
out['desktop_cs_by_dir'] = dict(per.most_common(8))


def web_excl(p):
    return ('.test.' in p) or p.startswith('web/src/booking/') or p.startswith('web/src/help/') or p.endswith('.d.ts')


jsx_text = re.compile(r'>\s*([^<>{}\n][^<>{}]*?)\s*<', re.S)
attr = re.compile(r'\b(aria-label|title|placeholder|label|hint|alt|aria-description|summary|description)="([^"{][^"]*)"')
tsx = list(files('web/src', ('.tsx',), web_excl))
ts = list(files('web/src', ('.ts',), web_excl))
jt = 0; at = 0; uniq = set(); words = 0
for p in tsx:
    s = read(p)
    for m in jsx_text.finditer(s):
        v = ' '.join(m.group(1).split())
        if re.search('[' + L + ']{3,}', v) and not re.search(r'[;=()]', v):
            jt += 1; uniq.add(v); words += len(v.split())
    for m in attr.finditer(s):
        v = m.group(2)
        if re.search('[' + L + ']{3,}', v):
            at += 1; uniq.add(v); words += len(v.split())
sq = re.compile(r"""(?<![\w])(['"`])((?:(?!\1)[^\\\n]|\\.)*)\1""")
mark = re.compile('[ÄÖÜäöüß]|\\b(der|die|das|und|nicht|Termin|Gast|Kunde|Kundin|wird|ist|mit|für|oder|bitte|Fehler)\\b')
lit = 0; litu = set()
for p in tsx + ts:
    for line in read(p).splitlines():
        s = line.strip()
        if s.startswith(('//', '*', '/*', 'import ', 'export type', 'type ')):
            continue
        for m in sq.finditer(line):
            v = m.group(2)
            if ' ' in v and len(re.findall('[' + L + ']{2,}', v)) >= 2 and not re.search(r'[{};=<>]', v) and mark.search(v):
                lit += 1; litu.add(v)
out['web_tsx_files'] = len(tsx); out['web_ts_files'] = len(ts)
out['web_jsx_text_nodes'] = jt; out['web_string_attrs'] = at
out['web_text_unique_jsx_attr'] = len(uniq); out['web_words_jsx_attr'] = words
out['web_ts_tsx_sentence_literals'] = lit; out['web_sentence_literals_unique'] = len(litu)

i18n = read('web/src/booking/i18n.tsx')
start = i18n.index('const de = {'); end = i18n.index('\n}\n', start)
body = i18n[start:end]
out['booking_de_keys'] = len(re.findall(r'^  ([A-Za-z0-9_]+):', body, re.M))
out['booking_de_functions'] = len(re.findall(r'^  [A-Za-z0-9_]+: \(', body, re.M))
out['booking_de_words'] = len(re.findall(r'[' + L + r']+', body))
for lang in ('en', 'fr', 'it'):
    s = read(f'web/src/booking/lang/{lang}.ts')
    out[f'booking_{lang}_keys'] = len(re.findall(r'^  ([A-Za-z0-9_]+):', s, re.M))
    out[f'booking_{lang}_words'] = len(re.findall(r'[' + L + r']+', s))
out['booking_code_files'] = len(list(files('web/src/booking', ('.tsx', '.ts'), lambda p: '.test.' in p or '/lang/' in p)))

md = list(files('web/src/help/content', ('.md',)))
chars = 0; wd = 0
for p in md:
    s = read(p); chars += len(s); wd += len(re.findall(r'\S+', s))
out['help_topics'] = len(md); out['help_words'] = wd; out['help_chars'] = chars
imgs = {}
for sub in ('app', 'web'):
    d = f'web/src/help/images/{sub}'
    imgs[sub] = len([f for f in os.listdir(d) if f.lower().endswith(('.png', '.jpg', '.webp'))]) if os.path.isdir(d) else 0
out['help_images'] = imgs
out['help_images_bytes'] = sum(os.path.getsize(os.path.join(d, f)) for d, _, fs in os.walk('web/src/help/images') for f in fs)

api = 'src/AispaStudio.Api'


def api_excl(p):
    return '/Demo/' in p or '/Data/Migrations/' in p


err = re.compile(r'(Results|TypedResults)\.(Problem|BadRequest|Conflict|NotFound|UnprocessableEntity|ValidationProblem|Forbid|Unauthorized)\(|throw new \w+Exception\(|Problem\(')
errn = 0; errfiles = set(); errlit = 0; erru = set()
for p in files(api, ('.cs',), api_excl):
    for line in read(p).splitlines():
        if err.search(line):
            errn += 1; errfiles.add(p)
            for m in cs_lit.finditer(line):
                v = m.group(1)
                if ' ' in v and len(re.findall('[' + L + ']{2,}', v)) >= 2:
                    errlit += 1; erru.add(v)
out['api_error_calls'] = errn; out['api_error_files'] = len(errfiles)
out['api_error_text_literals'] = errlit; out['api_error_unique'] = len(erru)
out['api_with_summary'] = sum(read(p).count('.WithSummary(') for p in files(api, ('.cs',), api_excl))
t, u, w, per = cs_texts(api, api_excl, 2)
out['api_cs_files'] = len(list(files(api, ('.cs',), api_excl)))
out['api_cs_sentence_literals_all'] = t; out['api_cs_unique_all'] = u
out['api_by_folder'] = dict(per.most_common(10))
print(json.dumps(out, indent=1, ensure_ascii=False))
```
