# Handtests

Handtestblöcke der Aufgaben seit dem Abschluss der Runde R07 (04.11.2026). Die Blöcke bis Slice 147 stehen im eingefrorenen Archiv, in der Manuellen Testliste der jeweiligen Rundenakte unter [work/rounds/](../../work/rounds/), zuletzt [R07](../../work/rounds/R07-reception-daily-layout.md).

Jede Aufgabe trägt beim Abschluss einen Block ein ([arbeitsweise.md](../arbeitsweise.md) Abschnitt 9), in der Reihenfolge der Aufgabennummern:

- Überschrift `### T-NNN – Titel`
- eine Zeile `Start:` (wie die App gestartet und wohin navigiert wird)
- nummerierte Schritte, nach jedem Schritt eine Zeile `Expected:`
- `Closes: Ä…` mit den erledigten Befunden
- `Result: ☐`, nach dem Handtest `Result: ☑` oder `Result: ☒` mit kurzer Notiz

Beim Rundenabschluss wird geprüft, dass jede Aufgabe der Runde einen vollständigen Block hat.

## Blöcke

Ohne Block, weil die Aufgabe nichts Sichtbares ändert: Antwortvorschläge auf Fragen kennen Anreise, Schließtage, Hinweise der Behandlungen und den Gutscheinkauf (19.11.2026); Erinnerung vor Ablauf der Frist für Karte oder Anzahlung (Mail an den Gast nach Stunden Wartezeit; durch Tests gedeckt), Obergrenze von 2 Erinnerungen je Gast und Tag (greift nur bei nachgebuchten Terminen; durch Test gedeckt), Entscheidung zur Mindestlücke (nur Doku), Kopfzeile `List-Unsubscribe` in der Erinnerung an die nächste Behandlung, Lückenvorschläge mit Treatwell-Hinweis und Einwirkzeit (ein Treatwell-Termin lässt sich ohne echtes Postfach nicht von Hand erzeugen; durch Ansichtstest gedeckt), Erinnerung nach 48 Stunden mit Verbindung (derselbe Satz wie im Block „Hinweis nach 48 Stunden“, nur unter „Beim Abgleich nicht übernommen“), Kassa ohne Verbindung Schritt 1 (nur Schnittstelle; der Block kommt mit Schritt 2), T-013 (Entscheidung, nur Doku), T-014 (Entscheidung, keine Änderung), T-016 (Sichtung der zurückgestellten Kleinigkeiten), T-030 (CI-Zeitlimits), T-033 (Beispieldaten und Test), T-034 (Werkzeug knip, unbenutztes Paket entfernt), T-035 (Skill und Doku), T-040 (ADR und Konzept, nur Doku), Export/Import Aufgabe A (Export-Dienst, Knopf ohne Seite; der Knopf ist im Block „Export/Import B“ geprüft), Verwaltungsmasken Schritt 1 (T-060: Regeln und zwei Bausteine, noch in keiner Maske verwendet), T-085 (Bildskript der Handbuchbilder, nur Werkzeug), T-089 und T-090 (Beispieldaten nur für die Vorschau im Designer).

### T-015 – Keine Stempeluhr in „Mein Tag“, Zeiten trägt die Leitung ein
Start: Browser in Handybreite (z. B. 390 px), die Adresse des Spas mit `/mein-tag/` öffnen, als Mitarbeiterin (Therapeutin) anmelden
1. „Mein Tag“ ansehen (Kopf und Tagesliste).
Expected: Der Tag mit den Terminen ist zu sehen, aber keine Stempeluhr und kein Hinweis „Zeiterfassung braucht eine Verbindung.“ steht da.
2. In der Browser-Entwicklerleiste (Netzwerk) die Seite neu laden.
Expected: Es gibt keinen Aufruf von `/api/time/me`.
3. Dann `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Leitung anmelden, **Verwaltung** › **Team** öffnen, oben **Zeiten** wählen, Standort und Woche wählen und in der Zeile einer Person die Zelle eines Tages anklicken.
Expected: Die Zeiten des Tages öffnen sich; **Zeit nachtragen** ist da.
4. **Zeit nachtragen** wählen, Beginn und Ende eintragen, bei **Grund (wird protokolliert)** „laut Stundenzettel“ eintragen und mit **Speichern** sichern.
Expected: Der Abschnitt steht im Tag; ohne Grund erscheint „Grund angeben.“ und nichts wird gespeichert.
Closes: – (T-015, Entscheidung des Auftraggebers 05.11.2026; R07 „Offene Entscheidungen“)
Result: ☐

### T-017 – Dienstplan: Tag als Popover direkt unter der Zelle
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Verwaltung** › **Team** öffnen und in der Leiste über der Fläche **Wochenplan** wählen
1. Auf die Zelle einer Person in der oberen Hälfte der Tabelle klicken.
Expected: Der Tag öffnet sich als Feld direkt unter der Zelle (nicht unter der Tabelle), mit Schatten, höchstens etwa 560 breit und ganz im sichtbaren Bereich.
2. Eine Zelle in der Spalte Sonntag anklicken.
Expected: Das Feld rutscht nach links, sodass es nicht über den Rand der Karte hinausragt.
3. Ohne zu schließen auf eine andere Zelle klicken.
Expected: Das Feld wechselt zum neuen Tag und steht unter der neuen Zelle.
4. Einen Dienst ändern und Strg+S drücken.
Expected: Die Meldung nach dem Speichern steht als umbrechender Text unter den Knöpfen; das Feld bleibt innerhalb der Karte.
5. **Schließen** klicken.
Expected: Das Feld verschwindet; Zeilen darunter sind wieder frei.
Closes: Ä370
Result: ☐

### T-018 – „Neu beginnen“ im Kopf der Leiste „KI und Hilfe“
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, „KI und Hilfe“ mit Strg+Leertaste öffnen
1. Den Kopf der Leiste ansehen, bevor etwas gefragt wurde.
Expected: Neben dem Kreuz steht kein „Neu beginnen“ (nichts zum Zurücksetzen).
2. „wer ist heute noch frei“ fragen.
Expected: Im Kopf erscheint links vom Kreuz der Knopf „Neu beginnen“; in der Zeile „Assistent“ gibt es keinen solchen Knopf mehr.
3. „Neu beginnen“ klicken.
Expected: Frage und Antwort sind weg, der Cursor steht im Fragefeld.
4. Im Kalender einen Termin öffnen, dort „KI und Hilfe“ wählen und etwas fragen.
Expected: Auch im Kalender-Reiter steht „Neu beginnen“ im Kopf neben dem Kreuz; der Titel ist nicht abgeschnitten.
Closes: Ä359
Result: ☐

### T-019 – Neue Kundin: Telefon schon beim Verlassen des Feldes formatiert
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, **Kunden** öffnen und **Neue Kundin / neuer Kunde** klicken
1. Bei **Telefon** „0664 1234567“ eintippen und mit Tab das Feld verlassen.
Expected: Das Feld zeigt sofort „+43 664 1234567“ (noch vor dem Speichern).
2. Im Feld „abc“ eintippen und mit Tab weitergehen.
Expected: Der Text bleibt wie getippt stehen, es erscheint kein Fehler.
3. Eine gültige Nummer eintragen, Namen angeben und anlegen; auf der Kundenkarte die Nummer ansehen.
Expected: Dieselbe Schreibweise „+43 664 1234567“ steht auf der Karte.
Closes: Ä364
Result: ☐

### T-020 – Kundenkarte: offene Gutscheine und letzte Käufe
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden. Vorher an der Kassa für einen Gast einen Gutschein verkaufen und einen Artikel (z. B. Massageöl) ohne Termin kassieren. Dann **Kunden** öffnen
1. Diesen Gast in der Liste wählen und den Verlauf rechts ansehen.
Expected: Unter „Offene Pakete“ stehen „Offene Gutscheine“ (Art, letzte vier Zeichen des Codes, Restwert, gültig bis) und „Letzte Käufe“ (Tag, Text, Menge, Betrag), neueste zuerst, höchstens fünf.
2. Einen Gast ohne Gutscheine und Käufe wählen.
Expected: Beide Abschnitte fehlen.
3. Als Therapeutin anmelden und denselben Gast öffnen.
Expected: „Offene Gutscheine“ und „Letzte Käufe“ fehlen (kein Recht zu buchen).
Closes: Ä367
Result: ☐

### T-021 – Lager: „Reicht für“ auch bei gewähltem Lagerort
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Lager** öffnen und den Reiter **Bestand** wählen
1. Ohne Lagerort die Spalte „Reicht für“ ansehen.
Expected: Für Material mit hinterlegter Packung steht eine Zahl an Behandlungen.
2. Einen Lagerort (z. B. ein Kabinenregal) wählen.
Expected: „Reicht für“ ist nicht leer, sondern rechnet mit der Menge an diesem Ort.
3. Einen Ort mit sehr wenig Material wählen.
Expected: Der Chip „nachbestellen“ erscheint für Material, das dort nur für wenige Behandlungen reicht.
Closes: Ä374
Result: ☐

### T-022 – Assistent: „schick … den Bogen“ springt zur Anamnese
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, eine Seite außerhalb des Kalenders (z. B. **Kassa**) öffnen und „KI und Hilfe“ mit Strg+Leertaste öffnen
1. Einen Gast mit nächstem Termin nennen, z. B. „schick der Moser den Bogen“.
Expected: Der Assistent bietet den Auftrag an (Knopf zum Öffnen des Termins).
2. Den Vorschlag anklicken.
Expected: Der Kalender öffnet den Termin, und der Abschnitt „Anamnese“ liegt ganz im Bild (inklusive „Foto hochladen“); der Fokus steht dort.
3. Den Hinweis oben im Panel ansehen.
Expected: Der Hinweis „Vom KI-Assistenten geöffnet …“ steht noch da; kein Weg (Tablet, Papier, Foto) ist vorgewählt.
Closes: Ä385
Result: ☐

### T-023 – Neue Kundin erkennt eine gleiche Mailadresse
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, **Kunden** öffnen und **Neue Kundin / neuer Kunde** klicken. Vorher die Mailadresse einer bestehenden Kundin in der Kundenkarte nachsehen
1. Bei **E-Mail** die Adresse nur halb eintippen (z. B. „anna@huber“).
Expected: Es erscheint kein Hinweis.
2. Die ganze Adresse in anderer Groß-/Kleinschreibung eintippen.
Expected: Der Kasten „Schon vorhanden?“ zeigt die bestehende Kundin mit dem Knopf „Diese Kundin nehmen“.
3. „Diese Kundin nehmen“ klicken.
Expected: Die vorhandene Karte öffnet sich, es wird keine zweite Kundin angelegt.
4. Erneut die Maske öffnen, dieselbe Adresse eintragen und trotzdem anlegen.
Expected: Anlegen bleibt möglich (der Hinweis ist nur ein Vorschlag).
Closes: Ä365
Result: ☐

### T-024 – Kundenkarte bestätigt das Kopieren
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, **Kunden** öffnen und eine Kundin mit Telefon und E-Mail wählen
1. Im Kopf der Karte auf die Telefonnummer klicken.
Expected: Neben der E-Mail-Zeile steht kurz „Kopiert.“; die Nummer liegt in der Zwischenablage (in ein Textfeld einfügen).
2. Etwa drei Sekunden warten.
Expected: „Kopiert.“ verschwindet von selbst.
3. Auf die E-Mail-Adresse klicken, dann gleich noch einmal.
Expected: „Kopiert.“ erscheint jeweils neu; die Adresse liegt in der Zwischenablage.
Closes: Ä368
Result: ☐

### T-025 – Rezeption sieht Betriebsinfo und Bedarfsprüfung
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, **Zahlen** öffnen
1. Die Reiter von **Zahlen** ansehen.
Expected: Der Reiter „Betriebsinfo“ ist da; „Berichte“ nicht.
2. „Betriebsinfo“ öffnen und einen Eintrag wählen.
Expected: Die Einträge der Stufe „Team“ sind lesbar; es gibt kein „Neuer Eintrag“ zum Bearbeiten, Einträge nur für die Leitung fehlen.
3. **Lager** öffnen und den Reiter **Bestellen** wählen.
Expected: Die „Bedarfsprüfung“ mit „Zeitraum“ steht da; Bestellvorschlag und Bestellungen fehlen.
4. Als Therapeutin anmelden und **Zahlen** öffnen.
Expected: Der Reiter „Betriebsinfo“ fehlt weiter.
Closes: Ä376, Ä373
Result: ☐

### T-026 – Handbuchbilder neu erzeugt
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, **Hilfe** öffnen; Bilder liegen in `web/src/help/images/app/`
1. Das Thema zum Fragen an den Assistenten (`ki-assistent-fragen`) öffnen und die Bilder `ki-und-hilfe-buchen`, `ki-auftrag-buchen` ansehen.
Expected: „Neu beginnen“ steht im Kopf neben dem Kreuz; `ki-auftrag-buchen` zeigt „Neuer Termin“ vorausgefüllt mit Hinweis über „Termin buchen“.
2. Die Bilder `ki-antwort-liste`, `ki-antwort-fehler` und die Bilder `ki-betrieb-*` ansehen.
Expected: Alle zeigen die aktuelle Leiste (Kopf mit Kreuz, ohne alten Knopf in der Zeile „Assistent“).
3. Die Bilder `kunden-bearbeiten`, `kunden-bearbeiten-fehler`, `kunden-kennzeichen`, `kunden-vorlieben` und `kassa-belege` ansehen.
Expected: Die Masken stimmen mit der laufenden App überein (gleiche Beschriftungen, keine alten Spalten).
4. Das Thema „Dienst für einen Tag planen oder ändern“ öffnen und `verwaltung-team-karte-dienstplan` ansehen.
Expected: Der Tag steht als Feld an der Zelle.
Closes: Ä358, Ä366, Ä382, Ä387
Result: ☐

### T-027 – Kassa per Tastatur
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, **Kassa** öffnen
1. Den Reiter **Belege** wählen, dann wieder **Kassieren**.
Expected: Der Cursor steht im Suchfeld „Gast, Code oder Artikel“ (nicht auf dem Reiter).
2. Ein Wort eintippen, das nur einen Artikel trifft, keine Zeile der Liste, dann Pfeil nach unten drücken.
Expected: Der Fokus springt auf den ersten Artikeltreffer.
3. Weiter mit Pfeil nach unten und nach oben, zuletzt mit Pfeil nach oben auf dem ersten Treffer (oder Esc).
Expected: Der Fokus wandert durch die Artikel und kehrt ins Suchfeld zurück.
Closes: – (Review Slice 137, ohne Ä-Nummer)
Result: ☐

### T-028 – Hilfe: „Erste Schritte“ als erste Gruppe der Navigation
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, **Hilfe** öffnen
1. Die Navigation links ansehen.
Expected: „Erste Schritte“ ist die erste einklappbare Gruppe, mit den Einstiegen der Rezeption in Lernreihenfolge; der Chip zeigt „Meine Rolle: Rezeption“.
2. Den Chip auf „Alle“ umschalten.
Expected: Die Einstiege aller Rollen stehen je einmal in der Gruppe.
3. Prüfen, ob ein Thema aus „Erste Schritte“ (z. B. „Status setzen“) in seinem Bereich (z. B. „Kalender“) noch einmal steht.
Expected: Nein, jedes Thema steht nur einmal.
4. Zur Startseite der Hilfe gehen.
Expected: Die Startseite zeigt „Erste Schritte“ wie bisher.
Closes: Ä380
Result: ☐

### T-029 – „Zuletzt gefragt“ um Mitternacht auch bei offener Leiste leer
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, „KI und Hilfe“ mit Strg+Leertaste öffnen (Test über Mitternacht: vor 24 Uhr beginnen)
1. Zwei Fragen stellen, z. B. „wer ist heute noch frei“.
Expected: Unter dem Fragefeld steht „Zuletzt gefragt“ mit beiden Fragen.
2. Die Leiste offen lassen und über Mitternacht (bis höchstens eine Stunde danach) warten.
Expected: „Zuletzt gefragt“ ist leer, ohne dass die Leiste geschlossen oder etwas gefragt wurde; eine offene Frage mit Antwort bleibt stehen.
3. Im Fragefeld Pfeil nach oben drücken.
Expected: Die Fragen von gestern kommen nicht zurück.
Closes: Ä360
Result: ☐

### T-031 – Kundenübersicht bestätigt das Kopieren
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, **Kalender** öffnen und einen Termin eines Gastes mit Telefon und E-Mail wählen
1. Im Kopf des Termins auf die Initialen oder den Namen klicken.
Expected: Links neben dem Panel öffnet sich die Kundenübersicht.
2. Beim Telefon auf den Knopf „Telefonnummer kopieren“ klicken.
Expected: Kurz steht „Kopiert.“ unter der E-Mail-Zeile; die Nummer liegt in der Zwischenablage.
3. Die Übersicht schließen, dann über den Namen statt über die Initialen wieder öffnen und bei der E-Mail den Knopf „E-Mail-Adresse kopieren“ klicken.
Expected: „Kopiert.“ erscheint, und die Adresse liegt wirklich in der Zwischenablage (einfügen).
4. Dasselbe im Posteingang (**Anfragen**) bei einer Anfrage mit Kundenübersicht.
Expected: Gleiches Verhalten.
Closes: Ä368 (Folgepunkt aus T-024)
Result: ☐

### T-032 – Kassieren von außen öffnet die Kassa auf „Kassieren“
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, **Kassa** öffnen und den Reiter **Belege** wählen
1. Zum **Kalender** wechseln, einen eingecheckten Termin öffnen und **Kassieren** klicken.
Expected: Die Kassa öffnet auf dem Reiter „Kassieren“ mit der Rechnung des Termins (nicht auf „Belege“).
2. Wieder zu **Belege** wechseln und von der Kundenkarte (**Kunden**, Gast wählen, **Kassieren**) kassieren.
Expected: Auch hier steht die Rechnung sichtbar auf „Kassieren“.
3. Zu **Belege** wechseln, andere Seite öffnen und über die Seitenleiste wieder **Kassa** wählen.
Expected: Die Kassa behält den zuletzt gewählten Reiter „Belege“.
Closes: – (Nebenbefund aus T-027)
Result: ☐

### T-037 – Anfangsbestand der Kassa im Tagesstart kommt als Feld
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Zahlen** öffnen (Reiter **Tagesstart**)
1. In der **Kassa** den **Tagesabschluss durchführen** und den gezählten Betrag merken, dann zu **Zahlen** wechseln.
Expected: Unter **Kassa** steht „Kassa <Kürzel>: Anfangsbestand“ mit genau diesem Betrag, z. B. „250,00 €“.
2. Eine Kassa ohne Abschluss ansehen (neue Kassa anlegen oder eine ohne Tagesabschluss wählen).
Expected: Bei dieser Kassa steht „kein Abschluss“.
3. Eine Kassa mit Abschluss über 1.000 € ansehen (z. B. 1.250,50 € zählen und abschließen).
Expected: Der Betrag steht mit Tausenderpunkt: „1.250,50 €“.
4. Bei einer Kassa in der Schweiz (Land CH) mit Abschluss ansehen.
Expected: Der Betrag steht als „CHF 250.00“.
5. Eine Kassa mit Warnung (z. B. Signatureinheit nicht erreichbar) ansehen.
Expected: Die Warnung steht weiter als Warnband „Kassa <Kürzel>: …“ unter den Werten.
Closes: Ä377
Result: ☐

### T-039 – Dienstplan: Chips im dunklen Farbschema ruhiger
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Verwaltung › Team › Wochenplan** öffnen (Demo-Daten, mehrere Personen mit Diensten)
1. Den Farbschema-Knopf in der Leiste so oft drücken, bis Dunkel gewählt ist (die Reihenfolge ist System, Hell, Dunkel).
Expected: Die Dienst-Chips sind dunkel getönt in der Farbe der Person (nicht mehr hellpastell), die Zeit steht klar lesbar in heller Schrift; Entwurf (gestrichelter Rand) und Abwesenheitsbalken sehen aus wie zuvor.
2. In der Woche von heute die Spalte von heute ansehen.
Expected: Die Chips sind in der hervorgehobenen Spalte noch als Chips erkennbar (Beobachtung aus T-039: der Abstand dort ist klein); Auffälliges bitte notieren.
3. Den Knopf weiter drücken, bis System gewählt ist, dann das Windows-Schema auf Hell stellen.
Expected: Die Chips sehen genau aus wie vor T-039 (Personenfarbe zu Weiß aufgehellt, dunkle Zeit).
4. Das Windows-Schema wieder auf Dunkel stellen.
Expected: Die Chips folgen dem Windows-Schema ohne Neustart.
Closes: Ä372
Result: ☐

### T-038 – Zahlen: Kopf bei schmalem Fenster
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Verwaltung › Zahlen** öffnen (Reiter **Mein Tag**)
1. Das Fenster mit der Maus auf die kleinste Größe ziehen (schmaler geht es nicht).
Expected: Alle fünf Reiter (Tagesstart, Mein Tag, Hygiene, Berichte, Betriebsinfo) stehen in einer Zeile und sind ganz zu sehen; Standort und Datum stehen rechts daneben oder rechtsbündig darunter, nichts ist abgeschnitten oder überlappt.
2. **KI und Hilfe** in der Leiste öffnen.
Expected: Die Reiter bleiben in einer Zeile; Standort und Datum rutschen rechtsbündig in eine zweite Zeile unter die Reiter.
3. **KI und Hilfe** schließen.
Expected: Standort und Datum stehen wieder rechts neben den Reitern.
4. Mit Tab von den Reitern weitergehen.
Expected: Die Reihenfolge bleibt Reiter, Standort, Datum, in einer wie in zwei Zeilen.
5. Den Reiter **Betriebsinfo** wählen.
Expected: Standort und Datum fehlen, die Reiter stehen wie zuvor allein in der Zeile.
Closes: Ä378
Result: ☐

### T-041 – Mehrinstanz: Rolle `Hosting:Role` für Api und Worker
Start: `docker compose up -d --build` im Repository-Ordner, danach eine zweite API-Instanz in einem eigenen Terminal mit `docker compose run --rm -p 127.0.0.1:5087:8080 -e Hosting__Role=<Rolle> api` (Rolle je Schritt)
1. `docker compose logs api` ansehen (die Instanz aus `compose.yaml` ohne Rolle).
Expected: Eine Zeile „Rolle All; Hintergrunddienste: …“ nennt unter anderem `DatabaseMigrator`, `PaymentRecovery`, `MailboxPoller`, `ReminderScheduler`, `HistoryCleanup` und `HrFileCleanup`.
2. Die zweite Instanz mit `-e Hosting__Role=Worker` starten und ihr Protokoll ansehen.
Expected: Die Zeile lautet „Rolle Worker; Hintergrunddienste: …“ mit denselben Diensten wie in Schritt 1.
3. `curl -i http://127.0.0.1:5087/api/v1/system/modules` und `curl -i http://127.0.0.1:5087/health` aufrufen.
Expected: Die Module antworten mit 404, `/health` mit 200 (`Healthy`); auch `/openapi/v1.json` antwortet mit 404.
4. Die zweite Instanz mit Strg+C beenden und mit `-e Hosting__Role=Api` neu starten, dann `curl -i http://127.0.0.1:5087/api/v1/system/modules` aufrufen.
Expected: Die Zeile lautet „Rolle Api; Hintergrunddienste: …“ ohne `PaymentRecovery`, `MailboxPoller`, `ReminderScheduler`, `RebookScheduler`, `AccessExpiryScheduler`, `CalendarSubscriptionScheduler`, `SavedCardCleanup`, `UnsecuredBookingRelease`, `HistoryCleanup` und `HrFileCleanup` (es bleiben nur Dienste der Instanz wie `DatabaseMigrator`); die Module antworten mit 200 und JSON.
5. Die zweite Instanz mit `-e Hosting__Role=Worker -e Hosting__Standalone=true` starten, danach mit `-e Hosting__Role=Backend`.
Expected: Beide Male bricht der Start sofort ab, zuerst mit „Der Einzelplatz läuft immer mit Hosting:Role=All …“, dann mit „Hosting:Role „Backend“ ist ungültig; erlaubt sind All, Api und Worker.“
6. Im Repository `Hosting__Role=Worker dotnet build src/AispaStudio.Api --no-incremental` ausführen (Bash; in PowerShell `$env:Hosting__Role = "Worker"; dotnet build src/AispaStudio.Api --no-incremental; $env:Hosting__Role = $null`), danach `git status web/openapi`.
Expected: Der Build läuft durch, `web/openapi/aispa-api.json` ist unverändert (die OpenAPI-Erzeugung gilt immer als `All`).
7. Bekannte Grenzen zur Kenntnis: In einem Aufbau nur aus Api und Worker erreichten Live-Nachrichten aus dem Worker (Posteingang, Buchungen aus der Terminannahme) bis T-046 keinen Client (mit `Live:Backplane=Redis` seit T-046 schon), und die Mail-Queue sowie die Postfach-Sperre galten bis T-048 je Instanz (seit T-048 nur im Worker, Block T-048).
Expected: Kein Fehler dieser Aufgabe; die Grenzen stehen im Nachtrag von ADR 0087. Nach dem Test `docker compose down`.
Closes: – (T-041, Aufgabe B der Arbeitsanweisung Mehrinstanz-Betrieb)
Result: ☐

### T-042 – Mehrinstanz: Schlüsselring in der Datenbank (`DataProtection:Store`)
Start: `docker compose up -d --build` im Repository-Ordner (wie bisher mit `FileSystem`), danach in AISPA an dieser API mit einem Testkonto anmelden
1. Unter „Verwaltung“ › „Kassa & Geld“ › „Zahlungen“ eigene Stripe-Testschlüssel (`sk_test_…`) eintragen und speichern.
Expected: Der Zugang gilt als hinterlegt.
2. `docker compose stop api`, danach in einem eigenen Terminal `docker compose run --rm -p 127.0.0.1:5086:8080 -e DataProtection__Store=Database api` starten und das Protokoll ansehen.
Expected: Eine Zeile „N Schlüssel aus /home/app/.aspnet/DataProtection-Keys übernommen“ mit N ab 1, vor den Zeilen der übrigen Migrationen; kein Fehler „An error occurred while reading the key ring“.
3. Die Seite in AISPA neu laden und „Zahlungen“ öffnen.
Expected: Die Anmeldung besteht weiter, der Stripe-Zugang gilt weiter als hinterlegt.
4. `docker compose exec db psql -U postgres -d aispadb -c 'SELECT "FriendlyName" FROM system."DataProtectionKeys"'` ausführen.
Expected: Je Datei aus dem Volume `api-keys` eine Zeile `key-…`.
5. Die Instanz mit Strg+C beenden und mit demselben Befehl neu starten.
Expected: Die Zeile lautet „0 Schlüssel aus … übernommen“, die Tabelle hat gleich viele Zeilen, die Anmeldung besteht weiter.
6. Die Instanz mit `-e DataProtection__Store=Redis` starten, danach mit `-e DataProtection__Store=Database -e Database__MigrateOnStartup=false` gegen eine neue, leere Datenbank (`-e ConnectionStrings__aispadb="Host=db;Database=leer;Username=postgres;Password=aispa-lokal"`, vorher `docker compose exec db createdb -U postgres leer`).
Expected: Beide Male bricht der Start sofort ab, zuerst mit „DataProtection:Store „Redis“ ist ungültig; erlaubt sind FileSystem und Database.“, dann mit „DataProtection:Store=Database braucht die Tabelle system."DataProtectionKeys", sie fehlt. …“.
7. Im Repository `DataProtection__Store=Database dotnet build src/AispaStudio.Api --no-incremental` ausführen (Bash; in PowerShell `$env:DataProtection__Store = "Database"; dotnet build src/AispaStudio.Api --no-incremental; $env:DataProtection__Store = $null`), danach `git status web/openapi`.
Expected: Der Build läuft durch, `web/openapi/aispa-api.json` ist unverändert (die OpenAPI-Erzeugung gilt immer als `FileSystem`).
8. Für den Handtest mit zwei Instanzen (T-047) vormerken: eine Google- oder Apple-Anmeldung im Kundenbereich, deren Rückruf über die zweite Instanz läuft (state und Correlation-Cookie sind mit dem Schlüsselring geschützt).
Expected: Kein Schritt dieses Blocks, die Prüfung gehört in den Handtest von T-047. Nach diesem Test `docker compose down`, danach wie gewohnt mit `docker compose up -d` und `FileSystem` weiter.
Closes: – (T-042, Aufgabe C der Arbeitsanweisung Mehrinstanz-Betrieb)
Result: ☐

