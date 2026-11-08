# Texte: Regeln für Hilfe, Oberfläche, ADRs, PR-Texte und Schnittstelle

Verbindlich für alles, was der Auftraggeber, das Team und die Gäste lesen: Hilfetexte (`web/src/help/content/`), Oberflächentexte in Windows-App und Web, ADRs, PR-Texte und die Beschreibungen der OpenAPI-Schnittstelle. Grundlage: [ADR 0077](../entscheidungen/0077-textregeln-nach-google-und-microsoft.md). Werte und Tonfall der Oberfläche stehen zusätzlich in [design.md](../design.md), Meldungen in [meldungen.md](meldungen.md), Masken in [masken.md](masken.md).

## 1. Quellen und Grenzen

| Quelle | Stand |
| --- | --- |
| [Google Developer Documentation Style Guide](https://developers.google.com/style/) | Kurzfassung aus dem Wissen über den Leitfaden (Stil, Wortwahl, Listen, Links, Zahlen, Schritte); **nicht für diese Seite neu abgerufen** |
| [Microsoft Writing Style Guide](https://learn.microsoft.com/style-guide/welcome/) | wie oben; die deutsche Fassung der Microsoft-Terminologie wurde **nicht** durchgesehen |
| Projektfestlegungen | unpersönliche Anrede, Kassa, Gast, fett für Knöpfe: aus dem Bestand der Hilfetexte und aus [design.md](../design.md) übernommen |

Beide Leitfäden sind englisch. Die Regeln unten sind auf Deutsch übertragen und nur dort übernommen, wo sie im Deutschen tragen. Wo ein Beispiel „vorher“ heißt, stand es so im Repository und wurde mit T-012 geändert; „Muster“ ist ein ausgedachter Fall.

**Geprüft wird automatisch** nur ein Teil: `web/src/help/textRules.test.ts` prüft die Hilfetexte auf T1 (Du-Form), T8, T9 (nur Richtungspaare), T10 (nur „klicken Sie hier“), T14 und T15. Zitate in „…“ und fett gesetzte Beschriftungen bleiben ungeprüft, weil dort die App oder der Gast wörtlich spricht. Alles andere ist Sache der Durchsicht; der PR-Abschnitt hakt es ab (Abschnitt 4).

## 2. Regeln

### Anrede und Satzbau

**T1. Unpersönlich anreden, nie „Du“.** Schritte stehen im Infinitiv („Auf **Anmelden** klicken.“), Erklärungen im Satz mit „man“ oder der Rolle („Die Rezeption wählt aus und übernimmt.“). „Sie“ nur, wenn eine Person unmittelbar angesprochen werden muss (Antwortentwürfe an Gäste). Die Hilfetexte nutzen es kaum; daran bleibt es.
- Gut: „Die Rezeption wählt aus und übernimmt.“ (`kalender-krankmeldung`)
- Schlecht: „Wer bist du?“ und „Dein Name“ (bis Oktober 2026 Beschriftungen am Empfangsgerät und in der Einzelplatz-Einrichtung); jetzt „Wer meldet sich an?“ und „Ihr Name“ (R01 Slice 06).

**T2. Aktiv statt Passiv.** Das Subjekt handelt, besonders bei Handlungsanweisungen. Passiv nur, wenn der Handelnde unwichtig oder unbekannt ist.
- Gut: „Die App speichert immer die ganze Liste dieser Behandlung.“ (Muster)
- Schlecht: „Gespeichert wird immer die ganze Liste dieser Behandlung.“ (`berichte-vorbereitung-festlegen`)

**T3. Gegenwart.** Das System tut etwas jetzt, nicht „wird tun“. Ausnahme: Rückmeldungen über ein Ergebnis stehen in der Vergangenheit („Termin verschoben auf Do 14:30“, meldungen.md M1).
- Gut: „Der Dienstplan bekommt für den angezeigten Tag einen Krankenstand.“ (`kalender-krankmeldung`)
- Schlecht: „Der Dienstplan wird einen Krankenstand bekommen.“ (Muster)

**T4. Ein Gedanke je Satz.** Höchstens etwa 25 Wörter; Aufzählungen, Bedingungen und Ausnahmen in eine Liste. Klammern nicht für ganze Regeln.
- Gut: „Mails gehen nur an Gäste mit Mailadresse.“ (`kalender-krankmeldung`)
- Schlecht: „… eine CSV-Liste mit „Datum;Titel“ oder „Von;Bis;Titel“ (Trenner Strichpunkt, Komma oder Tabulator, Datum als 26.10.2026 oder 2026-10-26, eine Kopfzeile ist erlaubt), höchstens 2 MB.“ (vorher, `kalender-abos`; Altbestand, bei der nächsten Änderung des Absatzes in eine Liste umbauen)

**T5. Bedingung vor Anweisung.** Erst „wann“, dann „was“.
- Gut: „Fällt eine Person aus, schlägt der Ausfall-Assistent für jeden ihrer offenen Termine Ersatz vor.“ (`kalender-krankmeldung`)
- Schlecht: „Der Ausfall-Assistent schlägt Ersatz vor, wenn eine Person ausfällt, und zwar für jeden ihrer offenen Termine.“ (Muster)

### Oberfläche und Schritte

**T6. Knöpfe und Menüeinträge fett, wie in der App beschriftet.** Seiten, Felder, Reiter, Meldungs- und Beispieltexte stehen in „…“. Schreibweise, Groß- und Kleinschreibung und Satzzeichen wie in der App; was die App ändert, ändert die Hilfe im selben PR (ADR 0056).
- Gut: „Auf **Gewählte übernehmen** klicken.“ (`kalender-krankmeldung`)
- Schlecht: „Auf den Übernehmen-Knopf klicken.“ (Muster; der Knopf heißt anders)

**T7. Schritte als nummerierte Liste, eine Handlung je Schritt.** Ein Schritt, ein Verb. Alternativen („oder“) als eigener Absatz oder Unterpunkt, nicht im selben Schritt. Das Ergebnis eines Schritts steht im nächsten Satz, nicht im Schritt.
- Gut: „1. Mit der rechten Maustaste auf den Spaltenkopf der Person klicken.“ (`kalender-krankmeldung`)
- Schlecht: „1. Den Assistenten öffnen: im Spaltenkopf … auf „… Termine umbuchen“ klicken, oder Rechtsklick auf den Spaltenkopf und **Ersatz für die Termine suchen**.“ (`kalender-krankmeldung`, zwei Wege in einem Schritt; Altbestand)

**T8. Kein „bitte“.** Eine Anweisung ist höflich genug, wenn sie klar ist. Das gilt für Hilfe und Oberfläche; Texte an Gäste (Vorbereitungshinweis einer Behandlung, Antwortentwurf) dürfen es tragen, weil dort der Gast spricht.
- Gut: „Notieren und sicher aufbewahren, dann „Verstanden“.“ (`abgleich`)
- Schlecht: „Bitte notieren und sicher aufbewahren, dann „Verstanden“.“ (vorher, `abgleich`)

**T9. Keine Richtungswörter für Stellen der Oberfläche.** Nicht „oben links“, „unten rechts“, „rechts oben“: Fenstergröße, Seitenleisten und Sprachrichtung ändern die Lage, Screenreader kennen kein „links“. Stattdessen den Namen des Bereichs oder des Elements.
- Gut: „In der Navigation auf das Profilbild (die Initialen) klicken.“ (`anmeldung-abmelden`)
- Schlecht: „Unten links auf das Profilbild (die Initialen) klicken.“ (vorher, `anmeldung-abmelden`)
- Nicht geprüft werden einzelne „oben“, „unten“, „links“ und „rechts“ (rund 350 Stellen, oft für die Zeitachse oder die Lage in einer Liste gemeint); neue Texte vermeiden sie trotzdem.

**T10. Links nennen ihr Ziel, nie „hier klicken“.** Linktext = Name der Zielseite oder ein Satzteil, der das Ziel sagt.
- Gut: „Passwort vergessen: [Passwort vergessen](hilfe:anmeldung-passwort-vergessen).“ (`anmeldung-anmelden`)
- Schlecht: „Klicken Sie hier, wenn Sie das Passwort vergessen haben.“ (Muster)

**T11. Knöpfe tragen ein Verb im Infinitiv, Titel ein Substantiv.** Kein „Jetzt“, kein „Hier“.
- Gut: **Speichern**, **Einchecken**, Titel „Team“, „Behandlungen“ (design.md)
- Schlecht: „Jetzt speichern“, „Hier eintragen“ (Muster)

### Wortwahl und Begriffe

**T12. Begriffe einheitlich.** Dieselbe Sache heißt überall gleich; österreichische Begriffe sind in Ordnung. Ein eigenes Wörterbuch gibt es noch nicht; bis dahin gilt die folgende Auswahl aus dem Bestand.

| Gemeint | Schreibweise | Nicht |
| --- | --- | --- |
| Verkauf und Bezahlen | Kassa (feste Wörter: Kassenlade, Kassenjournal) | Registrierkasse, POS |
| Person, die behandelt wird | Gast; Kundin/Kunde nur für den Stammdatensatz | Patient, Klient |
| Leistung am Gast | Behandlung | Anwendung, Service |
| Buchung im Kalender | Termin | Reservierung |
| Ort des Spas | Standort | Filiale |
| Menschen im Kalender und im Team (Sammelbegriff) | Mitarbeitende („Alle Mitarbeitenden“, „Mitarbeitende ohne Dienst“) | MA, Personen ohne Dienst, Therapeuten, Mitarbeiter, User |
| Ansicht und Stammdatenseite | Team | Personal, Belegschaft |
| Rolle und Gästewunsch bei der Behandlung | Therapeut:in; „Therapeutin/Therapeut“ nur beim Wunsch des Gastes | Therapeuten als Sammelbegriff |

- Gut: „Die Kassa schlägt die laufende Aktion vor.“ (`aktionen`)
- Schlecht: „Die Anwendung schlägt das Angebot vor.“ (Muster, drei Begriffe daneben)

**T13. Abkürzungen beim ersten Mal ausschreiben, Fachwörter erklären.** Bekannte Abkürzungen aus der Branche (RKSV, TSE, PDF, CSV) dürfen stehen; seltene mit Klammer.
- Gut: „Das Datenerfassungsprotokoll (DEP) lädt die Leitung … herunter.“ (`kassa-beleg-drucken`)
- Schlecht: „Das DEP lädt die Leitung herunter.“ (Muster)

**T14. Keine Versalien zur Betonung.** Betont wird mit Satzbau oder, sparsam, mit fett. Abkürzungen stehen in Versalien (Liste in `textRules.test.ts`).
- Gut: „Pflichtfeld.“ (`postfach`)
- Schlecht: „ACHTUNG: Daten gehen verloren“ (Muster)

**T15. Keine Ausrufezeichen.** Auch nicht bei Erfolg oder Warnung (design.md, Ton der Texte).
- Gut: „Gespeichert“ (Toast, meldungen.md M1)
- Schlecht: „Gespeichert!“ (Muster)

**T16. Positiv und konkret.** Sagen, was geht und was man tut, nicht nur was nicht geht; keine Schuldzuweisung, keine Floskeln („natürlich“, „einfach“, „leider“).
- Gut: „Auf **Neue Kundin / neuer Kunde** klicken.“ (`kunden-anlegen`)
- Schlecht: „Leider können Sie das einfach nicht ändern.“ (Muster)

### Zahlen, Einheiten, Daten

**T17. Dezimalkomma, Tausenderpunkt, Euro nachgestellt.** „35,00 €“, „6,3 %“, „1.187 Tests“. Zwischen Zahl und Einheit oder Zeichen steht ein Leerzeichen; **in Oberflächentexten (Code) ein geschütztes Leerzeichen** (U+00A0), damit der Umbruch nicht zwischen „35,00“ und „€“ fällt. In Markdown für Hilfetexte genügt das normale Leerzeichen; es wird nicht geprüft.
- Gut: „Vorab über Treatwell bezahlt: 35,00 €“ (`anfragen-treatwell-buchung`)
- Schlecht: „35.00 EUR“ (Muster)

**T18. Daten und Uhrzeiten wie im Land.** Datum „26.10.2026“ (ohne führende Null beim Tag in Fließtext erlaubt: „3.12.“), Uhrzeit „14:30“, Zeiträume „von … bis …“ oder mit Gedankenstrich; Monate im Fließtext ausgeschrieben. ISO-Schreibweise nur, wo ein Format verlangt wird (Import).
- Gut: „Die Datei reicht vom 1. Jänner bis zum gewählten Datum.“ (`berichte-pflichtberichte-abrufen`)
- Schlecht: „Die Datei reicht von 2026-01-01 bis 2026-10-23.“ (Muster)

### Meldungen und Fehler

**T19. Fehlermeldungen nennen Problem und Weg, ohne Schuld und ohne Code.** Aufbau, Länge und Ort stehen in [meldungen.md](meldungen.md) (Toast höchstens 60 Zeichen und ohne Schlusspunkt, Hinweise als ganze Sätze mit Punkt, Fehler bleiben stehen).
- Gut: „Für dieses Konto ist kein Zugang eingerichtet oder er wurde gesperrt.“ mit dem Weg „die Leitung prüft den Zugang unter „Verwaltung“ › „Zugänge““ (`anmeldung-anmelden`)
- Schlecht: „Fehler 403: Zugriff verweigert.“ (Muster)

### ADRs, PR-Texte, Schnittstelle

**T20. ADRs und PR-Texte sachlich, Entscheidung zuerst.** Deutsch mit Umlauten (ä, ö, ü, ß; Dateinamen und Code bleiben englisch und ASCII), Gegenwart, aktiv. Ein ADR folgt der [Vorlage](../entscheidungen/vorlage.md): Kontext, Optionen, Entscheidung, Begründung, Folgen. Im PR-Text stehen Detailentscheidungen mit Begründung, danach „Beim Schreiben festgestellt“ und „Nicht geprüft“; keine Marketingsprache, keine Ausrufezeichen. Personen stehen als Rolle, nie mit Namen: Wer entscheidet und freigibt, ist „der Auftraggeber“ („Der Auftraggeber hat entschieden“, „Freigabe des Auftraggebers“, „(Auftraggeber, 08.11.2026)“).
- Gut: „Eigene Entity `RoomClosure` je Raum, im Planer wirksam, mit Meldung und Umbuchungshilfe.“ (ADR 0076, Tabelle Optionen: Entscheidung steht in der gewählten Zeile)
- Schlecht: „Aenderung der Raumlogik, super wichtig!“ (Muster)

**T21. OpenAPI-Beschreibungen: ein Satz, Verb zuerst, was der Weg tut und was er verlangt.** `WithSummary` auf Deutsch, mit Punkt am Ende; Besonderheiten (Rechte, Grenzen, Frist) in Klammern oder im zweiten Satz.
- Gut: „Erzeugt einen Einmal-Code für die Anmeldung der Windows-App (10 Minuten gültig).“ (`WithSummary` in `src/AispaStudio.Api/Access`)
- Schlecht: „Code-Endpunkt“ (Muster; sagt nicht, was geschieht)

### Tooltips

**T22. Ein Tooltip ist ein kurzer Satz, der mehr sagt als die Beschriftung.** Höchstens ein Satz, höchstens zwei Kommas (kein Aufzählen), kein Wiederholen der sichtbaren Beschriftung, kein Schlusspunkt bei einem Satzfragment. Ein Knopf nur mit Symbol nennt im Tooltip seinen Namen (Fluent 2). In der kompakten Kopfzeile des Kalenders, wo ein Chip nur Symbol und Zahl zeigt, steht der Name des Chips mit Doppelpunkt davor. Geprüft von `TermsAndTooltipTests` (Windows-App) und `web/src/help/terms.test.ts` (Web).
- Gut: „Auch Mitarbeitende ohne Dienst an diesem Standort, mit Grund“ (Chip **Alle zeigen (3)**)
- Schlecht: „Alle zeigen (3): Auch Personen ohne Dienst an diesem Standort zeigen, mit Grund (frei, Urlaub, krank, Dienst an anderem Standort)“ (vorher; wiederholt die Beschriftung und zählt auf)

## 3. Wann was gilt

| Text | Regeln |
| --- | --- |
| Hilfethema (`web/src/help/content/`) | T1 bis T18, T19 für Meldungstexte; automatisch geprüft: T1 (Du-Form), T8, T9 (Richtungspaare), T10, T14, T15 |
| Oberflächentext (App, Web) | T1, T3, T6, T8, T11, T12, T14 bis T19, T22; Werte in design.md |
| ADR, PR-Text, Protokoll | T2, T3, T12, T14, T15, T20 |
| OpenAPI-Beschreibung | T3, T12, T21 |

## 4. Prüfschritt für das PR-Protokoll

Jeder PR-Abschnitt, der Hilfe, Oberflächentexte, ADRs oder Schnittstellenbeschreibungen ändert, enthält diesen Satz und hakt ihn ab:

> Texte nach [texte.md](../gestaltung/texte.md) durchgesehen (Anrede, aktiv, ein Gedanke je Satz, Beschriftungen wie in der App, Schritte mit einer Handlung, keine Richtungswörter, einheitliche Begriffe, Zahlen und Einheiten), `npx vitest run src/help` grün.
