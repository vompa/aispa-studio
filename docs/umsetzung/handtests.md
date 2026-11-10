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

Ohne Block, weil die Aufgabe nichts Sichtbares ändert: T-013 (Entscheidung, nur Doku), T-014 (Entscheidung, keine Änderung), T-016 (Sichtung der zurückgestellten Kleinigkeiten), T-030 (CI-Zeitlimits), T-033 (Beispieldaten und Test), T-034 (Werkzeug knip, unbenutztes Paket entfernt), T-035 (Skill und Doku), T-040 (ADR und Konzept, nur Doku), Export/Import Aufgabe A (Export-Dienst, Knopf ohne Seite; der Knopf ist im Block „Export/Import B“ geprüft).

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
1. **Termin stornieren …** klicken.
Expected: Über dem Feld „Grund“ steht mit Warnsymbol „Absage innerhalb der Stornofrist (bis <gestern> HH:mm). Laut Regel fallen 50 % Ausfallgebühr an; belasten kann die Standortleitung.“; „Grund“ ist mit „Kurzfristig an der Rezeption storniert“ vorbelegt.
2. **Abbrechen**, dann als Inhaber anmelden und denselben Termin stornieren wollen.
Expected: Der Hinweis endet mit „Belasten unter „Ausfallgebühr belasten …“ nach dem Stornieren.“
3. Einen gebuchten Termin in zwei Wochen anklicken und **Termin stornieren …** klicken.
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
7. **Zahlen** › **Berichte** › **Management** öffnen, **Exportieren** › **Kennzahlen (Excel)** speichern.
Expected: Kennzahl, Wert, Vorperiode und Einheit mit denselben Zahlen wie auf der Seite.
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
5. Bei der Dublette **Ergänzen** wählen, **Weiter** und **Nur prüfen** klicken.
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
Expected: Toasts „Gesendet an …“, „Gedruckt“ und „Gutschein … als übergeben vermerkt“; mit „noch nicht versendet“ fehlen diese drei, der Rest steht darauf. In der Liste ohne Filter steht beim ersten „Mail am <Datum>“ mit „an <Adresse>“, beim zweiten „Gedruckt am <Datum>“, beim dritten „Übergeben am <Datum>“; beim online gekauften Gutschein „Mail am …“ an die Adresse des Käufers. Ein Druck, der am Drucker scheitert, hinterlässt keinen Vermerk.
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
Result: ☐