### T-043 – Mehrinstanz: Migration als eigener Befehl (`migrate`)
Start: `docker compose down -v`, danach `docker compose up -d db`, damit der Datenbankserver mit der leeren Datenbank `aispadb` läuft (mit `-v` gehen die Daten des Compose-Aufbaus verloren)
1. `docker compose run --rm -e Database__MigrateOnStartup=false api migrate` ausführen und danach `echo $?` (PowerShell: `$LASTEXITCODE`).
Expected: Die Ausgabe endet mit der Zeile „Migration abgeschlossen“, der Exitcode ist 0, und der Befehl kehrt von selbst zurück (kein Server, keine Zeile „Starting Wolverine“). Beim ersten Lauf stehen Zeilen mit „fail:“ von Entity Framework im Protokoll (Verlaufstabelle noch nicht da); sie sind kein Fehler.
2. `docker compose exec db psql -U postgres -d aispadb -c '\dn'`
Expected: Die Schemata `aispa`, `online`, `health`, `hr` und `sync` sind da, `wolverine` fehlt (das legt erst der Start einer Instanz an).
3. Den Befehl aus Schritt 1 noch einmal ausführen.
Expected: Exitcode 0 und „Migration abgeschlossen“, keine neue Migration im Protokoll.
4. `docker compose run --rm -e Database__MigrateOnStartup=false -e "ConnectionStrings__aispadb=Host=db;Database=aispadbx;Username=postgres;Password=aispa-lokal" api migrate`, danach `docker compose exec db psql -U postgres -c '\l'`.
Expected: Exitcode 1 und eine Meldung „Die Datenbank „aispadbx“ gibt es nicht. Zuerst anlegen …“; die Datenbank `aispadbx` wurde nicht angelegt.
5. Zuerst einen Schlüssel ins Volume `api-keys` bringen: `docker compose run --rm -e Database__SeedDemoData=false api` starten und nach „Now listening on“ mit Strg+C beenden. Danach `docker compose run --rm -e Database__MigrateOnStartup=false -e DataProtection__Store=Database api migrate` ausführen.
Expected: Exitcode 0; die Protokollzeile „1 Schlüssel aus /home/app/.aspnet/DataProtection-Keys übernommen“ (N ab 1) steht vor „Migration abgeschlossen“, und `docker compose exec db psql -U postgres -d aispadb -c 'SELECT count(*) FROM system."DataProtectionKeys"'` zeigt mindestens 1.
5a. Den Befehl aus Schritt 5 noch einmal ausführen.
Expected: Exitcode 0, die Zeile lautet „0 Schlüssel aus … übernommen“, die Zahl der Zeilen in der Tabelle ist unverändert.
6. `docker compose up -d --build` ausführen.
Expected: Die API startet wie bisher und meldet sich unter `http://localhost:5086`. Danach wie gewohnt weiter.
Closes: – (T-043, Aufgabe D der Arbeitsanweisung Mehrinstanz-Betrieb)
Result: ☐

### T-044 – Mehrinstanz: Forwarded Headers (`Hosting:ForwardedHeaders`)
Start: `docker compose up -d --build` mit einer zusätzlichen Compose-Datei (`compose.proxy.yaml`, nicht im Repository), die dem Netz das Subnetz `10.200.0.0/24` und dem Dienst `web` die feste Adresse `10.200.0.10` gibt und bei `api` `Hosting__ForwardedHeaders__Enabled=true` sowie `Hosting__ForwardedHeaders__KnownProxies=10.200.0.10` setzt (kein Netz wie `172.16.0.0/12`: es enthält das Gateway). `OnlineBooking__PublicRequestsPerMinute=5` setzen, damit das Rate Limit schnell greift.
1. Sechs Aufrufe als Client 203.0.113.7 über den Container `web` (seine Adresse steht in `KnownProxies`): `docker compose exec web wget -S -O /dev/null --header "X-Forwarded-For: 203.0.113.7" http://api:8080/api/online/anamnesis/form`, danach ein Aufruf mit `203.0.113.8`.
Expected: Die ersten fünf Aufrufe antworten mit 200 und der sechste mit „429 Too Many Requests“; der Aufruf als 203.0.113.8 antwortet danach noch mit 200 (eigener Topf je Client). Ohne die Einstellung `Enabled` zählen beide in einem Topf.
2. In `compose.proxy.yaml` bei `KnownProxies` eine andere Adresse eintragen (zum Beispiel `10.200.0.99`), `docker compose up -d api` und Schritt 1 mit einer Wartezeit von einer Minute vor dem ersten Aufruf wiederholen.
Expected: Die Kopfzeile wird ignoriert: der Aufruf als 203.0.113.8 nach den ersten fünf antwortet ebenfalls mit 429, denn alle Aufrufe zählen zur Adresse von `web`. Danach wieder die Adresse von `web` eintragen.
3. Im Browser unter `http://localhost:5086` anmelden und mit den Entwicklerwerkzeugen die Antwort der Anmeldung ansehen. Danach den Test über `https` (Ingress oder TLS-Proxy davor, der `X-Forwarded-Proto: https` setzt) wiederholen.
Expected: Hinter dem Proxy mit `https` trägt das Set-Cookie `aispa.auth` das Attribut `secure`. Bei der Google-Anmeldung im Kundenbereich beginnt die `redirect_uri` der Challenge mit `https://`. Mit `Enabled=false` fehlt `secure` weiter.
4. In `compose.proxy.yaml` zusätzlich `Hosting__Standalone=true` setzen und `docker compose up -d api`.
Expected: Der Container beendet sich mit der Meldung „Der Einzelplatz läuft nie hinter einem Proxy …“. Dasselbe gilt für `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` im Einzelplatz.
5. Beide Zusätze wieder entfernen und `docker compose up -d --build`.
Expected: Die API startet wie bisher.
Closes: – (T-044, Aufgabe E der Arbeitsanweisung Mehrinstanz-Betrieb)
Result: ☐

### T-045 – Mehrinstanz: Health-Endpunkte im Betrieb (`Hosting:HealthEndpoints`)
Start: `docker compose down -v` (nur der lokale Testbetrieb: leert die Datenbank und den Schlüsselring), dann `docker compose up -d --build` mit einer zusätzlichen Compose-Datei (`compose.health.yaml`, nicht im Repository), die bei `api` `ASPNETCORE_ENVIRONMENT=Production`, `Hosting__HealthEndpoints=true`, `Database__MigrateOnStartup=true` und `Database__SeedDemoData=true` setzt. Alternativ `docker compose run --rm -e ASPNETCORE_ENVIRONMENT=Production -e Hosting__HealthEndpoints=true -e Database__MigrateOnStartup=true -e Database__SeedDemoData=true -p 127.0.0.1:5086:8080 api`. Das Image `aspnet` hat weder `curl` noch `wget`; Aufrufe kommen vom Host oder aus dem Container `web`.
1. Vom Host sofort nach dem Start wiederholt `curl -i http://127.0.0.1:5086/health` und `curl -i http://127.0.0.1:5086/alive`, bis beide antworten.
Expected: Während der Migration kommt gar keine Antwort (Verbindung abgelehnt), denn der Server lauscht erst danach. Beim Einspielen der Beispieldaten antwortet `/health` mit „503 Service Unavailable“ und `Unhealthy`, `/alive` mit „200 OK“ und `Healthy`. Danach antworten beide mit 200 und dem Text `Healthy`, ohne weitere Zeilen und ohne Namen oder Beschreibungen.
2. Aus dem Netz der Container: `docker compose exec web wget -S -O- http://api:8080/health`.
Expected: Dieselbe Antwort wie in Schritt 1 (200 und `Healthy`).
3. Über nginx: `curl -i http://localhost:5173/health` und `curl -i http://localhost:5173/alive`.
Expected: Beide antworten mit „404 Not Found“, nginx leitet sie nicht an die API.
4. `Hosting__HealthEndpoints` aus der Compose-Datei entfernen (Umgebung bleibt `Production`), `docker compose up -d api` und Schritt 1 wiederholen.
Expected: `/health` und `/alive` antworten mit „404 Not Found“.
5. Mit `Hosting__Standalone=true` zusammen mit `Hosting__HealthEndpoints=true` starten.
Expected: Der Container beendet sich mit der Meldung „Der Einzelplatz hat keine Health-Endpunkte im Betrieb …“.
6. Beide Zusätze wieder entfernen und `docker compose up -d --build`.
Expected: Die API startet wie bisher in `Development`; `curl -i http://127.0.0.1:5086/health` zeigt den ausführlichen Bericht mit `startup: Healthy`.
Closes: – (T-045, Aufgabe F der Arbeitsanweisung Mehrinstanz-Betrieb)
Result: ☐

### T-046 – Mehrinstanz: SignalR-Backplane (`Live:Backplane`)
Start: Docker Desktop läuft; `cd web && npm install`, danach `dotnet run --project src/AispaStudio.AppHost --launch-profile https -- --Live:Backplane=Redis` (eine API-Instanz; das echte Zwei-Instanzen-Szenario kommt mit T-047), im Dashboard die Ressource `windows-app` starten und anmelden
1. Im Dashboard die Ressourcen ansehen, dann das Protokoll der Ressource `api`.
Expected: Die Ressource `redis` läuft, `api` startet erst danach. Im Protokoll steht eine Zeile „Live-Backplane: Redis; Meldungen gehen an alle Instanzen“, in keiner Zeile steht ein Kennwort oder die Verbindungszeichenfolge.
2. In der Windows-App den Kalender öffnen, einen Termin anlegen, verschieben und absagen.
Expected: Der Termin erscheint, wandert und verschwindet ohne Neuladen; die Meldungen laufen über Redis und kommen an derselben Instanz an.3. `docker exec <redis-container> redis-cli -a "<Passwort aus dem Dashboard, Ressource redis>" PUBSUB CHANNELS "aispa*"` aufrufen.
Expected: Mehrere Kanäle mit dem Präfix `aispa` (einer je Gruppe und je Verbindung).
4. Die Ressource `redis` im Dashboard stoppen und in der Windows-App einen weiteren Termin anlegen.
Expected: Der Termin wird gespeichert und erscheint nach dem Neuladen des Kalenders, aber nicht live. Im Protokoll der API steht je Meldung eine Warnung „Live-Meldung „Terminänderung“ nicht zugestellt: Redis (Live:Backplane) ist nicht erreichbar …“, keine Wiederholung, und die Ressource `api` bleibt „Healthy“.
5. `redis` wieder starten, einen Termin anlegen.
Expected: Nach kurzer Zeit (die Instanz verbindet im Hintergrund neu) kommen Meldungen wieder live an; sonst die Windows-App neu starten und melden.
6. AppHost beenden und ohne `-- --Live:Backplane=Redis` neu starten.
Expected: Es gibt keine Ressource `redis`; im Protokoll der API steht „Live-Backplane: aus; Meldungen erreichen nur die Clients dieser Instanz“, der Kalender verhält sich wie vorher.
7. `docker compose run --rm -e Live__Backplane=Redis api`, danach mit `-e Live__Backplane=1`.
Expected: Beide Male bricht der Start sofort ab, zuerst mit „Live:Backplane=Redis braucht die Verbindungszeichenfolge ConnectionStrings:redis …“, dann mit „Live:Backplane „1“ ist ungültig; erlaubt sind None (Standard) und Redis.“ Danach `docker compose down`.
8. Im Repository `Live__Backplane=Redis dotnet build src/AispaStudio.Api --no-incremental` ausführen (Bash; in PowerShell `$env:Live__Backplane = "Redis"; dotnet build src/AispaStudio.Api --no-incremental; $env:Live__Backplane = $null`), danach `git status web/openapi`.
Expected: Der Build läuft durch (die OpenAPI-Erzeugung gilt immer als `Live:Backplane=None`), `web/openapi/aispa-api.json` ist unverändert.
Closes: – (T-046, Aufgabe G der Arbeitsanweisung Mehrinstanz-Betrieb)
Result: ☐

### T-047 – Mehrinstanz: Compose-Profil mit zwei API-Instanzen, Worker und Redis (`compose.scale.yaml`)
Start: `docker compose down -v` (leert den lokalen Testbetrieb), danach `docker compose -f compose.yaml -f compose.scale.yaml up -d --build` im Repository-Ordner; Web unter `http://localhost:5173`, Mails unter `http://localhost:8025`. Die API hat in diesem Aufbau keinen Port 5086: die Windows-App meldet sich an `http://localhost:5173` an (nginx leitet `/api` und `/hubs` weiter). Das Beispiel-Spa legt der Worker an; die Testkonten erscheinen nach dem Einspielen, gemessen nach rund einer Minute (der Dienst `worker` wird erst dann `healthy`).
1. `docker compose -f compose.yaml -f compose.scale.yaml ps -a` ausführen.
Expected: `migrate` ist mit Code 0 beendet; `api` hat zwei Zeilen (`aispa-api-1`, `aispa-api-2`), `worker` eine, alle `healthy`; `redis`, `db`, `mailpit` laufen, `web` läuft.
2. `docker compose -f compose.yaml -f compose.scale.yaml logs api worker | grep -E "Rolle |Live-Backplane"` (PowerShell: `… | Select-String "Rolle |Live-Backplane"`).
Expected: Beide `api`-Instanzen melden „Rolle Api; Hintergrunddienste: keine“, der Worker „Rolle Worker; Hintergrunddienste: HrFileCleanup, …, MailboxPoller, ReminderScheduler, …“; alle drei „Live-Backplane: Redis; Meldungen gehen an alle Instanzen“. Kein anderer Container nennt Hintergrunddienste.
3. In der Windows-App (Server `http://localhost:5173`) als Inhaberin anmelden. Unter „Verwaltung“ › „Kassa & Geld“ › „Zahlungen“ einen eigenen Stripe-Testschlüssel (`sk_test_…`) eintragen und speichern.
Expected: Die Anmeldung klappt, der Zugang gilt als hinterlegt.
4. `docker compose -f compose.yaml -f compose.scale.yaml restart api` ausführen, in der Windows-App weiterarbeiten (Seite wechseln, „Zahlungen“ öffnen).
Expected: Die Windows-App bleibt angemeldet (kein Anmeldebildschirm), der Stripe-Zugang gilt weiter als hinterlegt. Die Live-Verbindung baut sich nach kurzer Zeit wieder auf.
5. `http://localhost:5173/dashboard/` im Browser öffnen und koppeln, in der Windows-App im selben Standort mehrere Termine nacheinander verschieben, einchecken und absagen.
Expected: Jede Änderung erscheint ohne Neuladen auf der Tafel, ob sie an `aispa-api-1` oder `aispa-api-2` hängt (Protokolle mit `docker compose … logs -f api` ansehen: die Anfragen verteilen sich auf beide).
6. `docker stop aispa-api-2` ausführen, in der Windows-App einen Termin anlegen und im Browser unter `http://localhost:5173/buchen/` eine Online-Buchung beginnen.
Expected: Beides funktioniert weiter (nur die erste Anfrage kann einen Augenblick länger dauern). Danach `docker start aispa-api-2`; sobald die Instanz `healthy` ist, bekommt sie wieder Anfragen.
7. Den Posteingang mit einem Antwortentwurf öffnen und auf **Senden** klicken.
Expected: Die Antwort kommt in Mailpit an; im Protokoll des Workers steht der Versand, in keinem der beiden `api`-Protokolle (Einzelheiten der Mail-Queue: Block T-048).
8. Nur wenn ein Google-Zugang für den Kundenbereich eingerichtet ist: Anmeldung mit Google im Kundenbereich, deren Rückruf über die zweite Instanz läuft (state und Correlation-Cookie sind mit dem Schlüsselring geschützt, vorgemerkt in T-042 Schritt 8).
Expected: Die Anmeldung schließt ab, auch wenn Hinweg und Rückruf an verschiedenen Instanzen enden. Ohne Google-Zugang entfällt der Schritt.
9. `docker compose -f compose.yaml -f compose.scale.yaml down -v`, danach `docker compose up -d --build` ohne Zusatzdatei.
Expected: Der alte Aufbau startet wie bisher mit einer API unter `http://localhost:5086`, ohne Redis und Worker; die Windows-App meldet sich dort an. Danach `docker compose down`.
Closes: – (T-047, Aufgabe H der Arbeitsanweisung Mehrinstanz-Betrieb)
Result: ☐

### T-048 – Mehrinstanz: Mail-Queue über PostgreSQL mit einem Verarbeiter im Worker
Start: `docker compose up -d --build` im Repository-Ordner, danach `docker compose stop api`. Zwei Instanzen in eigenen Terminals: `docker compose run --rm -p 127.0.0.1:5086:8080 -e Hosting__Role=Api api` und `docker compose run --rm -e Hosting__Role=Worker api`. In AISPA an der API (Port 5086) als Inhaberin anmelden; der Mailversand geht an Mailpit (`http://localhost:8025`).
1. `docker compose exec db psql -U postgres -d aispadb -c '\dt wolverine_queues.*'` ausführen.
Expected: Die Tabellen der Queues `mail-intake` und `mailbox-check` stehen im Schema `wolverine_queues`.
2. Im Posteingang einen Antwortentwurf öffnen und auf **Senden** klicken.
Expected: Die Antwort kommt in Mailpit an; im Protokoll des Workers steht der Versand, im Protokoll der API nicht.
3. Den Worker mit Strg+C beenden, eine weitere Antwort senden und 30 Sekunden warten.
Expected: In Mailpit kommt nichts an, die Antwort gilt im Posteingang noch nicht als gesendet.
4. Den Worker mit demselben Befehl neu starten und die Zeit bis zur Mail in Mailpit messen.
Expected: Die Antwort kommt nach höchstens rund 25 Sekunden an (gemessen: 4 bis 24 s).
5. Im Postfach des Spas auf **Jetzt prüfen** klicken.
Expected: Den Abruf zeigt das Protokoll des Workers, nicht das der API.
6. `docker compose stop mailpit` ausführen, damit der Versand scheitert, dann eine Antwort senden und eine halbe Minute warten.
Expected: Im Protokoll des Workers steht „Mail-Queue: … nach 4 Versuchen in der Fehlerablage.“ mit der Stufe `Error`. Die Antwort ist wieder Entwurf und zeigt in der Windows-App die Warnung „Nicht gesendet: Der Versand ist mehrmals gescheitert. …“.
7. `docker compose start mailpit`, danach die Antwort erneut senden.
Expected: Die Antwort kommt an, die Warnung ist weg.
8. Die Abfrage für den Weg zurück auf `All` aus der Hilfe (Server, Abschnitt „Mail-Queue mit mehreren Instanzen“) ausführen, bis sie 0 ergibt. Dann beide Instanzen beenden und `docker compose up -d api` (Rolle `All`) starten, dann eine Antwort senden.
Expected: Die Antwort kommt an; `SELECT count(*) FROM wolverine.wolverine_incoming_envelopes WHERE received_at LIKE 'local://mail-intake%'` ergibt danach 0. Nach dem Test `docker compose down`.
Closes: – (T-048, Mail-Queue der Arbeitsanweisung Mehrinstanz-Betrieb)
Result: ☐

### T-052 – Fehler im UI-Thread landen im Protokoll
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden; Protokollordner `%LOCALAPPDATA%\AispaStudio\daten\protokolle` im Explorer öffnen (im Einzelplatz-Betrieb)
1. Eine Weile normal arbeiten (Kalender, Kassa, Kunden öffnen) und die App beenden.
Expected: Die Datei `app.log` gibt es nicht oder sie ist unverändert; im Normalbetrieb schreibt die App nichts hinein.
2. Einen Fehler auslösen, den die App nicht abfängt (es gibt in der Oberfläche bewusst keinen Auslöser: z. B. mit einer Debug-Build-Änderung, die in einem Klick-Handler `throw new InvalidOperationException("Test")` wirft), den Knopf klicken.
Expected: Die App bleibt offen und bedienbar; in `app.log` steht ein Eintrag mit Zeit, „UI-Thread“ und „InvalidOperationException: Test“ samt Stapel.
Closes: – (T-052, Linter AV-APP-03)
Result: ☐

