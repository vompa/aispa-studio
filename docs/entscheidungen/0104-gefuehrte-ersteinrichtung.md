# 0104. Geführte Ersteinrichtung: Setup, Startauswahl und Assistent

- **Status:** angenommen
- **Datum:** 27.11.2026
- **Entschieden von:** Auftraggeber (Einzelplatz als Normalfall, Setup mit Velopack ohne Signatur, Sicherung später, Startkatalog nach Art des Spas, Kassa mit eigenem Assistenten), Entwurf und Einzelheiten Claude

## Kontext

Wer AISPA Studio zum ersten Mal einrichtet, geht heute durch drei Teile, die nicht zusammenhängen:

1. **Vor dem Start:** ZIP-Datei entpacken und die Betriebsart über einen Schalter an einer Verknüpfung wählen (`--standalone`, [Einzelplatz](../installation/standalone.md)). Eine Update-Funktion gibt es nicht.
2. **Erster Start:** das Formular „Spa einrichten“ mit sieben Feldern auf der Anmeldung.
3. **Danach:** die Karte „Betrieb einrichten“ auf „Firmendaten“ mit sechs Schritten, die in vier Masken springen ([ADR 0099](0099-anlegen-entfernen-und-leerer-betrieb.md), Entscheidung 6).

Daraus folgen vier Lücken:

- Die Betriebsart hängt an einem Schalter. Ohne Schalter sucht die App einen Server und zeigt erst nach dem Scheitern die Server-Verbindung.
- Die tägliche Sicherung steht nur in der Anleitung. Im Einzelplatz ist der PC die einzige Kopie aller Daten, und der Wiederherstellungsschlüssel ist auch der Weg bei „Passwort vergessen“ ([ADR 0051](0051-passwort-vergessen-mit-wiederherstellungsschluessel.md)).
- Ein Schritt der Karte gilt als erledigt, sobald es einen Eintrag gibt. Eine Person ohne Arbeitszeiten zählt als fertig, buchen lässt sich trotzdem nichts.
- Jeder Schritt verlangt eine volle Maske. Bis zum ersten Termin sind vier Masken zu lernen.

Vorgabe des Auftraggebers (27.11.2026): Die App wird fast immer im Einzelplatz benutzt. Vor dem ersten Start steht ein kleines Setup, das die Voraussetzungen prüft und installiert.

## Optionen

| Option | Bewertung |
| --- | --- |
| A: Nur die Karte „Betrieb einrichten“ verbessern | wenig Aufwand, bleibt bei ADR 0099; Schalter, fehlende Sicherung und die vier vollen Masken bleiben |
| B: Setup, Startauswahl und ein Assistent über den vorhandenen Schritten | gewählt |
| C: Einrichtung im Gespräch mit der KI (Preisliste als PDF oder Foto, die KI schlägt Behandlungen vor) | schnell für die Inhaberin, aber die Module sind im Einzelplatz aus ([ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md)); später als Zusatz im Schritt „Behandlungen“ möglich |

Für das Setup standen zur Wahl: Velopack (gewählt), ein selbst geschriebenes Setup in .NET (keine fremde Abhängigkeit, aber Updates, Reparatur und Deinstallation wären selbst zu bauen) und die ZIP-Datei wie bisher.

## Entscheidung

**1. Setup vor dem ersten Start mit Velopack.** Eine `Setup.exe` ersetzt die ZIP-Datei und die Verknüpfung mit Schalter.

- Sie installiert ohne Administratorrechte für das angemeldete Windows-Konto, legt den Eintrag im Startmenü und die Deinstallation an und holt die WebView2-Runtime nach.
- Updates kommen über denselben Weg: Die App prüft beim Start im Hintergrund und spielt einen neuen Stand beim nächsten Start ein. Eine eigene Oberfläche dafür gibt es nicht.
- Die `Setup.exe` ist zum Start nicht signiert (Auftraggeber, 27.11.2026). Windows SmartScreen warnt deshalb bei der Installation. Die Signatur lässt sich später im Paketbau ergänzen, ohne Umbau.
- Die ZIP-Datei bleibt daneben im Release.
- .NET und PostgreSQL liegen dem Paket schon bei. Die Startprüfung der App bleibt die Stelle für Windows-Stand, 64 Bit und freien Platz.
- Die Daten bleiben unter `%LOCALAPPDATA%\AispaStudio`. Das Programm bekommt einen eigenen Ordner mit eigener Kennung, damit ein Update und eine Deinstallation die Daten nicht berühren. Die Machbarkeitsprobe belegt das, bevor das Setup gebaut wird.

**2. Der Einzelplatz ist der Normalfall.** Auf einem PC, auf dem noch nichts eingerichtet ist, zeigt der Start die Startauswahl.

- „Noch nichts eingerichtet“ heißt: Auf diesem PC wurde noch keine Betriebsart gewählt. Die Einstellungen der App bekommen dafür ein eigenes Merkmal. Ein PC, der schon im Einzelplatz läuft oder eine gespeicherte Verbindung hat, gilt als eingerichtet.
- Die Startauswahl erscheint nur, wenn kein Schalter gesetzt ist, keine Adresse über `AISPA_API_URL` vorgegeben ist und auf diesem PC kein Server antwortet.
- Hauptaktion ist **Neues Spa auf diesem PC einrichten**.
- Die Startauswahl führt zu den vorhandenen Ansichten, sie bringt keine eigene Logik mit.
- Darunter stehen die Nebenwege: **Beispiel-Spa ansehen** ([ADR 0100](0100-beispiel-spa-in-eigener-datenbank.md)), **Aus einer Sicherung wiederherstellen** und **Mit einem vorhandenen Spa verbinden** (Haupt-PC im Netz nach [ADR 0049](0049-mehrplatz-im-eigenen-netz.md), zentrale Datenbank, Server).
- Die Schalter `--standalone`, `--server` und `--demo` bleiben für Skripte und Tests.
- Ein PC mit gespeicherter Verbindung startet wie bisher.

**3. „Spa einrichten“ in Schritten.** Aus dem einen Formular werden drei kurze Schritte:

1. Name des Spas und Land.
2. Erster Standort.
3. Konto der Inhaberin bzw. des Inhabers.

Die Startprüfung bleibt davor und ist nur bei einem Fund sichtbar. Die Sicherung ist kein Schritt (Auftraggeber, 27.11.2026: Sicherung später); der Hinweis über allen Seiten bleibt der Weg dorthin.

**4. Assistent „Betrieb einrichten“ nach der Anmeldung.** Ziel ist der erste buchbare Termin. Jeder Schritt fragt nur das Nötigste:

| Schritt | Eingabe |
| --- | --- |
| Öffnungszeiten | Vorschlag Montag bis Samstag, eine Zeile für alle Tage, Abweichungen je Tag |
| Räume | mehrere Namen auf einmal; Bereiche wie die Sauna als zweite Liste |
| Behandlungen | Art des Spas wählen (zum Beispiel nur Massage, nur Kosmetik, mehrere zusammen); daraus kommt ein Startkatalog in Paketen, zugeordnet zu den Räumen; Preise trägt die Inhaberin ein |
| Team | Name und Rolle; Arbeitszeiten wie die Öffnungszeiten; Person kann alle gewählten Behandlungen |
| Abschluss | Probetermin buchen; danach Hinweise auf Zugänge, Kundenimport und Module |

Die Räume stehen vor den Behandlungen, weil jede Behandlung mindestens einen Raum braucht. Die Kassa bekommt danach einen eigenen Assistenten (Auftraggeber, 27.11.2026); die Karte „Betrieb einrichten“ verweist bis dahin auf die Maske.

Die Knöpfe heißen **Weiter** und **Zurück** ([Fluent-2-Ableitung](../gestaltung/fluent2-ableitung.md)). Der Assistent ist eine native Maske der Windows-App ([ADR 0084](0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md)) und folgt [masken.md](../gestaltung/masken.md).

**5. Kein zweiter Datenweg.** Der Assistent schreibt über dieselben Endpunkte wie die Masken. Die Pakete des Startkatalogs liegen in der API (`GET /api/setup/catalog`, `POST /api/setup/catalog/apply`) und legen Behandlungen auf demselben Weg an wie die Maske, ohne Preise. Sein Stand sind die Daten selbst, nicht ein eigener Merker. Wer abbricht, öffnet ihn über die Karte „Betrieb einrichten“ wieder und landet beim ersten offenen Schritt.

**6. Die API entscheidet, was fertig ist.** `GET /api/setup/progress` bekommt:

- feinere Kriterien je Schritt (zum Beispiel: mindestens eine Person mit Arbeitszeiten, die eine aktive Behandlung kann),
- eine Bereitschaftsprüfung: Findet der `ResourcePlanner` in den nächsten 14 Tagen eine freie Zeit für mindestens eine Behandlung? Wenn nicht, nennt die Antwort den Grund als fertigen Satz.

Die App zeigt nur an. Die Sicherung ist kein Schritt der API: Ihren Stand kennt nur die App auf dem PC.

**7. Nichts sperrt.** Jeder Schritt lässt sich überspringen. Die Masken bleiben der volle Weg, die Karte bleibt Einstieg und Stand.

**8. Verhältnis zu ADR 0099.** Entscheidung 6 dort sagt: keine neue Schiene, nur die Karte. Dieser ADR weicht davon ab und ergänzt die Karte um den Assistenten. Mit der Annahme bekommt ADR 0099 einen Nachtrag.

## Begründung

- **Einzelplatz zuerst:** Zum Start wird nur die Windows-App ausgeliefert ([ADR 0059](0059-zum-start-nur-die-windows-app.md)), fast immer als Einzelplatz. Der häufigste Weg braucht deshalb keinen Schalter und keine Anleitung.
- **Velopack statt Eigenbau:** Installieren, Aktualisieren und Entfernen sind gelöste Aufgaben. Ein eigenes Setup müsste jede davon nachbauen und pflegen.
- **Startkatalog nach Art des Spas:** Ein Massagestudio braucht keine Liste mit Maniküre. Pakete je Art halten die Auswahl kurz.
- **Assistent über den Masken:** Die Geschäftsregeln bleiben an einem Ort ([Architektur](../architektur.md)). Der Assistent ist eine zweite Sicht, kein zweites System; fällt er weg, fehlt keine Funktion.
- **Bereitschaftsprüfung statt Zählen:** „Ein Eintrag existiert“ sagt nicht, ob sich buchen lässt. Der `ResourcePlanner` beantwortet genau diese Frage schon.