### T-053 – Beenden ohne Hänger
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` im Einzelplatz-Betrieb starten (lokale Datenbank), als Rezeption anmelden, den Kalender öffnen
1. Das Fenster mit dem Kreuz schließen und im Task-Manager beobachten.
Expected: Das Fenster hängt nicht; es lässt sich bewegen und bleibt bedienbar oder verschwindet sofort. Der Prozess der App und die lokale API und Datenbank (`AispaStudio.Api`, `postgres`) laufen noch kurz und enden dann von selbst.
2. Beim Schließen noch einmal auf das Kreuz klicken, falls das Fenster noch da ist.
Expected: Nichts Neues passiert, es gibt keine zweite Beendigung und keinen Fehler.
3. Die App neu starten und anmelden.
Expected: Die Daten sind da; die Datenbank wurde geordnet gestoppt (kein Wiederherstellungshinweis von PostgreSQL in `protokolle\datenbank.log`).
4. Im Task-Manager den Prozess der lokalen API (`AispaStudio.Api`) beenden, die App danach schließen.
Expected: Die App endet trotzdem, spätestens nach etwa 20 Sekunden, ohne eingefrorenes Fenster.
Closes: – (T-053, Linter AV-APP-07)
Result: ☐

### Rezeptions-Lücken A – Verschieben nicht in die Vergangenheit
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, im Kalender einen gebuchten Termin von morgen anklicken
1. Im Terminfenster **Verschieben …** klicken.
Expected: Das Feld „Tag“ zeigt den Termintag; **−1 Tag** ist bedienbar.
2. Bei „Tag“ das Datum von gestern eintippen und das Feld verlassen.
Expected: Das Feld meldet das Datum als zu früh, **Verschieben** ist gesperrt; beim Zeigen darauf steht „Verfügbar für heute oder einen späteren Tag.“
3. Das Datum auf heute setzen.
Expected: **Verschieben** ist wieder bedienbar, **−1 Tag** ist gesperrt.
4. Mit einem zweiten Werkzeug (z. B. `curl`) `POST /api/appointments/{id}/move` mit einem Beginn von gestern senden.
Expected: Antwort 409 mit „Termine lassen sich nicht in die Vergangenheit verschieben. Bitte heute oder einen späteren Tag wählen.“; der Termin bleibt an seinem Platz.
Closes: L2 (live-tests-rezeption-onboarding.md, Abschnitt 3)
Result: ☐

### Rezeptions-Lücken B – Einchecken nur am Tag des Termins, Rechnung nur eingecheckt oder erledigt
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, im Kalender auf morgen blättern und einen gebuchten Termin anklicken
1. Das Terminfenster ansehen und den Termin markieren.
Expected: Kein Knopf **Einchecken**; die Tastenleiste zeigt **E** gesperrt mit „Einchecken: nur am Tag des Termins“; Rechtsklick auf den Termin bietet kein **Einchecken**.
2. Im Terminfenster **Mehr** aufklappen.
Expected: **Erledigt** und **Nicht erschienen** sind gesperrt; beim Zeigen darauf steht „Verfügbar ab dem Tag des Termins.“
3. Zurück auf heute, einen gebuchten Termin von heute anklicken.
Expected: **Einchecken** ist da, **E** in der Tastenleiste bedienbar.
4. **Kassa** öffnen und in „Heute abzurechnen“ unter „Später heute“ die Zeile eines noch nicht eingecheckten Termins ansehen und anklicken.
Expected: Die Zeile trägt „noch nicht eingecheckt“; es erscheint „Einchecken und kassieren?“ mit **Einchecken und kassieren** und **Abbrechen**.
5. **Abbrechen** wählen, dann erneut klicken und **Einchecken und kassieren** wählen.
Expected: Nach Abbrechen ändert sich nichts; nach Bestätigen steht der Termin auf der Rechnung, im Kalender ist er eingecheckt, die Zeile hat den Zusatz nicht mehr.
6. Mit einem zweiten Werkzeug (z. B. `curl`) `POST /api/pos/bills` mit der Id eines nur gebuchten Termins senden.
Expected: Antwort 409 „Der Gast ist noch nicht eingecheckt. Zuerst im Kalender einchecken.“
Closes: L1 (live-tests-rezeption-onboarding.md, Abschnitt 3)
Result: ☐

### Rezeptions-Lücken C – Storno-Dialog mit Frist und Ausfallgebühr
Start: `dotnet run --project src/AispaStudio.AppHost`, als Inhaber unter **Verwaltung** › **Standorte** für Wien Stornofrist 24 Stunden und Ausfallgebühr 50 % einstellen; dann als Rezeption im Kalender einen gebuchten Termin von heute Nachmittag anklicken
1. **Mehr** öffnen und **Termin stornieren …** klicken.
Expected: Über dem Feld „Grund“ steht mit Warnsymbol „Absage innerhalb der Stornofrist (bis <gestern> HH:mm). Laut Regel fallen 50 % Ausfallgebühr an; belasten kann die Standortleitung.“; „Grund“ ist mit „Kurzfristig an der Rezeption storniert“ vorbelegt.
2. **Abbrechen**, dann als Inhaber anmelden und denselben Termin stornieren wollen.
Expected: Der Hinweis endet mit „Belasten unter „Ausfallgebühr belasten …“ nach dem Stornieren.“
3. Einen gebuchten Termin in zwei Wochen anklicken, **Mehr** öffnen und **Termin stornieren …** klicken.
Expected: Keine Hinweiszeile, „Grund“ ist mit „An der Rezeption storniert“ vorbelegt.
4. Die Ausfallgebühr des Standorts auf 0 % stellen und einen Termin von heute Nachmittag stornieren wollen.
Expected: Nur „Absage innerhalb der Stornofrist (bis <gestern> HH:mm).“
Closes: L3 (live-tests-rezeption-onboarding.md, Abschnitt 3)
Result: ☐

### Rezeptions-Lücken D – Rückfrage vor „krank melden“
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Standortleitung anmelden, Kalender mit Team-Spalten
1. Rechtsklick auf den Spaltenkopf einer Person mit offenen Terminen, **… krank melden** wählen.
Expected: Rückfrage „<Name> krank melden?“ mit dem Datum des Tages, Knopf **Krank melden** rot, Fokus auf **Abbrechen**.
2. **Abbrechen** wählen.
Expected: Der Spaltenkopf ist unverändert, kein Krankenstand, kein Assistent.
3. Erneut wählen und **Krank melden** bestätigen.
Expected: Spaltenkopf „krank gemeldet“, Meldung unten, der Ausfall-Assistent öffnet sich.
4. Rechtsklick auf eine Pause und auf eine Sperrzeit, jeweils **… löschen** wählen.
Expected: Rückfrage „Pause löschen?“ bzw. „Sperrzeit löschen?“ mit der Zeit; erst **Löschen** entfernt den Eintrag.
Closes: L10 (live-tests-rezeption-onboarding.md, Abschnitt 3)
Result: ☐

### Rezeptions-Lücken E – Hinweis „Keine Kontaktmöglichkeit“ beim Buchen
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, unter **Kunden** eine Kundin nur mit Namen anlegen (ohne Telefon und E-Mail), dann im Kalender **Termin** wählen
1. Im Buchungsbereich die neue Kundin suchen und wählen.
Expected: Unter dem Gast steht die Pille **Keine Kontaktmöglichkeit**; beim Zeigen darauf „Ohne Telefon oder E-Mail gehen keine Erinnerung und keine Zusage hinaus, bis der Kontakt in der Kundenkarte ergänzt ist“; **Buchen** bleibt möglich.
2. **Ändern** und eine Kundin mit Telefon wählen.
Expected: Keine Pille.
Closes: L5 (live-tests-rezeption-onboarding.md, Abschnitt 3)
Result: ☐

### Rezeptions-Lücken F – Toast beim Buchen, Taste V, Automation-Namen
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, Kalender
1. **Termin** klicken, Gast und Behandlung wählen, **Buchen**.
Expected: Unten erscheint „Gebucht: <Gast>, <Tag> <HH:mm>“ wie nach der Freie-Zeit-Suche.
2. Einen gebuchten Termin anklicken (markieren) und **V** drücken.
Expected: Das Terminfenster öffnet sich mit der Verschieben-Karte (Tag, Beginn).
3. Einen eingecheckten Termin markieren und **V** drücken.
Expected: Nur der Hinweis „Verschieben: ziehen oder Alt + Pfeiltasten, zurück mit Strg+Z“.
4. **F1** drücken.
Expected: In der Gruppe „Markierter Termin“ steht **V** „Verschieben-Karte: anderer Tag oder Beginn“.
5. Mit einem UI-Automation-Werkzeug (z. B. Accessibility Insights) den Knopf **Termin** und den Text der Meldung unten ansehen.
Expected: Name „Termin“ bzw. „Meldung“.
Closes: L11, L16 (live-tests-rezeption-onboarding.md, Abschnitt 3)
Result: ☐

### Rezeptions-Lücken G – Tab „Entnahme“ nur mit Kassenaufsicht
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, **Kassa** › oben **Kassenlade** (Tagesabschluss)
1. Den Kasten rechts ansehen.
Expected: Überschrift „Einlage“, kein Reiter (es gibt nur die Einlage), darunter „Kein Umsatz. Entnehmen darf nur, wer die Kassenaufsicht hat.“
2. Abmelden, als Standortleitung anmelden, dieselbe Stelle öffnen.
Expected: Überschrift „Einlage oder Entnahme“, Tabs **Einlage** und **Entnahme**.
Closes: L6 (live-tests-rezeption-onboarding.md, Abschnitt 3)
Result: ☐

### Rezeptions-Lücken H – Handbuch an die App angleichen
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, Leiste **KI und Hilfe** öffnen
1. „Termin buchen“ aufschlagen und den Kalenderkopf daneben vergleichen.
Expected: Das Handbuch nennt den Knopf **Termin**, so steht er im Kopf.
2. „Paar und Spa-Tag“ und „Termin stornieren“ aufschlagen, einen Teil einer Paarbuchung anklicken.
Expected: Der Kasten im Terminfenster heißt **Paar**; **Ganze Buchung stornieren** fragt nach, wie beschrieben.
3. „Gutschein einlösen“ aufschlagen und in der Kassa das Feld **Gutschein-Code** ansehen.
Expected: Beispiel und Platzhalter haben die Form `K7M3-Q9XA-B2CD`.
4. „Kunden“ aufschlagen und eine Kundenkarte öffnen.
Expected: Kein Knopf **Bearbeiten** neben dem Kopf; der Weg geht über **…** › **Bearbeiten (Name, Anrede, Kontakt, Notiz)** oder den Stift **Notiz ändern**, wie beschrieben.
Closes: L15 (live-tests-rezeption-onboarding.md, Abschnitt 3)
Result: ☐

### Befunde Kassa A1 – Rückfrage vor dem Tagesabschluss
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, **Kassa** › **Kassenlade & Abschluss**
1. Bei „Gezählter Bestand“ einen Betrag eintragen, der vom Soll abweicht (z. B. 800,00), auf **Tagesabschluss durchführen** klicken.
Expected: Rückfrage „Tagesabschluss durchführen?“, Text „Der Abschluss wird unveränderlich gespeichert. Differenz zum Soll: …“, Fokus auf **Abbrechen**.
2. **Abbrechen** (oder Esc).
Expected: Nichts gespeichert, der Bericht links heißt weiter „Zwischenbericht seit dem letzten Abschluss“, der Zählbestand steht noch im Feld.
3. In einem zweiten Fenster einen Beleg bar kassieren, dann im ersten erneut **Tagesabschluss durchführen**.
Expected: Die Differenz in der Rückfrage rechnet den neuen Beleg schon ein.
4. **Abschließen**.
Expected: „Tagesabschluss gespeichert“ mit der Differenz aus der Rückfrage.
Closes: B1 (live-sichtpruefung-2026-11-09.md)
Result: ☐

### Befunde Kassa A8 – Kassa ohne Verbindung
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, **Kassa** öffnen, in „Später heute“ einen noch nicht eingecheckten Termin suchen
1. Den API-Dienst anhalten (Verbindung kappen), den Termin anklicken und **Einchecken und kassieren** wählen.
Expected: Rückfrage „Der Termin wird eingecheckt und auf die Rechnung gelegt.“; während des Wartens läuft ein Fortschrittsbalken; danach „Server nicht erreichbar. Verbindung prüfen und erneut versuchen.“ ohne „Zielcomputer“, Host oder Port.
2. Den Termin noch einmal anklicken.
Expected: Keine Rückfrage, stattdessen „Kassieren braucht eine Verbindung. Verbindung prüfen und erneut versuchen.“
3. Die Leisten oben ansehen.
Expected: Höchstens zwei (Offline-Banner und diese Meldung); die Signatur-Warnung der Kasse fehlt.
4. API-Dienst wieder starten, kurz warten.
Expected: Das Banner verschwindet, die Warnung der Kasse steht wieder da (falls sie bestand); der Termin lässt sich einchecken und kassieren; im Protokoll `protokolle\app.log` steht die technische Ursache einmal.
Closes: B2, B14, B15 (live-sichtpruefung-2026-11-09.md)
Result: ☐

### Befunde Kassa A5 – Artikel in einer Spalte
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, **Kassa** › **Kassieren**, in der Leiste „Artikel und Gutscheine“ aufklappen
1. Unter „Artikel“ alle Kacheln ansehen.
Expected: Eine Spalte, jeder Artikelname ganz und in einer Zeile (z. B. „Gesichtscreme Hydro 50 ml“), der Preis darunter; kein Wort ist mitten abgebrochen.
2. Unter „Gutschein verkaufen“ den Knopf ansehen.
Expected: „Wertgutschein verkaufen“ steht ganz da (bei Platzmangel in zwei Zeilen), ohne „…“.
3. Einen Artikel anklicken.
Expected: Er steht auf der Rechnung.
Closes: B6 (live-sichtpruefung-2026-11-09.md)
Result: ☐

### Befunde Kassa B19 – Kassenlade ohne einzelnen Reiter
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, **Kassa** › **Kassenlade & Abschluss**
1. Den Kasten rechts unter „Tagesabschluss“ ansehen.
Expected: Überschrift „Einlage“, darunter gleich die Felder **Betrag** und **Grund**; kein Reiter „Einlage“.
2. Abmelden, als Standortleitung anmelden, dieselbe Stelle öffnen.
Expected: Überschrift „Einlage oder Entnahme“ mit den Reitern **Einlage** und **Entnahme**.
Closes: B19 (live-sichtpruefung-2026-11-09.md)

### Kalender A2 – Toasts mit kurzem Zeitpunkt
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, **Kalender**
1. Einen Termin für einen Gast mit langem Namen (z. B. Elif Demir, Behandlung im Hand- und Fußpflegeplatz) für heute buchen.
Expected: Meldung „Gebucht: Elif Demir, 14:35“ ohne „…“ am Ende.
2. Den Termin stornieren und in der Meldung **Rückgängig** klicken.
Expected: „Wieder gebucht: Elif Demir, 14:35“ in einer Zeile.
3. Einen Termin auf einen anderen Tag ziehen (7-Tage-Ansicht), dann **Rückgängig**.
Expected: „Verschoben auf Do 14:35“, danach „Zurück auf 14:35“ (am heutigen Tag nur die Uhrzeit).
Closes: B3, B4 (live-sichtpruefung-2026-11-09.md)
Result: ☐

### Kalender A4 – „Neuer Termin“ mit festem Fuß
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, Fenster nicht höher als Full HD, **Kalender**
1. Eine freie Stelle anklicken (Panel **Neuer Termin**), einen Gast und eine Behandlung wählen, als Beginn eine belegte Zeit eintragen, **Termin buchen** klicken.
Expected: „Zeitraum ist belegt.“ steht ganz sichtbar direkt über **Termin buchen**; beide bleiben am unteren Rand des Panels, auch wenn die Eingaben darüber scrollen.
2. Einen Gast mit früherer Behandlung wählen.
Expected: In der Karte **Vorschläge** stehen **Übernehmen** bzw. **Freie Zeit suchen** unter dem Text, der Text bricht nicht auf fünf Zeilen.
3. Strg+S drücken.
Expected: Bucht wie ein Klick auf **Termin buchen**.
Closes: B7, B13 (live-sichtpruefung-2026-11-09.md)
Result: ☐

### Kalender A6 – Texte der Rückfragen
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, **Kalender**
1. **Neuer Termin** öffnen, eine Notiz tippen, mit dem Kreuz schließen.
Expected: „Änderungen verwerfen?“ mit dem Text „Die Eingaben gehen beim Schließen verloren.“, Antworten **Verwerfen** und **Weiter bearbeiten**.
2. Einen Termin an einem anderen Tag anklicken, **Stornieren** wählen.
Expected: „Termin stornieren?“ mit „Der Termin von … am Do. 12.11., 14:35–15:20, ist danach storniert …“ (kurzes Datum).
Closes: B9, B10 (live-sichtpruefung-2026-11-09.md)
Result: ☐

### Kalender – kleine Textbefunde B20, B23, B24
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, **Kalender**
1. **Neuer Termin** öffnen, die Behandlung „Anti-Aging-Gesichtsbehandlung mit Gesichtsmassage“ wählen.
Expected: Die Vorschau im Kalender bricht den Namen um, statt mit „…“ zu enden.
2. Rechtsklick auf eine freie Stelle › **Sperrzeit hier**, den Grund leer lassen, **Sperren** klicken.
Expected: Unter dem Feld **Grund** steht „Der Grund fehlt.“; keine rote Leiste.
3. Einen Termin für gestern buchen bzw. auf gestern verschieben (Tag per Hand eintippen).
Expected: Meldung „Termine lassen sich nicht in der Vergangenheit buchen. Möglich ist heute oder ein späterer Tag.“ (beim Verschieben „… verschieben …“).
Closes: B20, B23, B24 (live-sichtpruefung-2026-11-09.md)

### Befunde Kunden A3 – E-Mail-Fehler am Feld, Liste ohne hängendes Komma
Start: `dotnet run --project src/AispaStudio.AppHost`, als Rezeption anmelden, **Kunden** › **Neue Kundin / neuer Kunde**
1. Im Nachnamen „Probe“ eintragen, im E-Mail-Feld „keine-adresse“ tippen und mit Tab verlassen.
Expected: Unter dem E-Mail-Feld steht rot „Keine gültige E-Mail-Adresse. Mit @ angeben, z. B. name@beispiel.at.“; die Leiste über der Kundenliste bleibt leer.
2. Auf **Anlegen** klicken.
Expected: Der Cursor steht im E-Mail-Feld, die Fehlermeldung bleibt unter dem Feld, es wird keine Kundin angelegt, links erscheint keine rote Leiste.
3. Die Adresse in „probe@beispiel.at“ ändern und auf **Anlegen** klicken.
Expected: Der Fehler verschwindet beim Tippen, die Kundin wird angelegt.
4. Eine Kundin nur mit Nachname anlegen (kein Vorname) und in der Liste links ansehen.
Expected: Die Zeile heißt „Probe“ ohne Komma am Ende.
Closes: B5, B12 (live-sichtpruefung-2026-11-09.md)

### Einwirkzeit-Band im Kalender (flach)
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Rezeption anmelden, im Kalender einen Tag mit einer Packung oder Maske öffnen (Beispieldaten: „Reismilch-Körperpackung“, „Gesichtsbehandlung Klassik“), bei Bedarf mit dem Zoom hineingehen
1. Das Band „Einwirkzeit … Min. · frei“ im Termin der Person ansehen, zuerst hell, dann im dunklen Aussehen (Einstellungen).
Expected: Das Band ist eine flache Fläche im selben Ton wie die Streifen „Vorb.“ und „Nachb.“, ohne Schraffur, mit feinen Linien oben und unten; der Text steht klein am unteren Rand und ist gut lesbar.
2. Mit der Maus über das Band und über den Termin fahren.
Expected: Das Band ändert sich nicht; die Hover-Karte des Termins erscheint wie bisher und nennt den Ablauf.
3. Einen zweiten Termin derselben Person in die Einwirkzeit legen.
Expected: Die Spalte teilt sich in zwei Bahnen; das Band bleibt im ersten Termin flach und lesbar.
Closes: –

### B8 – Klicks gehen nicht mehr an der Hover-Karte verloren
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Rezeption anmelden, **Kalender**, Tag „Heute“ mit einem Termin im Status „Gebucht“
1. Den Zeiger auf eine Termin-Karte setzen und warten, bis die Hover-Karte erscheint; dann den Zeiger langsam nach rechts zum Seitenpanel führen und dabei über einer anderen Karte eine Sekunde stehen bleiben, bis deren Hover-Karte aufgeht.
Expected: Die Hover-Karte ist sichtbar und lesbar, auch dort, wo sie das Panel überdeckt.
2. Den Termin anklicken (Panel öffnet), den Zeiger in einem Zug auf **Einchecken** führen und klicken. Zehnmal wiederholen (jedes Mal **Einchecken zurücknehmen**).
Expected: Jedes Mal wird der Termin eingecheckt; kein Klick verpufft.
3. Den Zeiger bei geöffneter Hover-Karte auf einen Knopf im Panel führen, der unter der Karte liegt, und klicken.
Expected: Der Knopf löst aus. Die Hover-Karte reagiert nicht auf die Maus (Text darin lässt sich nicht anklicken).
4. Tooltips der Knöpfe (z. B. „Einchecken (E)“) und die Meldung unten mit **Rückgängig** ansehen.
Expected: Beide erscheinen wie bisher und sind lesbar.
Closes: B8 (live-sichtpruefung-2026-11-09.md)
Result: ☐

### Verlauf A – Endpunkt je Standort
Start: `dotnet run --project src/AispaStudio.AppHost`, Beispieldaten geladen, als Inhaberin angemeldet, Standort-Id aus `GET /api/v1/locations`; Aufrufe über die Scalar-Seite der API oder `curl` mit dem Sitzungs-Cookie
1. `GET /api/v1/locations/{id}/history?take=20` aufrufen.
Expected: 20 Einträge, neueste zuerst, `nextBefore` gesetzt; Buchungen, Verschiebungen und Stornos tragen Gast, Behandlung und Person.
2. Dieselbe Adresse mit `&before=<nextBefore>` wiederholen.
Expected: Die nächsten 20 Einträge ohne Doppelte und ohne Lücke.
3. `?kinds=Paid&kinds=Deposit&kinds=ReceiptVoided` aufrufen.
Expected: Nur Zahlungen zum Termin („Bezahlt bar: … (Beleg … Nr. …)“, „Anzahlung …“, „Beleg … storniert …“), `seq` negativ.
4. `?search=<Nachname eines Gastes>` und `?search=<Storno-Grund>` aufrufen.
Expected: Nur Einträge zu diesem Gast bzw. der Storno mit diesem Grund; Groß- und Kleinschreibung egal.
5. Als Rezeption anmelden und `?kinds=RosterChanged` aufrufen, danach ohne `kinds`.
Expected: 403 mit „… nur Inhaber und Manager.“; ohne `kinds` eine Seite ohne Dienstplan, Zugänge und Firmendaten.
6. Einen kassierten Termin im Kalender öffnen, Abschnitt „Verlauf“ aufklappen.
Expected: „Bezahlt … (Beleg … Nr. …)“ steht zwischen den übrigen Einträgen an der richtigen Stelle.
Closes: –
Result: ☐

### Verlauf B – Seite „Verlauf“
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, Navigation **Verlauf** (nach „Kunden“)
1. Die Seite öffnen.
Expected: Suchfeld, Zeitraum „Heute“ gewählt, Art-Chips „Buchungen … Zugänge“, Auswahl „Alle Kanäle“; die Liste zeigt den heutigen Tag mit Uhrzeit, Symbol, Satz und Gast/Person je Zeile.
2. „30 Tage“ wählen und bis ans Ende scrollen.
Expected: Weitere Tagesgruppen laden nach, ohne doppelte Einträge; am Fuß steht „Weitere laden“, solange es mehr gibt.
3. Chip „Stornos“ und danach „Zahlungen“ anklicken.
Expected: Nur Stornos bzw. Stornos und Zahlungen; der Chip „Zurücksetzen“ erscheint; Stornos innerhalb der Stornofrist tragen die Pille „Kurzfristig“.
4. Strg+F drücken, einen Gastnamen tippen, eine Sekunde warten.
Expected: Der Fokus steht im Suchfeld; nach etwa 300 ms lädt die Liste nur Einträge zu diesem Gast; Esc leert die Suche.
5. Einen Suchtext ohne Treffer eingeben.
Expected: „Nichts gefunden. Filter zurücksetzen?“ mit Knopf, der alles zurücksetzt.
6. Als Rezeption anmelden und die Seite öffnen.
Expected: Keine Chips „Dienstplan“ und „Zugänge“, kein Feld „Person“; Zahlungen sind sichtbar.
7. Dunkles Farbschema wählen.
Expected: Chips, Pillen und Symbole lesbar, keine hellen Flächen.
Closes: –
Result: ☐

### Verlauf C – Seitenpanel und Zähler „Storniert n“
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, heute mindestens einen Termin stornieren und einen kassieren
1. Navigation **Verlauf**, den Storno wählen, Enter drücken.
Expected: Rechts öffnet das Panel „Storno“ mit Satz, Gast, Person, Kanal, Grund und Werten; der Fokus steht auf „Stornierung zurücknehmen“.
2. Mit den Pfeiltasten auf die Zahlung wechseln.
Expected: Das Panel zeigt „Zahlung“ mit „Beleg anzeigen“ als primärem Knopf; Klick öffnet die Kassa, Reiter Belege, am Tag des Belegs.
3. Zurück zum Storno, „Stornierung zurücknehmen“, Rückfrage bestätigen.
Expected: Toast „Termin wiederhergestellt: <Gast>, <Zeit>“; die Liste lädt neu und zeigt oben „Wiederhergestellt von …“.
4. Esc drücken.
Expected: Das Panel schließt, der Fokus steht wieder in der Liste.
5. Kalender öffnen, einen weiteren Termin heute stornieren.
Expected: In der Kopfzeile nach „Warteliste“ steht „Storniert 1“; Klick öffnet den Verlauf für heute mit dem Chip „Stornos“.
6. Als Rezeption ohne Buchungsrecht anmelden und einen Storno öffnen.
Expected: „Stornierung zurücknehmen“ ist gesperrt mit Hinweis auf das fehlende Recht.
Closes: –
Result: ☐

### Verlauf E – Zurücknehmen aus dem Verlauf, auch zu neuer Zeit
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden; heute einen Termin stornieren, einen zweiten stornieren und seine Zeit danach mit einem anderen Termin derselben Person belegen
1. **Verlauf**, Chip **Stornos**, den ersten Storno öffnen, **Stornierung zurücknehmen**.
Expected: Rückfrage „Stornierung zurücknehmen?“; nach **Zurücknehmen** Toast „Termin wiederhergestellt: <Gast>, <Zeit>“, der Verlauf lädt neu.
2. Den zweiten Storno öffnen, **Stornierung zurücknehmen**.
Expected: Rückfrage „Zu neuer Zeit zurücknehmen?“ mit „Die Zeit ist inzwischen belegt: …“ und **Neue Zeit wählen**.
3. **Neue Zeit wählen**.
Expected: Der Kalender springt auf den Tag des ersten freien Vorschlags; rechts das Panel des stornierten Termins mit offener Verschieben-Karte, darunter „Freie Zeiten“ für Gast, Behandlung und Person.
4. Einen Vorschlag anklicken, **Verschieben**.
Expected: Der Termin steht zur neuen Zeit wieder gebucht im Kalender.
5. Einen Storno von gestern (Zeitraum **Gestern**) zurücknehmen und bei **Neue Zeit wählen** abbrechen.
Expected: Grund „Der Termin liegt in der Vergangenheit.“; nach Abbrechen bleibt der Termin storniert und steht weiter im Verlauf.
6. Bei einem Storno **Termin öffnen**, bei einer Zahlung **Beleg anzeigen**, bei einer Behandlungsänderung **Behandlung öffnen**.
Expected: Kalender mit dem Panel des stornierten Termins; Kassa, Reiter Belege, Beleg gewählt; Verwaltung mit der Behandlung.
7. Als Rezeption anmelden, **Verlauf** öffnen.
Expected: Das Feld „Person“ nennt die Personen, die im Zeitraum etwas getan haben; „Dienstplan öffnen“ und „Zugang öffnen“ gibt es nicht.
Closes: –
Result: ☐

### Verlauf D – Hilfe zur Seite „Verlauf“
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden
1. Navigation **Hilfe** öffnen und „Verlauf“ suchen.
Expected: Die Hilfe zeigt „Seite „Verlauf“ in der Windows-App“ mit Bild, Aufbau, Pillen, Seitenpanel und Rechten.
2. In der Hilfe nach „Stornos heute“, „bezahlt bar“ und „Storno zurücknehmen“ suchen.
Expected: „Seite „Verlauf““ bzw. „Im Verlauf suchen“ steht oben in den Treffern.
3. In „Im Verlauf suchen“ den Link „Stornierung zurücknehmen“ und in „Termin stornieren“ den Link „Aus dem Verlauf zurücknehmen“ anklicken.
Expected: Beide führen zum richtigen Abschnitt.
Closes: –
Result: ☐

### Verlauf F – Verkäufe ohne Termin
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden
1. In der **Kassa** einen Gutschein oder Artikel ohne Termin bar verkaufen.
Expected: Im **Verlauf** unter **Heute**, **Zahlungen** steht „Bezahlt bar: … für <Ware> (Beleg …)“ ohne Sprung „Termin öffnen“.
2. Im Verlauf nach dem Namen der Ware suchen.
Expected: Der Verkauf (und ein Storno dazu) steht in den Treffern.
3. Als Rezeption ohne Recht „Termine buchen“ anmelden.
Expected: Der Chip **Zahlungen** und die Verkäufe fehlen.
Closes: –
Result: ☐

### Verlauf H – Anfragen im Verlauf
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden
1. Eine Test-Anfrage einspielen (Beispieldaten oder `/api/dev/mails`), dann in **Anfragen** eine Anfrage als erledigt markieren.
Expected: Im **Verlauf**, Chip **Anfragen**, stehen „… eingegangen (Mail)“ und „Anfrage als erledigt markiert von <Person>“, ohne Text der Mail.
2. Die Zeile öffnen (Enter) und **Anfrage öffnen** wählen.
Expected: Die Seite **Anfragen** zeigt die Anfrage.
3. Als Therapeutin ohne Recht „Termine buchen“ anmelden.
Expected: Kein Chip **Anfragen**, keine Anfrage-Zeilen.
Closes: –
### Export/Import B – Kunden exportieren und Werbe-Einwilligung am Gast
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden und **Kunden** öffnen
1. Im Suchfeld „hu“ eingeben, im Kopf **Exportieren** › **Excel** wählen und speichern.
Expected: Der Speicherdialog schlägt `Kunden_<Datum>.xlsx` vor; unten steht kurz „Gespeichert: Kunden_<Datum>.xlsx“. In Excel stehen nur die Treffer der Suche, Kopfzeile fett mit Filter, Geburtstag als Datum; es gibt die Spalte „Vermerk“, aber keine Wünsche, Gesundheit, Rabatt oder Rechnung.
2. **Exportieren** › **CSV** speichern und die Datei in Excel per Doppelklick öffnen.
Expected: Umlaute sind richtig, die Spalten sind getrennt (Strichpunkt), das Datum steht als TT.MM.JJJJ.
3. **Exportieren** › **vCard** speichern und die Datei in Outlook (oder Kontakte) importieren.
Expected: Je Gast ein Kontakt mit Name, Telefon, E-Mail, Geburtstag und Anschrift.
4. Einen Gast wählen, im Menü **…** › **Kontakt als vCard** wählen und die Datei öffnen.
Expected: Outlook zeigt den einen Kontakt; der Dateiname ist `Kontakt_<Nachname>.vcf`.
5. In der Karte im Abschnitt **Werbung per Mail** den Stift klicken, **Ja** wählen, **Speichern** klicken und in der Rückfrage „Hat der Gast zugestimmt?“ **Erfassen** wählen.
Expected: Die Rückfrage nennt die Newsletter-Liste und „mündlich oder schriftlich zugestimmt“; danach zeigt der Abschnitt „Ja, am Empfang erfasst am <heute> von <Name>“, unten steht kurz „Werbe-Einwilligung gespeichert“; unter **Änderungen** steht „Werbe-Einwilligung: ja von <Name>“.
6. Die Suche leeren und **Exportieren** › **Newsletter-Liste** speichern.
Expected: Die Datei enthält nur Gäste mit „Ja“, Spalten E-Mail, Vorname, Nachname, Sprache, Anrede.
7. **Exportieren** › **Portal-CSV** speichern und im Editor öffnen.
Expected: Erste Zeile `First name,Last name,Email,Mobile,Date of birth,Notes,Marketing consent`, getrennt mit Komma, Geburtstag als JJJJ-MM-TT.
8. Abmelden, als Rezeption anmelden, **Kunden** › **Exportieren** öffnen.
Expected: Excel, CSV, Portal-CSV und vCard stehen da, die Newsletter-Liste nicht; im Excel fehlt die Spalte „Vermerk“.
Closes: Export/Import Aufgabe A und B (arbeitsanweisung-export-import.md)
Result: ☐

### Export/Import C – Terminliste und Kalenderdateien
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Kalender** in der Wochenansicht öffnen
1. Am Knopf **Termin** den Pfeil klicken und **Terminliste exportieren …** wählen.
Expected: Der Dialog „Terminliste exportieren?“ zeigt Von und Bis mit Montag und Sonntag der gezeigten Woche, Excel ist gewählt, der Fokus liegt auf **Exportieren**.
2. Bis auf einen Tag vor Von setzen und **Exportieren** klicken.
Expected: Der Dialog bleibt offen, unter „Bis“ steht „Bis liegt vor Von.“.
3. Bis wieder auf Sonntag setzen, **Exportieren** klicken und die Datei speichern.
Expected: Der Speicherdialog schlägt `Termine_<Montag>.xlsx` vor; unten steht kurz „Gespeichert: …“. In Excel: Tag als Datum, Beginn und Ende als Uhrzeit, Preis als Betrag, Status deutsch (Gebucht, Erledigt …), dazu die Spalte „Notiz“.
4. Einen Termin öffnen, im Panel **Mehr** aufklappen und **Kalenderdatei …** wählen; die Datei speichern und in Outlook (oder Apple Kalender, Google Kalender) öffnen.
Expected: Ein Termin mit Behandlung als Titel und dem Standort als Ort, zur richtigen Ortszeit, mit Erinnerung einen Tag vorher; kein Name der Person. Bei einer Buchung für einen Spa-Tag stehen alle Teile des Gastes als einzelne Termine darin.
5. Dieselbe Datei ein zweites Mal öffnen.
Expected: Der Kalender ersetzt den Termin, statt ihn doppelt einzutragen.
6. **Team** öffnen, eine Person wählen, Reiter **Dienstplan** und unten **Kalenderdatei …** speichern; die Datei in einem Kalender öffnen.
Expected: Neben dem Knopf steht „Gespeichert: Kalender_<Name>.ics“. Im Kalender stehen die Dienste der nächsten 90 Tage als „Dienst <Standort>“ und die Termine als „<Behandlung> (<Raum>)“, ohne Namen der Gäste und ohne Erinnerung.
7. Abmelden, als Rezeption anmelden, **Team** › eine andere Person › **Dienstplan** öffnen.
Expected: Der Knopf **Kalenderdatei …** fehlt; im Kalender exportiert die Terminliste ohne Spalte „Notiz“.
Closes: Export/Import Aufgabe C (arbeitsanweisung-export-import.md)
Result: ☐

### Export/Import D – Listen von Kassa, Lager, Personal und Berichten
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Kassa** öffnen und eine Kasse wählen
1. Im Bereich **Belege** einen Tag mit Belegen wählen, in der Kopfzeile **Exportieren** › **Belegliste (Excel)** wählen und speichern.
Expected: Der Speicherdialog schlägt `Belege_<Tag>.xlsx` vor; daneben steht „Gespeichert: …“. In Excel: Kasse, Nummer, Tag als Datum, Zeit als Uhrzeit, Zahlarten, Betrag als Betrag, Storno („storniert“ bzw. „Storno zu Nr. …“), Gast; Autofilter in der Kopfzeile.
2. **Exportieren** › **Umsatz je Tag (CSV)** speichern und in Excel öffnen.
Expected: Umlaute richtig, Spalten durch Strichpunkt getrennt, Beträge mit Komma.
3. **Exportieren** › **Gutscheinbestand (Excel)** und **Tagesabschlüsse (Excel)** speichern.
Expected: Gutscheine mit Code, Art, Wert, Rest, Gültig bis, Status und Käufer; Tagesabschlüsse mit Soll, Gezählt und Differenz.
4. **Verwaltung** › **Lager** öffnen, **Exportieren** › **Bestand je Lagerort (Excel)** und **Lagerbewegungen (Excel)** speichern.
Expected: Bestand je Material und Lagerort; Bewegungen der letzten 30 Tage bis heute.
5. **Team** › **Zeiten** öffnen, **Exportieren** › **Stundenliste (Excel)** speichern.
Expected: `Stundenliste_<Monatserster>.xlsx` mit Person, Tag, Soll, Ist, Pause und Differenz für den Monat, in dem die gezeigte Woche beginnt.
6. **Team** › **Wochenplan** öffnen, **Exportieren** › **Dienstplan der Woche (Excel)** speichern.
Expected: Personen als Zeilen, Montag bis Sonntag als Spalten („Mo 1.3.“), Dienste als „9:00–17:00“, Abwesenheiten als „Urlaub“ usw., ganz rechts die Stunden.
7. **Zahlen** › **Berichte** › **Management** öffnen, **Exportieren** › **Bericht (Excel)** speichern.
Expected: Das erste Blatt „Übersicht“ hat als erste vier Spalten Kennzahl, Wert, Vorperiode und Einheit mit denselben Zahlen wie auf der Seite.
8. Abmelden, als Manager eines Standorts anmelden, Kassa, Lager, Zeiten und Wochenplan öffnen.
Expected: Der Knopf **Exportieren** fehlt dort; im Bericht „Management“ ist er da.
Closes: Export/Import Aufgabe D (arbeitsanweisung-export-import.md)
Result: ☐

### Export/Import E – Kunden importieren
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Kunden** öffnen; die Beispieldatei `docs/beispiele/kunden-import-beispiel.xlsx` bereithalten
1. In der Kopfzeile **…** › **Kunden importieren …** wählen und die Beispieldatei öffnen.
Expected: Schritt 2 „Spalten zuordnen“: links die neun Spalten der Datei mit Beispielwerten, rechts vorgeschlagen Vorname, Nachname, E-Mail, Telefon, Geburtstag, Straße, PLZ, Ort, Werbe-Einwilligung; Datumsformat TT.MM.JJJJ.
2. Bei „Nachname“ „nicht importieren“ wählen und **Weiter** klicken.
Expected: **Weiter** ist gesperrt; am Feld „Zuordnung“ steht „Nachname fehlt. Bitte die Spalte mit dem Nachnamen zuordnen.“ Wieder „Nachname“ wählen.
3. **Weiter** klicken.
Expected: Schritt 3 zeigt „4 neu, 1 Dublette, 2 fehlerhaft“; die Dublette „Lena Beispiel“ mit „doppelt in der Datei (Zeile 2)“ und der Wahl **Überspringen** oder **Ergänzen**; fehlerhaft „Mia Demo“ (Geburtstag kein Datum) und Zeile 8 (Nachname fehlt).
4. **Fehlerzeilen als Excel …** speichern und öffnen.
Expected: Spalten Zeile, Grund und die Spalten der Datei, zwei Zeilen.
5. Bei der Dublette **Ergänzen** wählen, **Weiter** und **Probelauf starten** klicken.
Expected: Im Dialog „Probelauf: 4 neu, 1 ergänzt, 0 übersprungen, 2 fehlerhaft. Es wurde nichts gespeichert.“; in der Kundenliste ist noch niemand neu.
6. **Importieren** klicken.
Expected: Der Dialog schließt, Hinweis „Importiert: 4 neu, 1 ergänzt, 0 übersprungen, 2 fehlerhaft“; die Kundenliste zeigt Lena Beispiel, Paul Probe, Sophie Muster und Jonas Erfunden. In der Karte von Lena Beispiel steht im Verlauf „Angelegt beim Import von …“ und unter „Werbung per Mail“ eine Einwilligung aus dem Import; bei Paul Probe keine.
7. Dieselbe Datei noch einmal importieren bis Schritt 3.
Expected: Die vier Gäste sind jetzt Dubletten im Bestand („gleicher Nachname und gleiche Telefonnummer wie …“ bzw. „… E-Mail-Adresse wie …“).
8. Abbrechen, abmelden, als Manager anmelden, **Kunden** öffnen.
Expected: Unter **…** fehlt „Kunden importieren …“.
Closes: Export/Import Aufgabe E (arbeitsanweisung-export-import.md)
### PDF-Ausgaben A – Grundlage: Personalakte über den Dokument-Dienst
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, **Verwaltung › Personen**, eine Person mit Einträgen in der Personalakte öffnen, Reiter **Personalakte**
1. **Als PDF exportieren** klicken, im Speicherdialog den vorgeschlagenen Namen ansehen, speichern und die Datei öffnen.
Expected: Der vorgeschlagene Name ist `Personalakte-<Name>.pdf`. Oben links steht der Spa-Name groß, darunter nichts (kein Standort), rechts klein in Grau die Firmendaten (Firmenwortlaut, Anschrift, UID), darunter eine Linie; dann „Personalakte: <Name>“, der Satz „Auskunft nach Art. 15 DSGVO, erstellt am …“ in Grau und die Einträge wie bisher (Überschrift je Eintrag, Vereinbarungen mit `[x]`/`[ ]`).
2. Den Fuß der Seite ansehen.
Expected: Links „Erstellt am <Datum> <Uhrzeit> von <Vorname der angemeldeten Person>“, rechts „Seite 1 von 1“; Umlaute, „€“ und „–“ in den Texten sind richtig gesetzt; nur Schwarz und Grau, keine Farben.
3. In den Firmendaten (Verwaltung › Firmendaten) die UID ändern und den Export wiederholen.
Expected: Die neue UID steht rechts oben im Kopf.
Closes: Arbeitsanweisung PDF-Ausgaben, Aufgabe A (der Knopfblock „Drucken · PDF speichern · Per Mail senden“ erscheint erst mit Aufgabe B in der Kassa)
Result: ☐


### PDF-Ausgaben B – Beleg und Rechnung als PDF, per Mail senden
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, **Kassa**, einen Gast mit Mailadresse in der Kundenkarte abrechnen und bar bezahlen
1. Unter dem Beleg in der Mitte die Knöpfe ansehen.
Expected: Zuerst „Drucken“ (Bondrucker, ohne Bondrucker gesperrt mit Hinweis auf „Geräte“), darunter „A4 drucken“, „PDF speichern“ mit Pfeil und „Per Mail senden“; alle Knöpfe weiß mit Rahmen.
2. **PDF speichern** klicken, speichern und die Datei öffnen; dann am Pfeil **PDF im Bon-Format speichern** wählen und auch diese Datei öffnen.
Expected: Vorschlag `Beleg-<Kasse>-<Nr>.pdf`; A4 mit Belegkopf, Positionen, Summe, Steuerzeile, Zahlung mit Rückgeld und QR-Code „Signierter Belegcode (RKSV)“; das Bon-Format ist 80 mm schmal mit demselben Inhalt. Unten erscheint kurz „Gespeichert: Beleg-….pdf“.
3. **Per Mail senden** klicken, Adresse und Betreff ansehen, **Senden**.
Expected: Die Adresse aus der Kundenkarte ist vorbelegt, Betreff „Ihr Beleg Nr. … von <Spa>“; danach Toast „Gesendet an …“ und unter dem Beleg „Per Mail gesendet an … am <Datum Uhrzeit> von <Vorname>“. Im Postfach (Mailpit) liegt die Mail mit dem PDF im Anhang.
4. Reiter **Belege**, denselben Beleg wählen, **Empfänger erfassen** (Firma, Anschrift, UID), dann **PDF speichern**.
Expected: Vorschlag `Rechnung-<Kasse>-<Nr>.pdf`; Titel „Rechnung Nr. <Kasse>-<Nr>“, „Rechnung an“ mit Firma, Anschrift und UID, „Rechnungs- und Leistungsdatum“, darunter „Kopie“; der Vermerk der Mail aus Schritt 3 steht auch hier.
5. Einen Beleg stornieren und den Stornobeleg als PDF speichern.
Expected: „Stornobeleg Nr. …“ mit dem Grund und negativen Beträgen.
6. Als Therapeutin (ohne „Kassieren“ und „Belege ansehen“) anmelden und die Kassa suchen.
Expected: Belege und ihre PDFs sind nicht zu sehen; die API antwortet auf `…/pdf` und `…/send` mit 403.
Closes: Arbeitsanweisung PDF-Ausgaben, Aufgabe B
Result: ☐

### PDF-Ausgaben C – Terminbestätigung und Datenschutz-Auskunft
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Rezeption anmelden; im Standort Anschrift, Stornofrist und Hinweistext eingetragen, bei einer Behandlung einen Gästehinweis („Bitte Badekleidung mitbringen.“), ein Gast mit Mailadresse und einem Termin
1. Den Termin im Kalender anklicken, im Seitenpanel **Mehr** aufklappen und **Bestätigung …** klicken.
Expected: Unter „Mehr“ erscheint die Karte „Terminbestätigung“ mit „Drucken“, „PDF speichern“ und „Per Mail senden“; ein zweiter Klick auf „Bestätigung …“ schließt sie. Bei einem stornierten Termin fehlt der Knopf.
2. **PDF speichern** klicken, speichern und die Datei öffnen.
Expected: Vorschlag `Terminbestätigung-<TT-MM-JJJJ>.pdf`; Titel „Terminbestätigung“, Gast, Tag mit Wochentag, Tabelle mit Zeit, Behandlung, Dauer, nur dem Vornamen der Person und Preis, „Wo“ mit Standort und Anschrift, „Absagen“ mit Datum und Uhrzeit der Stornofrist und dem Hinweistext, „Was mitbringen“ mit dem Gästehinweis; unten „Erstellt am … von <Vorname>“.
3. **Per Mail senden** klicken, Adresse und Betreff ansehen, **Senden**; danach im Panel **Verlauf** aufklappen.
Expected: Die Adresse aus der Kundenkarte ist vorbelegt, Betreff „Ihre Terminbestätigung: <Behandlung> am <TT.MM.JJJJ>“; Toast „Gesendet an …“; im Verlauf steht oben „Bestätigung per Mail an … gesendet von <Vorname>“ (ein schon geöffneter Verlauf lädt von selbst neu). Im Postfach (Mailpit) liegt die Mail mit dem PDF im Anhang.
4. Ein Paar oder einen Spa-Tag mit zwei Teilen buchen und die Bestätigung eines Teils speichern.
Expected: Beide Teile stehen in der Tabelle, mit Spalte „Gast“ und einer Zeile „Summe“.
5. **Kunden** öffnen, einen Gast mit Terminen, Zahlungen und (wenn vorhanden) Anamnese wählen, im Menü **…** **Auskunft als PDF** klicken, **PDF speichern**.
Expected: Unter dem Kopf der Karte erscheint „Auskunft über gespeicherte Daten“ mit „Drucken“ und „PDF speichern“ (ohne „Per Mail senden“), darunter nach dem Speichern „Gespeichert: Auskunft-….pdf“. Das PDF zeigt Stammdaten, Kontakt, Wünsche und Vermerke, Termine mit Status, Zahlungen, Gutscheine auf den Namen, Online-Konto, Gesundheitsangaben (mit Recht die Antworten des Bogens) und den Verlauf der Änderungen; Seitenumbruch mit wiederholter Kopfzeile, „Seite n von m“. Wählt man einen anderen Gast, schließt die Karte.
6. Als Therapeutin anmelden und die Kundenkarte öffnen.
Expected: Das Menü „…“ fehlt (kein Recht „Buchen“); die API lässt das Lesen der Bestätigung und der Auskunft wie das Lesen des Termins bzw. der Kundenkarte zu.
Closes: Arbeitsanweisung PDF-Ausgaben, Aufgabe C
Result: ☐

### PDF-Ausgaben D – Tagesabschluss als PDF
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden; in der Kassa einige Verkäufe (bar und Karte) und je eine Einlage und Entnahme mit Grund gebucht
1. **Kassa** öffnen, Reiter **Kassenlade & Abschluss**, bei „Gezählter Bestand“ einen Betrag eintragen, der um einige Euro vom Soll abweicht, eine Notiz eintragen und **Tagesabschluss durchführen** bestätigen.
Expected: Die Karte „Tagesabschluss gespeichert“ erscheint, rechts oben die Karte „Tagesabschluss als PDF“ mit „Drucken“ und „PDF speichern“ (ohne „Per Mail senden“). Vor dem Abschluss (Zwischenbericht) fehlt die Karte.
2. **PDF speichern** klicken, speichern und die Datei öffnen.
Expected: Vorschlag `Tagesabschluss-<Kasse>-<JJJJ-MM-TT>.pdf`; Titel „Tagesabschluss <Kasse>“, Zeitraum, Belege von–bis mit Anzahl Verkäufe und Stornos, Summen je Zahlart mit „Umsatz gesamt“, Steuern, Kassenlade mit Soll-Bestand, gezähltem Bestand und Differenz, die Notiz, die Einlage und die Entnahme mit Grund, unten „Gezählt von“ und „Geprüft von“ als Unterschriftenzeilen; Fuß „Erstellt am … von <Vorname>“. Toast: „Gespeichert: Tagesabschluss-….pdf“.
3. **Drucken** klicken.
Expected: Das PDF geht an den Standarddrucker von Windows (nicht an den Bondrucker); Toast „Gedruckt“.
4. Als Rezeption ohne das Recht „Berichte mit Beträgen“ einen Abschluss durchführen.
Expected: Die Knöpfe „Drucken“ und „PDF speichern“ sind gesperrt, der Hinweis nennt das Recht „Berichte mit Beträgen“; die API antwortet auf `…/day-closings/<id>/pdf` mit 403.
Closes: Arbeitsanweisung PDF-Ausgaben, Aufgabe D
Result: ☐

### PDF-Ausgaben E – Tagesplan, Gutschein, Stammblatt und Warteliste als PDF
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Rezeption anmelden; im Kalender sind für heute Termine bei mindestens zwei Personen und eine Pause eingetragen, auf der Warteliste stehen zwei Wünsche (einer mit Telefonnummer), eine Kundin hat Adresse, Wünsche und ein paar erledigte Besuche
1. Im Kalender **+ Termin ▾** öffnen und **Tagesplan drucken …** wählen.
Expected: Unter dem Knopf erscheint die Karte „Tagesplan drucken“ mit dem Tag, der Wahl „Team“ und „Räume“ (die Ansicht des Kalenders ist vorgewählt) und den Knöpfen „Drucken“ und „PDF speichern“ (ohne „Per Mail senden“). Ein zweiter Klick auf den Menüpunkt oder das Kreuz schließt sie; ein Wechsel des Tages schließt sie ebenfalls.
2. **PDF speichern** (Team), die Datei öffnen; danach „Räume“ wählen und noch einmal speichern.
Expected: Vorschlag `Tagesplan-<Standort>-<JJJJ-MM-TT>.pdf`; A4 quer, Titel „Tagesplan <Wochentag>, <Datum>“, „Nach Team · n Termine“; je Person eine Spalte, Zeilen nach Beginn, in der Zelle Zeit, Gast, Behandlung und Status; Pausen und Sperrzeiten grau hinterlegt; stornierte Termine fehlen; unten „n Personen auf der Warteliste“ („1 Person“, „Niemand“). Die Raumansicht hat eine Spalte je Raum und nennt die Person hinter der Behandlung. Toast „Gespeichert: …“.
3. **Warteliste ansehen** öffnen.
Expected: Oben in der Leiste steht „Druckt die offenen Wünsche, die am <Tag> gelten.“ mit „Drucken“ und „PDF speichern“. Gespeichert: `Warteliste-<Standort>-<JJJJ-MM-TT>.pdf` mit Gast, Telefon, Wunsch, Zeitraum (darunter die Tageszeit) und Stand („Wartet“ oder „n× freien Termin gemeldet“); nur Wünsche, deren Zeitraum den Tag einschließt; ohne solche „Keine offenen Wünsche an diesem Tag.“.
4. **Kunden** › Kundin öffnen › **…** › **Stammblatt als PDF**.
Expected: Die Karte „Stammblatt“ mit „Drucken“ und „PDF speichern“ erscheint (ohne Senden); ein Wechsel der Kundin schließt sie. PDF `Stammblatt-<Name>.pdf`: Kontakt mit Anschrift, Wünsche und Vermerke, „Nächste Termine“ (höchstens 5, nicht stornierte), „Letzte Besuche“ (die letzten 5 erledigten); keine Anamnese-Antworten, bei vorhandenem Bogen nur „Gesundheitsangaben: vorhanden, Auskunft durch die Leitung“.
5. In der **Kassa** einen Gutschein verkaufen (Wert 50,00 € bar), nach dem Bezahlen unter dem Beleg **Gutschein <Code> als Geschenk** ansehen und **PDF speichern**.
Expected: Unter dem Beleg ein eigener Knopfblock je Gutschein mit „Drucken“, „PDF speichern“, „Per Mail senden“. PDF `Gutschein-<Code>.pdf`: A4, Spa-Name groß, „Geschenkgutschein“, „50,00 €“, der Code mit QR-Code, „Gültig bis: ohne Befristung“, „Einlösbar in <Standorte>“, „Ausgestellt am“. Ein Behandlungsgutschein zeigt statt des Betrags die Behandlung.
6. **Per Mail senden** beim Gutschein: die Adresse der Käuferin ist vorbelegt (leer, wenn der Beleg keinen Gast hat); eine eigene Adresse eintippen und bestätigen.
Expected: Rückfrage mit Adresse, Betreff „Ihr Gutschein von <Spa>“ und Text; nach dem Senden Toast „Gesendet an …“, die Mail kommt mit dem PDF im Anhang an; eine ungültige Adresse wird abgelehnt.
7. Den verkauften Beleg stornieren und den Gutschein noch einmal als PDF speichern.
Expected: Auf dem PDF steht „Dieser Gutschein ist storniert und nicht mehr einlösbar.“.
8. Als Backoffice (ohne Kalender) die Adresse `…/api/locations/<id>/day-plan/pdf?date=<Tag>` aufrufen, als Therapeut `…/api/pos/vouchers/<id>/pdf`.
Expected: Beide antworten mit 403; ein Therapeut darf Tagesplan, Stammblatt und Warteliste drucken, das Backoffice nur Gutschein und Beleg.
Closes: Arbeitsanweisung PDF-Ausgaben, Aufgabe E
Result: ☐

### PDF-Ausgaben F – Dienstplan-Aushang, Stundenliste, Inventurliste, Bestellung und Ablaufliste als PDF
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden; im Wochenplan sind für die aktuelle Woche Dienste, ein Urlaub, eine Schulung und eine Notiz eingetragen (dazu ein unveröffentlichter Entwurf), eine Person hat Zeiten gestempelt oder nachgetragen, im Lager gibt es Material mit Lieferant (Mailadresse und Lieferzeit eingetragen), eine Bestellung als Entwurf und Chargen mit Ablaufdatum in den nächsten Wochen
1. **Verwaltung › Team › Wochenplan** öffnen, neben der Wochenwahl **Aushang als PDF** klicken, **PDF speichern**, die Datei öffnen.
Expected: Unter der Wochenwahl erscheint die Karte „Dienstplan-Aushang“ mit „Drucken“ und „PDF speichern“ (ohne „Per Mail senden“) und dem Satz „Druckt den veröffentlichten Dienstplan der Woche …, ohne Entwürfe.“; ein zweiter Klick auf den Knopf schließt sie, ein Wechsel der Woche oder des Standorts passt den Pfad an. Vorschlag `Dienstplan-<Standort>-<Montag>.pdf`; A4 quer, Titel „Dienstplan 01.03. – 07.03.2027“, darunter Standort und „Veröffentlicht am …“; eine Zeile je Person, eine Spalte je Tag, in den Zellen die Zeit (Dienste an einem anderen Standort mit dessen Namen), Urlaub, Krank, Frei und Schulung grau hinterlegt, die Notiz des Tages darunter, rechts die Stunden der Woche. Der Entwurf steht nicht darauf, erst nach dem Veröffentlichen. Als Rezeption (ohne das Recht zu planen) sind beide Knöpfe gesperrt mit Hinweis „Den Aushang druckt nur die Standortleitung.“.
2. **Verwaltung › Team › Personen** › eine Person › Reiter **Zeiten**, **Stundenliste als PDF**, speichern und öffnen. Danach in der Übersicht **Zeiten** nachsehen.
Expected: Auf der Karte der Person erscheint die Karte „Stundenliste“ mit „Drucken“ und „PDF speichern“ und dem Satz „Druckt die Stundenliste für <Monat> <Jahr>.“ (der Monat der gezeigten Woche; ein Wechsel der Woche über den Monatswechsel ändert ihn). `Stundenliste-<Name>-<JJJJ-MM>.pdf`, A4 hoch: je Tag mit Dienst oder Zeiten Kommen, Gehen, Pause, Ist, Soll und Saldo, unten „Monatssumme“, der Satz „Tage ohne Dienst und ohne Zeiten sind nicht aufgeführt.“ und zwei Unterschriftenzeilen („Unterschrift <Name>“, „Unterschrift Leitung“). Die Zahlen stimmen mit der Zeiten-Maske überein. In der Übersicht aller Zeiten gibt es den Knopf nicht.
3. **Verwaltung › Lager › Inventur**: **Zählliste drucken …** klicken (vor der Zählung), speichern und öffnen; dann **Zählung starten** und den Knopf noch einmal prüfen.
Expected: Die Karte „Zählliste drucken …“ mit „Drucken“ und „PDF speichern“; `Zählliste-<Lagerort>-<Datum>.pdf`: alle aktiven Materialien mit Einheit und Soll, eine leere Spalte „Gezählt“ (zwei Zeilen hoch zum Hinschreiben) und Unterschriftenzeilen „Gezählt von“ und „Geprüft von“; ein Wechsel des Lagerorts passt den Pfad an.
4. Eine **Zählung vom Handy** übernehmen („Übernehmen“ bei den wartenden Zählungen), den Knopf **Inventurliste drucken …** klicken und speichern.
Expected: `Inventurliste-<Lagerort>-<Datum>.pdf` mit Material, Einheit, Soll, Gezählt, Differenz (mit Vorzeichen), Wert und „Summe der Differenzen (n Positionen)“; nur gezähltes Material, Kopfzeile nennt „gezählt von <Name>“.
5. **Verwaltung › Lager › Bestellen**: unter einer Bestellung die Knöpfe ansehen, **PDF speichern**, dann **Per Mail senden**.
Expected: Unter den Knöpfen der Bestellung steht der Block „Drucken · PDF speichern · Per Mail senden“. `Bestellung-<Nr>-<Lieferant>.pdf`: Lieferant, Ansprechperson, Bestelldatum, „Lieferung an“ (der Standort), „Erwartete Lieferung“ (nur mit Lieferzeit beim Lieferanten), Positionen mit Menge, Einheit, Artikelnr., Bezeichnung, Einzelpreis und Summe, „Gesamtsumme netto“ und der Hinweis. Die Rückfrage zeigt die Mailadresse des Lieferanten (leer, wenn keine eingetragen ist), den Betreff „Bestellung <Nr> vom <Datum>“ und einen Text mit Gruß; nach „Senden“ Toast „Gesendet an …“, die Mail kommt mit dem PDF im Anhang an, der Stand der Bestellung bleibt (Entwurf), und unter den Knöpfen steht „Gesendet am <Datum> <Uhrzeit> an <Adresse> von <Vorname>“; ein zweites Senden fügt eine zweite Zeile hinzu. Eine ungültige Adresse wird abgelehnt.
6. Mit einer Standortleitung, deren Vollmacht „Bestellungen auslösen“ abgelaufen ist (Team › Regelungen und Vollmachten), das Senden der Bestellung versuchen.
Expected: Die App meldet den Grund wie bei „Bestellen und senden“ („Bestellungen auslösen“); es geht keine Mail hinaus, es entsteht kein Vermerk.
7. **Verwaltung › Lager › Chargen**, oben bei „Ablaufliste“ **Ablaufliste als PDF** klicken, den Zeitraum auf 60 Tage stellen, speichern.
Expected: Unter „Ablaufliste“ erscheint die Wahl „Zeitraum“ (7, 14, 30, 60, 90 Tage; 30 vorgewählt) mit „Drucken“ und „PDF speichern“. `Ablaufliste-<Standort>-<Datum>.pdf`: „Ablaufliste: nächste 60 Tage“, Material, Charge, Lagerort, Ablaufdatum, Frist („heute“, „noch 5 Tage“) und Menge, zuerst ablaufend zuerst; Abgelaufenes steht nicht darauf; ohne Charge im Zeitraum „In diesem Zeitraum läuft keine Charge ab.“.
8. Als Rezeption die Adressen `…/api/locations/<id>/roster/pdf?week=<Tag>` und `…/api/purchase-orders/<id>/pdf` aufrufen; als Backoffice `…/api/employees/<id>/timesheet/pdf?month=2026-06`.
Expected: Die ersten beiden antworten mit 403; das Backoffice darf die Stundenliste drucken (wie die Zeiten lesen), nicht den Aushang, die Inventur-, Bestell- und Ablaufliste.
Closes: Arbeitsanweisung PDF-Ausgaben, Aufgabe F
Result: ☐

### PDF-Ausgaben Zusatz – Gutscheinliste
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden; an der Kassa sind mehrere Gutscheine verkauft (ein Wertgutschein, ein Behandlungsgutschein, ein Paket), einer davon ist eingelöst, einer storniert, einer hat ein Ablaufdatum in 10 Tagen und einer ein Ablaufdatum in der Vergangenheit; ein Gutschein wurde online gekauft (mit Empfänger).
1. **Kassa**, in der Kopfzeile **Gutscheinliste …** klicken, ohne Filter **PDF speichern**, die Datei öffnen.
Expected: Die Karte „Gutscheinliste“ mit „Ausgestellt von“ und „bis“, „Status“ (Alle, aktiv, aufgebraucht, abgelaufen, storniert), „läuft in 30 Tagen ab“, „noch nicht versendet“ und „Drucken“ und „PDF speichern“ (ohne „Per Mail senden“); ein zweiter Klick auf den Knopf oder das Kreuz schließt sie. `Gutscheinliste-<Standort>-<Datum>.pdf`, A4 quer: eine Zeile je Gutschein mit Code, Art, Ausgestellt, Ausgangswert, Restwert, Gültig bis („–“ ohne Ablaufdatum), Status, Käufer, Empfänger („–“ bei Kassa-Verkäufen, beim Online-Gutschein der Name), Kanal (Kassa oder Online) und Versand; unten „Offene Restwerte (Verbindlichkeit)“ mit Betrag und Zahl der aktiven Gutscheine, „Abgelaufen, noch nicht ausgebucht“ mit dem Rest des abgelaufenen, und ein Satz zur Berechnung. Bei vielen Gutscheinen läuft die Tabelle über mehrere Seiten mit wiederholtem Kopf.
2. Den Zeitraum auf heute und gestern stellen, den Status auf „aktiv“ stellen, speichern; dann „läuft in 30 Tagen ab“ ankreuzen, speichern.
Expected: Der Pfad der Karte ändert sich mit jeder Wahl; die Kopfzeile des PDFs nennt die Auswahl („Ausgestellt 08.11.2026 bis 09.11.2026 · Status: aktiv“, „läuft in 30 Tagen ab“); mit „läuft in 30 Tagen ab“ steht nur der Gutschein mit dem Ablaufdatum in 10 Tagen darauf. Ein Ende vor dem Beginn sperrt „Drucken“ und „PDF speichern“ mit dem Hinweis „Das Ende darf nicht vor dem Beginn liegen.“.
3. Einen frisch verkauften Gutschein unter dem Beleg **Per Mail senden**, einen zweiten **Drucken** (Drucker oder PDF-Drucker), einen dritten **Übergeben** klicken; danach die Liste mit „noch nicht versendet“ drucken.
Expected: Toasts „Gesendet an …“, „Druckdialog geöffnet“ (seit T-099) und „Gutschein … als übergeben vermerkt“; mit „noch nicht versendet“ fehlen diese drei, der Rest steht darauf. In der Liste ohne Filter steht beim ersten „Mail am <Datum>“ mit „an <Adresse>“, beim zweiten „Zum Drucken geöffnet am <Datum>“ (seit T-099), beim dritten „Übergeben am <Datum>“; beim online gekauften Gutschein „Mail am …“ an die Adresse des Käufers. Lässt sich der Druckdialog nicht öffnen, bleibt kein Vermerk.
4. Als Rezeption ohne das Recht „Berichte mit Beträgen“ die Kassa öffnen und die Karte öffnen; als Rezeption die Adresse `…/api/pos/locations/<id>/vouchers/pdf` aufrufen.
Expected: Die Karte lässt sich öffnen, „Drucken“ und „PDF speichern“ sind gesperrt mit dem Hinweis auf Kassenaufsicht oder „Berichte mit Beträgen“; der Aufruf antwortet mit 403. Mit Standortleitung oder Backoffice geht es.
Closes: Zusatz zur Arbeitsanweisung PDF-Ausgaben (Gutscheinliste)
Result: ☐

### PDF-Ausgaben G – Hilfe
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden; die Hilfe über das Fragezeichen oder **Hilfe** in der Seitenleiste öffnen, Suche „PDF“
1. In der Suche **PDF** eintippen, danach **Rechnung schicken**, **drucken** und **per Mail** (jeweils einzeln).
Expected: Der Ablauf „Dokumente drucken, als PDF speichern und per Mail senden“ steht jedes Mal unter den Treffern, bei „Rechnung schicken“ auch „Belege ansehen und drucken“ und „Rechnung mit Empfänger“.
2. Den Ablauf „Dokumente drucken, als PDF speichern und per Mail senden“ öffnen und lesen.
Expected: Es gibt „Der Knopfblock“ (mit dem Bild der Kassa), „Drucken“, „PDF speichern“, „Per Mail senden“ (mit dem Bild der Rückfrage), „Wenn ein Knopf grau ist“ und „Gut zu wissen“; die Knopfnamen und Hinweistexte stimmen wörtlich mit der App überein; alle Verweise unter „Der Knopfblock“ öffnen das richtige Thema und den richtigen Abschnitt. Unter „Auf dieser Seite“ stehen die sechs Abschnitte, unter „Verwandte Themen“ die verlinkten Seiten.
3. Die Themen „Belege ansehen und drucken“ (Abschnitt „Als PDF speichern oder per Mail senden“), „Rechnung mit Empfänger“ (Abschnitt „Rechnung als PDF“, mit dem Bild der Rechnung) und „Tagesabschluss“ (Abschnitt „Tagesabschluss als PDF“) lesen und mit der Kassa vergleichen.
Expected: Die Knöpfe „Drucken“ (Bondrucker), „A4 drucken“, „PDF speichern“ (Pfeil: „PDF im Bon-Format speichern“) und „Per Mail senden“, die Dateinamen und die Vermerke „Per Mail gesendet an …“ stimmen mit der Kassa überein; das Bild der Rechnung zeigt Beispieldaten (Day-Spa Wien, Muster GmbH), keine echten Namen.
4. Die Themen „Terminbestätigung als PDF drucken oder per Mail senden“, „Tagesplan drucken oder als PDF speichern“, „Warteliste ansehen“ (Abschnitt „Tagesliste drucken“) und „Kundenkarte ansehen“ (Abschnitt „Auskunft und Stammblatt als PDF“) lesen und im Kalender und in der Kundenkarte nachvollziehen.
Expected: Die Wege (Panel › **Mehr** › **Bestätigung …**, Menü neben **Termin** › **Tagesplan drucken …**, Menü **…** an der Kundenkarte) führen zu den beschriebenen Karten; die Hilfe nennt für die Auskunft die Gesundheitsangaben nur mit dem Recht für die Anamnese, sonst den Satz „Gesundheitsangaben: vorhanden, Auskunft durch die Leitung“.
5. Die Themen „Gutschein verkaufen“ (Abschnitte „Gutschein als PDF übergeben“ und „Gutscheinliste drucken“, mit dem Bild der Karte), „Dienstplan veröffentlichen“ (Abschnitt „Aushang drucken“), „Arbeitszeit korrigieren“ (Abschnitt „Stundenliste drucken“), „Inventur buchen“, „Bestellen und Lieferung buchen“ und „Bald ablaufende Chargen abarbeiten“ (je mit dem Abschnitt zum PDF) lesen und mit der Verwaltung vergleichen.
Expected: Knopfnamen („Gutscheinliste …“, „Aushang als PDF“, „Stundenliste als PDF“, „Zählliste drucken …“, „Inventurliste drucken …“, „Ablaufliste als PDF“), Filter, Dateinamen und die Hinweise bei fehlendem Recht stimmen mit den Masken überein.
6. In der Kassa am Fragezeichen neben einer Maske die Seitenerklärung „Kassa“ öffnen; im Kalender und bei den Kunden ebenso.
Expected: Die Listen der Abläufe nennen „Dokumente drucken, als PDF speichern und per Mail senden“, „Gutscheinliste drucken“ (Kassa), „Terminbestätigung als PDF drucken oder senden“ und „Tagesplan drucken“ (Kalender) und „Auskunft und Stammblatt als PDF“ (Kunden).
Closes: Arbeitsanweisung PDF-Ausgaben, Aufgabe G

### Gutschein-Ablaufdatum je Land
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Rezeption anmelden, **Kassa**, Bereich „Kassieren“, Panel „Artikel und Gutscheine“ aufklappen
1. Unter „Gutschein verkaufen“ das Feld **Einzulösen bis** ansehen (Kasse in Österreich).
Expected: Das Feld ist leer; darunter steht „In Österreich ohne Ablaufdatum; leer lassen.“
2. Ein Datum eintragen, z. B. `31.12.2028`, und das Feld verlassen.
Expected: Unter dem Feld steht der Hinweis „In Österreich sind Fristen unter 5 Jahren ohne Auszahlungsmöglichkeit unwirksam.“; **Wertgutschein verkaufen** bleibt möglich.
3. Wert `50` eintragen, **Wertgutschein verkaufen**, bar bezahlen.
Expected: Der Beleg zeigt unter „Gutschein …“ die Zeile „Einzulösen bis 31.12.2028“; der gedruckte Beleg ebenso.
4. Feld leeren, einen Behandlungsgutschein verkaufen und bezahlen.
Expected: Auf dem Beleg steht keine Zeile „Einzulösen bis“ (unbefristet).
5. Wenn eine Kasse in Deutschland eingerichtet ist (Test-TSE): Kassa dieses Standorts öffnen.
Expected: Das Feld ist mit dem 31.12. des dritten Jahres nach heute vorbelegt (heute 09.11.2026 → 31.12.2029); ein früheres Datum zeigt „Kürzer als die übliche Frist bis …“.
6. Einen Gutschein mit abgelaufenem Datum einlösen (z. B. ein Testgutschein mit Datum von gestern).
Expected: Beim Bezahlen erscheint „Gutschein … ist am … abgelaufen.“
Closes: –
Result: ☐

### Gast anonymisieren
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden
1. Einen Testgast mit Termin in der Vergangenheit, Notiz und Mailadresse anlegen. Kundenkarte öffnen, Menü **…**, **Gast anonymisieren …**, **Abbrechen**.
Expected: Nichts passiert, der Gast bleibt.
2. Dem Gast einen Termin morgen buchen, dann erneut **Gast anonymisieren …** und **Endgültig anonymisieren**.
Expected: Meldung „Der Gast lässt sich noch nicht anonymisieren. Offen: 1 künftiger Termin …“.
3. Den Termin stornieren und erneut anonymisieren.
Expected: Die Karte schließt; die Suche nach dem Namen findet nichts. Im Kalender und im Verlauf steht der alte Termin mit „Gelöschter Gast“, ohne Notiz und Stornogrund; „Kundenkarte öffnen“ zeigt „Anonymisiert am …“ ohne Bearbeiten, Buchen und Kassieren.
4. Als Rezeption anmelden.
Expected: Im Menü der Kundenkarte gibt es keinen Eintrag **Gast anonymisieren …**.
Closes: –
Result: ☐

### Kundenkarte 3 : 2 mit Warnstreifen und Verlauf
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Rezeption anmelden, **Kunden** öffnen, „Brigitte Müller“ wählen
1. Das Fenster auf mindestens 1440 px Breite ziehen und die Kundenkarte ansehen.
Expected: „Gut zu wissen“ steht links und ist breiter als „Verlauf“ rechts (3 : 2). Im Verlauf steht der nächste Termin als Kachel mit „Nächster Termin · in … Tagen“, unter „Zuletzt“ stehen drei Termine und rechts „meist bei Lena Huber“. Keine Zeile bricht um.
2. Mit der Maus auf den Namen im Kopf zeigen, dann auf einen Chip unter „Zuletzt“ (Paket, Gutschein oder „Letzter Kauf“).
Expected: Am Namen erscheint die Anrede für Nachrichten; am Chip der volle Wortlaut mit „gültig bis“ oder den letzten Käufen.
3. Einen Gast mit „Nicht behandeln“ oder mit Ausfällen öffnen.
Expected: Unter dem Kopf steht ein Hinweis „Warnung“ mit „Nicht behandeln: …“ und den Ausfällen; bei den Wünschen steht kein Warn-Chip mehr.
4. Das Fenster schmaler ziehen, bis die Karte unter 720 px breit ist.
Expected: „Verlauf“ rutscht in voller Breite unter „Gut zu wissen“.
5. In Windows unter **Einstellungen** › **Anzeige** die Skalierung auf 125 %, danach auf 150 % stellen und die Schritte 1 und 4 wiederholen.
Expected: Die Teilung bleibt 3 : 2, kein Text ist abgeschnitten, die Kachel und die Chips bleiben lesbar.
Closes: –
Result: ☐

### Trinkgeld, Schritt 1 – Konto in der Buchhaltung
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, **Verwaltung** › **Buchhaltung** öffnen
1. Die Zuordnung der Konten ansehen.
Expected: Unter „Kassenüberschuss (Tagesabschluss)“ steht „Trinkgeld für das Team (Verbindlichkeit)“ mit 3790.
2. Den Wert auf 3795 ändern und speichern, die Seite neu öffnen.
Expected: 3795 bleibt stehen. Das Feld „Trinkgeld“ beim Bezahlen kommt mit Schritt 2.
Closes: –
Result: ☐

### Trinkgeld, Schritt 2 – Trinkgeld an der Kassa eintragen
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Rezeption anmelden, **Kassa** öffnen, einen Termin unter „Heute abzurechnen“ wählen (z. B. 89,00 €)
1. Bei **Karte** `100` eintragen.
Expected: Unter den Feldern steht „Zu viel: 11,00 € über dem offenen Betrag“ und der Verweis **Als Trinkgeld eintragen**; **Bezahlen und Beleg erstellen** ist gesperrt.
2. Auf **Als Trinkgeld eintragen** klicken.
Expected: Bei **Trinkgeld** steht `11,00`, darunter „Alles gedeckt, davon 11,00 € Trinkgeld“; der Verweis ist weg.
3. **Bezahlen und Beleg erstellen**.
Expected: Der Beleg zeigt Summe 89,00 €, darunter „Trinkgeld (kein Umsatz) 11,00 €“ und „Karte 100,00 €“. Das Feld **Trinkgeld** ist wieder leer.
4. Einen zweiten Termin wählen, bei **Trinkgeld** `5` eintragen, bei **Bar gegeben** auf **Rest** klicken, dann einen glatten höheren Betrag eintragen.
Expected: **Rest** trägt den offenen Betrag plus 5,00 ein; das Rückgeld rechnet das Trinkgeld ab.
5. **Kassenlade & Abschluss** öffnen.
Expected: Unter „Zahlungsarten“ steht als letzte Zeile „davon Trinkgeld (kein Umsatz) 16,00 €“; der Umsatz enthält das Trinkgeld nicht.
6. Den ersten Beleg unter **Belege** stornieren und den Bericht neu ansehen.
Expected: Das Trinkgeld im Bericht sinkt um 11,00 €.
Closes: –
Result: ☐

### Kassa ohne Verbindung – Ersatzbeleg und Nacherfassung
Start: Server mit `docker compose up -d --build`, Windows-App mit diesem Server verbunden (nicht Einzelplatz), als Rezeption anmelden, **Kassa** einmal öffnen, dann den Server anhalten (`docker compose stop api`) und warten, bis unten „Offline“ steht
1. Im Kalender einen eingecheckten Termin von heute öffnen und **Kassieren** wählen.
Expected: Die Kassa zeigt die Rechnung mit „inkl. USt“ an der Position; unter „Bezahlen“ steht der Hinweis „Ersatzbeleg“.
2. Bei **Bar gegeben** einen glatten höheren Betrag eintragen und **Bezahlen und Beleg erstellen** wählen.
Expected: „Bezahlt“ mit „Ersatzbeleg E-<Kasse>-<Datum>-<Uhrzeit>-…“, Summe, „Bar“, „Rückgeld“ und dem Vermerk „Ersatzbeleg, der signierte Beleg folgt“; keine Knöpfe für PDF und Mail. Mit eingerichtetem Bondrucker druckt **Drucken** den Bon mit „ERSATZBELEG“.
3. Denselben Termin noch einmal kassieren wollen.
Expected: „Für diesen Termin gibt es schon einen Ersatzbeleg.“
4. Einen Artikel auf eine neue Rechnung legen, bei **Gutschein** einen Betrag und Code eintragen und bezahlen wollen; danach **Parken**.
Expected: „Ohne Verbindung nur bar oder mit Karte am eigenständigen Gerät.“ und „Ohne Verbindung lässt sich die Rechnung nicht parken. Bezahlen oder verwerfen.“
5. Die App beenden und neu starten, dann den Server wieder starten (`docker compose start api`) und bis zu 15 Sekunden warten.
Expected: Unten steht wieder „Online“. Unter **Belege** steht ein neuer signierter Beleg mit „Nacherfassung zu Ersatzbeleg … vom …“; der Termin steht nicht mehr unter „Heute abzurechnen“.
6. Gegenprobe: ohne Verbindung einen Termin kassieren, den Termin am Server an einem zweiten Gerät ebenfalls kassieren, dann die Verbindung herstellen.
Expected: Oben steht „Beim Abgleich nicht übernommen“ mit dem Ersatzbeleg, dem Grund „Dieser Termin steht schon auf einer Rechnung.“ und **Erneut senden**, ohne **Erledigt**.
Closes: –
Result: ☐

### Kassa ohne Verbindung – Hinweis nach 48 Stunden
Start: wie „Kassa ohne Verbindung – Ersatzbeleg und Nacherfassung“; Server angehalten, ein Ersatzbeleg ist ausgegeben
1. Die Uhr des PCs um drei Tage vorstellen und in der App **Verbindung prüfen** wählen.
Expected: Der Hinweis „Keine Verbindung zum Server“ endet mit „Der älteste Ersatzbeleg ist vom … und seit mehr als 48 Stunden nicht nacherfasst: In Österreich den Ausfall der Kasse über FinanzOnline melden.“
2. Die Uhr des PCs vorgestellt lassen, die Uhr des Servers ebenfalls um drei Tage vorstellen und den Server starten (`docker compose start api`).
Expected: Nach der Nacherfassung zeigt die Kassa oben „Die Kasse war länger als 48 Stunden nicht erreichbar: Ersatzbeleg vom …, nacherfasst am …. Beginn und Ende des Ausfalls über FinanzOnline melden.“
Closes: –
Result: ☐

### Kassa ohne Verbindung – Liste der offenen Ersatzbelege
Start: wie „Kassa ohne Verbindung – Ersatzbeleg und Nacherfassung“; Server angehalten, zwei Ersatzbelege sind ausgegeben
1. In der Kassa **Belege** wählen.
Expected: Über „Belege heute“ steht „Ersatzbelege, noch nicht nacherfasst (2)“ mit Nummer, Zeit, Betrag und „wartet auf Nacherfassung“.
2. Einen Ersatzbeleg anklicken und **Drucken** wählen.
Expected: Rechts steht der Ersatzbeleg mit „Ersatzbeleg, der signierte Beleg folgt“, ohne „Beleg stornieren“; mit Bondrucker kommt der Bon noch einmal.
3. Den Server starten und bis zu 15 Sekunden warten.
Expected: Die Liste „Ersatzbelege“ verschwindet; die signierten Belege stehen unter „Belege heute“.
4. Gegenprobe mit einem abgelehnten Ersatzbeleg (Termin am Server schon abgerechnet).
Expected: Die Zeile zeigt „nicht übernommen: Dieser Termin steht schon auf einer Rechnung.“ und **Erneut senden**.
Closes: –
Result: ☐

### Kassa ohne Verbindung – Eintritt auf dem Ersatzbeleg
Start: wie „Kassa ohne Verbindung – Ersatzbeleg und Nacherfassung“; vor dem Anhalten des Servers steht unter „Heute abzurechnen“ ein Eintritt in die Sauna mit Preis
1. Ohne Verbindung den Eintritt unter „Heute abzurechnen“ anklicken.
Expected: Die Rechnung zeigt den Eintritt mit Personenzahl und dem Preis der Liste; unter „Bezahlen“ steht der Hinweis „Ersatzbeleg“.
2. Mit Karte bezahlen.
Expected: Der Ersatzbeleg nennt den Eintritt und die Summe.
3. Im Kalender unter „Sauna und Ruheraum“ einen neuen Eintritt buchen und kassieren wollen.
Expected: „Der Preis dieses Eintritts ist ohne Verbindung nicht bekannt. Den Betrag notieren und mit Verbindung kassieren.“
4. Den Server starten und bis zu 15 Sekunden warten.
Expected: Unter **Belege** steht der signierte Beleg mit dem Eintritt und „Nacherfassung zu Ersatzbeleg …“.
Closes: –
Result: ☐

### Kundenmaske – „Schon vorhanden?“ mit Grund
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Rezeption anmelden, **Kunden** › **Neue Kundin**
1. Die Telefonnummer einer vorhandenen Kundin in anderer Schreibweise eintragen (z. B. `0664 …` statt `+43 664 …`) und einen anderen Nachnamen.
Expected: Unter „Schon vorhanden?“ steht die vorhandene Karte zuerst, mit dem Grund „gleiche Telefonnummer“ unter Name und Nummer.
2. Nur einen häufigen Nachnamen eintragen.
Expected: Karten aus der Suche erscheinen ohne Grund; erkennt die App einen ähnlichen Namen, steht dieser Grund dabei.
Closes: L13 (erster Teil)
Result: ☐

### Datenschutz-Auskunft – Eintritte, Warteliste, Einwilligung und Nachrichten
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, **Kunden** öffnen, eine Kundin mit Eintritt, Wartelisteneintrag, Werbe-Einwilligung und zugeordneter Mail wählen
1. Im Menü **…** der Kundenkarte **Auskunft als PDF** wählen und das PDF speichern.
Expected: Nach „Termine“ stehen „Eintritte“ und „Warteliste“, nach „Gutscheine auf den Namen“ stehen „Einwilligung in Werbung“ (Stand, Adresse, Zeitpunkte) und „Nachrichten“ (Zeitpunkt, Kanal, Absender, Betreff, kein Wortlaut). Keine Tabelle läuft über den Rand.
2. Dasselbe für eine Kundin ohne diese Daten.
Expected: „Keine Eintritte.“, „Keine Einträge auf der Warteliste.“, „Keine Einwilligung gespeichert.“, „Keine Nachrichten zugeordnet.“
Closes: –
Result: ☐

### Trinkgeld, Schritt 3 – je Person im Tagesabschluss
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Rezeption anmelden, **Kassa** öffnen
1. Einen Termin mit 5,00 Trinkgeld kassieren, danach nur einen Artikel mit 2,00 Trinkgeld.
Expected: Beide Belege zeigen „Trinkgeld (kein Umsatz)“.
2. **Kassenlade & Abschluss** öffnen.
Expected: Unter „Zahlungsarten“ steht „davon Trinkgeld (kein Umsatz) 7,00 €“, darunter „Trinkgeld <Name der behandelnden Person> 5,00 €“ und „Trinkgeld Team 2,00 €“.
3. Den Tagesabschluss als PDF speichern.
Expected: Das PDF nennt dieselben beiden Zeilen.
Closes: –
Result: ☐

### Team – Urlaubskonto auf der Karte der Person
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, **Verwaltung** › **Team** öffnen
1. Eine Person wählen und den Bereich **Arbeitszeit** öffnen.
Expected: Unter der Woche steht „Urlaubskonto“ mit dem laufenden Jahr; ohne Anspruch steht bei „Rest“ ein Strich, und die Zeile darunter sagt, dass kein Anspruch eingetragen ist.
2. Bei **Anspruch (Tage)** 25 und bei **Übertrag aus dem Vorjahr** 2,5 eintragen, **Urlaubskonto speichern** klicken.
Expected: „Gespeichert.“ erscheint, bei „Rest“ steht 27,5 Tage abzüglich der schon eingetragenen Urlaubstage.
3. In der Woche darüber einen künftigen Arbeitstag der Person anklicken, **Urlaub** wählen, **Sofort gültig, ohne Veröffentlichen** anhaken und speichern.
Expected: „Geplant“ steigt um 1 Tag, „Rest“ sinkt um 1 Tag.
4. Dasselbe an einem Wochentag, an dem die Person laut Stammdaten nicht arbeitet.
Expected: „Geplant“ und „Rest“ bleiben; die Zeile darunter nennt „1 Urlaubseintrag an einem anderen Wochentag zählt nicht.“
5. Mit dem Pfeil **Vorheriges Jahr** blättern.
Expected: Das Konto zeigt das Vorjahr mit leerem Anspruch; zurückgeblättert stehen 25 und 2,5 wieder da.
6. Als Rezeption anmelden und die Karte derselben Person öffnen.
Expected: Den Bereich „Arbeitszeit“ gibt es für die Rezeption nicht.
Closes: –
Result: ☐

### T-065 – Personenkarte: vier Bereiche, Übersicht und Verlauf als Seitenleiste
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, **Verwaltung** › **Team** › **Personen** öffnen
1. Eine Person mit Zugang wählen (z. B. Lena Huber).
Expected: Die Karte beginnt mit der **Übersicht** und zeigt fünf Kacheln: Nächster Dienst, Diese Woche, Resturlaub, Offene Anträge, Zertifikate. Darüber stehen vier Bereiche: Übersicht, Profil, Arbeitszeit, Lohn und Akte. Rechts im Kopf steht **Verlauf**.
2. Nacheinander **Profil**, **Arbeitszeit** und **Lohn und Akte** anklicken.
Expected: **Profil** zeigt Stammdaten, Zugang und Rolle, Qualifikationen und Zuständigkeiten untereinander. **Arbeitszeit** zeigt Dienstplan und Abwesenheit mit Urlaubskonto und Kalenderdatei, darunter die Zeiten. **Lohn und Akte** zeigt Lohn, Personalakte, Regelungen und Vollmachten. Mit den Pfeiltasten lässt sich zwischen den Bereichen wechseln.
3. Zurück zur **Übersicht** und die Kachel **Resturlaub** anklicken.
Expected: Die Karte wechselt zu **Arbeitszeit** und zeigt das Urlaubskonto.
4. In der Übersicht die Kachel **Zertifikate** anklicken.
Expected: Die Karte wechselt zu **Profil** und rollt zu den Zuständigkeiten.
5. In der Übersicht die Kachel **Offene Anträge** anklicken.
Expected: Die Team-Ansicht **Anträge** öffnet sich.
6. Zur Person zurückkehren und **Verlauf** im Kopf der Karte klicken.
Expected: Rechts öffnet sich eine Seitenleiste über der Karte mit den Verläufen von Zugang, Dienstplan, Personalakte, Regelungen und Zuständigkeiten. **Esc** drücken: Die Leiste schließt, der Fokus kehrt zur Schaltfläche **Verlauf** zurück. Ein zweites Mal öffnen und mit dem Kreuz schließen.
7. Aus einer anderen Stelle der App (z. B. aus der Kassa oder der Shell) einen Sprung in die Personalakte einer Person auslösen, wo es einen solchen Sprung gibt.
Expected: Die Karte öffnet im Bereich **Lohn und Akte** und rollt zum Abschnitt **Personalakte**.
8. Im Bereich **Profil** bei **Vollständiger Name** einen Buchstaben anhängen und unten **Speichern** klicken oder Strg+S drücken.
Expected: „Gespeichert.“ erscheint, der Name steht in der Liste. Wechselt man ohne Speichern zu einer anderen Person, fragt die App „Änderungen speichern?“.
9. Im Bereich **Lohn und Akte** einen Eintrag der Personalakte ändern.
Expected: Die Speichern-Leiste unten ist aktiv und gilt für Lohn und Akte zusammen.
10. Als Rezeption anmelden und **Team** öffnen.
Expected: Die Rezeption sieht nur den Wochenplan, keine Personenkarte. Inhaber und Manager sehen alle vier Bereiche, die Standortleitung nur die Bereiche, für die sie das Recht hat.
Closes: –
Result: ☐

### T-061 – Lager: vier Reiter, Ansichten im Bestand und Einrichtung
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, **Verwaltung** › **Lager** öffnen
1. Die Übersicht ansehen.
Expected: Die Schiene zeigt nur „Übersicht“, „Bestand“, „Bestellen“ und „Inventur“. Darunter stehen die Kacheln „Lagerwert“, „Nachbestellen“, „Unter Minimum“, „Chargen laufen ab“ und „Mehrverbrauch“, dann „Was ist zu tun“ und der Abschnitt „Verbrauch“.
2. Auf die Kachel **Unter Minimum** klicken.
Expected: Der Reiter „Bestand“ ist gewählt, bei „Ansicht“ steht „Material“, „Nur unter Minimum“ ist angehakt.
3. Bei **Ansicht** erst **Chargen**, dann **Bewegungen** wählen.
Expected: Der Reiter „Bestand“ bleibt gewählt. Bei „Chargen“ stehen die Ablauflisten und das Feld „Lagerort“ fehlt; bei „Bewegungen“ steht die Liste mit „Charge zurückverfolgen“ darunter. Die Zahl an „Chargen“ bleibt in jeder Ansicht sichtbar.
4. Oben rechts auf **Einrichtung** klicken.
Expected: Statt der Schiene stehen „Zurück“, „Einrichtung“ und die Wahl „Lagerorte“ und „Lieferanten“. Die Lagerorte sind offen; Suchfeld und „Bewegung buchen“ fehlen.
5. **Lieferanten** wählen, danach auf **Zurück** klicken.
Expected: Erst steht die Liste der Lieferanten da, nach „Zurück“ wieder der Bestand in der Ansicht „Bewegungen“.
6. Zur Übersicht wechseln und auf die Kachel **Mehrverbrauch** klicken.
Expected: Die Seite rollt zum Abschnitt „Verbrauch“.
7. Das Fenster so schmal wie möglich ziehen und danach maximieren.
Expected: Schmal steht die Schiene in einer Zeile, die Werkzeuge stehen rechtsbündig in der Zeile darunter, nichts ist abgeschnitten. Maximiert stehen die Werkzeuge rechts neben der Schiene.
8. Als Rezeption anmelden und das Lager öffnen.
Expected: Die Schiene zeigt „Übersicht“, „Bestand“ und „Bestellen“; im Bestand gibt es die Ansichten „Material“ und „Bewegungen“. „Einrichtung“, die Kachel „Nachbestellen“ und der Abschnitt „Verbrauch“ fehlen.
Closes: –
Result: ☐

### Verwaltung modernisieren – Behandlung mit vier Bereichen und Verlauf als Seitenleiste
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, **Verwaltung** › **Angebot** › **Behandlungen** öffnen
1. Eine gespeicherte Behandlung wählen und das Fenster auf die kleinste Größe ziehen (1100 × 680).
Expected: Unter dem Namen steht eine Schiene mit genau vier Bereichen auf einer Zeile: Grunddaten, Durchführung, Preise und Verkauf, Qualifikationen und Räume.
2. **Durchführung** wählen.
Expected: „Vor- und Nachbereitung“, „Vorbereitungsschritte“, „Material je Termin“ und „Zum Termin bereitstellen“ stehen als Gruppen untereinander.
3. **Preise und Verkauf** wählen.
Expected: „Preise nach Wochentag und Uhrzeit“ und „Passt dazu“ stehen untereinander, bei mehreren Standorten darüber „Preis je Standort“.
4. **Grunddaten** wählen und **Weitere Angaben** aufklappen, bei **Ausfallgebühr** 50 eintragen, speichern, eine andere Behandlung und dann wieder diese wählen.
Expected: „Weitere Angaben“ ist bei dieser Behandlung von selbst offen und zeigt 50; bei einer Behandlung ohne eigene Regel ist die Gruppe zu.
5. Im Kopf der Karte **Verlauf** wählen.
Expected: Rechts öffnet die Seitenleiste „Verlauf“ mit den Änderungen, die neueste oben; der Fokus steht in der Leiste. Unter der Maske gibt es keinen Abschnitt „Verlauf“ mehr.
6. **Esc** drücken.
Expected: Die Leiste schließt, der Fokus steht wieder auf **Verlauf**; die Verwaltung bleibt offen.
7. Im Kalender einen Termin mit der rechten Maustaste anklicken und **Behandlungszeiten bearbeiten …** wählen.
Expected: Die Behandlung öffnet im Bereich „Durchführung“, der Fokus steht im Feld „Vorbereitung (min)“.
8. **Verwaltung** › **Kassa & Geld** › **Kosten** öffnen und von einer Behandlung zu ihrem Material springen.
Expected: Die Behandlung öffnet im Bereich „Durchführung“ bei der Gruppe „Material je Termin“.
9. **Behandlung anlegen** wählen.
Expected: Die Schiene zeigt Grunddaten, Durchführung und Qualifikationen und Räume, bei mehreren Standorten auch Preise und Verkauf; die Schaltfläche „Verlauf“ fehlt.
10. Als Standortleitung ohne das Recht auf Berichte mit Beträgen anmelden und eine Behandlung unter **Durchführung** öffnen.
Expected: Die Gruppe „Material je Termin“ fehlt, alles andere steht da.
Closes: –
Result: ☐

### T-063 – Zahlen: Berichte als Abschnitte
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, **Verwaltung** › **Zahlen** öffnen
1. Den Reiter **Berichte** wählen.
Expected: Unter den fünf Reitern steht keine zweite Schiene. Oben steht „Management“ mit **Von**, **Bis**, **Mit anderen Spas vergleichen** und **Exportieren**; im Kopf der Maske steht nur **Standort**.
2. Nach unten rollen.
Expected: Unter einer Linie steht „Pflichtberichte“ mit dem Feld **Datum** rechts, darunter die Kassen, die Gutscheine und die Arbeitszeitaufzeichnung.
3. Bei den Pflichtberichten ein anderes **Datum** wählen.
Expected: Die Pflichtberichte laden neu; die Kennzahlen im Management bleiben stehen.
4. Den **Standort** im Kopf wechseln.
Expected: Beide Abschnitte zeigen den neuen Standort.
5. In „KI und Hilfe“ „Wie war der Umsatz im Oktober?“ fragen und **Im Bericht öffnen** klicken.
Expected: „Berichte“ ist offen, das Management steht oben mit dem Zeitraum der Antwort.
6. Das Fenster auf die kleinste Größe ziehen und „KI und Hilfe“ öffnen.
Expected: Die fünf Reiter stehen in einer Zeile; der Standort rutscht rechtsbündig darunter.
7. Als Rezeption anmelden und **Zahlen** öffnen.
Expected: Den Reiter „Berichte“ gibt es nicht.
Closes: –
Result: ☐

### T-064 – Betrieb: Schließzeiten und Raumsperren in einer Liste, Formular als Seitenleiste
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, **Verwaltung** › **Betrieb** öffnen
1. Die Reiter unter dem Balken ansehen.
Expected: Fünf Reiter in einer Zeile: Firmendaten, Standorte & Räume, Schließzeiten, Kalender-Abos, Handbuch. „Raumsperren“ gibt es nicht mehr als Reiter.
2. **Schließzeiten** wählen.
Expected: Eine Liste „Schließzeiten und Raumsperren“: kommende zuerst, jede Zeile mit dem Kennzeichen „Standort“ oder „Raum“, Zeitraum, Grund, Zustand und Zahl offener Termine. Über der Liste der Filter „Alle / Standort geschlossen / Raum gesperrt“, unter der Liste „Vergangene (n)“. Oben rechts **Einrichtung** und fest **Neue Schließzeit**; die Wartezeit steht nicht mehr als Karte über der Liste.
3. Eine Zeile wählen.
Expected: Das Formular öffnet sich rechts in einer Seitenleiste; die Liste bleibt links stehen. Esc oder das Kreuz schließt die Leiste.
4. **Vergangene** aufklappen, eine beendete Schließzeit wählen, den **Grund für das Team** ändern und die Leiste mit dem Kreuz schließen.
Expected: Die Rückfrage „Änderungen speichern?“ erscheint. Nach **Abbrechen** bleibt die Leiste offen und die Änderung steht noch da.
5. Die Änderung verwerfen. **Neue Schließzeit** wählen, oben **Einzelner Raum** wählen, einen Zeitraum eintragen und **Strg+S** drücken.
Expected: Unten erscheint kurz „Raumsperre gespeichert“ (kein Balken); die neue Raumsperre steht in der Liste mit dem Kennzeichen „Raum“ und ist gewählt.
6. **Löschen** wählen und bestätigen.
Expected: Unten erscheint kurz „Raumsperre gelöscht“, die Leiste schließt, die Zeile ist weg.
7. **Einrichtung** wählen, die Wartezeit ändern und **Wartezeit speichern** wählen.
Expected: Die Seitenleiste „Einrichtung“ zeigt das Feld „Senden nach (Minuten)“; nach dem Speichern erscheint „Wartezeit gespeichert“.
8. In den **Kalender** wechseln, in der Raumansicht mit der rechten Maustaste auf den Kopf eines Raums klicken und **Raum sperren …** wählen.
Expected: Die Seite „Schließzeiten“ ist offen; ein neues Formular „Einzelner Raum“ mit dem Raum steht in der Seitenleiste, der Cursor im ersten Feld.
9. Einen Standort ohne Schließzeit und Raumsperre wählen.
Expected: Statt der Liste steht „<Standort> ist durchgehend offen“ mit den Knöpfen **Schließzeit anlegen** und **Raum sperren**.
Closes: –
Result: ☐

### T-064 – Firmendaten: Verlauf als Seitenleiste; Listen, Erinnerungen und Warnungen
Start: Windows-App, als Inhaberin anmelden, **Verwaltung** › **Betrieb** › **Firmendaten** öffnen
1. Oben rechts neben „Firmendaten“ auf **Verlauf** klicken.
Expected: Rechts öffnet die Seitenleiste „Verlauf“ mit dem Untertitel „Firmendaten“ und den Änderungen, die neueste oben. Am Ende des Formulars steht kein Abschnitt „Verlauf“ mehr.
2. **Esc** drücken.
Expected: Die Seitenleiste schließt, der Fokus steht wieder auf **Verlauf**.
3. Nacheinander **Standorte & Räume**, **Schließzeiten**, **Kalender-Abos** und **Handbuch** öffnen.
Expected: Die Liste links ist in allen vier Masken gleich breit.
4. **Kontakt** › **Erinnerungen** öffnen und bei den Wiederbuchungs-Hinweisen auf **Versandprotokoll** klicken.
Expected: Rechts öffnet die Seitenleiste „Versandprotokoll“ mit den Wiederbuchungs-Hinweisen, die Karten links bleiben sichtbar; „Alle“, „Erinnerung“, „Wiederbuchung“ und „Fehlgeschlagen“ wechseln die Auswahl, ein Klick auf eine Zeile klappt Text oder Grund auf, „Exportieren“ speichert die Liste.
5. **Kassa & Geld** › **Zahlungen** mit hinterlegtem Testschlüssel (`sk_test_…`) öffnen.
Expected: Die Warnung „Testschlüssel hinterlegt“ steht oben unter dem Stand des Kontos, vor der Wahl der Zahlungsart.
6. **Kassa & Geld** › **Kosten** öffnen, wenn bei einer Behandlung das Material fehlt.
Expected: Über der Tabelle steht ein Hinweis, wie vielen Behandlungen Angaben fehlen; die Zeile der Behandlung nennt weiter, was fehlt.
7. **Kontakt** › **Postfach & Nummern** öffnen, nachdem ein Versand aus dem Postfach fehlgeschlagen ist.
Expected: „Letzter Versand fehlgeschlagen: …“ steht oben beim Stand des letzten Abrufs.
Closes: –
Result: ☐

### Online-Buchung – Einwilligung in die Erinnerung an die nächste Behandlung
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin, dazu der Kundenbereich im Browser
1. In der Windows-App unter **Verwaltung** › **Kontakt** › **Erinnerungen** die Wiederbuchungs-Hinweise einschalten und für eine Behandlung einen Abstand eintragen.
Expected: Die Einstellung ist gespeichert.
2. Im Kundenbereich eine Behandlung und eine Zeit wählen.
Expected: Unter dem Datenschutzhinweis steht das Häkchen „Ich möchte per E-Mail an meine nächste Behandlung erinnert werden (freiwillig) …“; es ist nicht gesetzt.
3. Das Häkchen setzen und buchen.
Expected: Die Buchung wird bestätigt. Im Postfach des Gastes liegt zusätzlich eine Mail zum Bestätigen der Erinnerungen.
4. In der Windows-App den Termin im Kalender öffnen.
Expected: Auf der Karte „Wiederbuchungs-Hinweis“ steht die Einwilligung als angefragt.
5. Die Wiederbuchungs-Hinweise ausschalten und im Kundenbereich erneut bis zu den Angaben gehen.
Expected: Das Häkchen fehlt.
Closes: –
Result: ☐

### Kalender – Lücken schließen in der 7-Tage-Ansicht
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption, **Kalender** öffnen
1. Bei einer Person an zwei Tagen der Woche je zwei Termine so buchen, dass dazwischen 20 Minuten frei bleiben.
Expected: Die Termine stehen im Kalender.
2. Auf den Namen der Person in der Kopfzeile klicken.
Expected: Die 7-Tage-Ansicht der Person erscheint.
3. Im Menü neben **Termin** **Lücken schließen** wählen.
Expected: Oben steht „7 Tage ab …, nur <Name>“. Beide Vorschläge stehen in der Liste, jeder mit seinem Tag vor „bei <Name> von … auf …“.
4. Bei einem Vorschlag **Auf … verschieben** klicken.
Expected: Der Termin springt am richtigen Tag auf die neue Zeit; der Vorschlag verschwindet, der andere bleibt.
5. **Filter aufheben** klicken und **Lücken schließen** erneut öffnen.
Expected: Die Zeile „7 Tage ab …“ fehlt; die Liste gilt nur für den angezeigten Tag.
Closes: –
Result: ☐

### „Mein Tag“ – Einwirkzeit als freie Zeit
Start: `dotnet run --project src/AispaStudio.AppHost`, „Mein Tag“ im Browser als Therapeut:in mit einem Termin „Körperpackung“ heute
1. Den Tagesplan öffnen.
Expected: Auf der Karte der Körperpackung steht unter Zeit und Raum „Einwirkzeit HH:MM–HH:MM (20 Min.): In dieser Zeit sind Sie frei.“ Die Zeiten passen zum Band im Kalender der Windows-App.
2. Eine Karte ohne Einwirkzeit ansehen, z. B. eine Massage.
Expected: Dort steht keine Zeile „Einwirkzeit“.
Closes: –
Result: ☐

### Terminerinnerung – mehrere Termine eines Gastes am selben Tag
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption; ein Gast mit Mailadresse
1. Für den Gast am selben Tag zwei Termine buchen, z. B. 10:00 und 13:00 Uhr, so dass die erste Erinnerung bald fällig ist (Verwaltung › Kontakt › Erinnerungen: Stunden passend einstellen).
Expected: Beide Termine stehen im Kalender.
2. Etwa fünf Minuten warten und das Postfach des Gastes ansehen.
Expected: Eine Mail „Erinnerung: Ihre 2 Termine am …“ mit beiden Uhrzeiten; unter jedem Termin steht ein eigener Link „Bestätigen oder absagen“.
3. Den zweiten Termin im Kalender öffnen und die Erinnerungen ansehen.
Expected: Dort steht „Zusammen mit dem Termin um 10:00 Uhr erinnert.“; eine eigene Mail zum zweiten Termin kommt nicht.
Closes: –
Result: ☐

### Inventur am Handy – Funkloch beim Zählen
Start: „Mein Tag“ am Handy als Therapeut:in, Reiter **Inventur**, eine Zählung an einem Lagerort gestartet
1. Am Handy den Flugmodus einschalten und bei zwei Materialien eine Menge eintragen.
Expected: Unter der Liste steht „2 Mengen warten auf Verbindung.“; **Abschicken** ist gesperrt; es erscheint kein „Nicht gespeichert“.
2. Den Flugmodus ausschalten.
Expected: Nach wenigen Sekunden steht „Gespeichert“, der Hinweis verschwindet, **Abschicken** geht wieder.
3. Erneut Flugmodus, eine Menge ändern, die Seite schließen, den Flugmodus ausschalten, „Mein Tag“ öffnen und die Zählung am selben Lagerort fortsetzen.
Expected: Die geänderte Menge steht im Feld und wird gleich gespeichert.
4. In der Windows-App unter **Verwaltung** › **Lager** › **Inventur** die Zählung übernehmen.
Expected: Die Mengen vom Handy stehen in der Zählliste.
Closes: –
Result: ☐

### Lager – Lieferung am Handy mit Lieferschein-Foto
Start: API mit eingerichtetem Schlüssel (`Ai:Claude:ApiKey`), „Mein Tag“ am Handy als Standortleitung; in der Windows-App eine Bestellung mit zwei Positionen als bestellt markiert; ein Lieferschein dazu auf Papier
1. In „Mein Tag“ den Reiter **Lieferung** öffnen.
Expected: Die Bestellung steht zur Wahl, darunter je offener Position die Felder Geliefert, Charge und Ablaufdatum.
2. Auf **Lieferschein fotografieren (optional)** tippen und den Lieferschein aufnehmen.
Expected: Nach einigen Sekunden steht „Von der KI gelesen.“; Mengen, Chargen, Ablaufdaten und die Lieferscheinnummer sind eingetragen und stimmen mit dem Papier überein. Unsichere Positionen tragen „Bitte prüfen: …“.
3. Eine Menge ändern und auf **Lieferung buchen** tippen.
Expected: „Lieferung zu Bestellung … gebucht“. In der Windows-App zeigt die Bestellung die gelieferten Mengen, der Bestand ist gestiegen.
4. Dasselbe ohne Schlüssel (oder mit `Ai:Provider=RuleBased`): Foto wählen.
Expected: Der Hinweis „Das Lesen von Lieferscheinen ist nicht eingerichtet. Bitte die Mengen von Hand eintragen.“; von Hand eintragen und buchen geht.
5. Als Rezeption „Mein Tag“ öffnen.
Expected: Den Reiter **Lieferung** gibt es nicht.
Closes: –
Result: ☐

### T-066 – Team: fünf Ansichten, Anträge als Seitenleiste im Wochenplan
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden, **Verwaltung** › **Team** öffnen
1. Die Leiste über der Fläche ansehen.
Expected: Sie zeigt fünf Ansichten in einer Zeile: Personen, Wochenplan, Zeiten, Lohn, Rechte und Zuständigkeiten. „Anträge“, „Wer darf was?“ und „Zuständigkeiten“ stehen dort nicht mehr.
2. **Wochenplan** wählen.
Expected: Oben in der Werkzeugleiste steht **Anträge**; liegen offene Anträge vor, steht ihre Zahl daneben. Ohne offene Anträge fehlt die Zahl.
3. **Anträge** klicken.
Expected: Rechts öffnet sich eine Seitenleiste „Anträge“ über dem Wochenraster, mit Fragezeichen und Kreuz im Kopf. Jeder Antrag zeigt Person, Art, Zeitraum, Anmerkung und darunter **Ablehnen** und **Genehmigen**. Der Fokus steht in der Leiste; F6 erreicht sie als eigenen Bereich.
4. **Esc** drücken.
Expected: Die Leiste schließt, der Fokus steht wieder auf **Anträge**. Ein zweites Mal öffnen und mit dem Kreuz schließen.
5. Die Leiste öffnen und einen Antrag mit **Genehmigen** bestätigen.
Expected: Die Meldung nennt die genehmigte Abwesenheit, der Antrag verschwindet, die Zahl am Knopf sinkt um eins, und der Wochenplan zeigt die Abwesenheit.
6. Das Fenster auf die Mindestgröße 1100 × 680 verkleinern und die Leiste öffnen.
Expected: Die Leiste liegt rechts über dem Raster, das Raster behält seine Breite und lässt sich unter der geschlossenen Leiste weiter bedienen. Beide Knöpfe jedes Antrags stehen vollständig in der Leiste.
7. **Personen** wählen, eine Person mit offenem Antrag öffnen und in der Übersicht die Kachel **Offene Anträge** klicken.
Expected: Der Wochenplan öffnet sich mit der Seitenleiste **Anträge**.
8. **Rechte und Zuständigkeiten** wählen.
Expected: Die Seite zeigt untereinander die Abschnitte **Wer darf was?** und **Zuständigkeiten**, jeder mit Zwischentitel und Fragezeichen. Die Liste der Personen fehlt.
9. Als Rezeption anmelden und **Team** öffnen.
Expected: Die Rezeption sieht nur den Wochenplan, ohne Schaltfläche **Anträge** und ohne **Rechte und Zuständigkeiten**. Als Standortleitung sieht man Wochenplan und Zeiten mit der Schaltfläche **Anträge**, aber nicht **Rechte und Zuständigkeiten**.
Closes: –
Result: ☐

### „Mein Tag“ – das eigene Urlaubskonto
Start: In der Windows-App bei einer Person Anspruch 25 und Übertrag 2,5 für das laufende Jahr eingetragen und einen Urlaubstag an einem ihrer Arbeitstage veröffentlicht; „Mein Tag“ als diese Person
1. Den Reiter **Abwesenheit** öffnen.
Expected: Über dem Formular steht „Urlaubskonto <Jahr>“ mit „Rest 26,5 Tage von 27,5 Tage (genommen … , geplant …)“; die Zahlen stimmen mit der Karte der Person in der Windows-App überein.
2. Als Person ohne eingetragenen Anspruch öffnen.
Expected: „Genommen …, geplant …. Ein Anspruch ist für dieses Jahr noch nicht eingetragen.“
Closes: –
Result: ☐

### Kundenbereich – „Ich komme später“ durch den Gast
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App mit offenem Kalender; ein online gebuchter Termin, der in etwa einer Stunde beginnt; die Bestätigungsmail des Gastes
1. Den Link „Termin ansehen“ aus der Mail öffnen.
Expected: Unter dem Termin steht „Ich komme später“ mit 5, 10, 15, 20 und 30 Minuten.
2. Auf **15 Minuten** tippen.
Expected: „Sie haben gemeldet, dass Sie 15 Minuten später kommen.“ Im Kalender der Windows-App steht am Termin ohne Neuladen „kommt …“ mit der späteren Zeit.
3. Auf **Doch pünktlich** tippen.
Expected: Der Hinweis verschwindet auf der Seite und im Kalender.
4. Den Link eines Termins öffnen, der erst in einigen Tagen ist.
Expected: „Ich komme später“ wird nicht angeboten.
Closes: –
Result: ☐

### Anfragen – einen späteren als den nächsten Termin absagen
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption; ein Gast mit Mailadresse und zwei gebuchten Terminen an verschiedenen Tagen; eine Mail des Gastes „den zweiten Termin muss ich absagen“ über das Test-Postfach
1. **Anfragen** öffnen und die Nachricht anklicken.
Expected: Über „Termin stornieren“ steht **Welcher Termin?** mit beiden Terminen; gewählt ist der frühere mit „(nächster Termin)“.
2. Den späteren Termin wählen und **Termin stornieren** klicken.
Expected: Die Rückfrage nennt Tag, Uhrzeit und Behandlung des späteren Termins.
3. Bestätigen.
Expected: „Termin storniert. Die Antwort wird neu entworfen.“ Im Kalender ist der spätere Termin storniert, der frühere steht noch; der Antwortentwurf nennt den späteren Termin.
Closes: –
Result: ☐

### „Mein Tag“ – Hinweis „Anamnese prüfen“
Start: Eine Behandlung mit dem Häkchen „Anamnese prüfen“ (Verwaltung › Behandlungen); ein Gast mit ausgefülltem Fragebogen, in dem mindestens ein „Ja“ steht; ein Termin dieses Gastes heute bei einer Therapeutin; „Mein Tag“ als diese Therapeutin
1. Den Tagesplan öffnen.
Expected: Auf der Karte des Termins steht „Anamnese prüfen: Der Fragebogen enthält Angaben, die für diese Behandlung wichtig sein können.“ (oder „Allergie angegeben …“, wenn das Ja bei den Allergien steht). Der Inhalt des Bogens steht nicht da.
2. Einen Termin desselben Gastes mit einer Behandlung ohne das Häkchen ansehen.
Expected: Kein Hinweis „Anamnese prüfen“.
Closes: –
Result: ☐

### Online-Buchung – Wunsch „Therapeutin“ oder „Therapeut“
Start: Im Team ist bei einer Person „Therapeutin“ und bei einer anderen „Therapeut“ eingetragen, beide mit derselben Behandlung und denselben Arbeitszeiten; der Kundenbereich im Browser
1. Behandlung wählen und zum Schritt „Zeit“ gehen.
Expected: Über den Tagen steht „Behandlung durch (freiwillig)“ mit Egal, Therapeutin, Therapeut.
2. Die Therapeutin im Kalender der Windows-App für eine freie Zeit buchen, dann im Kundenbereich „Therapeutin“ wählen.
Expected: Diese Zeit fehlt in der Liste; mit „Therapeut“ oder „Egal“ ist sie da.
3. „Therapeut“ wählen, eine Zeit nehmen und buchen.
Expected: Im Kalender steht der Termin beim Therapeuten; in der Notiz steht „Wunsch bei der Online-Buchung: Therapeut.“
Closes: –
Result: ☐

### Berichte – KI-Kommentar im Managementbericht
Start: `dotnet run --project src/AispaStudio.AppHost` mit KI-Schlüssel und Beispieldaten, Windows-App als Inhaber
1. **Verwaltung** › **Zahlen** › **Berichte** öffnen; Zeitraum vom Monatsersten bis heute (mindestens sieben Tage).
Expected: Zuerst „Die KI kommentiert den Zeitraum …“, nach ein paar Sekunden drei bis fünf Sätze mit der Pille „KI“ und dem Hinweis „Maßgeblich sind die Zahlen selbst.“; jede Zahl darin steht auch in den Kennzahlen.
2. Als Standortleitung ohne „Berichte mit Beträgen“ denselben Zeitraum öffnen.
Expected: Der Kommentar nennt keine Beträge.
3. Einen einzelnen Tag wählen (Von = Bis).
Expected: Kein Kommentar.
Closes: –
Result: ☐

### Kunden – Vorschläge für Kennzeichen
Start: `dotnet run --project src/AispaStudio.AppHost` mit Beispieldaten; ein Gast mit zwei Terminen „Nicht erschienen“ in den letzten 12 Monaten
1. In der Windows-App als Inhaberin **Kunden** öffnen und den Gast wählen.
Expected: Im Abschnitt **Kennzeichen** steht „Vorschlag: Leitung informieren“ mit „In den letzten 12 Monaten 2-mal nicht erschienen.“
2. **Verwerfen** klicken, die Karte neu öffnen.
Expected: Der Vorschlag kommt nicht wieder. Als Rezeption ist der Vorschlag nie zu sehen.
3. Bei einem Gast mit zwei gemeldeten Verspätungen von 10 Minuten als Rezeption **Übernehmen** bei „Braucht mehr Zeit“.
Expected: Das Kennzeichen steht mit der Begründung als Grund; ein neuer Termin bekommt 10 Minuten Puffer.
Closes: –
Result: ☐

### Kalender – Lücken schließen mit Kette
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption; bei einer Person drei Massagen an einem Tag: 9:05, 10:30 und direkt danach 11:35
1. Im Menü neben **Termin** **Lücken schließen** wählen.
Expected: Ein Vorschlag für 10:30 auf 10:10 mit der Zeile „Danach: …, 11:35 auf 11:15“ und dem Knopf **Alle 2 verschieben**.
2. **Alle 2 verschieben** klicken.
Expected: Beide Termine stehen im Kalender 20 Minuten früher; die Meldung „2 Termine bei … verschoben“; der Vorschlag ist weg.
Closes: –
Result: ☐

### Behandlungskarte – Gesundheitshinweise mit Protokoll
Start: `dotnet run --project src/AispaStudio.AppHost` mit Beispieldaten; ein Gast mit ausgefülltem Fragebogen (z. B. „Medikamente: Ja, Blutverdünner“) und einem Termin bei Lena heute
1. Als Lena „Mein Tag“ am Handy öffnen.
Expected: Auf der Karte des Gastes steht **Gesundheitshinweise ansehen**; nach dem Antippen „Medikamente: Blutverdünner“ und „Ihr Abruf wird protokolliert.“
2. Als Rezeption denselben Termin über die Behandlungskarte öffnen; als andere Therapeutin ohne diesen Termin versuchen.
Expected: Die Rezeption sieht die Hinweise; die andere Therapeutin bekommt keinen Knopf (und der Aufruf wird abgelehnt).
3. In der Windows-App als Inhaberin den Termin öffnen, Karte **Anamnese**, **Abrufe ansehen**.
Expected: Lenas Abruf als „Behandlungskarte (Behandlung am Datum)“ mit Uhrzeit; ein **Ansehen** in der Karte erscheint danach als „Bogen im Termin“. Als Rezeption gibt es den Knopf **Abrufe ansehen** nicht.
Closes: –
Result: ☐

### Berichte – Wochen- und Monatsbericht per Mail
Start: `dotnet run --project src/AispaStudio.AppHost` mit KI-Schlüssel, Beispieldaten und Mailversand (Mailpit), Server-Uhr auf einen Montag nach 7 Uhr
1. Eine Stunde warten (oder den Server neu starten) und das Postfach des Inhabers öffnen.
Expected: Eine Mail „Wochenbericht {Standort}: … bis …“ mit KI-Kommentar, „Worauf achten:“, Kennzahlen mit „(Woche davor: …)“ und dem Hinweis zum Abbestellen; die Rezeption bekommt keine.
2. Server neu starten.
Expected: Keine zweite Mail für dieselbe Woche.
3. In der Windows-App als Manager **Verwaltung** › **Zahlen** › **Berichte** › Management: Haken **Wochen- und Monatsbericht per Mail** entfernen; als Rezeption denselben Reiter öffnen.
Expected: Der Haken bleibt nach erneutem Öffnen entfernt; die nächste Mail geht nicht mehr an den Manager. Die Rezeption sieht keinen Haken.
Closes: –
Result: ☐

### Berichte – Wirkung von Aktionen
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaber, eine laufende Aktion „Herbst“ für alle Behandlungen
1. An der Kassa zwei Termine (ein Stammgast, ein neuer Gast) abrechnen, jeweils **Anwenden** beim Hinweis zur Aktion, bezahlen. Einen dritten mit Aktion bezahlen und stornieren.
2. **Verwaltung** › **Zahlen** › **Berichte** › Management, Zeitraum mit heute.
Expected: Tabelle „Aktionen“ mit Herbst: 2 Positionen, 2 Gäste, davon 1 neu, Rabatt und Umsatz brutto passend zu den Belegen.
3. Als Standortleitung ohne „Berichte mit Beträgen“ denselben Bericht öffnen.
Expected: Positionen und Gäste stehen da, Rabatt und Umsatz zeigen „–“.
Closes: –
Result: ☐

### Leiste „KI und Hilfe“ – neuen Gast beim Buchen anlegen
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption, mit KI-Schlüssel
1. Rechts die Leiste **KI und Hilfe** öffnen, „Wann ist morgen eine Massage frei?“ fragen und eine Zeit anklicken.
Expected: Unter „Für wen?“ stehen Suchfeld und **Neuer Gast …**.
2. „Mara Neu“ ins Suchfeld tippen, **Neuer Gast …** klicken, eine Telefonnummer ergänzen und **Anlegen** klicken.
Expected: Vor- und Nachname waren schon eingetragen; nach dem Anlegen steht „Mara Neu“ unter „Für wen?“, **Termin buchen** ist bereit. In **Kunden** gibt es Mara Neu.
Closes: –
Result: ☐

### Posteingang – Verschiebungswunsch übernehmen
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption; ein Gast mit Mailadresse hat einen gebuchten Termin in den nächsten Tagen
1. Vom Gast eine Mail an das Test-Postfach schicken: „Hallo, ich muss meinen Termin leider verschieben, geht Dienstag nachmittags?“
Expected: In **Anfragen** steht die Nachricht mit „Prüfen“; der Hinweis nennt den Termin, darunter **Freie Zeiten zum Verschieben** mit bis zu drei Knöpfen „Auf … verschieben“, möglichst am Dienstagnachmittag.
2. Eine Zeit anklicken und in der Rückfrage **Verschieben** wählen.
Expected: „Termin auf … verschoben. Die Antwort wird neu entworfen.“; im Kalender liegt der Termin auf der neuen Zeit; der Entwurf an den Gast nennt die neue Zeit.
Closes: –
Result: ☐

### Wiederbuchungs-Hinweis – Abmelden mit einem Klick im Mailprogramm
Start: Testbetrieb mit echtem Mailversand an ein eigenes Gmail- oder Outlook-Postfach; ein Gast mit bestätigter Einwilligung, dessen letzte Behandlung lange genug her ist
1. Den Wiederbuchungs-Hinweis an das Postfach gehen lassen und die Mail öffnen.
Expected: Neben dem Absender bietet das Mailprogramm **Abmelden** an (Gmail: „Abmelden“ oben; Outlook: Hinweisleiste).
2. **Abmelden** klicken.
Expected: Keine Seite öffnet sich; in der Windows-App zeigt die Kundenkarte die Einwilligung als widerrufen.
Closes: –
Result: ☐

### Termin-Panel – Karten- und Anzahlungslink per SMS
Start: `dotnet run --project src/AispaStudio.AppHost` mit eingerichtetem SMS-Anbieter (Twilio), Windows-App als Rezeption; am Standort sind Ausfallgebühr oder Anzahlung eingestellt; ein Gast ohne Mailadresse, mit eigener Handynummer in der Kundenkarte
1. Für den Gast einen Termin buchen, öffnen und **Anzahlung per Link** (oder **Karte per Link anfordern**) klicken.
Expected: Meldung „Link an den Gast verschickt“; aufs Handy kommt eine SMS „Anzahlung für …: <Link>“.
2. Den Link öffnen und bezahlen (bzw. zustimmen und die Karte hinterlegen).
Expected: Nach dem Bezahlen kommt die SMS „Anzahlung erhalten: …“. Bei der Karte kommt nach „Nicht erschienen“ und **Ausfallgebühr belasten …** die SMS „Ausfallgebühr für …“.
3. Einen Gast ohne Mailadresse und ohne Handynummer öffnen.
Expected: Kein Knopf zum Anfordern.
Closes: –
Result: ☐

### Sauna und Ruheraum – Namen am Eintritt nachtragen
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption; am Standort gibt es eine Sauna als Bereich
1. **Paar oder Spa-Tag** mit Gast 1 aus der Kartei (Massage) und Gast 2 mit **Name folgt** buchen, Gast 2 bekommt nur die Sauna.
Expected: In **Sauna und Ruheraum** steht der Eintritt „Gast 2 · Name folgt“ mit **Namen nachtragen** darunter.
2. **Namen nachtragen** klicken, einen Gast suchen und **Gast eintragen** klicken.
Expected: Der Eintritt trägt den Namen des Gastes; die Massage von Gast 1 bleibt bei Gast 1.
Closes: –
Result: ☐

### Paar und Spa-Tag – Ausfallgebühr für einen Gast ohne Namen
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption; am Standort ist eine Ausfallgebühr eingestellt, Zahlungsanbieter im Testbetrieb; die buchende Person hat eine Mailadresse
1. **Paar oder Spa-Tag** mit Gast 1 aus der Kartei und Gast 2 mit **Name folgt** buchen, den Termin von Gast 2 öffnen und **Karte per Link anfordern** klicken.
Expected: Die Mail geht an die buchende Person und nennt „… – für Gast 2 Ihrer Buchung“.
2. Für den Termin von Gast 2 **Namen nachtragen** und einen anderen Gast eintragen; danach den Link aus der Mail öffnen.
Expected: Die Terminseite öffnet sich und bietet das Hinterlegen der Karte an, aber kein Absagen und kein Verschieben.
3. Zustimmen und die Karte hinterlegen (im Testbetrieb „Karte simulieren“), dann den Termin in der Windows-App öffnen.
Expected: Unter „Ausfallgebühr“ steht „Karte hinterlegt …“ und „Zahlt die buchende Person der Gruppe.“
Closes: –
Result: ☐

### Kassa – Online-Zahlungen nachsehen
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption; Zahlungsanbieter im Testbetrieb; im Kundenbereich einen Gutschein kaufen und mit „Zahlung simulieren“ bezahlen
1. In der Kassa oben auf das Weltkugel-Symbol **Online-Zahlungen** klicken.
Expected: Die Karte zeigt unter „Online gekaufte Gutscheine“ den Gutschein mit Betrag, Zeit, „Ausgestellt“ und Beleg; unter „Erstattungen“ steht „Noch keine.“
2. Als Kassenaufsicht unter „Belege“ den Gutscheinbeleg stornieren und die Karte neu öffnen.
Expected: Der Gutschein steht als „Storniert und erstattet“; unter „Erstattungen“ steht er mit Beleg, Stornobeleg und „Erstattet am …“.
Closes: –
Result: ☐

### Paar und Spa-Tag – Sauna für einen Gast ohne Namen
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption; am Standort gibt es eine Sauna als Bereich
1. **Paar oder Spa-Tag** öffnen, Gast 1 aus der Kartei wählen, bei Gast 2 **Name folgt** anhaken. Im ersten Schritt Gast 1 eine Massage und Gast 2 die Sauna (60 Min.) geben, im zweiten Schritt umgekehrt; eine Zeit suchen.
Expected: Unter den Gästen steht „… Die Namen lassen sich später im Termin nachtragen, auch für Sauna und Ruheraum.“; **Buchen** ist nicht gesperrt.
2. Buchen und die Belegung „Sauna und Ruheraum“ ansehen.
Expected: Ein Eintritt steht als „Gast 2 · Name folgt“, der andere auf Gast 1.
3. Den Massagetermin von Gast 2 öffnen, **Namen nachtragen** und einen Gast eintragen.
Expected: Auch der Sauna-Eintritt von Gast 2 trägt danach diesen Namen.
Closes: –
Result: ☐

### Ausfall-Assistent – Ersatz an einem anderen Tag
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption; eine Person mit einem Termin heute, alle anderen mit derselben Qualifikation sind heute abwesend (Urlaub im Dienstplan), arbeiten aber an einem der nächsten Tage; der Gast hat eine Mailadresse; kein zweiter Standort mit Platz
1. Die Person krank melden und den Ausfall-Assistenten öffnen.
Expected: Der Termin steht unter **Andere Tage** mit Tag, Uhrzeit, Person und Raum, nicht angehakt; die Erklärung endet mit „nur nach Rücksprache mit dem Gast übernehmen“.
2. Den Vorschlag anhaken und **Gewählte übernehmen** klicken.
Expected: Der Termin liegt am vorgeschlagenen Tag; im Postfach des Gastes liegt „Änderung Ihres Termins am …“ mit „Wie besprochen verschieben wir Ihren Termin …“.
Closes: –
Result: ☐

### Termin-Panel – Anzahlung per Link anfordern
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption; am Standort ist in den Online-Regeln eine Anzahlung (z. B. 30 %) eingestellt, Zahlungskonto (Testbetrieb genügt) und Kassa sind eingerichtet; ein Gast mit Mailadresse
1. Für den Gast in der Windows-App einen Termin in den nächsten Tagen buchen und ihn öffnen.
Expected: Im Abschnitt „Anzahlung“ steht „Der Standort verlangt 30 % (…)“ und der Knopf **Anzahlung per Link**.
2. Auf **Anzahlung per Link** klicken.
Expected: Meldung „Link an den Gast verschickt“; im Abschnitt steht „Noch nicht bezahlt · … (30 %)“, der Knopf heißt jetzt **Link erneut senden**. Im Postfach des Gastes liegt die Mail „Anzahlung für …“ mit Betrag und Link.
3. Den Link öffnen und die Anzahlung bezahlen (im Testbetrieb „Zahlung simulieren“).
Expected: Auf der Terminseite steht keine Frist zum Bezahlen. Nach dem Bezahlen zeigt das Termin-Panel „Bezahlt, Anzahlungsbeleg erstellt“, der Knopf ist verschwunden; an der Kassa ist der Betrag beim Kassieren des Termins abgezogen.
4. Einen zweiten Termin für einen Gast ohne Mailadresse buchen und öffnen.
Expected: Kein Knopf **Anzahlung per Link**.
Closes: –
Result: ☐

### Ausfall-Assistent – Gäste ohne Ersatz benachrichtigen
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Rezeption; eine Person mit einem Termin heute, für den niemand sonst die Qualifikation hat; der Gast hat eine Mailadresse
1. Im Kalender die Person als ausgefallen markieren und den Ausfall-Assistenten öffnen.
Expected: Beim Termin steht „kein Ersatz an diesem Tag“; unter „Gewählte übernehmen“ steht **Ohne Ersatz: informieren**.
2. Auf **Ohne Ersatz: informieren** klicken.
Expected: Die Rückfrage „1 Gast benachrichtigen?“ erklärt, dass der Termin stehen bleibt.
3. Bestätigen.
Expected: An der Zeile steht „Gast benachrichtigt“, darunter „1 Gast benachrichtigt.“ Im Postfach des Gastes liegt die Mail „Ihr Termin am …“; der Termin steht weiter im Kalender. Der Knopf ist verschwunden.
Closes: –
Result: ☐

### Masken-Durchsicht – Mehr, Sauna-Zeilenmenü und Stand „Bezahlt“
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin anmelden, **Kalender**; einen gebuchten Termin von heute und für die Sauna einen Eintritt mit zwei Gästen anlegen
1. Den Termin anklicken und im Termin-Panel **Mehr** öffnen.
Expected: Eine schlichte Liste mit einheitlichen Symbolen; **Termin stornieren …** steht darin und fragt nach. **Entf** fragt ebenfalls nach.
2. In der Sauna auf **⋯** an der Zeile eines Gastes klicken.
Expected: Das Menü bietet **Stornieren**; der Name in der Zeile bricht um, statt gekürzt zu werden.
3. Den Termin in der **Kassa** kassieren und bar bezahlen.
Expected: Statt der leeren Zahlungsmaske steht „Bezahlt · Beleg Nr. …“ mit Zahlungsart und bei Überzahlung „Rückgeld: …“; der Fokus liegt im Suchfeld, ein Satz nennt, womit die nächste Rechnung beginnt.
Closes: –
Result: ☐

### Kunden – Anonymisierter Gast verschwindet aus dem Stand ohne Verbindung
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App auf zwei Geräten (oder zwei Windows-Konten) als Inhaberin anmelden; auf beiden **Kunden** öffnen und denselben Gast ohne künftige Termine suchen
1. Auf Gerät A die Kundenkarte öffnen, **…** und **Gast anonymisieren …** wählen, bestätigen. Danach die Verbindung von Gerät A trennen und den Namen suchen.
Expected: Die Suche ohne Verbindung findet den Gast nicht; andere Gäste findet sie.
2. Auf Gerät B höchstens 10 Minuten warten oder die App neu starten, dann die Verbindung trennen und den Namen suchen.
Expected: Auch Gerät B findet den Gast nicht; im Kalender steht an seinen alten Terminen nach dem Neuladen „Gelöschter Gast“.
Closes: –
Result: ☐

### Verwaltung – „Gespeichert“ erscheint als Toast unten in der App
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin anmelden
1. **Verwaltung** › **Betrieb** › **Firmendaten** öffnen, ein Feld ändern, **Speichern** klicken.
Expected: Unten mittig im Seitenbereich erscheint für 5 Sekunden „Gespeichert“ (ohne Punkt); neben **Speichern** steht kein Text mehr. Der Mauszeiger auf der Meldung hält sie an, das Kreuz schließt sie.
2. Gleich danach in **Team** eine Person öffnen, die Rolle ändern und speichern.
Expected: Es steht wieder nur eine Meldung „Gespeichert“; sie ersetzt die vorige.
3. Im **Kalender** einen Termin auf eine andere Zeit ziehen.
Expected: Dieselbe Meldung unten mittig zeigt „Verschoben auf …“ mit **Rückgängig** und bleibt 8 Sekunden; **Rückgängig** und Strg+Z setzen den Termin zurück.
4. Im **Lager** **Exportieren** wählen und eine Datei speichern.
Expected: Die Meldung „Gespeichert: …“ trägt den Knopf **Ordner öffnen**.
5. Im Team **Person hinzufügen**, mit Zugang anlegen.
Expected: Der Hinweis zur Anmeldung mit dem Startpasswort steht als Hinweisleiste auf der Karte und bleibt, bis man ihn schließt.
Closes: –
Result: ☐

### Meldung unten – Toast sitzt über der unteren Leiste jeder Seite
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin anmelden
1. Im **Kalender** einen Termin auf eine andere Zeit ziehen.
Expected: Die Meldung „Verschoben auf …“ steht über der Tastenleiste am unteren Rand; die Leiste bleibt ganz lesbar.
2. Das Fenster auf etwa 1100 Pixel Breite ziehen, **Verwaltung** › **Aktionen** öffnen, eine Aktion ändern und **Speichern** klicken.
Expected: „Gespeichert“ steht über der Leiste mit **Speichern** und **Verwerfen**; beide Knöpfe bleiben frei.
3. In der **Kassa** einen verkauften Gutschein als übergeben vermerken.
Expected: Die Meldung „Gutschein … als übergeben vermerkt“ steht über der Zeile **Summe**; die Summe bleibt frei.
Closes: –
Result: ☐

### T-086 – „Speichern“ bleibt aktiv und prüft
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin anmelden
1. **Verwaltung** › **Team**, **Person hinzufügen**, nichts eintragen, **Speichern** klicken.
Expected: **Speichern** ist nicht grau. Das Feld **Vollständiger Name** wird rot und nennt, was fehlt; der Cursor steht darin; es wird keine Person angelegt.
2. Im **Kalender** **Termin** öffnen, Behandlung wählen, keinen Gast eintragen, **Termin buchen** klicken.
Expected: Das Feld für den Gast wird rot mit Fehlertext und hat den Cursor; es wird nichts gebucht.
3. In der **Kassa** **Einlage** wählen, Grund eintragen, den Betrag leer lassen, den Knopf klicken.
Expected: Das Betragsfeld wird rot und nennt, was fehlt; es wird nichts gebucht.
4. In einer **Kundenkarte** einen Rabatt eintragen, die PIN der Freigabe leer lassen, den Knopf klicken.
Expected: Das Feld der PIN wird rot und hat den Cursor; es wird kein Rabatt vergeben.
5. **Verwaltung** › **Geräte**, ein Terminal ohne Adresse speichern.
Expected: Das Feld der Adresse wird rot mit Fehlertext; nichts wird gespeichert.
6. Gegenprobe: In der **Kassa** weniger zahlen, als die Rechnung verlangt.
Expected: **Bezahlen und Beleg erstellen** bleibt grau; der Hinweis am Knopf lautet „Verfügbar, sobald die Zahlungen den Betrag decken und jeder Betrag lesbar ist.“
7. Wieder in **Team** **Person hinzufügen**, den Namen leer lassen, **Strg+S** drücken.
Expected: Dasselbe wie in Schritt 1: Das Namensfeld wird rot, nichts wird gespeichert.
Closes: Regelfrage 2 der Masken-Durchsicht vom 19.11.2026 (Speichern ohne Pflichtangaben)
Result: ☐

### T-087 – Kalender: Knöpfe zu verdeckten Spalten stehen in der Kopfzeile
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin anmelden
1. Im **Kalender** auf **Räume** schalten und das Fenster so schmal ziehen, dass nicht alle Räume Platz haben.
Expected: Ganz rechts in der zweiten Zeile der Kopfzeile steht ein Knopf mit „+n“ und Pfeil nach rechts. Über den Karten der letzten Spalte liegt kein Knopf; ihr Text ist ganz lesbar.
2. Auf den Knopf klicken.
Expected: Der Kalender blättert eine Fensterbreite weiter. Links daneben erscheint ein zweiter Knopf mit Pfeil nach links und „+n“; der Hinweis nennt die Namen der verdeckten Räume.
3. Das Fenster auf etwa 1100 Pixel Breite ziehen und in die Mitte scrollen.
Expected: Beide Knöpfe stehen nebeneinander rechts in Zeile 2, kein Chip liegt darunter oder darüber, die Kopfzeile bleibt zwei Zeilen hoch.
4. Das Fenster so breit ziehen, dass alle Spalten Platz haben.
Expected: Beide Knöpfe verschwinden; die Chips der Zeile 2 stehen wie bisher.
Closes: R1 der Masken-Durchsicht vom 19.11.2026 (Knopf am rechten Kalenderrand deckt Kartentext)
Result: ☐

### T-088 – Begriffe: „Kassa“, „Haupt-PC“, „Server“
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App als Inhaberin anmelden
1. Die **Kassa** öffnen und die Auswahl oben ansehen.
Expected: Dort steht „Kassa WIEN-1“, nicht „Kasse WIEN-1“.
2. Einen Termin kassieren und den Beleg ansehen; danach den Tagesabschluss als PDF öffnen.
Expected: Auf Beleg und Tagesabschluss steht „Kassa“ vor der Kassen-ID. „Kassenlade“ und „Kassenjournal“ heißen wie bisher.
3. Die API anhalten (im Aspire-Dashboard den Dienst stoppen) und in der App **Verbindung prüfen** klicken.
Expected: Die Warnung beginnt mit „Keine Verbindung.“ und nennt keinen Server.
4. Im **Kalender** die **Warteliste** öffnen.
Expected: Der Hinweis lautet „Wird etwas frei, schreibt AISPA Studio den Wartenden selbst.“
5. Die Hilfe öffnen und nach „Kasse“ suchen.
Expected: Die Seiten zur Kassa erscheinen (Suchwort bleibt), im Text steht „Kassa“.
6. Gegenprobe: **System** › **Netz** öffnen.
Expected: „Mit Server verbinden“ und „Haupt-PC“ stehen dort wie bisher.
Closes: Regelfrage „einheitliche Begriffe“ der Masken-Durchsicht vom 19.11.2026
Result: ☐

### T-091 – Veraltete Beispieldaten beim Start neu einspielen
Start: Visual Studio, Startprojekt `AispaStudio.AppHost`, Konfiguration Debug
1. In `src/AispaStudio.Api/appsettings.Development.json` `"DemoData": { "WeeksAhead": 1 }` eintragen, den AppHost starten, an `postgres` im Aspire-Dashboard „Beispiel-Spa neu einspielen …“ ausführen und warten, bis die API bereit ist.
Expected: Der Kalender reicht nur rund eine Woche voraus.
2. Den Eintrag aus Schritt 1 wieder entfernen und den AppHost neu starten.
Expected: Im Log der API steht „Beispieldaten veraltet (letzter Termin …): Datenbank aispadb neu angelegt“. Nach etwa einer halben Minute reicht der Kalender wieder vier Wochen voraus.
3. Den AppHost ein zweites Mal neu starten.
Expected: Keine solche Logzeile; die Daten bleiben, wie sie sind.
Closes: Auftrag vom 25.11.2026 (Kalender nach Ablauf der Beispieldaten leer)
Result: ☐

### T-099 – Drucken öffnet den Druckdialog von Windows
Start: `dotnet run --project src/AispaStudio.AppHost`, Windows-App, als Inhaberin anmelden; in Windows „Microsoft Print to PDF“ oder OneNote als Standarddrucker, ein zweiter Drucker ist eingerichtet.
1. **Team** → **Wochenplan**, eine veröffentlichte Woche wählen, **Aushang als PDF**, dann **Drucken**.
Expected: Nach ein bis zwei Sekunden öffnet sich der Druckdialog von Windows („Microsoft Edge WebView2 – Drucken“), unten steht kurz „Druckdialog geöffnet“. Es öffnet sich weder OneNote noch eine Frage nach dem Speicherort. Kein zusätzliches Fenster der App und kein Eintrag in der Taskleiste.
2. Im Dialog den zweiten Drucker wählen, die Ausrichtung ansehen, **Querformat** wählen und drucken.
Expected: Der zweite Drucker ist wählbar. Notieren, ob die Ausrichtung auf Hoch- oder Querformat vorbelegt war. Der Ausdruck zeigt den ganzen Aushang auf einer Seite quer, mit scharfer Schrift, nicht leer.
3. **Drucken** noch einmal klicken und im Dialog **Abbrechen**.
Expected: Der Dialog geht wieder auf und schließt ohne Druck, in der App steht keine Fehlermeldung, die Druckerwarteschlange bleibt leer.
4. **Kassa**, einen Beleg öffnen, **A4 drucken**, im Dialog „Microsoft Print to PDF“ wählen und in eine Datei drucken.
Expected: Das PDF zeigt die Rechnung mit Rändern und Schrift wie bei **PDF speichern**.
5. Einen Gutschein verkaufen, unter dem Beleg **Drucken**, im Dialog **Abbrechen**; danach **Gutscheinliste …** → **PDF speichern**.
Expected: In der Spalte „Versand“ steht beim Gutschein „Zum Drucken geöffnet am <Datum>“.
6. Die App mit dem Kreuz schließen.
Expected: Die App beendet sich ganz; im Task-Manager bleibt kein Prozess „AispaStudio.Desktop“.
Closes: Ä388
Result: ☐

### T-103 – Dienstplan-Export mit Tagen links
Start: Windows-App, angemeldet als Inhaberin, Bereich „Team“, Wochenplan mit Diensten in mindestens zwei Wochen
1. „Exportieren“ → „Dienstplan der Woche“ als Excel wählen und die gespeicherte Datei in Excel öffnen.
Expected: Die erste Spalte heißt „Tag“ und trägt sieben Zeilen „Mo. …“ bis „So. …“; rechts davon steht je Person eine Spalte mit dem Namen im Kopf, in derselben Reihenfolge wie im Wochenplan; ganz rechts „Besetzung“, ganz unten die Zeile „Stunden“.
2. Einen Dienst an einem anderen Standort und einen Urlaub in der Woche prüfen.
Expected: Der Dienst am anderen Standort trägt den Standortnamen in der Zelle und zählt nicht in „Besetzung“; Urlaub steht als Text in der Zelle und zählt ebenfalls nicht.
3. Dasselbe als CSV speichern und in Excel öffnen.
Expected: Gleicher Aufbau wie in Schritt 1, Umlaute stimmen.
4. „Aushang als PDF“ speichern.
Expected: Der Aushang hat unverändert die Personen als Zeilen und die Tage als Spalten.
Closes: Auftrag vom 26.11.2026 (Export gedreht)
Result: ☐

### T-100 – Wochenplan lesbar
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Verwaltung** › **Team** › **Wochenplan** öffnen; Fenster auf die Mindestbreite 1100 px ziehen
1. Im schmalsten Fenster eine Woche mit mehreren Diensten ansehen, auch bei einer Person mit Dienst an einem anderen Standort mit langem Namen.
Expected: Alle sieben Tage und die Zeile „Besetzung“ sind ohne waagrechten Rollbalken zu sehen; nichts ist abgeschnitten, ein gekürzter Standort endet mit „…“. Notieren, ob die Zeit eines geplanten (fetten) Dienstes in zwei Zeilen umbricht.
2. In der Woche von heute den Kopf der Spalte von heute und die Fläche dahinter ansehen.
Expected: Der Kopf heißt „Heute · Do. 26.11.“ (mit dem Datum von heute), die helle Fläche reicht durchgehend vom Kopf bis zur Zeile „Besetzung“ ohne Lücke; der Chip in dieser Spalte hebt sich von der Fläche ab. In einer anderen Woche ist nichts hervorgehoben.
3. Einen Dienst an einem anderen Standort ansehen und den Mauszeiger auf den Standort halten.
Expected: Der Dienst hat keine graue Fläche, in der zweiten Zeile stehen Ortssymbol und Standort; der Tooltip nennt den vollen Namen.
4. Eine Zelle in der ersten und in der letzten Spalte anklicken.
Expected: Das Feld zum Planen öffnet sich direkt unter der Zelle und ist vollständig sichtbar; mit **Esc** schließt es und der Fokus kehrt zur Zelle zurück.
5. Bei der Person auf **Personen**, die Karte öffnen, Reiter **Arbeitszeit**.
Expected: Dieselben Chips auf der Kartenfläche ohne zweite Karte; die Tabelle lässt sich waagrecht verschieben. Notieren, ob der Streifen für heute unter dem Kopf von heute liegt.
6. In **System** auf das dunkle Farbschema wechseln und den Wochenplan ansehen. Danach mit **Tab** durch eine Zeile gehen.
Expected: Chips, Balken, „Frei“ und „Besetzung“ sind gut lesbar. In der Zelle mit Standort gibt es nur einen Tab-Stopp (die Zelle), keinen zweiten für den Standort.
Closes: –
Result: ☐

### T-092 – Management-Bericht: Zahlen richtigstellen
Start: `dotnet run --project src/AispaStudio.AppHost` mit Beispieldaten, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Verwaltung** › **Zahlen** › **Berichte** › Management-Bericht für den laufenden Monat öffnen
1. In der Kassa einen Wertgutschein über 50 € verkaufen und den Bericht neu laden.
Expected: „Umsatz brutto“ steigt um 50 €, „Umsatz ohne Gutscheinverkauf (netto)“ und „Durchschnittsbon ohne Gutscheinverkauf (netto)“ bleiben gleich, „Gutscheine verkauft“ steigt um 50 €.
2. Eine Behandlung mit diesem Gutschein bezahlen und den Bericht neu laden.
Expected: „Gutscheine eingelöst“ steigt um den bezahlten Betrag; in der Tabelle je Behandlung zählt die Behandlung mit ihrem Betrag aus dem Beleg.
3. Eine Behandlung mit Rabatt kassieren und die Tabellen je Behandlung und je Person ansehen.
Expected: Der Umsatz entspricht dem Beleg nach Rabatt (netto), nicht dem Terminpreis.
4. Einen vergangenen Termin ohne Beleg lassen und die Warnungen ansehen.
Expected: „1 Termin noch nicht kassiert.“ (bzw. die Anzahl); künftige Termine zählen nicht mit. Eine Kassendifferenz unter 5,00 € im Zeitraum erzeugt keine Warnung.
5. Den Zeitraum auf den ersten Monat der Beispieldaten stellen.
Expected: Der Hinweis „Vergleich reicht vor den Beginn der Aufzeichnung …“ erscheint.
6. Den Bericht als Excel exportieren.
Expected: Der Dateiname lautet `Kennzahlen_<von>_bis_<bis>.xlsx`, Euro-Werte stehen als Währung.
Closes: Arbeitsanweisung Berichte-Ausbau, Aufgabe A (Befunde 1, 2, 3, 6)
Result: ☐

### T-093 – Management-Bericht: Zeitraum, Vergleich, Kacheln
Start: `dotnet run --project src/AispaStudio.AppHost` mit Beispieldaten, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Verwaltung** › **Zahlen** › **Berichte** › Management-Bericht öffnen
1. Im Feld **Zeitraum** nacheinander **Dieser Monat**, **Letzter Monat**, **Dieses Quartal**, **Dieses Jahr** und **Letzte 12 Monate** wählen.
Expected: Von und Bis ändern sich passend (Letzter Monat ist der ganze Vormonat, Letzte 12 Monate endet heute); der Bericht lädt jedes Mal neu. Reicht Bis bis heute, steht „Heute ist noch nicht abgeschlossen. Die Zahlen können sich noch ändern.“, beim Letzten Monat nicht.
2. Ein Datum von Hand ändern.
Expected: Die Auswahl im Feld **Zeitraum** verschwindet (Platzhalter „Schnellwahl“).
3. **Vergleichen mit** nacheinander auf **Zeitraum davor**, **Vormonat** und **Vorjahr** stellen.
Expected: Die Zeile „Vergleich: …“ nennt den Zeitraum (Vormonat: derselbe Abschnitt, bei einem ganzen Monat der ganze Vormonat; Vorjahr: derselbe Zeitraum im Vorjahr). Die Kacheln schreiben „Vorperiode“, „Vormonat“ bzw. „Vorjahr“ und die Werte ändern sich.
4. Die Kacheln ansehen.
Expected: Jede Kachel mit Vergleichswert zeigt Pfeil und Veränderung („↓ 24 %“, bei Quoten „↓ 3,0 Prozentpunkte“, gleiche Werte „→ unverändert“). Besser: Haken und grüne Fläche, schlechter: Warnzeichen (mehr Umsatz und weniger No-Shows sind besser). „Offene Gutscheine“ und „Offene Anfragen“ haben keinen Vergleich. Pfeil und Text sagen dasselbe wie die Farbe.
5. Mit Tab zu einer Kachel gehen und den Namen vom Screenreader (Erzähler) lesen lassen.
Expected: Zum Beispiel „Umsatz brutto 19.250,97 €, 24 % unter dem Vorjahr“ (ohne Pfeilzeichen).
6. Den Bericht mit **Vorjahr** als Excel exportieren und speichern.
Expected: Die Spalte heißt „Vorjahr“ und enthält die Werte des Vorjahreszeitraums. Neben „Gespeichert: …“ steht **Ordner öffnen**; ein Klick öffnet den Explorer mit der Datei.
7. Dieselben Schritte als Rolle ohne „Berichte mit Beträgen“ (Backoffice) ansehen.
Expected: Die Euro-Kacheln fehlen weiterhin, Schnellwahl, Vergleich und Veränderung der übrigen Kacheln funktionieren.
Closes: Arbeitsanweisung Berichte-Ausbau, Aufgabe B
Result: ☐

### T-101 – Kopfzeile des Wochenplans mit Menü „Ausgabe“
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Verwaltung** › **Team** › **Wochenplan** öffnen; Fenster auf die Mindestbreite 1100 px ziehen
1. Mit **›** vier Wochen vorblättern (auch über einen Wechsel von einstelligen zu zweistelligen Tagen), danach mit **‹** zurück; dabei einmal eine Datei über **Ausgabe** speichern.
Expected: **Standort**, **Anträge**, **Ausgabe** und das Fragezeichen stehen in einer Zeile und bleiben an derselben Stelle; die Oberkante der Tabelle springt nicht. Die Meldung „Gespeichert: …“ erscheint unten und verschiebt nichts. Notieren, ob die Zeile bei 1100 px vollständig sichtbar ist.
2. **Ausgabe** mit der Maus öffnen, dann mit **Esc** schließen. Danach den Knopf per **Tab** anwählen, mit **Enter** öffnen, mit den Pfeiltasten alle Einträge durchgehen und mit **Esc** schließen.
Expected: Das Menü liegt über dem Plan, rechtsbündig unter dem Knopf, und schiebt die Tabelle nicht. Es hat die Gruppen „Aushang · veröffentlichter Stand“ (**Drucken …**, **Als PDF speichern**) und „Liste · mit Entwürfen“ (Dienstplan und Abwesenheiten je als Excel und CSV). Die Pfeile überspringen die Überschriften; nach **Esc** liegt der Fokus wieder auf **Ausgabe**.
3. **Ausgabe** › **Drucken …** wählen, im Druckdialog von Windows **Abbrechen** klicken.
Expected: Der Druckdialog von Windows öffnet sich nach ein bis zwei Sekunden (Ausrichtung **Querformat** wählbar), unten steht kurz „Druckdialog geöffnet“; nach **Abbrechen** ist nichts gedruckt und der Wochenplan unverändert.
4. Nacheinander **Als PDF speichern**, den Dienstplan als Excel, als CSV und die Abwesenheiten als Excel speichern, jeweils im Speicherdialog bestätigen und die Datei öffnen.
Expected: Nach jedem Speichern erscheint die Meldung „Gespeichert: <Dateiname>“, die Kopfzeile bleibt unverändert. Das PDF enthält nur veröffentlichte Dienste, die Listen auch Entwürfe.
5. Als Standortleitung und als Rezeptionistin anmelden und den Wochenplan öffnen.
Expected: Die Standortleitung sieht in **Ausgabe** nur die Gruppe „Aushang“. Die Rezeptionistin sieht keinen Knopf **Ausgabe**; **Anträge** und das Fragezeichen stehen weiter rechtsbündig. Notieren, ob die Rezeptionistin den Aushang lieber ausgegraut mit Hinweis sehen soll.
6. Als Inhaberin **Personen**, eine Person, Reiter **Arbeitszeit** öffnen und **Ausgabe** ansehen.
Expected: Der Knopf steht am rechten Rand der Wochenzeile; das Menü hat nur die Gruppe „Liste · mit Entwürfen“, keinen Aushang.
Closes: Auftrag vom 26.11.2026 (Kopfzeile Wochenplan)
Result: ☐

### T-102 – Besetzung über den Tag im Wochenplan
Start: `dotnet run --project src/AispaStudio.AppHost`, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Verwaltung** › **Team** › **Wochenplan** öffnen (Standort mit Öffnungszeiten und mindestens einem Dienst in der Woche)
1. Unter der Zeile **Besetzung** den Abschnitt **Besetzung über den Tag** ansehen; die Spalten mit denen des Plans darüber vergleichen.
Expected: Je Tag eine Spalte, genau unter den Tagen des Plans; eine Zeile je volle Stunde von der frühesten Öffnung bis zum spätesten Schluss der Woche (Beschriftung „10–11“). Die Spalte von heute läuft als helle Fläche bis ans Ende der Matrix durch. Die Seite rollt senkrecht, wenn sie höher ist als das Fenster; die Matrix hat keinen eigenen Rollbalken.
2. Eine Stunde suchen, in der eine Person Dienst hat, und eine, in der der Standort geöffnet ist, aber niemand Dienst hat. Danach einen Dienst von 10:00 bis 10:20 Uhr (unter 30 Minuten) planen.
Expected: Die erste Zelle zeigt die Zahl der Personen am Standort. Die zweite trägt eine gelbe Fläche, ein Warndreieck und „0“ (nicht nur Farbe). Der Dienst unter 30 Minuten in der Stunde ändert die Zahl nicht.
3. Einen Tag ohne Öffnungszeit (Ruhetag) und einen Tag mit Schließzeit (Betriebsurlaub oder Feiertag) ansehen.
Expected: Die Zellen sind grau ohne Zahl und ohne Warndreieck; es gibt dort keine Lücke.
4. Einen Dienst an einem anderen Standort und einen Urlaubstag eintragen, dann den Entwurf eines Dienstes planen.
Expected: Dienst an einem anderen Standort und Abwesenheit ändern die Zahlen nicht; der Entwurf zählt mit, solange der Plan ihn zeigt.
5. Mit dem Screenreader (Sprachausgabe von Windows) eine Zelle der Matrix anwählen.
Expected: Die Ansage nennt Tag, Stunde und Besetzung, z. B. „Mi. 25.11., 18 bis 19 Uhr: 1 Person im Dienst“, bei einer Lücke „niemand im Dienst“, außerhalb der Öffnung „geschlossen“. Die Zellen sind nicht anklickbar.
6. Bei einem Standort ohne eingetragene Öffnungszeiten den Wochenplan öffnen, dann eine Person öffnen (Reiter **Arbeitszeit**, **Dienstplan und Abwesenheit**) und das Fragezeichen am Zwischentitel anklicken.
Expected: Im Wochenplan steht statt der Matrix ein Satz mit dem Weg zu **Stammdaten** › **Standorte & Räume**. Auf der Karte der Person fehlt der Abschnitt. Das Fragezeichen öffnet die Hilfe „Besetzung über den Tag“.
Closes: Auftrag vom 26.11.2026 (Besetzung über den Tag)
Result: ☐

### T-094 – Management-Bericht: neue Kennzahlen und Verlauf
Start: `dotnet run --project src/AispaStudio.AppHost` mit Beispieldaten, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Verwaltung** › **Zahlen** › **Berichte** › Management-Bericht öffnen, Zeitraum **Letzter Monat**
1. Die Kacheln ansehen.
Expected: Vier Gruppen mit Überschrift: „Umsatz und Ertrag“ (mit Deckungsbeitrag, Deckungsbeitrag in Prozent, Umsatz je Dienststunde, Umsatz je Behandlungsstunde, Produktanteil am Umsatz, Produktumsatz je Behandlung, Umsatz je Gast), „Auslastung“, „Gäste“ (mit „Abwandernde Stammgäste (heute)“ ohne Vergleich) und „Ausfälle“ (mit „Entgangen durch No-Show“ und „Entgangen durch Storno“; weniger ist besser).
2. Den Abschnitt **Verlauf** ansehen, mit der Maus auf eine Säule zeigen und **Werte als Tabelle** aufklappen.
Expected: Drei Säulendiagramme (Umsatz ohne Gutscheinverkauf, Behandlungen, Auslastung Personal), eine Säule je Tag; der Tooltip nennt Tag und Wert, die Tabelle dieselben Werte. Mit **Letzte 12 Monate** steht jede Säule für eine Woche.
3. Die Tabelle **Auslastung nach Wochentag und Tageszeit** ansehen.
Expected: Je Wochentag mit Diensten Vormittag (bis 11), Mittag (11–14), Nachmittag (14–17), Abend (ab 17) in Prozent; Felder unter 50 % zeigen „… · schwach“ auf Warnfläche, Abschnitte ohne Dienst einen Strich. Mit dem Erzähler liest ein Feld „Montag Vormittag: 40,0 %, schwach“.
4. Die **Vorschau nächste vier Wochen** ansehen, dann im Kalender einen Termin in der nächsten Woche buchen und den Bericht neu laden.
Expected: Vier Zeilen zu je sieben Tagen ab heute mit Terminen, gebuchtem Umsatz (brutto) und gebuchter Auslastung, darüber „Stand heute, …“. Nach der Buchung steigen Termine und Umsatz der zweiten Zeile.
5. Die Tabellen **Räume** und **Buchungen nach Kanal** ansehen.
Expected: Je Raum Auslastung und Termine (ohne Sauna und Ruheraum); je Herkunft (Rezeption, Online-Buchung, Mail …) die Termine, zusammen so viele wie die Kachel „Behandlungen“.
6. Die Tabellen **Beste Gäste** und **Abwandernde Stammgäste** ansehen.
Expected: Höchstens zehn beste Gäste mit Besuchen und Ausgaben; abwandernde Stammgäste mit Name, letztem Besuch (über 90 Tage her) und mindestens drei Besuchen, so viele wie die Kachel zählt (höchstens 50 in der Liste). Kein Gast der Liste hat einen künftigen Termin (Stichprobe in der Kundenkarte).
7. Einen Zeitraum ab sieben Tagen wählen und den KI-Kommentar abwarten (nur mit KI-Schlüssel).
Expected: Der Kommentar nennt keine Gästenamen.
8. Als Rolle ohne „Berichte mit Beträgen“ anmelden und den Bericht öffnen.
Expected: Euro-Kacheln, Umsatz im Verlauf, gebuchter Umsatz der Vorschau und Ausgaben der besten Gäste fehlen (Spalte ohne Kopf, Werte „–“); die besten Gäste sind nach Besuchen geordnet. Auslastung, Anzahlen und Quoten bleiben.
9. Als Backoffice anmelden und den Bericht öffnen.
Expected: Die Tabellen „Beste Gäste“ und „Abwandernde Stammgäste“ fehlen; die Kachel „Abwandernde Stammgäste (heute)“ steht da.
Closes: Arbeitsanweisung Berichte-Ausbau, Aufgabe C
Result: ☐

### T-095 – Management-Bericht als Dokument (Excel, PDF, Word)
Start: `dotnet run --project src/AispaStudio.AppHost` mit Beispieldaten, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Verwaltung** › **Zahlen** › **Berichte** › Management-Bericht öffnen, Zeitraum **Letzter Monat**; Excel und Word zum Ansehen auf diesem Rechner installiert
1. Neben **Exportieren** den Knopfblock ansehen und **Exportieren** aufklappen.
Expected: **Drucken**, **PDF speichern** und **Per Mail senden** stehen im Kopf des Abschnitts; das Menü bietet **Bericht (Excel)**, **Bericht (Word)** und **Kennzahlen (CSV)**.
2. **Bericht (Excel)** speichern und die Datei in Excel öffnen.
Expected: `Bericht_<von>_bis_<bis>.xlsx` mit einem Blatt je Abschnitt; das erste Blatt „Übersicht“ hat die Spalte „Veränderung (bei % in Prozentpunkten)“; Umsatz und Auslastung haben graue Datenbalken, bei Prozent reicht 100 % über die ganze Breite der Zelle.
3. **PDF speichern** wählen und das PDF öffnen.
Expected: `Management-Bericht-<Standort>-<von>-bis-<bis>.pdf` auf A4 hoch; Titel, Zeitraum, Alarme, Kennzahlen in vier Gruppen; der Verlauf folgt ohne leere halbe Seite direkt auf die Kennzahlen; Auslastung als graue Balken (100 % = ganze Breite); „Seite n von m“ im Fuß.
4. **Drucken** wählen, im Windows-Druckdialog abbrechen.
Expected: Der Druckdialog öffnet sich mit dem Bericht; nach dem Abbrechen wird nichts gedruckt und kein Fehler gezeigt.
5. **Per Mail senden** wählen, die vorbelegte eigene Adresse stehen lassen und senden.
Expected: Toast „Gesendet an …“; die Mail mit dem PDF-Anhang kommt im eigenen Postfach an. Im Export-Protokoll steht „Export Bericht per Mail (PDF, …, an <Adresse>)“.
6. **Bericht (Word)** speichern und die Datei in Word öffnen.
Expected: `Management-Bericht-<Standort>-<von>-bis-<bis>.docx` auf A4 hoch, schwarz-weiß; Titel, Zeitraum, Alarme, Kennzahlen in vier Gruppen mit Tabellen; nach jeder Gruppe „Anmerkungen:“ mit einem leeren Absatz, in den man schreiben kann; der Verlauf steht als Tabelle, ohne Balken; Kopf mit Betrieb und Standort; Fuß mit „Erstellt am …“ und „Seite n von m“, die Zahlen stimmen nach dem Öffnen (bei „Seite 1 von 1“ mit F9 aktualisieren, wenn Word sie nicht selbst füllt). Word meldet keine Beschädigung.
7. Die Zahlen der drei Dateien mit der Seite vergleichen: Kachel „Behandlungen“ und Zeile „Behandlungen“ in der Übersicht.
Expected: Dieselben Zahlen in Excel, PDF und Word wie auf der Seite.
8. Einen Zeitraum ab sieben Tagen wählen, den KI-Kommentar abwarten (nur mit KI-Schlüssel) und **Bericht (Word)** erneut speichern.
Expected: Der Kommentar steht unter „KI-Auswertung“ mit dem Hinweis auf die KI direkt darunter.
9. Hilfe zum Management öffnen (Fragezeichen) und zu „Bericht als Word-Datei“ springen.
Expected: Der Abschnitt beschreibt Anmerkungen, Verlauf als Tabelle und Kopf und Fuß so, wie die Datei sie zeigt.
10. Abmelden, als Standortleitung ohne „Berichte mit Beträgen“ anmelden, Management öffnen und **Bericht (Word)** speichern.
Expected: In der Datei fehlen Euro-Kennzahlen, Erlöskonten und Zahlungsarten; Auslastung, Anzahlen und Quoten stehen darin.
11. Als Inhaberin im Bereich **Verlauf** das Export-Protokoll ansehen.
Expected: Je Datei ein Eintrag („Export Bericht (Excel, …)“, „(PDF, …)“, „(Word, …)“, die Sendung mit der Adresse).
Closes: Arbeitsanweisung Berichte-Ausbau, Aufgabe D
Result: ☐

### T-096 – Management-Bericht: KI-Auswertung per Knopf
Start: `dotnet run --project src/AispaStudio.AppHost` mit Beispieldaten, im Aspire-Dashboard die Ressource `windows-app` starten, als Inhaberin anmelden, **Verwaltung** › **Zahlen** › **Berichte** › Management-Bericht öffnen, Zeitraum **Letzter Monat**. Schritte 2 bis 9 nur mit eingetragenem Claude-Schlüssel (`dotnet user-secrets set "Ai:Claude:ApiKey" "<schlüssel>" --project src/AispaStudio.Api`, danach neu starten); ohne Schlüssel nur Schritt 1.
1. Ohne Claude-Schlüssel auf **Mit KI auswerten** zeigen.
Expected: Der Knopf mit der Pille „KI“ ist gesperrt; der Tooltip sagt „Verfügbar, sobald auf dem Server ein Claude-Schlüssel eingetragen ist.“. Daneben führt das Fragezeichen zur Hilfe „Bericht mit KI auswerten“.
2. Mit Schlüssel auf **Mit KI auswerten** klicken.
Expected: Rechts geht die Seitenleiste „Mit KI auswerten“ auf (360 Pixel), der Fokus steht im Feld **Schwerpunkt**; **Auswerten** steht fest im Fuß, unter dem Feld **Eigene Frage (freiwillig)** stehen der Zähler „0 von 300 Zeichen“ und der Hinweis, die Frage ohne Zahlen zu stellen.
3. Schwerpunkt **Gesamt** lassen und **Auswerten** klicken.
Expected: Erst „Die KI wertet die Zahlen des Berichts aus …“ mit Fortschrittsbalken, nach einigen Sekunden das Ergebnis: Pille „KI“ mit „Schwerpunkt Gesamt“, **Kernaussagen** (drei bis fünf), **Auffälligkeiten** mit „Mögliche Ursache (Vermutung): …“ und „Kennzahl: …“, **Maßnahmen** je mit „Woran Sie den Erfolg sehen: …“, **Was die Zahlen nicht zeigen**, darunter der Hinweis „Von der KI aus den Zahlen dieses Berichts ausgewertet. …“.
4. Die Zahlen im Ergebnis mit den Kacheln vergleichen.
Expected: Jede Zahl im Ergebnis steht so auf der Seite (Kachel, Veränderung oder Tabelle); keine Zahl ist neu gerechnet, kein Gästename kommt vor.
5. **Auswerten** sofort noch einmal klicken.
Expected: Dasselbe Ergebnis ohne Wartezeit (gespeichert zu denselben Zahlen, demselben Schwerpunkt und derselben Frage).
6. **Kopieren** klicken und den Text in einen Editor einfügen.
Expected: „Kopiert.“ in der Leiste; im Editor stehen die vier Teile mit Überschriften und der Hinweis am Ende.
7. **In den Bericht übernehmen** klicken, dann **PDF speichern**, **Exportieren** › **Bericht (Word)** und **Bericht (Excel)**.
Expected: In der Leiste steht „Im Bericht übernommen: …“ mit **Aus dem Bericht nehmen**. Im PDF und in Word steht die Auswertung unter „KI-Auswertung, Schwerpunkt Gesamt“ mit dem Hinweis direkt darunter; in Excel auf dem Blatt „Hinweise“, der Hinweis als eigene Zeile.
8. Den Zeitraum auf **Dieser Monat** ändern und **PDF speichern**.
Expected: Die Leiste beginnt neu ohne Ergebnis; das PDF enthält keine KI-Auswertung mehr.
9. Als **Eigene Frage** „Warum hatten wir 999 Gäste?“ eingeben und auswerten.
Expected: Entweder ein Ergebnis ohne die Zahl 999 oder „Auswertung gescheitert“ mit dem Grund und **Erneut versuchen**; im zweiten Fall zeigt die Leiste nichts von der KI.
10. Abmelden, als Standortleitung ohne „Berichte mit Beträgen“ anmelden und Schritt 3 wiederholen.
Expected: Die Auswertung nennt keine Beträge in Euro.
Closes: Arbeitsanweisung Berichte-Ausbau, Aufgabe E
Result: ☐