## Folgen und Stand

Umgesetzt ist noch nichts. Die Aufgaben stehen in der Aufgabenliste.

**Geändertes Verhalten** (vom Auftraggeber am 27.11.2026 freigegeben). Heute sucht ein Start ohne Schalter rund 30 Sekunden einen Server unter `localhost:5086` und zeigt danach „Keine Verbindung“. Künftig sieht ein PC ohne Einrichtung sofort die Startauswahl; die Verbindung zu einem vorhandenen Spa liegt dort einen Klick weiter. PCs mit gespeicherter Verbindung, mit einem erreichbaren Server oder mit einem Schalter starten wie bisher.

**Aufgaben:**

| Aufgabe | Inhalt |
| --- | --- |
| T-130 | Machbarkeitsprobe Velopack als Befund |
| T-131 | Setup und Updates mit Velopack: Paket, Versionsnummer, Schritt in der CI, Anleitung [Einzelplatz](../installation/standalone.md) |
| T-132 | Startauswahl mit dem Merkmal „noch nichts eingerichtet“ |
| T-133 | „Spa einrichten“ in drei Schritten |
| T-134 | API: feinere Kriterien, Bereitschaftsprüfung, Pakete des Startkatalogs |
| T-135 | Assistent mit den Schritten Öffnungszeiten, Räume, Behandlungen und Team |
| T-136 | Abschluss mit Probetermin, Handbuch und Bilder |
| T-137 | Assistent für die Kassa: zuerst ein Befund, dann ein eigener Plan |

**In der Machbarkeitsprobe zu klären:**

- Ob Programmordner und Datenordner getrennt bleiben und eine Deinstallation die Daten stehen lässt.
- Wie das Setup die WebView2-Runtime nachholt.
- Ob die mitgelieferte PostgreSQL die Visual-C++-Runtime von außen braucht.
- Paketgröße (heute rund 185 MB als ZIP) und ob Updates nur die Unterschiede laden.
- Ob die Windows-Aufgabe „AISPA Studio Sicherung“ nach einem Update noch auf den richtigen Pfad zeigt. Sie speichert heute den Pfad der laufenden Anwendung.

**Weitere Folgen:**

- Das Repository führt heute keine Versionsnummer. Das Setup braucht eine; sie kommt mit T-131.
- `SetupStepDto` wird nur erweitert, kein Feld fällt weg ([ADR 0085](0085-api-version-in-den-pfaden.md)).

**Nicht Teil dieses ADR:** die Ersteinrichtung im Server-Betrieb und der KI-Vorschlag aus einer Preisliste (Option C).

**Entschieden vom Auftraggeber am 27.11.2026:**

- Signatur der `Setup.exe`: zum Start ohne. Die Wege und Preise wurden am selben Tag geprüft. Ein günstiges Zertifikat für offene Software nennt den Namen einer Person, ein Firmenzertifikat setzt eine Firma voraus.
- Sicherung: später, kein Schritt der Einrichtung.
- Startkatalog: ja, nach Art des Spas in Paketen.
- Kassa: danach, mit eigenem Assistenten. Belege bleiben in jedem Fall bei der Kassa ([ADR 0010](0010-kassa-compliance-by-design.md)).

## Quellen

- [ADR 0099](0099-anlegen-entfernen-und-leerer-betrieb.md) (Karte „Betrieb einrichten“, leerer Betrieb), [ADR 0041](0041-einzelplatz-standalone-und-zuschaltbare-webmodule.md), [ADR 0049](0049-mehrplatz-im-eigenen-netz.md), [ADR 0051](0051-passwort-vergessen-mit-wiederherstellungsschluessel.md), [ADR 0059](0059-zum-start-nur-die-windows-app.md), [ADR 0084](0084-alle-masken-nativ-web-fuer-dashboard-und-handy.md), [ADR 0085](0085-api-version-in-den-pfaden.md), [ADR 0100](0100-beispiel-spa-in-eigener-datenbank.md)
- [Einzelplatz und mehrere PCs im Spa](../installation/standalone.md), [Betriebsarten und Module](../installation/operating-modes.md)
- [Masken](../gestaltung/masken.md), [Fluent-2-Ableitung](../gestaltung/fluent2-ableitung.md), [Texte](../gestaltung/texte.md)
- Code heute: `src/AispaStudio.Api/Scheduling/SetupProgress.cs`, `src/AispaStudio.Desktop/Views/LoginView.axaml` („Spa einrichten“), `src/AispaStudio.Desktop/Views/CompanyView.axaml` (Karte)
